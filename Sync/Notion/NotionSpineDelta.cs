namespace DynaDocs.Sync.Notion;

using DynaDocs.Models;
using DynaDocs.Sync.Model;
using DynaDocs.Sync.Notion.Dtos;
using DynaDocs.Sync.Notion.Provisioning;

/// <summary>
/// One cheap sync-daemon tick (ns-13): reconcile only what changed since the last tick, never the whole corpus.
/// Per object type it (1) asks Notion for pages edited on or after its stamp cursor — a single server-side filtered
/// query that returns empty on a quiet tick at ANY corpus size — (2) stat-walks the type's dir to notice locally
/// changed or deleted files, and (3) feeds ONLY that changed-id union to the same reconcile engine the manual sync
/// uses. Untouched records are never read, parsed, or re-pushed; their base entries carry forward verbatim and the
/// snapshot is not rewritten. The interactive <c>dydo notion sync</c> keeps its full-read correctness; this path
/// never provisions (it reads recorded ids) and never detects remote archives on a fast tick — those surface on the
/// periodic <paramref name="census"/> (a body-free id/stamp pagination) and on the manual full sync.
/// </summary>
public static class NotionSpineDelta
{
    public static NotionDeltaTickResult Run(
        INotionClient client, NotionSpineState state, bool census, bool validateProvisioning, bool allowMassDelete = false)
    {
        var model = SyncModelLoader.Load(state.DydoRoot);
        var types = model.InDependencyOrder();
        var dataSourceIds = ResolveDataSourceIds(client, state, types, validateProvisioning);

        // Load each type's base snapshot ONCE (N1): the relation maps and the per-type reconcile share it, not two
        // parses. A relation resolves against every type's base map (a child's blocked-by → its parent's page). This
        // map is built once at tick start and NOT refreshed mid-tick, so a parent and its child both created in the
        // SAME tick defer the child's relation by one tick — it self-heals next tick via the stale-echo re-push (N2).
        var stores = types.ToDictionary(t => t.Type, t => new BaseSnapshotStore(state.SnapshotPath(t.Type)));
        var localToPageByType = types.ToDictionary(t => t.Type, t => LocalToPageIds(stores[t.Type]));

        var summary = NotionDeltaTickResult.Empty(census);
        foreach (var type in types)
        {
            if (!dataSourceIds.TryGetValue(type.Type, out var dataSourceId))
                continue; // not provisioned (or a validation probe dropped it) — the manual sync must provision first
            summary = summary.Add(RunType(client, state, type, dataSourceId, localToPageByType, stores[type.Type], census, allowMassDelete));
        }
        return summary;
    }

    private static NotionDeltaTickResult RunType(
        INotionClient client, NotionSpineState state, SyncObjectType type, string dataSourceId,
        IReadOnlyDictionary<string, Dictionary<string, string>> localToPageByType, BaseSnapshotStore store,
        bool census, bool allowMassDelete)
    {
        var docsDir = Path.Combine(state.DydoRoot, type.Dir);
        var shadowDir = SpineShadowDir(state.DydoRoot, type.Type);
        var files = EnumerateDocFiles(docsDir); // path -> localId, honouring the same _-prefix skip as LoadDocs

        // Promote any conflict shadow a human has resolved BEFORE reconciling (F3), so its content wins over the
        // still-diverged board this tick and the reconcile does not re-detect the same conflict and overwrite the
        // resolution. Cheap: the shadow dir is tiny, and PromoteResolvedShadows reads the network only when a resolved
        // shadow actually needs its base aligned — so a tick with no shadows makes no query. The doc stubs carry each
        // existing file's REAL canonical path (F9), so a resolved shadow for a folder-routed doc promotes back to its
        // routed subfolder — never to the type-dir root, which would duplicate the stem and wedge the tick. A promoted
        // file's new mtime is picked up by a re-scan, so it reconciles this same tick.
        if (Directory.Exists(shadowDir))
        {
            var canonicalStubs = files.Select(kv =>
                new SyncDoc { LocalId = kv.Value, SourcePath = kv.Key, Fields = [], Body = "" }).ToList();
            if (NotionSpineSync.PromoteResolvedShadows(
                    shadowDir, BuildAdapter(client, dataSourceId, type, localToPageByType, store, null), store, docsDir, canonicalStubs))
                files = EnumerateDocFiles(docsDir);
        }

        var currentMtimes = files.Keys.ToDictionary(p => p, p => File.GetLastWriteTimeUtc(p).Ticks);
        var delta = new NotionDeltaState(NotionDeltaState.PathFor(state.DydoRoot, state.SnapshotAdapterName(type.Type)));
        var (localChanged, filesChanged) = LocalChanges(files, currentMtimes, delta.Files);

        // Remote changes. A null cursor (first/degraded tick) or a census reads the full page list for ids+stamps
        // ONLY; otherwise one filtered query returns the pages edited since the cursor (boundary pages included —
        // re-read every tick until a strictly later stamp is seen, so a same-minute re-edit is never lost — F1).
        var (hits, disappeared, maxStamp) = ReadRemoteDelta(client, dataSourceId, delta.Cursor, census, store);
        // Establish a non-null baseline cursor even for an empty board (F2b), so the next tick is a normal filtered
        // tick rather than another full cold-start read.
        var cursorToSave = maxStamp ?? (delta.Cursor == null ? NotionDeltaState.SentinelEpoch : null);

        // Read bodies for the filter hits ONLY (none on a cold-start tick). The same adapter drives the reconcile.
        var adapter = BuildAdapter(client, dataSourceId, type, localToPageByType, store, hits);
        var hitRecords = hits.Count > 0 ? adapter.ReadExternalState() : [];
        var (changed, hitLocalIds) = ChangedUnion(localChanged, hitRecords, disappeared, ExternalIdToLocalId(store));

        var cursorAdvanced = cursorToSave != null && (delta.Cursor == null || string.CompareOrdinal(cursorToSave, delta.Cursor) > 0);
        if (changed.Count == 0)
        {
            // Nothing to reconcile; persist only if the cursor advanced or a file's mtime moved, so a truly quiet
            // tick rewrites no state (no write amplification at 40k entries).
            if (filesChanged || cursorAdvanced) SaveDeltaState(delta, currentMtimes, cursorToSave);
            return NotionDeltaTickResult.Empty(census);
        }

        // A cold-start / degraded tick (empty state) reconciles the LOCAL changes it detected — those are O(changes)
        // once the manual sync has seeded the state, and correctly O(corpus) only in the genuinely-degraded case
        // (missing/corrupt state) where degrading to a full local reconcile is the safe choice (F2a). It reads no
        // remote bodies (hits is empty), so it never becomes an O(corpus) body storm.
        var repoDocs = BuildRepoDocs(files, changed);
        var external = BuildExternal(hitRecords, changed, hitLocalIds, disappeared, store);
        var runner = new SyncRunner(
            adapter, store, RepoFolderLayout.For(type, docsDir).PathFor,
            localId => Path.Combine(shadowDir, localId + ".md"), allowMassDelete);
        var run = runner.RunDelta(repoDocs, external, changed);

        // Persist state only when something actually moved (minor 1): in steady state the newest page is ALWAYS a
        // boundary hit that reconciles to None, so an unconditional save would rewrite delta.json every tick — a
        // multi-MB write every 15s at 100x. A tripped mass-delete fuse also must NOT advance the cursor past the
        // remote edits it declined to apply (F4). So save iff a file's mtime moved, the cursor advanced, or the
        // reconcile produced a real (non-None) result — and never on a fuse trip.
        var reconciledSomething = run.Results.Any(r => r.Action != ReconcileAction.None);
        if (!run.FuseTripped && (filesChanged || cursorAdvanced || reconciledSomething))
            SaveDeltaState(delta, currentMtimes, cursorToSave);
        return Summarize(run, census);
    }

    /// <summary>Diff this tick's file scan against the last: a file whose mtime moved or is new, and a recorded file
    /// now gone (a local deletion). Returns the changed local ids and whether any file changed at all (the flag the
    /// quiet-tick state save gates on).</summary>
    private static (HashSet<string> Changed, bool Any) LocalChanges(
        IReadOnlyDictionary<string, string> files, IReadOnlyDictionary<string, long> currentMtimes, IReadOnlyDictionary<string, long> stored)
    {
        var changed = new HashSet<string>(StringComparer.Ordinal);
        var any = false;
        foreach (var (path, mtime) in currentMtimes)
            if (!stored.TryGetValue(path, out var m) || m != mtime)
            {
                changed.Add(files[path]);
                any = true;
            }
        foreach (var path in stored.Keys)
            if (!currentMtimes.ContainsKey(path))
            {
                changed.Add(Path.GetFileNameWithoutExtension(path));
                any = true;
            }
        return (changed, any);
    }

    /// <summary>The changed-id union: local changes, plus each filter hit's local id (mapped exactly as the runner
    /// maps it), plus each census-disappeared id. Also returns the set of hit local ids so the caller knows which
    /// records already carry a live external record (the rest get base-derived synthetics).</summary>
    private static (HashSet<string> Changed, HashSet<string> HitLocalIds) ChangedUnion(
        HashSet<string> localChanged, IReadOnlyList<SyncRecord> hitRecords, HashSet<string> disappeared,
        IReadOnlyDictionary<string, string> extIdToLocal)
    {
        var changed = new HashSet<string>(localChanged, StringComparer.Ordinal);
        var hitLocalIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in hitRecords)
        {
            var localId = LocalIdFor(record, extIdToLocal);
            hitLocalIds.Add(localId);
            changed.Add(localId);
        }
        foreach (var id in disappeared)
            changed.Add(id);
        return (changed, hitLocalIds);
    }

    /// <summary>Parse ONLY the changed union's files. A deleted or Notion-only id has no file (null repo side).</summary>
    private static List<SyncDoc> BuildRepoDocs(IReadOnlyDictionary<string, string> files, HashSet<string> changed)
    {
        var repoDocs = new List<SyncDoc>();
        foreach (var localId in changed)
            if (PathForLocalId(files, localId) is { } path)
                repoDocs.Add(SyncDocFile.Read(path, localId, path));
        return repoDocs;
    }

    /// <summary>The external side: the hit records (bodies read) plus a base-derived synthetic for every local-only
    /// change, so a record whose remote is unchanged compares equal to base (extChanged false) and the local
    /// edit/delete wins. A census-disappeared id is left ABSENT (external null) so the engine sees the archive.</summary>
    private static List<SyncRecord> BuildExternal(
        IReadOnlyList<SyncRecord> hitRecords, HashSet<string> changed, HashSet<string> hitLocalIds,
        HashSet<string> disappeared, BaseSnapshotStore store)
    {
        var external = new List<SyncRecord>(hitRecords);
        foreach (var localId in changed)
            if (!hitLocalIds.Contains(localId) && !disappeared.Contains(localId)
                && store.Get(localId) is { ExternalId: { } externalId } baseDoc)
                external.Add(new SyncRecord { ExternalId = externalId, Fields = baseDoc.Fields, Body = baseDoc.Body });
        return external;
    }

    /// <summary>Read the remote delta for one type. Cold start (null cursor) reads the full page list for ids/stamps
    /// ONLY — no body reads, no reconcile of remote — it just baselines the cursor. A census does the same full read
    /// AND reports base external ids that have disappeared (remote archives). The steady-state path is one filtered
    /// query for the pages edited on or after the cursor. Boundary pages (stamp == cursor) ARE returned and re-read
    /// each tick until a strictly later stamp is seen for them — the norm-compare yields None when unchanged, but a
    /// same-minute re-edit is caught rather than silently deduped forever (F1).</summary>
    private static (IReadOnlyList<NotionPage> Hits, HashSet<string> Disappeared, string? MaxStamp) ReadRemoteDelta(
        INotionClient client, string dataSourceId, string? cursor, bool census, BaseSnapshotStore store)
    {
        if (cursor != null && !census)
        {
            var filtered = client.QueryDataSourceSince(dataSourceId, cursor);
            return (filtered, new HashSet<string>(StringComparer.Ordinal), MaxStamp(filtered));
        }

        var alive = client.QueryDataSource(dataSourceId).Where(p => !p.Archived).ToList();
        IReadOnlyList<NotionPage> hits = cursor == null
            ? []
            : alive.Where(p => p.LastEditedTime != null && string.CompareOrdinal(p.LastEditedTime, cursor) >= 0).ToList();

        var disappeared = new HashSet<string>(StringComparer.Ordinal);
        if (census)
        {
            var live = alive.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var localId in store.LocalIds)
                if (store.Get(localId)?.ExternalId is { } externalId && !live.Contains(externalId))
                    disappeared.Add(localId);
        }
        return (hits, disappeared, MaxStamp(alive));
    }

    private static string? MaxStamp(IReadOnlyList<NotionPage> pages) =>
        pages.Select(p => p.LastEditedTime).Where(s => s != null).DefaultIfEmpty(null).Max(StringComparer.Ordinal);

    private static void SaveDeltaState(NotionDeltaState delta, Dictionary<string, long> mtimes, string? maxStamp)
    {
        delta.SetFiles(mtimes);
        delta.AdvanceCursor(maxStamp);
        delta.Save();
    }

    private static NotionDeltaTickResult Summarize(SyncRunResult run, bool census)
    {
        if (run.FuseTripped)
            return NotionDeltaTickResult.Empty(census) with { FuseTrips = 1 };
        var created = run.Results.Count(r => r.Action == ReconcileAction.Create);
        var updated = run.Results.Count(r =>
            r.Action is ReconcileAction.PushToExternal or ReconcileAction.Merged or ReconcileAction.WriteToRepo);
        var archived = run.Results.Count(r => r.Action == ReconcileAction.Delete && r.ExternalDelete != null);
        var reconciled = run.Results.Count(r => r.Action != ReconcileAction.None);
        return new NotionDeltaTickResult(created, updated, archived, run.ConflictCount, 0, reconciled, census);
    }

    /// <summary>Resolve each type's data source id. The normal tick reads recorded ids with NO network call
    /// (LoadTracked); a validation tick (the daemon's periodic provision probe) builds a provisioner and re-checks
    /// each type still exists, dropping any that need re-provisioning by the manual sync.</summary>
    private static Dictionary<string, string> ResolveDataSourceIds(
        INotionClient client, NotionSpineState state, IReadOnlyList<SyncObjectType> types, bool validate)
    {
        if (!validate)
            return NotionProvisioner.LoadTracked(state.ProvisionPath)
                .ToDictionary(t => t.ObjectType, t => t.DataSourceId);

        var provisioner = new NotionProvisioner(client, state.ProvisionPath);
        var ids = new Dictionary<string, string>();
        foreach (var type in types)
            if (provisioner.Lookup(type.Type) is { } record)
                ids[type.Type] = record.DataSourceId;
        return ids;
    }

    private static NotionSyncAdapter BuildAdapter(
        INotionClient client, string dataSourceId, SyncObjectType type,
        IReadOnlyDictionary<string, Dictionary<string, string>> localToPageByType,
        BaseSnapshotStore store, IReadOnlyList<NotionPage>? pagesOverride)
    {
        var (relationByField, relationPageToLocal) = RelationMaps(type, localToPageByType);
        var engineSchema = type.Properties.Where(p => p.Value.EngineComputed).ToDictionary(p => p.Key, p => p.Value.Type);
        var mappedExternalIds = ExternalIdToLocalId(store).Keys.ToHashSet(StringComparer.Ordinal);
        return new NotionSyncAdapter(
            client, dataSourceId, type.FieldSchema(), relationByField, relationPageToLocal, type.Icon,
            engineSchema, store.GetLastActivity, mappedExternalIds, pagesOverride);
    }

    private static (Dictionary<string, IReadOnlyDictionary<string, string>> ByField, Dictionary<string, string> PageToLocal) RelationMaps(
        SyncObjectType type, IReadOnlyDictionary<string, Dictionary<string, string>> localToPageByType)
    {
        var byField = new Dictionary<string, IReadOnlyDictionary<string, string>>();
        var pageToLocal = new Dictionary<string, string>();
        foreach (var (name, def) in type.Properties)
        {
            if (def.Type != "relation" || def.To == null)
                continue;
            var target = localToPageByType.TryGetValue(def.To, out var m) ? m : new Dictionary<string, string>();
            byField[name] = target;
            foreach (var (local, page) in target)
                pageToLocal[page] = local;
        }
        return (byField, pageToLocal);
    }

    private static Dictionary<string, string> LocalToPageIds(BaseSnapshotStore store)
    {
        var map = new Dictionary<string, string>();
        foreach (var localId in store.LocalIds)
            if (store.Get(localId)?.ExternalId is { } externalId)
                map[localId] = externalId;
        return map;
    }

    private static Dictionary<string, string> ExternalIdToLocalId(BaseSnapshotStore store)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var localId in store.LocalIds)
            if (store.Get(localId)?.ExternalId is { } externalId)
                map[externalId] = localId;
        return map;
    }

    /// <summary>The local id a record resolves to — mirroring <see cref="SyncRunner"/>'s own mapping exactly so the
    /// changed-id union keys the same records the runner iterates: the base-mapped id, else the record's carried
    /// <c>local-id</c>, else its external id, all sanitized to a safe file name.</summary>
    private static string LocalIdFor(SyncRecord record, IReadOnlyDictionary<string, string> extIdToLocal) =>
        extIdToLocal.TryGetValue(record.ExternalId, out var known)
            ? known
            : SyncRunner.SanitizeLocalId(record.Fields.FirstOrDefault(f => f.Key == SyncRunner.LocalIdField)?.Value ?? record.ExternalId);

    private static string? PathForLocalId(IReadOnlyDictionary<string, string> files, string localId)
    {
        foreach (var (path, id) in files)
            if (id == localId)
                return path;
        return null;
    }

    /// <summary>Enumerate a type's <c>*.md</c> docs as path→localId, applying the exact filter
    /// <see cref="NotionSpineSync.LoadDocs"/> uses (recursive, skipping any <c>_</c>-prefixed file or folder), so the
    /// mtime scan sees precisely the files the reconcile would.</summary>
    private static Dictionary<string, string> EnumerateDocFiles(string dir)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!Directory.Exists(dir))
            return files;
        foreach (var path in Directory.EnumerateFiles(dir, "*.md", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(dir, path);
            if (relative.Split('/', '\\').Any(segment => segment.StartsWith('_')))
                continue;
            files[path] = Path.GetFileNameWithoutExtension(path);
        }
        return files;
    }

    /// <summary>The spine's per-type conflict shadow dir — the same location the full sync uses (DR 035 §4), so a
    /// conflict a fast tick diverts and one the manual sync diverts land in the same place.</summary>
    private static string SpineShadowDir(string dydoRoot, string objectType) =>
        Path.Combine(dydoRoot, "_system", "notion_sync_spine", objectType);
}
