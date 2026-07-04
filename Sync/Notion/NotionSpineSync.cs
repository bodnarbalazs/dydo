namespace DynaDocs.Sync.Notion;

using DynaDocs.Models;
using DynaDocs.Sync.Model;
using DynaDocs.Sync.Notion.Provisioning;

/// <summary>
/// Orchestrates one model-driven sync tick (slice brief §3/§4/§6): load the project's sync model,
/// provision each object type's database idempotently in dependency order, then reconcile each type's
/// canonical repo docs against its data source — parents before children, so a child's relation can
/// resolve its parent's Notion page id from the parent's just-advanced base. No object-type names are
/// hardcoded; everything comes from the model. <c>--dry-run</c> previews the loaded model, the databases
/// to create vs reuse, and the per-type reconcile actions, writing nothing to Notion.
/// </summary>
public static class NotionSpineSync
{
    public static void Run(INotionClient client, string dydoRoot, string parentPageId, bool dryRun, TextWriter output, bool prune = false)
    {
        var model = SyncModelLoader.Load(dydoRoot);
        var types = model.InDependencyOrder();
        var provisioner = new NotionProvisioner(client, NotionProvisioner.PathFor(dydoRoot));

        output.WriteLine(dryRun
            ? $"notion sync --dry-run: model has {types.Count} object type(s) [{string.Join(" -> ", types.Select(t => t.Type))}] under parent page {parentPageId}"
            : $"notion sync: provisioning + reconciling {types.Count} object type(s) under parent page {parentPageId}");

        var dataSourceIds = Provision(provisioner, types, parentPageId, dryRun, output);
        if (!dryRun)
        {
            provisioner.Save();
            CheckDrift(client, model, types, dataSourceIds, prune, output);
        }

        Reconcile(client, dydoRoot, types, dataSourceIds, dryRun, output);
    }

    /// <summary>Schema-shape ownership (DR 029 §6): after every type's data source is resolved, compare its
    /// live schema against the model and report rogue additions — warned and left, or deleted under
    /// <paramref name="prune"/>. Read-only in the default path; never runs in dry-run.</summary>
    private static void CheckDrift(
        INotionClient client, SyncModel model, IReadOnlyList<SyncObjectType> types,
        IReadOnlyDictionary<string, string> dataSourceIds, bool prune, TextWriter output)
    {
        foreach (var type in types)
            if (dataSourceIds.TryGetValue(type.Type, out var dataSourceId))
                NotionSchemaDrift.Check(model, type, dataSourceId, client, prune, output);
    }

    /// <summary>Resolve each type's data source id, creating the database when not already provisioned.
    /// In dry-run nothing is created; an unprovisioned type simply has no entry in the returned map.</summary>
    private static Dictionary<string, string> Provision(
        NotionProvisioner provisioner, IReadOnlyList<SyncObjectType> types,
        string parentPageId, bool dryRun, TextWriter output)
    {
        var dataSourceIds = new Dictionary<string, string>();
        var created = new List<SyncObjectType>();
        foreach (var type in types)
        {
            var existing = provisioner.Lookup(type.Type);
            if (existing != null)
            {
                output.WriteLine($"  provision  {type.Type,-9} reuse data source {existing.DataSourceId}");
                dataSourceIds[type.Type] = existing.DataSourceId;
            }
            else if (dryRun)
            {
                output.WriteLine($"  provision  {type.Type,-9} would create database \"{type.NotionTitle}\"");
            }
            else
            {
                var record = provisioner.Create(type, parentPageId, dataSourceIds);
                output.WriteLine($"  provision  {type.Type,-9} created database {record.DatabaseId} (data source {record.DataSourceId})");
                dataSourceIds[type.Type] = record.DataSourceId;
                created.Add(type);
            }
        }

        // Rollup + formula post-pass (DR 029 §5 / DR 030 §2/§4) in CHILD-FIRST order — the reverse of the
        // parent-first create order. A parent's rollup targets a child property (a checkbox, a date, or a
        // child FORMULA such as `attention`), and a parent's health/attention formulas read those rollups.
        // Real Notion validates a rollup's target property at creation exactly as it validates a formula's
        // referents, so every child column a parent references — including a child's DEFERRED formula, only
        // PATCHed in that child's own formula pass — must already exist when the parent's pass runs. Doing
        // AddRollups(type) then AddFormulas(type) per type, children before parents, guarantees it: e.g.
        // SprintTask.attention is patched before Sprint's attention-count rollup targets it, and Sprint's
        // needs-human-count formula before Campaign's needs-human rollup sums it.
        var postPass = types.Reverse().ToList();
        if (dryRun)
        {
            foreach (var type in postPass)
            {
                if (NotionProvisioner.HasRollups(type))
                    output.WriteLine($"  provision  {type.Type,-9} would add rollup properties");
                if (NotionProvisioner.HasDeferredFormulas(type))
                    output.WriteLine($"  provision  {type.Type,-9} would add formula properties");
            }
        }
        else
        {
            foreach (var type in postPass.Where(created.Contains))
            {
                if (NotionProvisioner.HasRollups(type))
                {
                    provisioner.AddRollups(type);
                    output.WriteLine($"  provision  {type.Type,-9} added rollup properties");
                }
                if (NotionProvisioner.HasDeferredFormulas(type))
                {
                    provisioner.AddFormulas(type);
                    output.WriteLine($"  provision  {type.Type,-9} added formula properties");
                }
            }
        }
        return dataSourceIds;
    }

    private static void Reconcile(
        INotionClient client, string dydoRoot, IReadOnlyList<SyncObjectType> types,
        IReadOnlyDictionary<string, string> dataSourceIds, bool dryRun, TextWriter output)
    {
        var localToPageByType = new Dictionary<string, Dictionary<string, string>>();

        foreach (var type in types)
        {
            var docsDir = Path.Combine(dydoRoot, type.Dir);
            var docs = LoadDocs(docsDir);

            if (!dataSourceIds.TryGetValue(type.Type, out var dataSourceId))
            {
                output.WriteLine($"  sync       {type.Type,-9} would create database, then create {docs.Count} page(s)");
                continue;
            }

            var store = new BaseSnapshotStore(BaseSnapshotStore.PathFor(dydoRoot, "notion-" + type.Type.ToLowerInvariant()));

            // Publish this type's own local↔page map from its base snapshot BEFORE building relation maps,
            // so a self-relation (SprintTask.blocked-by → SprintTask) resolves against pages synced on a
            // prior tick — without this seed the type's map is only exposed after its own reconcile, so a
            // self-relation would never resolve on write nor render back to local ids on read (DR 029 §5).
            // It is refreshed after reconcile to publish this tick's newly-created pages to any children.
            localToPageByType[type.Type] = LocalToPageIds(store);

            var (relationLocalToPage, relationPageToLocal) = RelationMaps(type, localToPageByType);

            // Engine-computed properties (last-activity, DR 030 §3) are written one-way from the base store's
            // per-object activity date and dropped on read, so they never enter frontmatter.
            var engineSchema = type.Properties
                .Where(p => p.Value.EngineComputed)
                .ToDictionary(p => p.Key, p => p.Value.Type);
            var adapter = new NotionSyncAdapter(
                client, dataSourceId, type.FieldSchema(), relationLocalToPage, relationPageToLocal, type.Icon,
                engineSchema, store.GetLastActivity);
            var runner = new SyncRunner(adapter, store, RepoFolderLayout.For(type, docsDir).PathFor);

            if (dryRun)
            {
                foreach (var result in runner.Plan(docs))
                    output.WriteLine($"  sync       {type.Type,-9} {result.Action,-14} {result.LocalId}");
            }
            else
            {
                var run = runner.Run(docs);
                output.WriteLine($"  sync       {type.Type,-9} reconciled {run.Results.Count} object(s)");
                if (run.ConflictCount > 0)
                    output.WriteLine($"             {run.ConflictCount} conflict(s): {string.Join(", ", run.ConflictedLocalIds)}");
            }

            localToPageByType[type.Type] = LocalToPageIds(store);
        }
    }

    /// <summary>Merge the local↔page id maps of every parent type this type relates to, so its relation
    /// values resolve to real Notion page ids on write and back to parent local ids on read.</summary>
    private static (Dictionary<string, string> LocalToPage, Dictionary<string, string> PageToLocal) RelationMaps(
        SyncObjectType type, IReadOnlyDictionary<string, Dictionary<string, string>> localToPageByType)
    {
        var localToPage = new Dictionary<string, string>();
        foreach (var target in type.RelationTargets())
            if (localToPageByType.TryGetValue(target, out var parentMap))
                foreach (var (local, page) in parentMap)
                    localToPage[local] = page;

        var pageToLocal = new Dictionary<string, string>();
        foreach (var (local, page) in localToPage)
            pageToLocal[page] = local;
        return (localToPage, pageToLocal);
    }

    /// <summary>Pool every <c>*.md</c> under a type's canonical directory — recursively, across all subfolders
    /// — as a <see cref="SyncDoc"/> keyed by stem. Folder placement is derived presentation (slice brief §3):
    /// an object filed under <c>resolved/</c> is the same logical row as one at the dir root, so both are read.
    /// Skips any file whose stem or a containing folder is <c>_</c>-prefixed (e.g. <c>_index.md</c>,
    /// <c>_templates/</c>): by dydo convention those are folder metadata, not domain objects, never synced as rows.</summary>
    public static List<SyncDoc> LoadDocs(string dir)
    {
        if (!Directory.Exists(dir))
            return [];

        var docs = new List<SyncDoc>();
        foreach (var path in Directory.EnumerateFiles(dir, "*.md", SearchOption.AllDirectories).OrderBy(p => p))
        {
            var relative = Path.GetRelativePath(dir, path);
            if (relative.Split('/', '\\').Any(segment => segment.StartsWith('_')))
                continue;
            var localId = Path.GetFileNameWithoutExtension(path);
            docs.Add(SyncDocFile.Read(path, localId, path));
        }
        return docs;
    }

    /// <summary>Build the local id → Notion page id map for a type from its base snapshot, for child relations.</summary>
    private static Dictionary<string, string> LocalToPageIds(BaseSnapshotStore store)
    {
        var map = new Dictionary<string, string>();
        foreach (var localId in store.LocalIds)
        {
            var externalId = store.Get(localId)?.ExternalId;
            if (externalId != null)
                map[localId] = externalId;
        }
        return map;
    }
}
