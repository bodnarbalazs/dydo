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
        AdapterName + "-" + ShortHash(parentPageId);

    private static string ShortHash(string value)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes)[..8].ToLowerInvariant();
    }
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
        //    sub-folders precede files in the sidebar — DR 033 §7). Each new page is persisted to the store the
        //    instant it is created: a mid-phase CreatePage failure (a 429/500 under the ~3 req/s throttle, or a
        //    process kill) must never leave a created page unrecorded and re-minted as a duplicate on retry.
        var rootPageId = EnsureRootPage(client, store, parentPageId);
        var folderPageId = new Dictionary<string, string> { [RootLocalId] = rootPageId };
        EnsureFolderPages(client, store, root, rootPageId, folderPageId);
        EnsureFilePages(client, store, root, folderPageId);

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
        var adapter = new DocsPageAdapter(client, rootPageId, parentByLocalId, titleByLocalId, managed, output);
        var runner = new SyncRunner(adapter, store,
            (localId, _, _) => pathByLocalId.TryGetValue(localId, out var path) ? path : Path.Combine(dydoRoot, localId + ".md"),
            // A genuine two-sided conflict is diverted here — never written to the canonical repo file (DR 035
            // §4/§5, the root-cause fix for issue 0235). The shadow tree lives under _system, already excluded
            // from the mirror's walk (ExcludedDirs), so a conflict can never cascade back through the sync.
            localId => ShadowPathFor(dydoRoot, localId));

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
            File.WriteAllText(canonical, content);
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
                        Body = DocsMarkdownNormalizer.CleanForPersist(client.GetPageMarkdown(pageId)),
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

    private static string EnsureRootPage(INotionClient client, BaseSnapshotStore store, string parentPageId)
    {
        var existing = store.Get(RootLocalId)?.ExternalId;
        if (existing != null)
            return existing;

        var page = client.CreatePage(new NotionPageCreateRequest
        {
            Parent = NotionParent.Page(parentPageId),
            Properties = TitleProperty(RootTitle),
        });
        store.Set(RootBase(RootLocalId, page.Id));
        store.Save();
        return page.Id;
    }

    /// <summary>Create each subfolder's page under its parent's page (top-down), reusing an existing page from the
    /// store so a re-run never duplicates. A newly minted page is recorded with an empty body — the engine fills it
    /// from the folder's index doc on the same tick — persisted immediately (crash-safety, §1) and its id published
    /// so its own children resolve their parent.</summary>
    private static void EnsureFolderPages(
        INotionClient client, BaseSnapshotStore store, DocsNode node, string nodePageId, Dictionary<string, string> folderPageId)
    {
        foreach (var child in OrderedFolders(node))
        {
            var pageId = store.Get(child.LocalId)?.ExternalId;
            if (pageId == null)
            {
                var page = client.CreatePage(new NotionPageCreateRequest
                {
                    Parent = NotionParent.Page(nodePageId),
                    Properties = TitleProperty(child.Title),
                });
                pageId = page.Id;
                store.Set(RootBase(child.LocalId, pageId));
                store.Save();
            }
            folderPageId[child.LocalId] = pageId;
            EnsureFolderPages(client, store, child, pageId, folderPageId);
        }
    }

    /// <summary>Create each file's page under its folder's page, in nav-order then alphabetical (DR 033 §7), after
    /// every folder page already exists so a folder's sub-folders always precede its files in the Notion sidebar.
    /// Reuses an existing page from the store so a re-run never duplicates; a newly minted page is recorded with an
    /// empty body — the engine fills it from the file on the same tick — and persisted immediately (crash-safety, §1).</summary>
    private static void EnsureFilePages(
        INotionClient client, BaseSnapshotStore store, DocsNode node, IReadOnlyDictionary<string, string> folderPageId)
    {
        foreach (var file in OrderedFiles(node))
        {
            if (store.Get(file.LocalId)?.ExternalId != null)
                continue;
            var page = client.CreatePage(new NotionPageCreateRequest
            {
                Parent = NotionParent.Page(folderPageId[node.LocalId]),
                Properties = TitleProperty(file.Title),
            });
            store.Set(RootBase(file.LocalId, page.Id));
            store.Save();
        }
        foreach (var child in OrderedFolders(node))
            EnsureFilePages(client, store, child, folderPageId);
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

    private static SyncDoc RootBase(string localId, string externalId) => new()
    {
        LocalId = localId,
        ExternalId = externalId,
        Fields = [],
        Body = "",
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
