namespace DynaDocs.Sync.Notion;

using DynaDocs.Models;
using DynaDocs.Sync.Model;
using DynaDocs.Sync.Notion.Dtos;
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
    public static NotionSpineSyncResult Run(INotionClient client, NotionSpineState state, bool dryRun, TextWriter output, bool prune = false, bool allowMassDelete = false)
    {
        var dydoRoot = state.DydoRoot;
        var model = SyncModelLoader.Load(dydoRoot);
        var types = model.InDependencyOrder();
        var provisioner = new NotionProvisioner(client, state.ProvisionPath, output);

        output.WriteLine(dryRun
            ? $"notion sync --dry-run: model has {types.Count} object type(s) [{string.Join(" -> ", types.Select(t => t.Type))}] under parent page {state.ParentPageId}"
            : $"notion sync: provisioning + reconciling {types.Count} object type(s) under parent page {state.ParentPageId}");

        var (dataSourceIds, minted) = Provision(provisioner, types, state, dryRun, output);
        // No provisioner.Save() here: persistence now lives inside Create and MarkPostPassDone, each of which
        // writes the instant its fact is established (wave 5). A standalone Save would only re-serialize identical
        // state.
        if (!dryRun)
            ReconcileSchema(client, provisioner, model, types, dataSourceIds, minted, prune, output);

        return Reconcile(client, state, types, dataSourceIds, minted, dryRun, allowMassDelete, output);
    }

    /// <summary>The two model-vs-live schema passes for every resolved type, sharing ONE live-schema read each
    /// (never runs in dry-run):
    /// <list type="bullet">
    /// <item>Additive model-evolution (ns-11, model → live): for a REUSED board, add a property, a select
    /// option, or a title the model gained since provisioning. Skipped for a freshly minted board — it was just
    /// built from the current model.</item>
    /// <item>Drift check (DR 029 §6, live → model): warn on (or, under <paramref name="prune"/>, delete) a
    /// property or option Notion has but the model does not.</item>
    /// </list>
    /// A reused type reads its live schema once and feeds it to BOTH passes; the additive PATCH only adds model
    /// properties, so the pre-additive snapshot the drift check sees carries the identical rogue set.</summary>
    private static void ReconcileSchema(
        INotionClient client, NotionProvisioner provisioner, SyncModel model, IReadOnlyList<SyncObjectType> types,
        IReadOnlyDictionary<string, string> dataSourceIds, IReadOnlySet<string> minted, bool prune, TextWriter output)
    {
        foreach (var type in types)
        {
            if (!dataSourceIds.TryGetValue(type.Type, out var dataSourceId))
                continue;
            NotionDataSource? live = null;
            if (!minted.Contains(type.Type))
            {
                live = client.RetrieveDataSource(dataSourceId);
                provisioner.ApplyModelAdditions(type, live, dataSourceIds, output);
            }
            NotionSchemaDrift.Check(model, type, dataSourceId, client, prune, output, live);
        }
    }

    /// <summary>Resolve each type's data source id, creating the database when not already provisioned.
    /// In dry-run nothing is created; an unprovisioned type simply has no entry in the returned map. Returns
    /// the set of types whose database was freshly MINTED this run (Lookup returned null → Create), so
    /// <see cref="Reconcile"/> can reset their base snapshot before reconciling: a re-provision (definitive
    /// 404, or the database no longer owning its recorded data source) mints a fresh EMPTY database whose
    /// pages the stale base does not point at (finding 1).</summary>
    private static (Dictionary<string, string> DataSourceIds, HashSet<string> Minted) Provision(
        NotionProvisioner provisioner, IReadOnlyList<SyncObjectType> types,
        NotionSpineState state, bool dryRun, TextWriter output)
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
                BaseSnapshotStore.DeleteSnapshot(state.SnapshotPath(type.Type));
                var record = provisioner.Create(type, state.ParentPageId, dataSourceIds);
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
        // Slice.attention is patched before Sprint's attention-count rollup targets it, and Sprint's
        // needs-human-count formula before Campaign's needs-human rollup sums it.
        // A type owes its post-pass if it is created THIS run, or was recorded on a prior run whose post-pass
        // never completed (a mid-provision throw records databases before their rollups/formulas are PATCHed;
        // the retry reuses them, so their attention-layer schema must still be added — review R2-1). By the
        // time this runs every type's database exists (created this run or reused), so the children whose
        // reverse relations a parent's rollups read are all present: running the post-pass child-first is safe.
        var postPass = types.Reverse().ToList();
        if (dryRun)
            PreviewPostPass(provisioner, postPass, created, output);
        else
            RunPostPass(provisioner, postPass, output);
        return (dataSourceIds, minted);
    }

    /// <summary>Dry-run preview of the rollup/formula/view post-pass: for every type that a real run would
    /// post-pass — created this run, or recorded-but-unpassed — report which schema layers it would add,
    /// writing nothing to Notion.</summary>
    private static void PreviewPostPass(
        NotionProvisioner provisioner, IReadOnlyList<SyncObjectType> postPass,
        List<SyncObjectType> created, TextWriter output)
    {
        foreach (var type in postPass.Where(t => created.Contains(t) || provisioner.PostPassPending(t.Type)))
        {
            if (NotionProvisioner.HasSelfRelations(type))
                output.WriteLine($"  provision  {type.Type,-9} would add self-relation properties");
            if (NotionProvisioner.HasRollups(type))
                output.WriteLine($"  provision  {type.Type,-9} would add rollup properties");
            if (NotionProvisioner.HasDeferredFormulas(type))
                output.WriteLine($"  provision  {type.Type,-9} would add formula properties");
            if (NotionProvisioner.HasViews(type))
                output.WriteLine($"  provision  {type.Type,-9} would add {type.Views!.Count} view(s)");
        }
    }

    /// <summary>Execute the rollup/formula/view post-pass for every recorded-but-unpassed type, PATCHing each
    /// schema layer in dependency order and marking the type post-passed the instant it completes.</summary>
    private static void RunPostPass(
        NotionProvisioner provisioner, IReadOnlyList<SyncObjectType> postPass, TextWriter output)
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
            // Views last: they reference properties/rollups/formulas by id, so every column must exist first.
            if (NotionProvisioner.HasViews(type))
            {
                provisioner.AddViews(type);
                output.WriteLine($"  provision  {type.Type,-9} added {type.Views!.Count} view(s)");
            }
            // Persist completion immediately (mirrors the per-create Save) so a later throw in this same
            // post-pass does not force an already-done type to re-run — and marks the flag for reuse ticks.
            provisioner.MarkPostPassDone(type.Type);
        }
    }

    private static NotionSpineSyncResult Reconcile(
        INotionClient client, NotionSpineState state, IReadOnlyList<SyncObjectType> types,
        IReadOnlyDictionary<string, string> dataSourceIds, IReadOnlySet<string> minted, bool dryRun, bool allowMassDelete, TextWriter output)
    {
        var localToPageByType = new Dictionary<string, Dictionary<string, string>>();
        var fuseTripped = new List<string>();

        foreach (var type in types)
        {
            var docsDir = Path.Combine(state.DydoRoot, type.Dir);
            var docs = LoadDocs(docsDir);

            if (!dataSourceIds.TryGetValue(type.Type, out var dataSourceId))
            {
                output.WriteLine($"  sync       {type.Type,-9} would create database, then create {docs.Count} page(s)");
                continue;
            }

            var store = new BaseSnapshotStore(state.SnapshotPath(type.Type));

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
            // so a self-relation (Slice.blocked-by → Slice) resolves against pages synced on a
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
            // The pages this type's base already maps at tick start — an ambiguous-create recovery must never
            // adopt one of these (they belong to other records; ns-5, review major 1). A minted type's store was
            // just reset, so this is empty and every repo doc re-creates.
            var mappedExternalIds = localToPageByType[type.Type].Values.ToHashSet();
            var adapter = new NotionSyncAdapter(
                client, dataSourceId, type.FieldSchema(), relationLocalToPageByField, relationPageToLocal, type.Icon,
                engineSchema, store.GetLastActivity, mappedExternalIds);
            var shadowDir = SpineShadowDir(state.DydoRoot, type.Type);

            if (dryRun)
            {
                var runner = new SyncRunner(adapter, store, RepoFolderLayout.For(type, docsDir).PathFor);
                foreach (var result in runner.Plan(docs))
                    output.WriteLine($"  sync       {type.Type,-9} {result.Action,-14} {result.LocalId}");
            }
            else
            {
                // Promote any shadow file a human has resolved (DR 035 §4) before reconciling, so its content becomes
                // the canonical PM doc this tick and is pushed — the spine half of the resolution flow. Re-read the
                // type's docs after a promotion so the reconcile sees the adopted body.
                if (PromoteResolvedShadows(shadowDir, adapter, store, docsDir, docs))
                    docs = LoadDocs(docsDir);

                // A genuine two-sided conflict is diverted to the spine shadow tree — NEVER written as conflict
                // markers into the canonical PM file (DR 035 §4/§5, the spine sibling of the docs-mirror fix for
                // issue 0235; 0291's degraded path and the 0235/0236 phantom-conflict class made canonical markers
                // worse). The shadow tree lives under _system, outside every type's own dir, so a diverted conflict
                // is never re-read as a repo doc and can never cascade back through the sync.
                var runner = new SyncRunner(
                    adapter, store, RepoFolderLayout.For(type, docsDir).PathFor,
                    localId => Path.Combine(shadowDir, localId + ".md"), allowMassDelete);

                var run = runner.Run(docs);
                if (run.FuseTripped)
                {
                    fuseTripped.Add(type.Type);
                    ReportFuseTrip(type.Type, run.WouldDeletePaths, output);
                    // The apply was aborted, but any two-sided conflicts were already diverted to shadows in the
                    // reconcile loop before the fuse tripped — report them so the human is not left unaware (finding 3).
                    ReportConflicts(run, docs, docsDir, shadowDir, output);
                }
                else
                {
                    output.WriteLine($"  sync       {type.Type,-9} reconciled {run.Results.Count} object(s)");
                    ReportConflicts(run, docs, docsDir, shadowDir, output);

                    // Seed the daemon's cheap-tick state (ns-13 F2c) so a watchdog started after this manual sync
                    // begins warm — no remote gap (cursor = the newest stamp this sync saw) and no phantom local
                    // changes (mtimes = the post-sync on-disk state). NOT on a tripped run (minor 2): a fuse abort
                    // applied nothing, so advancing the cursor past the read-but-unapplied edits would make the daemon
                    // skip them. A dry-run never reaches here.
                    NotionDeltaState.Seed(
                        NotionDeltaState.PathFor(state.DydoRoot, state.SnapshotAdapterName(type.Type)),
                        adapter.MaxSeenStamp, NotionDeltaState.ScanMtimes(docsDir));
                }
            }

            localToPageByType[type.Type] = LocalToPageIds(store);
        }

        return new NotionSpineSyncResult { FuseTrippedTypes = fuseTripped };
    }

    /// <summary>Report a mass-delete fuse abort for one type (slice ns-2): the type's APPLY was aborted — no page
    /// archived, no repo file deleted, base un-advanced — because it would have locally deleted a large share of its
    /// tracked records (any two-sided conflicts were still diverted to shadows before the abort and are reported
    /// separately). Names the override flag and lists the would-be-deleted paths, first 20 then a "+N more" tail so
    /// a runaway plan does not flood the log.</summary>
    private static void ReportFuseTrip(string type, IReadOnlyList<string> wouldDelete, TextWriter output)
    {
        output.WriteLine(
            $"  sync       {type,-9} ABORTED by mass-delete fuse: would delete {wouldDelete.Count} local record(s) — refusing. "
            + "Pass --allow-mass-delete to override. Would delete:");
        foreach (var path in wouldDelete.Take(20))
            output.WriteLine($"             {path}");
        if (wouldDelete.Count > 20)
            output.WriteLine($"             +{wouldDelete.Count - 20} more");
    }

    /// <summary>The spine's conflict shadow tree for one object type (DR 035 §4, slice ns-4): a diverted conflict
    /// lands at <c>dydo/_system/notion_sync_spine/&lt;type&gt;/&lt;name&gt;.md</c> — under <c>_system</c> so it is
    /// outside every type's own dir and never re-read as a repo doc, and split per type so two types can never
    /// collide on a shared local-id stem. Deliberately a SIBLING of the docs mirror's <c>_system/notion_sync/</c>
    /// shadow root, never NESTED inside it: <see cref="DocsTreeSync"/>'s promote pass enumerates
    /// <c>notion_sync/**</c> recursively, so a nested spine shadow a docs-only run encountered would be promoted to
    /// a junk canonical path and deleted — silently losing the human's resolution (finding 1).</summary>
    private static string SpineShadowDir(string dydoRoot, string objectType) =>
        Path.Combine(dydoRoot, "_system", "notion_sync_spine", objectType);

    /// <summary>Report this type's conflicts (slice ns-4). Each conflict diverted to the shadow tree names BOTH the
    /// canonical PM file — left untouched at its last-good state — and the shadow path holding the conflicted body,
    /// so the operator knows exactly where to resolve it. A delete/modify conflict that resurrected a side carries
    /// no markers, so it is not shadowed; those are reported by local id as before.</summary>
    private static void ReportConflicts(SyncRunResult run, IReadOnlyList<SyncDoc> docs, string docsDir, string shadowDir, TextWriter output)
    {
        var shadowed = run.ShadowedLocalIds.ToHashSet();
        foreach (var localId in run.ShadowedLocalIds)
        {
            var canonical = docs.FirstOrDefault(d => d.LocalId == localId)?.SourcePath is { Length: > 0 } path
                ? path
                : Path.Combine(docsDir, localId + ".md");
            // A type dir carries forward slashes from the model JSON, so a doc's SourcePath mixes separators;
            // GetFullPath normalizes both paths to the platform separator for a clean, uniform report line.
            output.WriteLine(
                $"             conflict {localId} diverted to shadow (canonical untouched): "
                + $"{Path.GetFullPath(canonical)} -> {Path.GetFullPath(Path.Combine(shadowDir, localId + ".md"))}");
        }

        var unshadowed = run.ConflictedLocalIds.Where(id => !shadowed.Contains(id)).ToList();
        if (unshadowed.Count > 0)
            output.WriteLine($"             {unshadowed.Count} conflict(s): {string.Join(", ", unshadowed)}");
    }

    /// <summary>Promote every shadow file a human has resolved — one no longer carrying merge sentinels — onto its
    /// canonical PM doc, then delete it (DR 035 §4 resolution flow; the spine sibling of
    /// <see cref="DocsTreeSync"/>'s PromoteResolvedShadows). A shadow still bearing markers is left untouched: the
    /// human has not finished, and the reconcile re-derives the same conflict deterministically.
    /// <para>The resolution must WIN over the still-diverged Notion side (else the reconcile re-detects the two-
    /// sided edit and re-diverts): the base body is aligned to the CURRENT external body, so the reconcile reads
    /// Notion as unchanged and pushes the resolved repo body over it (repo-wins) rather than merging a fresh
    /// conflict. That external read is GUARDED — a page archived/trashed while the conflict sat unresolved is simply
    /// skipped (base left as-is) rather than throwing at the same point every tick and wedging the whole type's
    /// sync; the reconcile then resurrects the doc from the surviving repo edit. Returns whether anything was
    /// promoted, so the caller re-reads the type's docs.</para></summary>
    internal static bool PromoteResolvedShadows(
        string shadowDir, NotionSyncAdapter adapter, BaseSnapshotStore store, string docsDir, IReadOnlyList<SyncDoc> docs)
    {
        if (!Directory.Exists(shadowDir))
            return false;

        // Build tolerantly (first-wins): two repo files sharing a stem is SyncRunner.IndexByLocalId's deliberate
        // both-paths error to raise, not ours to pre-empt with ToDictionary's bare ArgumentException (finding 2).
        var canonicalByLocalId = new Dictionary<string, string>();
        foreach (var d in docs)
            canonicalByLocalId.TryAdd(d.LocalId, d.SourcePath);
        // Read the external side lazily and once: only when a resolved shadow actually needs its base aligned, and
        // reused across every promotion this tick so a batch of resolutions costs one data-source read, not N.
        IReadOnlyList<SyncRecord>? external = null;

        var promoted = false;
        foreach (var shadowFile in Directory.EnumerateFiles(shadowDir, "*.md"))
        {
            var content = File.ReadAllText(shadowFile);
            if (ThreeWayTextMerge.ContainsConflictMarkers(content))
                continue; // still unresolved — leave it for the human

            var localId = Path.GetFileNameWithoutExtension(shadowFile);
            var canonical = canonicalByLocalId.TryGetValue(localId, out var path) && !string.IsNullOrEmpty(path)
                ? path
                : Path.Combine(docsDir, localId + ".md");

            Directory.CreateDirectory(Path.GetDirectoryName(canonical)!);
            File.WriteAllText(canonical, content);
            File.Delete(shadowFile);
            promoted = true;

            if (store.Get(localId) is { ExternalId: { } pageId } snap)
            {
                external ??= adapter.ReadExternalState();
                if (external.FirstOrDefault(r => r.ExternalId == pageId) is { } record)
                    store.Set(new SyncDoc
                    {
                        LocalId = snap.LocalId,
                        ExternalId = snap.ExternalId,
                        Fields = snap.Fields,
                        Body = record.Body,
                        SourcePath = "",
                    });
            }
        }
        if (promoted)
            store.Save();
        return promoted;
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
