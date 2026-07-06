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

        var (dataSourceIds, minted) = Provision(provisioner, types, dydoRoot, parentPageId, dryRun, output);
        // No provisioner.Save() here: persistence now lives inside Create and MarkPostPassDone, each of which
        // writes the instant its fact is established (wave 5). A standalone Save would only re-serialize identical
        // state.
        if (!dryRun)
            CheckDrift(client, model, types, dataSourceIds, prune, output);

        Reconcile(client, dydoRoot, types, dataSourceIds, minted, dryRun, output);
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
    /// In dry-run nothing is created; an unprovisioned type simply has no entry in the returned map. Returns
    /// the set of types whose database was freshly MINTED this run (Lookup returned null → Create), so
    /// <see cref="Reconcile"/> can reset their base snapshot before reconciling: a re-provision (definitive
    /// 404, or the database no longer owning its recorded data source) mints a fresh EMPTY database whose
    /// pages the stale base does not point at (finding 1).</summary>
    private static (Dictionary<string, string> DataSourceIds, HashSet<string> Minted) Provision(
        NotionProvisioner provisioner, IReadOnlyList<SyncObjectType> types,
        string dydoRoot, string parentPageId, bool dryRun, TextWriter output)
    {
        var dataSourceIds = new Dictionary<string, string>();
        var minted = new HashSet<string>();
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
                // Record it as would-be-created so the dry-run post-pass preview below matches a real run.
                // A real run runs the post-pass for types created this run PLUS any recorded-but-unpassed type
                // (a mid-provision throw can record a database without post-passing it) — never a fully
                // provisioned, already-post-passed reuse (finding 8 / review R2-1).
                created.Add(type);
            }
            else
            {
                // Durably reset this type's base snapshot BEFORE the fresh EMPTY database is minted (finding 2,
                // strictly better than after: review R2-1). NotionProvisioner.Create persists the new database's ids
                // immediately, but the in-memory store.Reset() in Reconcile is not persisted until SyncRunner's
                // end-of-tick Save — so an abort between the mint and that Save (a transient 429/5xx in CheckDrift, a
                // throw in the adapter's external read, a process kill) would leave the fresh database recorded while
                // the STALE snapshot.json survived on disk, and the NEXT run — reusing the now-valid empty database
                // with nothing minted, hence no reset — would read every base+repo pair as an external delete and
                // wipe the repo. Deleting first makes the window data-preserving both ways: a crash after the delete
                // but before the create just re-mints next run, and a delete failure (share-lock/AV/read-only) throws
                // BEFORE any database exists, so nothing is minted against a stale snapshot.
                //
                // SCOPE BOUNDARY (wave 8, item 1): re-minting a PARENT type here re-creates its pages with new ids, and
                // a child's still-valid relation is preserved and re-pushed to point at those new pages (ReconcileEngine's
                // stale-echo branch). What is NOT done is re-pointing the CHILD relation property's SCHEMA — pinned to the
                // parent's original data_source_id at child-create time — to the new parent data source. So the re-pushed
                // page ids target a relation schema whose data source was deleted; live Notion MAY reject that, wedging the
                // child's sync loudly mid-tick. This is strictly non-destructive (an aborted tick advances no base and
                // deletes no repo file) — a loud wedge, never the pre-wave-8 silent clear. Full convergence requires schema
                // re-pointing plus reverse-relation/rollup re-synthesis, deferred to the retro-provisioning work pending
                // live-Notion verification.
                BaseSnapshotStore.DeleteSnapshot(BaseSnapshotStore.PathFor(dydoRoot, "notion-" + type.Type.ToLowerInvariant()));
                var record = provisioner.Create(type, parentPageId, dataSourceIds);
                output.WriteLine($"  provision  {type.Type,-9} created database {record.DatabaseId} (data source {record.DataSourceId})");
                dataSourceIds[type.Type] = record.DataSourceId;
                minted.Add(type.Type);
                // No `created.Add` here: the real-run post-pass below drives entirely off PostPassPending
                // (a create records the type with PostPassDone=false), so this type is already covered
                // whether it was created this run or is a recorded-but-unpassed reuse (finding 10).
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
        // A type owes its post-pass if it is created THIS run, or was recorded on a prior run whose post-pass
        // never completed (a mid-provision throw records databases before their rollups/formulas are PATCHed;
        // the retry reuses them, so their attention-layer schema must still be added — review R2-1). By the
        // time this runs every type's database exists (created this run or reused), so the children whose
        // reverse relations a parent's rollups read are all present: running the post-pass child-first is safe.
        var postPass = types.Reverse().ToList();
        if (dryRun)
        {
            foreach (var type in postPass.Where(t => created.Contains(t) || provisioner.PostPassPending(t.Type)))
            {
                if (NotionProvisioner.HasSelfRelations(type))
                    output.WriteLine($"  provision  {type.Type,-9} would add self-relation properties");
                if (NotionProvisioner.HasRollups(type))
                    output.WriteLine($"  provision  {type.Type,-9} would add rollup properties");
                if (NotionProvisioner.HasDeferredFormulas(type))
                    output.WriteLine($"  provision  {type.Type,-9} would add formula properties");
            }
        }
        else
        {
            foreach (var type in postPass.Where(t => provisioner.PostPassPending(t.Type)))
            {
                // Self-relations first: a deferred formula may read one, so it must exist before AddFormulas.
                if (NotionProvisioner.HasSelfRelations(type))
                {
                    provisioner.AddSelfRelations(type);
                    output.WriteLine($"  provision  {type.Type,-9} added self-relation properties");
                }
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
                // Persist completion immediately (mirrors the per-create Save) so a later throw in this same
                // post-pass does not force an already-done type to re-run — and marks the flag for reuse ticks.
                provisioner.MarkPostPassDone(type.Type);
            }
        }
        return (dataSourceIds, minted);
    }

    private static void Reconcile(
        INotionClient client, string dydoRoot, IReadOnlyList<SyncObjectType> types,
        IReadOnlyDictionary<string, string> dataSourceIds, IReadOnlySet<string> minted, bool dryRun, TextWriter output)
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

            // A freshly minted database is EMPTY, but this type's base snapshot still points at the pages of the
            // now-abandoned database. Reconciling against the stale base would read every external as deleted and
            // mass-delete the repo (finding 1). The durable reset already happened at mint time (Provision deleted
            // the snapshot file via BaseSnapshotStore.DeleteSnapshot), so the store constructed just above loaded
            // EMPTY — this in-memory Reset is a same-process belt-and-suspenders guard, harmless if the file was
            // already cleared. Either way every repo doc re-pushes as a CREATE into the new database. Deliberate
            // tradeoff: a mere LOSS OF ACCESS (Notion 404s an unshared page just as it does a deleted one) now
            // re-pushes the repo as creates — possibly duplicating into a fresh database — rather than deleting the
            // repo. Data-preserving by design: a spurious duplicate is recoverable, a mass repo deletion is not.
            if (minted.Contains(type.Type))
                store.Reset();

            // Publish this type's own local↔page map from its base snapshot BEFORE building relation maps,
            // so a self-relation (SprintTask.blocked-by → SprintTask) resolves against pages synced on a
            // prior tick — without this seed the type's map is only exposed after its own reconcile, so a
            // self-relation would never resolve on write nor render back to local ids on read (DR 029 §5).
            // It is refreshed after reconcile to publish this tick's newly-created pages to any children.
            localToPageByType[type.Type] = LocalToPageIds(store);

            var (relationLocalToPageByField, relationPageToLocal) = RelationMaps(type, localToPageByType);

            // Engine-computed properties (last-activity, DR 030 §3) are written one-way from the base store's
            // per-object activity date and dropped on read, so they never enter frontmatter.
            var engineSchema = type.Properties
                .Where(p => p.Value.EngineComputed)
                .ToDictionary(p => p.Key, p => p.Value.Type);
            var adapter = new NotionSyncAdapter(
                client, dataSourceId, type.FieldSchema(), relationLocalToPageByField, relationPageToLocal, type.Icon,
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

    /// <summary>Build this type's relation id maps. On write each relation FIELD maps to its own declared
    /// target type's local→page map (keyed by field name), so two fields pointing at different databases never
    /// collide on a shared local-id stem (slice brief §3). On read a single merged page→local map suffices —
    /// Notion page ids are globally unique, so no two target types can collide on a key.</summary>
    private static (Dictionary<string, IReadOnlyDictionary<string, string>> LocalToPageByField, Dictionary<string, string> PageToLocal) RelationMaps(
        SyncObjectType type, IReadOnlyDictionary<string, Dictionary<string, string>> localToPageByType)
    {
        var localToPageByField = new Dictionary<string, IReadOnlyDictionary<string, string>>();
        var pageToLocal = new Dictionary<string, string>();
        foreach (var (name, def) in type.Properties)
        {
            if (def.Type != "relation" || def.To == null)
                continue;
            var targetMap = localToPageByType.TryGetValue(def.To, out var m) ? m : new Dictionary<string, string>();
            localToPageByField[name] = targetMap;
            foreach (var (local, page) in targetMap)
                pageToLocal[page] = local;
        }
        return (localToPageByField, pageToLocal);
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
