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
            WriteDryRunPlan(root, dydoRoot, output);
            return;
        }

        var store = new BaseSnapshotStore(BaseSnapshotStore.PathFor(dydoRoot, AdapterName));

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

        var managed = ManagedPageIds(store);
        var adapter = new DocsPageAdapter(client, rootPageId, parentByLocalId, titleByLocalId, managed);
        var runner = new SyncRunner(adapter, store, (localId, _, _) =>
            pathByLocalId.TryGetValue(localId, out var path) ? path : Path.Combine(dydoRoot, localId + ".md"));

        var result = runner.Run(docs);
        output.WriteLine($"notion docs sync: reconciled {result.Results.Count} page(s) under \"{RootTitle}\"");
        if (result.ConflictCount > 0)
            output.WriteLine($"  {result.ConflictCount} conflict(s): {string.Join(", ", result.ConflictedLocalIds)}");
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
    /// the run WOULD touch — the root "Docs" page, each folder page, each doc page — plus an archive line for every
    /// managed page whose repo doc/folder has disappeared. Membership in the <c>notion-docs</c> snapshot store
    /// distinguishes create (unseen) from update (already mapped); the store is only READ here, never saved, so a
    /// dry-run leaves no trace.</summary>
    private static void WriteDryRunPlan(DocsNode root, string dydoRoot, TextWriter output)
    {
        var known = new HashSet<string>(new BaseSnapshotStore(BaseSnapshotStore.PathFor(dydoRoot, AdapterName)).LocalIds);
        output.WriteLine(
            $"notion docs sync --dry-run: {CountFolders(root)} folder page(s) and {CountFiles(root)} doc page(s) under a \"{RootTitle}\" page");

        var planned = new HashSet<string>();
        WritePlanNode(root, known, planned, output, isRoot: true);

        foreach (var localId in known.Where(id => !planned.Contains(id)).OrderBy(x => x, StringComparer.Ordinal))
            output.WriteLine($"  docs       {"archive",-7}  {localId}");
    }

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
