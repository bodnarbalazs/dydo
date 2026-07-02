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
    public static void Run(INotionClient client, string dydoRoot, string parentPageId, bool dryRun, TextWriter output)
    {
        var types = SyncModelLoader.Load(dydoRoot).InDependencyOrder();
        var provisioner = new NotionProvisioner(client, NotionProvisioner.PathFor(dydoRoot));

        output.WriteLine(dryRun
            ? $"notion sync --dry-run: model has {types.Count} object type(s) [{string.Join(" -> ", types.Select(t => t.Type))}] under parent page {parentPageId}"
            : $"notion sync: provisioning + reconciling {types.Count} object type(s) under parent page {parentPageId}");

        var dataSourceIds = Provision(provisioner, types, parentPageId, dryRun, output);
        if (!dryRun)
            provisioner.Save();

        Reconcile(client, dydoRoot, types, dataSourceIds, dryRun, output);
    }

    /// <summary>Resolve each type's data source id, creating the database when not already provisioned.
    /// In dry-run nothing is created; an unprovisioned type simply has no entry in the returned map.</summary>
    private static Dictionary<string, string> Provision(
        NotionProvisioner provisioner, IReadOnlyList<SyncObjectType> types,
        string parentPageId, bool dryRun, TextWriter output)
    {
        var dataSourceIds = new Dictionary<string, string>();
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
                var created = provisioner.Create(type, parentPageId, dataSourceIds);
                output.WriteLine($"  provision  {type.Type,-9} created database {created.DatabaseId} (data source {created.DataSourceId})");
                dataSourceIds[type.Type] = created.DataSourceId;
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

            var (relationLocalToPage, relationPageToLocal) = RelationMaps(type, localToPageByType);

            var adapter = new NotionSyncAdapter(client, dataSourceId, type.FieldSchema(), relationLocalToPage, relationPageToLocal, type.Icon, output);
            var store = new BaseSnapshotStore(BaseSnapshotStore.PathFor(dydoRoot, "notion-" + type.Type.ToLowerInvariant()));
            var runner = new SyncRunner(adapter, store, localId => Path.Combine(docsDir, localId + ".md"));

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

    /// <summary>Read every <c>*.md</c> in a type's canonical directory as a <see cref="SyncDoc"/> keyed by stem.
    /// Skips <c>_</c>-prefixed files (e.g. <c>_index.md</c>, <c>_tasks.md</c>): by dydo convention those are
    /// folder metadata, not domain objects, so they must never sync as rows.</summary>
    public static List<SyncDoc> LoadDocs(string dir)
    {
        if (!Directory.Exists(dir))
            return [];

        var docs = new List<SyncDoc>();
        foreach (var path in Directory.EnumerateFiles(dir, "*.md", SearchOption.TopDirectoryOnly).OrderBy(p => p))
        {
            var localId = Path.GetFileNameWithoutExtension(path);
            if (localId.StartsWith('_'))
                continue;
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
