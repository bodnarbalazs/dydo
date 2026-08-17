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
    /// <summary>Body re-reads of a boundary page (stamp == cursor) are limited to pages edited within this window of
    /// "now" — a page last edited long ago cannot receive a same-minute edit now, so it is never re-read (ns-13). The
    /// margin absorbs a couple of minutes of clock skew between the local clock and Notion's; err large. Without it a
    /// steady quiet tick would re-read the newest page of EVERY type every tick, blowing the request budget.</summary>
    private static readonly TimeSpan BoundaryRecencyMargin = TimeSpan.FromMinutes(2);

    public static NotionDeltaTickResult Run(
        INotionClient client, NotionSpineState state, bool census, bool validateProvisioning,
        bool allowMassDelete = false, DateTime? nowUtc = null, TextWriter? diagnostics = null)
        => RunCore(client, state, census, validateProvisioning, allowMassDelete, nowUtc, null, diagnostics ?? TextWriter.Null);

    /// <summary>Exercises the production delta path with a reader that can model transport outcomes the block
    /// endpoint cannot otherwise express, such as a truncated body export.</summary>
    internal static NotionDeltaTickResult RunForTest(
        INotionClient client, NotionSpineState state, bool census, bool validateProvisioning,
        Func<NotionSyncAdapter, IReadOnlyList<SyncRecord>> reader, bool allowMassDelete = false,
        DateTime? nowUtc = null, TextWriter? diagnostics = null) =>
        RunCore(client, state, census, validateProvisioning, allowMassDelete, nowUtc, reader, diagnostics ?? TextWriter.Null);

    private static NotionDeltaTickResult RunCore(
        INotionClient client, NotionSpineState state, bool census, bool validateProvisioning,
        bool allowMassDelete, DateTime? nowUtc, Func<NotionSyncAdapter, IReadOnlyList<SyncRecord>>? reader,
        TextWriter diagnostics)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        var requestsBefore = client.RequestCount;
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
            summary = summary.Add(RunType(client, state, type, dataSourceId, localToPageByType, stores[type.Type], census,
                allowMassDelete, now, reader, diagnostics));
        }
        return summary with { Requests = client.RequestCount - requestsBefore };
    }

    private static NotionDeltaTickResult RunType(
        INotionClient client, NotionSpineState state, SyncObjectType type, string dataSourceId,
        IReadOnlyDictionary<string, Dictionary<string, string>> localToPageByType, BaseSnapshotStore store,
        bool census, bool allowMassDelete, DateTime nowUtc, Func<NotionSyncAdapter, IReadOnlyList<SyncRecord>>? reader,
        TextWriter diagnostics)
    {
        var docsDir = Path.Combine(state.DydoRoot, type.Dir);
        var shadowDir = SpineShadowDir(state.DydoRoot, type.Type);
        var files = EnumerateDocFiles(docsDir);
        var delta = new NotionDeltaState(NotionDeltaState.PathFor(state.DydoRoot, state.SnapshotAdapterName(type.Type)));

        // Remote changes. A null cursor (first/degraded tick) or a census reads the full page list for ids+stamps
        // ONLY; otherwise one filtered query returns the pages edited on or after the cursor. Of those, only pages
        // strictly newer than the cursor OR recently edited (within the recency margin — the same-minute-re-edit
        // window, F1) get a body re-read; an idle type's old newest page is never re-read (the request-budget fix).
        var (hits, disappeared, maxStamp) = ReadRemoteDelta(client, dataSourceId, delta.Cursor, census, store, nowUtc);
        // Establish a non-null baseline cursor even for an empty board (F2b), so the next tick is a normal filtered
        // tick rather than another full cold-start read.
        var cursorToSave = maxStamp ?? (delta.Cursor == null ? NotionDeltaState.SentinelEpoch : null);

        // Read bodies for the filter hits ONLY (none on a cold-start tick). The same adapter drives the reconcile.
        var adapter = BuildAdapter(client, dataSourceId, type, localToPageByType, store, hits);
        var hitRecords = hits.Count > 0 ? reader?.Invoke(adapter) ?? adapter.ReadExternalState() : [];
        var promotion = PromoteResolvedShadows(client, dataSourceId, type, localToPageByType, store, docsDir, shadowDir, hitRecords);
        files = promotion.Files;
        var currentMtimes = files.Keys.ToDictionary(p => p, p => File.GetLastWriteTimeUtc(p).Ticks);
        var (localChanged, filesChanged) = LocalChanges(files, currentMtimes, delta.Files);
        var (changed, hitLocalIds) = ChangedUnion(localChanged, hitRecords, disappeared, ExternalIdToLocalId(store));

        // A v1 snapshot and every durable body intent are correctness work even while mtimes and page stamps are
        // quiet. Read the complete native projection only for those exceptional entries: pending creates need their
        // exact operation id before ordinary local-id pairing, and migration cannot safely classify from a stale
        // synthetic. The usual quiet tick remains one filtered query and zero body reads.
        var preObserved = hitRecords.Concat(promotion.Observed)
            .GroupBy(record => record.ExternalId, StringComparer.Ordinal)
            .Select(group => group.Last())
            .ToList();
        var (specialIds, specialRecords) = ReadMigrationAndPendingRecords(
            client, dataSourceId, type, localToPageByType, store, preObserved);
        // The exceptional read deliberately contains just the snapshot/intent pages.  Merge it with the normal
        // filtered hits (the latter may already contain one of those pages) before building the delta external
        // view; a full corpus body read here would turn one stuck journal entry into an O(board) watchdog tick.
        var observed = preObserved.Concat(specialRecords)
            .GroupBy(record => record.ExternalId, StringComparer.Ordinal)
            .Select(group => group.Last())
            .ToList();
        foreach (var localId in specialIds)
            changed.Add(localId);
        var observedLocalIds = observed.Select(record => LocalIdFor(record, ExternalIdToLocalId(store)))
            .ToHashSet(StringComparer.Ordinal);

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
        var external = BuildExternal(observed, changed, observedLocalIds, disappeared, store);
        var migration = NotionSnapshotMigration.Classify(
            store, repoDocs, observed, adapter, shadowDir, docsDir, diagnostics);
        NotionSnapshotMigration.ApplyShadows(migration, shadowDir);
        var migrationBodies = migration.Adoptions.ToDictionary(pair => pair.Key, pair => pair.Value.Bodies);
        var runner = new SyncRunner(
            adapter, store, RepoFolderLayout.For(type, docsDir).PathFor,
            localId => Path.Combine(shadowDir, localId + ".md"), allowMassDelete, useProjectedBodies: true, migrationBodies: migrationBodies);
        var run = runner.RunDelta(repoDocs, external, changed, migration.Shadows.Keys.ToHashSet());

        // Persist state only when something actually moved (minor 1): in steady state the newest page is ALWAYS a
        // boundary hit that reconciles to None, so an unconditional save would rewrite delta.json every tick — a
        // multi-MB write every 15s at 100x. A tripped mass-delete fuse also must NOT advance the cursor past the
        // remote edits it declined to apply (F4). So save iff a file's mtime moved, the cursor advanced, or the
        // reconcile produced a real (non-None) result — and never on a fuse trip. A truncated body is explicitly
        // unhandled: retaining the prior state makes the same remote page a filter hit next tick instead of
        // advancing past unavailable content.
        var reconciledSomething = run.Results.Any(r => r.Action != ReconcileAction.None);
        var unhandledProjection = migration.Shadows.Count > 0 || run.Results.Any(r => r.UnhandledProjection);
        var needsFollowUp = run.PendingRecoveryLocalIds.Count > 0;
        var postRunMtimes = NotionDeltaState.ScanMtimes(docsDir);
        var canonicalChanged = postRunMtimes.Count != currentMtimes.Count
            || postRunMtimes.Any(pair => !currentMtimes.TryGetValue(pair.Key, out var before) || before != pair.Value);
        if (ShouldSaveDeltaState(run, unhandledProjection, needsFollowUp, filesChanged || canonicalChanged, cursorAdvanced, reconciledSomething))
            SaveDeltaState(delta, postRunMtimes, cursorToSave);
        ReportUnhandled(diagnostics, type, files, docsDir, shadowDir, run);
        return Summarize(run, census) with { Conflicts = Summarize(run, census).Conflicts + migration.Shadows.Count };
    }

    private static bool ShouldSaveDeltaState(
        SyncRunResult run, bool unhandledProjection, bool needsFollowUp,
        bool filesChanged, bool cursorAdvanced, bool reconciledSomething) =>
        !run.FuseTripped && !unhandledProjection && !needsFollowUp
        && (filesChanged || cursorAdvanced || reconciledSomething);

    private static (Dictionary<string, string> Files, IReadOnlyList<SyncRecord> Observed) PromoteResolvedShadows(INotionClient client, string dataSourceId,
        SyncObjectType type, IReadOnlyDictionary<string, Dictionary<string, string>> localToPageByType,
        BaseSnapshotStore store, string docsDir, string shadowDir, IReadOnlyList<SyncRecord> hitRecords)
    {
        var files = EnumerateDocFiles(docsDir);
        if (!Directory.Exists(shadowDir))
            return (files, []);
        var canonicalStubs = files.Select(kv =>
            new SyncDoc { LocalId = kv.Value, SourcePath = kv.Key, Fields = [], Body = "" }).ToList();
        var targetPageIds = Directory.EnumerateFiles(shadowDir, "*.md")
            .Where(path => !ThreeWayTextMerge.ContainsConflictMarkers(File.ReadAllText(path)))
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .Where(localId => localId != null)
            .Cast<string>()
            .Select(localId => store.Get(localId)?.ExternalId)
            .Where(pageId => pageId != null)
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);
        if (targetPageIds.Count == 0)
            return (files, []);
        var observedIds = hitRecords.Select(record => record.ExternalId).ToHashSet(StringComparer.Ordinal);
        var pages = client.QueryDataSource(dataSourceId).Where(page => targetPageIds.Contains(page.Id) && !observedIds.Contains(page.Id)).ToList();
        var adapter = BuildAdapter(client, dataSourceId, type, localToPageByType, store, pages);
        var targeted = pages.Count == 0 ? [] : adapter.ReadExternalState();
        var observed = hitRecords.Concat(targeted).ToList();
        return (NotionSpineSync.PromoteResolvedShadows(shadowDir, adapter, store, docsDir, canonicalStubs, observed)
            ? EnumerateDocFiles(docsDir) : files, targeted);
    }

    private static void ReportUnhandled(TextWriter diagnostics, SyncObjectType type,
        IReadOnlyDictionary<string, string> files, string docsDir, string shadowDir, SyncRunResult run)
    {
        foreach (var result in run.Results.Where(result => result.UnhandledProjection))
        {
            var canonical = PathForLocalId(files, result.LocalId) ?? Path.Combine(docsDir, result.LocalId + ".md");
            var shadow = Path.Combine(shadowDir, result.LocalId + ".md");
            diagnostics.WriteLine($"  sync       {type.Type,-9} unhandled {result.LocalId}: "
                + $"{result.StructuredConflictReason ?? "pending body-write recovery"}; canonical {Path.GetFullPath(canonical)}; shadow {Path.GetFullPath(shadow)}");
        }
    }

    private static (HashSet<string> SpecialIds, IReadOnlyList<SyncRecord> Observed) ReadMigrationAndPendingRecords(
        INotionClient client, string dataSourceId, SyncObjectType type,
        IReadOnlyDictionary<string, Dictionary<string, string>> localToPageByType, BaseSnapshotStore store,
        IReadOnlyList<SyncRecord> hits)
    {
        var specialIds = store.LocalIds.Where(localId => !store.IsV2(localId)
            || store.GetPendingBodyWrite(localId) != null).ToHashSet(StringComparer.Ordinal);
        if (specialIds.Count == 0)
            return (specialIds, hits);
        var externalIds = new HashSet<string>(StringComparer.Ordinal);
        var operationIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var localId in specialIds)
        {
            var snapshot = store.Get(localId);
            if (snapshot?.ExternalId is { } externalId)
                externalIds.Add(externalId);
            if (store.GetPendingBodyWrite(localId) is { Kind: BodyWriteOperationKind.Create } intent)
                operationIds.Add(intent.OperationId);
        }

        var alreadyRead = hits.Select(record => record.ExternalId).ToHashSet(StringComparer.Ordinal);
        var hitOperations = hits.Select(record => record.OperationId)
            .Where(operationId => operationId != null)
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);
        if (externalIds.All(alreadyRead.Contains) && operationIds.All(hitOperations.Contains))
            return (specialIds, []);

        // Notion exposes no retrieve-page-by-id endpoint in the deliberately small client surface.  One metadata
        // query is therefore the least request-costly way to locate an unbound Create by its exact write id; bodies
        // are read only for the exceptional pages selected below, never every page returned by the query.
        var exceptionalPages = client.QueryDataSource(dataSourceId)
            .Where(page => externalIds.Contains(page.Id)
                || (page.Properties.TryGetValue(NotionSyncAdapter.WriteIdProperty, out var writeId)
                    && operationIds.Contains(NotionRichText.Flatten(writeId.RichText))))
            .Where(page => !alreadyRead.Contains(page.Id))
            .ToList();
        if (exceptionalPages.Count == 0)
            return (specialIds, []);
        var adapter = BuildAdapter(client, dataSourceId, type, localToPageByType, store, exceptionalPages);
        return (specialIds, adapter.ReadExternalState());
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
    /// query for the pages edited on or after the cursor. Only pages that are genuinely newer than the cursor, or
    /// recent enough to still be inside the same-minute-re-edit window (F1), get a body re-read — an idle type's old
    /// newest page is skipped, so a quiet tick costs one filtered query and no body reads.</summary>
    private static (IReadOnlyList<NotionPage> Hits, HashSet<string> Disappeared, string? MaxStamp) ReadRemoteDelta(
        INotionClient client, string dataSourceId, string? cursor, bool census, BaseSnapshotStore store, DateTime nowUtc)
    {
        var recencyThreshold = FormatMinute(nowUtc - BoundaryRecencyMargin);

        if (cursor != null && !census)
        {
            var filtered = client.QueryDataSourceSince(dataSourceId, cursor);
            return (BodyHits(filtered, cursor, recencyThreshold), new HashSet<string>(StringComparer.Ordinal), MaxStamp(filtered));
        }

        var alive = client.QueryDataSource(dataSourceId).Where(p => !p.Archived).ToList();
        IReadOnlyList<NotionPage> candidates = cursor == null
            ? []
            : alive.Where(p => p.LastEditedTime != null && string.CompareOrdinal(p.LastEditedTime, cursor) >= 0).ToList();
        var hits = cursor == null ? candidates : BodyHits(candidates, cursor, recencyThreshold);

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

    /// <summary>The pages from a filter result that warrant a body re-read: strictly newer than the cursor (a genuine
    /// new edit), or edited on or after the recency threshold (still inside the same-minute-re-edit window, F1). A
    /// page sitting at the cursor stamp but edited long ago is dropped — no same-minute risk, no wasteful read.</summary>
    private static List<NotionPage> BodyHits(IReadOnlyList<NotionPage> pages, string cursor, string recencyThreshold) =>
        pages.Where(p =>
            string.CompareOrdinal(p.LastEditedTime ?? "", cursor) > 0
            || string.CompareOrdinal(p.LastEditedTime ?? "", recencyThreshold) >= 0).ToList();

    private static string FormatMinute(DateTime utc) =>
        utc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:00.000'Z'", System.Globalization.CultureInfo.InvariantCulture);

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
