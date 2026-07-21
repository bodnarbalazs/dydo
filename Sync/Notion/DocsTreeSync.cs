namespace DynaDocs.Sync.Notion;

using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Sync.Model;
using DynaDocs.Sync.Notion.Dtos;

/// <summary>
/// Orchestrates the Docs → Notion nested-page mirror (DR 033), the browse-tree sibling of the queryable
/// <see cref="NotionSpineSync"/> spine. It recurses <c>dydo/</c> minus the exclusion set (§5), projects each
/// folder to a Notion page and each <c>.md</c> doc to a child page under it, consumes a folder's
/// <c>_index.md</c>/<c>index.md</c> as that folder's page body, and archives the page of any doc that
/// disappears. Structure is repo-owned and created here one-way (root "Docs" page, then folder pages
/// top-down, so every parent id is known); the per-page BODY merge is delegated to <see cref="SyncRunner"/>
/// via <see cref="DocsPageAdapter"/> — no new merge machinery. A re-run is idempotent: existing pages are
/// reused from the <c>notion-docs</c> snapshot store, never re-created.
/// </summary>
public static class DocsTreeSync
{
    public const string AdapterName = "notion-docs";
    private const string RootTitle = "Docs";

    /// <summary>The parent-scoped snapshot store name (finding 4). <see cref="BaseSnapshotStore.PathFor"/> keys
    /// only by adapter name, so a bare <c>notion-docs</c> would make two different <c>--parent-page</c> targets
    /// (a scratch smoke page vs. the real workspace) share ONE snapshot — the scratch run's external ids then
    /// leak into the real run as stale/foreign state. Folding a short stable hash of the parent page id into the
    /// name gives each target its own snapshot, so alternating targets never cross-contaminate.</summary>
    public static string SnapshotAdapterName(string parentPageId) =>
        AdapterName + "-" + ParentPageKey.Hash8(parentPageId);

    // The dydo-root folder's local id — its page is the "Docs" root. A single reserved token so it can never
    // collide with a real doc path (a repo path is never bare ".").
    private const string RootLocalId = ".";

    public static void Run(INotionClient client, string dydoRoot, string parentPageId, bool dryRun, TextWriter output)
    {
        var excluded = ExcludedDirs(dydoRoot);
        var offLimits = LoadOffLimits(dydoRoot);
        var root = BuildTree(dydoRoot, excluded, offLimits);

        if (dryRun)
        {
            WriteDryRunPlan(root, client, dydoRoot, parentPageId, output);
            return;
        }

        var store = new BaseSnapshotStore(BaseSnapshotStore.PathFor(dydoRoot, SnapshotAdapterName(parentPageId)));

        // 1. Structure (repo-owned, one-way): ensure the root "Docs" page, then every folder page (top-down, so a
        //    child's parent id is always resolved first), then every file page (after all folders exist, so
        //    sub-folders precede files in the sidebar — DR 033 §7). Each page is created WITH its body in one
        //    atomic POST (DR 035 §1/§2 create-with-body): a fresh sync writes every body at page-create, so the
        //    body phase below is normally a pure no-op and issues NO destructive PATCH — and each folder body is
        //    written before that folder has any child pages, so the write can never trash the nested docs (§3).
        //    Each new page is persisted to the store the instant it is created (see CreatePageWithBodyAndRecord):
        //    a mid-phase CreatePage failure (a 429/500 under the ~3 req/s throttle, or a process kill) must never
        //    leave a created page unrecorded and re-minted as a duplicate on retry. The recorded base is GUARDED
        //    against a silent create-with-body ignore (DR 035 review finding): the create's markdown field is
        //    doc-sourced and create-with-body is unconfirmed against a page_id parent, so if live Notion drops it
        //    the page is EMPTY — recording the full body against it would make the body phase read the empty page
        //    as an external clear and WIPE every canonical doc (issue 0235, full-tree blast radius). The base is
        //    recorded EMPTY first and only upgraded to the body once a read-back confirms Notion kept it, so a
        //    silent ignore degrades gracefully to a child-safe body-phase PATCH instead of a wipe.
        var rootPageId = EnsureRootPage(client, store, parentPageId, IndexBody(root), output);
        var folderPageId = new Dictionary<string, string> { [RootLocalId] = rootPageId };
        EnsureFolderPages(client, store, root, rootPageId, folderPageId, output);
        EnsureFilePages(client, store, root, folderPageId, output);

        // 2. Body + frontmatter (bidirectional): reconcile every folder-index body and doc file through the engine.
        var docs = new List<SyncDoc>();
        var parentByLocalId = new Dictionary<string, string>();
        var titleByLocalId = new Dictionary<string, string>();
        var pathByLocalId = new Dictionary<string, string>();
        Collect(root, folderPageId, docs, parentByLocalId, titleByLocalId, pathByLocalId);

        // Promote any shadow file a human has resolved (DR 035 §4) before reconciling, so its content becomes the
        // repo doc this tick and is pushed — the resolution flow's second half. Done against pathByLocalId, so a
        // promoted body lands on the canonical file BuildTree/Collect already read; re-read it after promotion.
        if (PromoteResolvedShadows(dydoRoot, client, store, pathByLocalId))
        {
            docs.Clear(); parentByLocalId.Clear(); titleByLocalId.Clear(); pathByLocalId.Clear();
            Collect(root, folderPageId, docs, parentByLocalId, titleByLocalId, pathByLocalId);
        }

        var managed = ManagedPageIds(store);
        var adapter = new DocsPageAdapter(
            client, rootPageId, parentByLocalId, titleByLocalId, managed, output, LastSyncedBodyByPageId(store));
        var runner = new SyncRunner(adapter, store,
            (localId, _, _) => pathByLocalId.TryGetValue(localId, out var path) ? path : Path.Combine(dydoRoot, localId + ".md"),
            // A genuine two-sided conflict is diverted here — never written to the canonical repo file (DR 035
            // §4/§5, the root-cause fix for issue 0235). The shadow tree lives under _system, already excluded
            // from the mirror's walk (ExcludedDirs), so a conflict can never cascade back through the sync.
            localId => ShadowPathFor(dydoRoot, localId));

        // The mass-delete fuse (SyncRunner, ns-2) counts RepoDeletes, and a repo-owned-structure reconcile emits
        // NONE: an external-gone page whose repo doc is present routes to CreateToExternal, and RepoDelete is only
        // produced in ReconcileEngine's !repoOwnedStructure branch (ExternalDeleted). So the fuse can never trip on
        // the docs path — result.FuseTripped is structurally always false here and needs no handling. Only the
        // shadow count below is actionable (RepoOwnedStructure_AllExternalGone_PlansZeroRepoDeletes_FuseCannotTrip pins this).
        var result = runner.Run(docs);
        output.WriteLine($"notion docs sync: reconciled {result.Results.Count} page(s) under \"{RootTitle}\"");
        if (result.ShadowedLocalIds.Count > 0)
            output.WriteLine(
                $"  {result.ShadowedLocalIds.Count} conflict(s) diverted to {ShadowDirRel} (canonical files untouched): "
                + string.Join(", ", result.ShadowedLocalIds));
    }

    /// <summary>The shadow tree root (DR 035 §4), under <c>_system</c> so it is already outside the mirror's walk
    /// (see <see cref="ExcludedDirs"/>) — a diverted conflict can never be re-uploaded and cascade.</summary>
    private const string ShadowDirRel = "_system/notion_sync";

    /// <summary>The shadow-file path mirroring a doc's repo-relative path (DR 035 §4). The dydo-root index's
    /// reserved <c>.</c> local id has no relative path, so it maps to a reserved stem that can never collide with
    /// a real doc.</summary>
    private static string ShadowPathFor(string dydoRoot, string localId) =>
        Path.Combine(dydoRoot, "_system", "notion_sync", (localId == RootLocalId ? "_root" : localId) + ".md");

    /// <summary>Promote every shadow file a human has resolved — one no longer carrying merge sentinels — onto its
    /// canonical repo doc, then delete it (DR 035 §4 resolution flow). A shadow file still bearing markers is left
    /// untouched: the human has not finished, and the reconcile re-derives the same conflict deterministically. The
    /// canonical path is recovered from the shadow's relative path, independent of the current tree so a doc that
    /// vanished from the tree while its resolution was pending is still restored.
    /// <para>The resolution must WIN over the still-diverged Notion side (else the reconcile re-detects the two-
    /// sided edit and re-diverts): the base is aligned to the CURRENT external body, so the reconcile reads Notion
    /// as unchanged and pushes the resolved repo body over it (repo-wins) rather than merging a fresh conflict. That
    /// alignment read is GUARDED: a base entry always carries an external id (a create records it only with the
    /// assigned id), but the page it points at may have been archived/trashed in Notion while the conflict sat
    /// unresolved, so the read 404/400s — on failure the alignment is SKIPPED (the base left as-is) rather than
    /// throwing at the same point every tick and wedging the whole sync; the reconcile then resurrects the doc from
    /// the repo-owned structure. Returns whether anything was promoted, so the caller re-reads the affected docs.</para></summary>
    private static bool PromoteResolvedShadows(
        string dydoRoot, INotionClient client, BaseSnapshotStore store, IReadOnlyDictionary<string, string> pathByLocalId)
    {
        var shadowRoot = Path.Combine(dydoRoot, "_system", "notion_sync");
        if (!Directory.Exists(shadowRoot))
            return false;

        var promoted = false;
        foreach (var shadowFile in Directory.EnumerateFiles(shadowRoot, "*.md", SearchOption.AllDirectories))
        {
            var content = File.ReadAllText(shadowFile);
            if (ThreeWayTextMerge.ContainsConflictMarkers(content))
                continue; // still unresolved — leave it for the human

            var relative = Path.GetRelativePath(shadowRoot, shadowFile);
            var stem = relative[..^".md".Length].Replace('\\', '/');
            var localId = stem == "_root" ? RootLocalId : stem;
            // The canonical path is the CURRENT tree's path for this local id — the root index (its reserved "."
            // local id maps to the real _index.md/index.md, never the junk "dydo/..md" that RootLocalId + ".md"
            // would build) for _root, else the doc's own path. The fallback covers a doc that vanished from the
            // tree while its resolution was pending: an absent root restores to _index.md, an absent doc to its stem.
            var canonical = pathByLocalId.GetValueOrDefault(localId,
                stem == "_root"
                    ? Path.Combine(dydoRoot, "_index.md")
                    : Path.Combine(dydoRoot, stem.Replace('/', Path.DirectorySeparatorChar) + ".md"));

            Directory.CreateDirectory(Path.GetDirectoryName(canonical)!);
            // Promote through CleanForPersist, never verbatim: this is a write to a CANONICAL file, and the
            // invariant is that EVERY canonical write is cleaned (strips child-page `<page url>` structure tags and
            // expiring signing params, DR 035 §3 / issue 0235). A human resolving a shadow may keep a hunk that still
            // carries the child-page tag soup the read-side strip removes elsewhere; this was the one ingress the
            // read-side strip left open, so it is closed here — soup can never reach a canonical file by any path.
            File.WriteAllText(canonical, DocsMarkdownNormalizer.CleanForPersist(content));
            File.Delete(shadowFile);
            promoted = true;

            if (store.Get(localId) is { ExternalId: { } pageId } snap)
            {
                try
                {
                    store.Set(new SyncDoc
                    {
                        LocalId = snap.LocalId,
                        ExternalId = snap.ExternalId,
                        Fields = snap.Fields,
                        Body = DocsMarkdownNormalizer.CleanForPersist(client.GetPageMarkdown(pageId).Markdown),
                        SourcePath = "",
                    });
                }
                catch (NotionApiException)
                {
                    // The page was archived/trashed in Notion while the conflict sat unresolved, so its body read
                    // 404/400s. Skip the base alignment (leave the base as-is) rather than wedge the whole sync on
                    // one unreadable page — the reconcile resurrects the doc from the repo-owned structure.
                }
            }
        }
        if (promoted)
            store.Save();
        return promoted;
    }

    /// <summary>The dydo-root-relative dirs to exclude from the mirror (DR 033 §5). The spine dirs are DERIVED
    /// from the sync model — whatever the spine owns as a queryable database is excluded here by construction, so
    /// the two surfaces never overlap and no hardcoded list drifts as the spine grows — plus the framework/tooling
    /// dirs that hold no browsable docs.</summary>
    private static HashSet<string> ExcludedDirs(string dydoRoot)
    {
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "_system", "agents", "_assets", ".obsidian",
        };
        foreach (var type in SyncModelLoader.Load(dydoRoot).Objects)
            if (!string.IsNullOrWhiteSpace(type.Dir))
                excluded.Add(type.Dir.Replace('\\', '/').Trim('/'));
        return excluded;
    }

    /// <summary>The guard's universal off-limits checker (DR 033 §5), so the mirror honors the SAME source the
    /// <c>PreToolUse</c> guard uses and never uploads a guard-off-limits file. Loading against <paramref name="dydoRoot"/>
    /// finds the project's <c>files-off-limits.md</c>; outside a project no patterns load and nothing is excluded.</summary>
    private static IOffLimitsService LoadOffLimits(string dydoRoot)
    {
        var service = new OffLimitsService();
        service.LoadPatterns(dydoRoot);
        return service;
    }

    private static bool IsOffLimits(IOffLimitsService offLimits, string path) =>
        offLimits.IsPathOffLimits(path) != null;

    private static string EnsureRootPage(
        INotionClient client, BaseSnapshotStore store, string parentPageId, string rootBody, TextWriter output)
    {
        var existing = store.Get(RootLocalId)?.ExternalId;
        if (existing != null)
            return existing;

        return CreatePageWithBodyAndRecord(client, store, parentPageId, RootLocalId, RootTitle, rootBody, output);
    }

    /// <summary>Create a page WITH its body atomically (DR 035 §1/§2) and record its base — CRASH-safe AND WIPE-safe
    /// against a silent create-with-body ignore (DR 035 review finding). The create's <c>markdown</c> field is
    /// doc-sourced and create-with-body is unconfirmed against a page_id parent (dydo/reference/notion-sync.md), so
    /// it may be silently dropped, leaving the page EMPTY.
    /// <para>The base is recorded EMPTY the instant the page exists (crash-safety, §1 — a mid-phase failure must
    /// never orphan an unrecorded page and re-mint it as a duplicate). An empty base can never drive a wipe: if a
    /// crash strikes before the read-back below, the body phase sees external == repo (both the body Notion actually
    /// stored) and converges, never reading a full-body base against a possibly-empty page and emptying the canonical
    /// doc (the issue 0235 full-tree wipe). Only AFTER a read-back confirms Notion kept a non-empty body is the base
    /// upgraded to it, so the body phase is a no-op. If the read-back comes back EMPTY the field was ignored: the base
    /// stays empty and we warn — the body phase then writes the body via a child-safe PATCH (graceful degradation, a
    /// fresh sync self-heals instead of aborting or wiping). One read-back GET per created page, only on a full fresh
    /// bootstrap.</para></summary>
    private static string CreatePageWithBodyAndRecord(
        INotionClient client, BaseSnapshotStore store, string parentPageId, string localId, string title, string body, TextWriter output)
    {
        var page = client.CreatePage(new NotionPageCreateRequest
        {
            Parent = NotionParent.Page(parentPageId),
            Properties = TitleProperty(title),
            Markdown = body.Length > 0 ? body : null,
        });
        store.Set(CreatedBase(localId, page.Id, ""));
        store.Save();
        if (body.Length == 0)
            return page.Id;

        if (client.GetPageMarkdown(page.Id).Markdown.Length == 0)
        {
            output.WriteLine(
                $"notion docs sync: page {page.Id} ({title}) sent a non-empty body but read back empty — Notion ignored "
                + "the markdown field on create; leaving the base empty so the body phase writes it via a child-safe PATCH");
            return page.Id;
        }

        store.Set(CreatedBase(localId, page.Id, body));
        store.Save();
        return page.Id;
    }

    /// <summary>Create each subfolder's page under its parent's page (top-down), reusing an existing page from the
    /// store so a re-run never duplicates. A newly minted page is created WITH its index-doc body atomically via
    /// <see cref="CreatePageWithBodyAndRecord"/> (DR 035 §1/§2) — before it has any child pages, so the body write
    /// can never trash the nested docs (§3) — its base recorded read-back-guarded so the body phase is a no-op on a
    /// faithful create and a child-safe PATCH on a silent ignore. Its id is published so its own children resolve
    /// their parent.</summary>
    private static void EnsureFolderPages(
        INotionClient client, BaseSnapshotStore store, DocsNode node, string nodePageId,
        Dictionary<string, string> folderPageId, TextWriter output)
    {
        foreach (var child in OrderedFolders(node))
        {
            var pageId = store.Get(child.LocalId)?.ExternalId
                ?? CreatePageWithBodyAndRecord(client, store, nodePageId, child.LocalId, child.Title, IndexBody(child), output);
            folderPageId[child.LocalId] = pageId;
            EnsureFolderPages(client, store, child, pageId, folderPageId, output);
        }
    }

    /// <summary>Create each file's page under its folder's page, in nav-order then alphabetical (DR 033 §7), after
    /// every folder page already exists so a folder's sub-folders always precede its files in the Notion sidebar.
    /// Reuses an existing page from the store so a re-run never duplicates; a newly minted page is created WITH its
    /// file body atomically via <see cref="CreatePageWithBodyAndRecord"/> (DR 035 §1/§2), its base recorded
    /// read-back-guarded so the body phase is a no-op on a faithful create and a PATCH on a silent ignore.</summary>
    private static void EnsureFilePages(
        INotionClient client, BaseSnapshotStore store, DocsNode node,
        IReadOnlyDictionary<string, string> folderPageId, TextWriter output)
    {
        foreach (var file in OrderedFiles(node))
        {
            if (store.Get(file.LocalId)?.ExternalId != null)
                continue;
            CreatePageWithBodyAndRecord(
                client, store, folderPageId[node.LocalId], file.LocalId, file.Title, FileBody(file), output);
        }
        foreach (var child in OrderedFolders(node))
            EnsureFilePages(client, store, child, folderPageId, output);
    }

    /// <summary>A folder page's body is its index doc (DR 033), read the same way <see cref="Collect"/> reads it so
    /// the base recorded at create-time equals the repo body the body phase computes — keeping the fresh sync a
    /// no-op. An off-limits or absent index yields an empty body (a bare container page).</summary>
    private static string IndexBody(DocsNode node) =>
        node.IndexPath != null ? SyncDocFile.Read(node.IndexPath, node.LocalId, node.IndexPath).Body : "";

    private static string FileBody(DocsFile file) =>
        SyncDocFile.Read(file.Path, file.LocalId, file.Path).Body;

    /// <summary>The base snapshot's last-synced body per page id — handed to <see cref="DocsPageAdapter"/> so a body
    /// that reads back TRUNCATED (past Notion's ~20k-block export ceiling, DR 035 caveat) reuses its last-good body
    /// instead of the cut-short read, never truncating the canonical file.</summary>
    private static Dictionary<string, string> LastSyncedBodyByPageId(BaseSnapshotStore store)
    {
        var map = new Dictionary<string, string>();
        foreach (var localId in store.LocalIds)
            if (store.Get(localId) is { ExternalId: { } externalId } snap)
                map[externalId] = snap.Body;
        return map;
    }

    /// <summary>Flatten the tree into the engine's inputs: a <see cref="SyncDoc"/> per folder (its index body) and
    /// per file, plus the structural maps <see cref="DocsPageAdapter"/> needs (parent page id, page title, and the
    /// repo path to write a merged doc back to).</summary>
    private static void Collect(
        DocsNode node, IReadOnlyDictionary<string, string> folderPageId,
        List<SyncDoc> docs, Dictionary<string, string> parentByLocalId,
        Dictionary<string, string> titleByLocalId, Dictionary<string, string> pathByLocalId)
    {
        // The folder's page body is its index doc; absent one, an empty body that a Notion-side edit would
        // materialise into a fresh _index.md at this path.
        var indexPath = node.IndexPath ?? Path.Combine(node.Dir, "_index.md");
        docs.Add(node.IndexPath != null
            ? SyncDocFile.Read(node.IndexPath, node.LocalId, node.IndexPath)
            : new SyncDoc { LocalId = node.LocalId, Fields = [], Body = "", SourcePath = indexPath });
        titleByLocalId[node.LocalId] = node.Title;
        pathByLocalId[node.LocalId] = indexPath;

        foreach (var file in node.Files)
        {
            docs.Add(SyncDocFile.Read(file.Path, file.LocalId, file.Path));
            parentByLocalId[file.LocalId] = folderPageId[node.LocalId];
            titleByLocalId[file.LocalId] = file.Title;
            pathByLocalId[file.LocalId] = file.Path;
        }

        foreach (var child in node.Folders)
        {
            parentByLocalId[child.LocalId] = folderPageId[node.LocalId];
            Collect(child, folderPageId, docs, parentByLocalId, titleByLocalId, pathByLocalId);
        }
    }

    private static HashSet<string> ManagedPageIds(BaseSnapshotStore store)
    {
        var ids = new HashSet<string>();
        foreach (var localId in store.LocalIds)
        {
            var externalId = store.Get(localId)?.ExternalId;
            if (externalId != null)
                ids.Add(externalId);
        }
        return ids;
    }

    // ---- Tree building -----------------------------------------------------------------------------------

    private static DocsNode BuildTree(string dydoRoot, ISet<string> excluded, IOffLimitsService offLimits) =>
        BuildNode(dydoRoot, RootLocalId, RootTitle, excluded, offLimits)!; // the root is always kept, even if empty

    /// <summary>Build a folder node: its index doc (title + nav-order), its non-index <c>.md</c> files, and its
    /// included subfolders. A non-root folder with no files, no kept subfolders, and no index doc is pruned
    /// (returns null) so empty scaffolding folders never mint a barren Notion page. A guard-off-limits index or
    /// file is omitted (DR 033 §5): it must never become externally editable in Notion and merge back through the
    /// engine's file I/O, bypassing the <c>PreToolUse</c> guard — so an off-limits index leaves a bare container.</summary>
    private static DocsNode? BuildNode(string dir, string localId, string titleDefault, ISet<string> excluded, IOffLimitsService offLimits)
    {
        var indexPath = FindIndex(dir);
        if (indexPath != null && IsOffLimits(offLimits, indexPath))
            indexPath = null;
        var (title, navOrder) = indexPath != null ? TitleAndOrder(indexPath, titleDefault) : (titleDefault, DefaultNavOrder);

        var node = new DocsNode { LocalId = localId, Title = title, Dir = dir, IndexPath = indexPath, NavOrder = navOrder };

        foreach (var file in Directory.EnumerateFiles(dir, "*.md").OrderBy(p => p, StringComparer.Ordinal))
        {
            var name = Path.GetFileName(file);
            // Index files are the folder body (handled above); other underscore-prefixed files are metadata by dydo
            // convention (templates, drafts) and are not browsable docs.
            if (IsIndex(name) || name.StartsWith('_'))
                continue;
            if (IsOffLimits(offLimits, file))
                continue;
            var stem = Path.GetFileNameWithoutExtension(name);
            var (fileTitle, fileOrder) = TitleAndOrder(file, stem);
            node.Files.Add(new DocsFile
            {
                LocalId = Join(localId, stem),
                Title = fileTitle,
                Path = file,
                NavOrder = fileOrder,
            });
        }

        foreach (var sub in Directory.EnumerateDirectories(dir).OrderBy(p => p, StringComparer.Ordinal))
        {
            var name = Path.GetFileName(sub);
            var subLocalId = Join(localId, name);
            // Full-path match only (finding 3): a bare leaf-name match would wrongly exclude ANY folder anywhere
            // whose name collides with a spine/framework dir (e.g. a deeper understand/releases/).
            if (excluded.Contains(subLocalId))
                continue;
            var child = BuildNode(sub, subLocalId, name, excluded, offLimits);
            if (child != null)
                node.Folders.Add(child);
        }

        if (localId != RootLocalId && node.Files.Count == 0 && node.Folders.Count == 0 && indexPath == null)
            return null;
        return node;
    }

    private static IEnumerable<DocsNode> OrderedFolders(DocsNode node) =>
        node.Folders.OrderBy(f => f.NavOrder).ThenBy(f => f.LocalId, StringComparer.Ordinal);

    private static IEnumerable<DocsFile> OrderedFiles(DocsNode node) =>
        node.Files.OrderBy(f => f.NavOrder).ThenBy(f => f.LocalId, StringComparer.Ordinal);

    private static string Join(string localId, string segment) =>
        localId == RootLocalId ? segment : localId + "/" + segment;

    private static string? FindIndex(string dir)
    {
        foreach (var name in new[] { "_index.md", "index.md" })
        {
            var path = Path.Combine(dir, name);
            if (File.Exists(path))
                return path;
        }
        return null;
    }

    private static bool IsIndex(string fileName) =>
        fileName.Equals("_index.md", StringComparison.OrdinalIgnoreCase)
        || fileName.Equals("index.md", StringComparison.OrdinalIgnoreCase);

    /// <summary>A doc's Notion page title (frontmatter <c>title</c> if present, else the file/folder name) and its
    /// optional <c>nav-order</c> override for sibling ordering (DR 033 §7). A missing/non-numeric nav-order sorts
    /// after every explicitly ordered sibling.</summary>
    private static (string Title, int NavOrder) TitleAndOrder(string path, string fallbackTitle)
    {
        var doc = SyncDocFile.Read(path, fallbackTitle, path);
        var title = doc.GetField("title");
        var order = int.TryParse(doc.GetField("nav-order"), out var n) ? n : DefaultNavOrder;
        return (!string.IsNullOrWhiteSpace(title) ? title! : fallbackTitle, order);
    }

    private static Dictionary<string, NotionPropertyValue> TitleProperty(string title) => new()
    {
        ["title"] = new NotionPropertyValue { Type = "title", Title = NotionRichText.Of(title) },
    };

    /// <summary>The base snapshot entry for a just-created page, recording the body written atomically in its
    /// create (DR 035 §1/§2) so the body phase reads base == repo == external and stays a no-op — no PATCH.</summary>
    private static SyncDoc CreatedBase(string localId, string externalId, string body) => new()
    {
        LocalId = localId,
        ExternalId = externalId,
        Fields = [],
        Body = body,
        SourcePath = "",
    };

    /// <summary>Preview the reconcile without writing (DR 033, smoke visibility): a header, then one line per page
    /// the run WOULD touch — the root "Docs" page, each folder page, each doc page — plus a line for every managed
    /// page whose repo doc/folder has disappeared. Membership in the parent-scoped snapshot store distinguishes
    /// create (unseen) from update (already mapped); the store is only READ here, never saved, so a dry-run leaves
    /// no trace.
    /// <para>The disappeared-doc lines are ACCURATE, not a local-only guess (finding 5a, issue 0221): the real run
    /// only archives a removed doc's page when the page still EXISTS in Notion AND its body has not drifted from the
    /// base since the last sync. So each is labeled from the live tree read here:
    /// <list type="bullet">
    /// <item>"retire" — the page is already gone from Notion (both sides gone → Retire, not archive);</item>
    /// <item>"resurrect" — the page is present but its body drifted from the base, so <see cref="ReconcileEngine"/>'s
    /// delete-vs-external-edit Conflict RESURRECTS the repo file from the surviving Notion edit rather than archiving;</item>
    /// <item>"archive" — the page is present and unchanged, so the removal propagates as a genuine archive.</item>
    /// </list>
    /// Reads only; no page is minted, no body appended, no snapshot saved.</para></summary>
    private static void WriteDryRunPlan(DocsNode root, INotionClient client, string dydoRoot, string parentPageId, TextWriter output)
    {
        var store = new BaseSnapshotStore(BaseSnapshotStore.PathFor(dydoRoot, SnapshotAdapterName(parentPageId)));
        var known = new HashSet<string>(store.LocalIds);
        output.WriteLine(
            $"notion docs sync --dry-run: {CountFolders(root)} folder page(s) and {CountFiles(root)} doc page(s) under a \"{RootTitle}\" page");

        var planned = new HashSet<string>();
        WritePlanNode(root, known, planned, output, isRoot: true);

        var disappeared = known.Where(id => !planned.Contains(id)).OrderBy(x => x, StringComparer.Ordinal).ToList();
        if (disappeared.Count == 0)
            return;

        var present = PresentExternalBodies(client, store);
        foreach (var localId in disappeared)
        {
            var externalId = store.Get(localId)?.ExternalId;
            string label;
            if (externalId == null || !present.TryGetValue(externalId, out var externalBody))
                label = "retire";
            else
                label = BodyDrifted(store.Get(localId)?.Body ?? "", externalBody) ? "resurrect" : "archive";
            output.WriteLine($"  docs       {label,-9}  {localId}");
        }
    }

    /// <summary>The live managed tree's current pages keyed external id → body, so the dry-run can tell a retire
    /// (page gone), a resurrect (page present but body drifted from base), and an archive (page present, unchanged)
    /// apart. Reads the Notion tree via the same walk the real run uses; an empty store (nothing minted yet) has no
    /// root page, so nothing is present.</summary>
    private static Dictionary<string, string> PresentExternalBodies(INotionClient client, BaseSnapshotStore store)
    {
        var rootPageId = store.Get(RootLocalId)?.ExternalId;
        if (rootPageId == null)
            return [];
        var adapter = new DocsPageAdapter(
            client, rootPageId, new Dictionary<string, string>(), new Dictionary<string, string>(), ManagedPageIds(store));
        return adapter.ReadExternalState().ToDictionary(r => r.ExternalId, r => r.Body);
    }

    /// <summary>Whether a Notion page's body has drifted from the base snapshot's recorded body, compared modulo the
    /// markdown dialect normalization (DR 035 §3) and line endings — exactly the equality <see cref="ReconcileEngine"/>
    /// uses to decide the delete-vs-external-edit branch. A drift means the real run resurrects rather than archives.</summary>
    private static bool BodyDrifted(string baseBody, string externalBody) =>
        DocsMarkdownNormalizer.Normalize(baseBody) != DocsMarkdownNormalizer.Normalize(externalBody);

    private static void WritePlanNode(DocsNode node, ISet<string> known, ISet<string> planned, TextWriter output, bool isRoot)
    {
        planned.Add(node.LocalId);
        output.WriteLine(isRoot
            ? $"  docs       {Action(known, node.LocalId),-7}  \"{RootTitle}\" (root page)"
            : $"  docs       {Action(known, node.LocalId),-7}  {node.LocalId}  ({node.Title})");

        foreach (var file in OrderedFiles(node))
        {
            planned.Add(file.LocalId);
            output.WriteLine($"  docs       {Action(known, file.LocalId),-7}  {file.LocalId}  ({file.Title})");
        }
        foreach (var child in OrderedFolders(node))
            WritePlanNode(child, known, planned, output, isRoot: false);
    }

    private static string Action(ISet<string> known, string localId) => known.Contains(localId) ? "update" : "create";

    private static int CountFolders(DocsNode node) => 1 + node.Folders.Sum(CountFolders);
    private static int CountFiles(DocsNode node) => node.Files.Count + node.Folders.Sum(CountFiles);

    private const int DefaultNavOrder = int.MaxValue;

    private sealed class DocsNode
    {
        public required string LocalId { get; init; }
        public required string Title { get; init; }
        public required string Dir { get; init; }
        public required string? IndexPath { get; init; }
        public required int NavOrder { get; init; }
        public List<DocsNode> Folders { get; } = [];
        public List<DocsFile> Files { get; } = [];
    }

    private sealed class DocsFile
    {
        public required string LocalId { get; init; }
        public required string Title { get; init; }
        public required string Path { get; init; }
        public required int NavOrder { get; init; }
    }
}
