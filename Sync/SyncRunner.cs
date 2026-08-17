namespace DynaDocs.Sync;

using DynaDocs.Models;

/// <summary>
/// Drives one bidirectional sync tick for a single object type (Decision 025). It reads repo
/// docs and the adapter's external state, pairs them up via the base snapshot's local↔external
/// mapping, reconciles each object, then applies the results: writes repo files, pushes a change
/// set to the adapter, records any conflicts, and advances + saves the base. Notion-agnostic —
/// it only ever touches <see cref="ISyncAdapter"/> and <see cref="SyncDoc"/>.
/// </summary>
public sealed class SyncRunner
{
    /// <summary>Reserved frontmatter/record key carrying an object's stable local id across the
    /// external boundary, so an externally-created object can be filed under the right repo name.</summary>
    public const string LocalIdField = "local-id";

    private readonly ISyncAdapter _adapter;
    private readonly BaseSnapshotStore _base;
    private readonly Func<string, IReadOnlyList<SyncField>, string?, string> _repoPathFor;
    private readonly Func<string, string>? _conflictShadowPathFor;
    private readonly bool _allowMassDelete;
    private readonly bool _useProjectedBodies;

    /// <param name="repoPathFor">The canonical repo path for a doc, given its local id, fields, and current
    /// on-disk path (null when the doc has none yet). Fields are passed so folder placement can derive from
    /// status (slice brief §3); the current path lets an unmapped status keep an existing doc in place rather
    /// than pulling it to the root (finding 1). A flat layout simply ignores both. See <see cref="RepoFolderLayout"/>.</param>
    /// <param name="conflictShadowPathFor">Where to divert a conflicted body instead of the canonical repo file
    /// (DR 035 §4/§5 — the docs mirror). When set, a body about to be persisted that carries this run's merge
    /// sentinels is written to the returned shadow path and the canonical file is left at its last-good state,
    /// so the sync can NEVER corrupt a canonical doc with conflict markers (root cause of issue 0235). Null
    /// (the default, and the spine) keeps the historical behavior of writing the merged body — markers and all —
    /// to the canonical file.</param>
    /// <param name="allowMassDelete">Disables the mass-delete fuse (slice ns-2) for this run. Off by default: a
    /// reconcile that would locally delete more than 5 records AND more than 20% of the type's tracked base
    /// records aborts before applying anything (see <see cref="SyncRunResult.FuseTripped"/>), a loud tripwire
    /// against a poisoned/stale snapshot or a Notion-side mass archive sweeping the repo. Pass true to override.</param>
    public SyncRunner(
        ISyncAdapter adapter, BaseSnapshotStore baseStore,
        Func<string, IReadOnlyList<SyncField>, string?, string> repoPathFor,
        Func<string, string>? conflictShadowPathFor = null,
        bool allowMassDelete = false,
        bool useProjectedBodies = false)
    {
        _adapter = adapter;
        _base = baseStore;
        _repoPathFor = repoPathFor;
        _conflictShadowPathFor = conflictShadowPathFor;
        _allowMassDelete = allowMassDelete;
        _useProjectedBodies = useProjectedBodies;
    }

    private static SyncDoc WithBody(SyncDoc doc, string body) => new()
    {
        LocalId = doc.LocalId,
        ExternalId = doc.ExternalId,
        Fields = doc.Fields,
        Body = body,
        SourcePath = doc.SourcePath,
    };

    /// <summary>
    /// Compute the reconcile decisions for this tick without writing anything — no repo files, no
    /// external change set, no base advance. Used by <c>notion sync --dry-run</c> to preview the plan.
    /// </summary>
    public IReadOnlyList<ReconcileResult> Plan(IReadOnlyList<SyncDoc> repoDocs)
    {
        var records = _adapter.ReadExternalState();
        var externalByLocalId = MapExternalToLocalId(records, out _);
        var statuses = MapBodyReadStatuses(records, externalByLocalId);
        var repoByLocalId = IndexByLocalId(repoDocs);

        var localIds = new HashSet<string>(_base.LocalIds);
        localIds.UnionWith(repoByLocalId.Keys);
        localIds.UnionWith(externalByLocalId.Keys);

        var representable = _adapter.RepresentableScalarKeys;
        var results = new List<ReconcileResult>();
        foreach (var localId in localIds.OrderBy(x => x))
        {
            var baseDoc = _base.Get(localId);
            repoByLocalId.TryGetValue(localId, out var repo);
            externalByLocalId.TryGetValue(localId, out var external);
            results.Add(statuses.TryGetValue(localId, out var status) && status == SyncBodyReadStatus.Truncated
                ? TruncatedResult(localId, baseDoc, repo, external) : ReconcileOne(baseDoc, repo, external));
        }
        return results;
    }

    /// <summary>Index repo docs by their local id (filename stem). Two docs sharing a stem — a documented
    /// historical artifact when the same id lives in two subfolders, and reachable if a non-atomic move is
    /// interrupted — would otherwise crash <see cref="Enumerable.ToDictionary"/> with a message naming no
    /// files. Fail with a clear error naming BOTH paths so the operator can resolve it (finding 2).</summary>
    private static Dictionary<string, SyncDoc> IndexByLocalId(IReadOnlyList<SyncDoc> repoDocs)
    {
        var byLocalId = new Dictionary<string, SyncDoc>();
        foreach (var doc in repoDocs)
        {
            if (byLocalId.TryGetValue(doc.LocalId, out var existing))
                throw new InvalidOperationException(
                    $"two repo files share local id '{doc.LocalId}': '{existing.SourcePath}' and '{doc.SourcePath}' — rename or remove one");
            byLocalId[doc.LocalId] = doc;
        }
        return byLocalId;
    }

    public SyncRunResult Run(IReadOnlyList<SyncDoc> repoDocs) =>
        Run(repoDocs, _adapter.ReadExternalState(), onlyLocalIds: null, saveWhenClean: true);

    /// <summary>Reconcile ONLY the changed-id union (ns-13 delta tick): <paramref name="repoDocs"/> is just the
    /// files that changed on disk, <paramref name="external"/> only the pages the daemon's filtered query surfaced
    /// (plus base-derived synthetics for local-only changes), and <paramref name="changedLocalIds"/> the union of
    /// both. Every untouched record's base entry carries forward verbatim — it is never iterated, so it is never
    /// read, parsed, or re-pushed — and the snapshot <see cref="BaseSnapshotStore.Save"/> is skipped when the tick
    /// mutated nothing. The mass-delete fuse still sees the FULL tracked-base count, so a delta that would archive a
    /// large share still trips. Correctness is identical to a full <see cref="Run(IReadOnlyList{SyncDoc})"/> over the
    /// same union: the reconcile engine is unchanged.</summary>
    public SyncRunResult RunDelta(
        IReadOnlyList<SyncDoc> repoDocs, IReadOnlyList<SyncRecord> external, IReadOnlySet<string> changedLocalIds) =>
        Run(repoDocs, external, changedLocalIds, saveWhenClean: false);

    private SyncRunResult Run(
        IReadOnlyList<SyncDoc> repoDocs, IReadOnlyList<SyncRecord> externalRecords,
        IReadOnlySet<string>? onlyLocalIds, bool saveWhenClean)
    {
        var externalByLocalId = MapExternalToLocalId(externalRecords, out var ambiguousPendingCreates);
        var bodyReadStatuses = MapBodyReadStatuses(externalRecords, externalByLocalId);
        var repoByLocalId = IndexByLocalId(repoDocs);
        var pending = RecoverPendingWrites(repoByLocalId, externalByLocalId, ambiguousPendingCreates, out var retryCreates);

        IEnumerable<string> localIds;
        if (onlyLocalIds != null)
        {
            localIds = onlyLocalIds;
        }
        else
        {
            var union = new HashSet<string>(_base.LocalIds);
            union.UnionWith(repoByLocalId.Keys);
            union.UnionWith(externalByLocalId.Keys);
            localIds = union;
        }

        // Tracked-record count for the mass-delete fuse, captured before reconcile: base entries are only
        // removed later in CommitBase, so this is the type's tracked-base size going into the run.
        var trackedCount = _base.LocalIds.Count;

        var representable = _adapter.RepresentableScalarKeys;
        var (results, shadowed) = ReconcileAll(localIds, pending, retryCreates, ambiguousPendingCreates,
            repoByLocalId, externalByLocalId, bodyReadStatuses);

        // Mass-delete fuse (slice ns-2): count the LOCAL file deletions this reconcile would materialize — a Delete
        // that removes a repo file (RepoDelete), not an external-only archive of an already-removed doc — and abort
        // BEFORE any file is touched if it crosses the threshold. Returned as a result, never thrown, so the caller
        // maps the trip to a tool error while sibling types still reconcile.
        var wouldDelete = results
            .Where(r => r.Action == ReconcileAction.Delete && r.RepoDelete != null)
            .Select(r => r.RepoDelete!)
            .ToList();
        if (!_allowMassDelete && FuseTrips(wouldDelete.Count, trackedCount))
            return new SyncRunResult
            {
                Results = results,
                ShadowedLocalIds = shadowed.ToList(),
                FuseTripped = true,
                WouldDeletePaths = wouldDelete,
            };

        ApplyAndCommit(results, shadowed, repoByLocalId, externalByLocalId, saveWhenClean);

        return new SyncRunResult { Results = results, ShadowedLocalIds = shadowed.ToList() };
    }

    private (List<ReconcileResult> Results, HashSet<string> Shadowed) ReconcileAll(IEnumerable<string> localIds,
        IReadOnlySet<string> pending, IReadOnlySet<string> retryCreates, IReadOnlySet<string> ambiguousPendingCreates,
        IReadOnlyDictionary<string, SyncDoc> repo,
        IReadOnlyDictionary<string, SyncDoc> external, IReadOnlyDictionary<string, SyncBodyReadStatus> bodyReadStatuses)
    {
        var results = new List<ReconcileResult>();
        var shadowed = new HashSet<string>();
        foreach (var localId in localIds.OrderBy(x => x))
        {
            if (ambiguousPendingCreates.Contains(localId))
            {
                results.Add(new ReconcileResult
                {
                    LocalId = localId,
                    Action = ReconcileAction.Conflict,
                    StructuredConflictReason = "external create identity is ambiguous",
                    UnhandledProjection = true,
                });
                shadowed.Add(localId);
                continue;
            }
            if (pending.Contains(localId))
                continue;
            repo.TryGetValue(localId, out var currentRepo);
            external.TryGetValue(localId, out var currentExternal);
            var baseDoc = retryCreates.Contains(localId) ? null : _base.Get(localId);
            var result = bodyReadStatuses.TryGetValue(localId, out var status) && status == SyncBodyReadStatus.Truncated
                ? TruncatedResult(localId, baseDoc, currentRepo, currentExternal)
                : ReconcileOne(baseDoc, currentRepo, currentExternal);
            results.Add(result);
            RecordActivity(result, currentRepo);
            RouteConflictToShadow(result, shadowed);
        }
        return (results, shadowed);
    }

    private void ApplyAndCommit(List<ReconcileResult> results, HashSet<string> shadowed,
        IReadOnlyDictionary<string, SyncDoc> repo, IReadOnlyDictionary<string, SyncDoc> external, bool saveWhenClean)
    {
        var changes = new SyncChangeSet();
        var operations = PrepareBodyWriteIntents(results, shadowed);
        if (operations.Count > 0)
            _base.Save();
        foreach (var result in results)
        {
            if (shadowed.Contains(result.LocalId))
                continue;
            repo.TryGetValue(result.LocalId, out var currentRepo);
            external.TryGetValue(result.LocalId, out var currentExternal);
            ApplyResult(result.LocalId, result, currentRepo, changes, operations.GetValueOrDefault(result.LocalId));
            EnqueueEngineComputedRefresh(result.LocalId, result, currentExternal, changes);
        }

        var assigned = new Dictionary<string, string>();
        var deleted = new HashSet<string>();
        var emptyBodied = new HashSet<string>();
        var applied = false;
        SyncApplyResult applyResult = new();
        try
        {
            applyResult = _adapter.ApplyWithReceipts(changes, assigned, deleted, emptyBodied);
            applied = true;
        }
        catch (AmbiguousCreateIdentityException ambiguity)
        {
            if (!ConvertAmbiguousCreateToUnhandled(results, shadowed, operations, repo, ambiguity))
                throw;
        }
        finally
        {
            CommitBase(results, shadowed, assigned, deleted, emptyBodied, applyResult.BodyWriteReceipts, operations, applied);
            if (applied)
                _base.PruneOrphanLastActivity();
            if (saveWhenClean || _base.Dirty)
                _base.Save();
        }
    }

    private bool ConvertAmbiguousCreateToUnhandled(List<ReconcileResult> results, HashSet<string> shadowed,
        IReadOnlyDictionary<string, string> operations, IReadOnlyDictionary<string, SyncDoc> repo,
        AmbiguousCreateIdentityException ambiguity)
    {
        var index = results.FindIndex(result => operations.TryGetValue(result.LocalId, out var operationId)
            && operationId == ambiguity.OperationId);
        if (index < 0)
            return false;

        var localId = results[index].LocalId;
        var intent = _base.GetPendingBodyWrite(localId);
        if (intent == null)
            return false;
        repo.TryGetValue(localId, out var currentRepo);
        ShadowAmbiguousPendingCreate(intent, currentRepo);
        shadowed.Add(localId);
        results[index] = new ReconcileResult
        {
            LocalId = localId,
            Action = ReconcileAction.Conflict,
            StructuredConflictReason = "external create identity is ambiguous",
            UnhandledProjection = true,
        };
        return true;
    }

    private ReconcileResult ReconcileOne(SyncDoc? baseDoc, SyncDoc? repo, SyncDoc? external)
    {
        var bodyBase = baseDoc == null ? null : _base.GetDualBodyBase(baseDoc.LocalId);
        // A human resolving a shadow by making the canonical document exactly match the observed external
        // projection is decisive for the BODY only. Reconcile fields against the newly aligned body bases so a
        // property edit still flows; marker safety remains at the shadow-routing boundary.
        if (_useProjectedBodies && bodyBase != null && repo != null && external != null
            && repo.Body == external.Body
            && repo.Body != bodyBase.LocalBody
            && external.Body != bodyBase.ExternalBody)
            return ReconcileAlignedProjectedBodies(baseDoc!, repo, external);
        return _useProjectedBodies && (bodyBase != null || baseDoc == null)
            ? ReconcileEngine.ReconcileProjected(baseDoc, bodyBase, repo, external,
                _adapter.NormalizeFields, _adapter.RepresentableScalarKeys)
            : ReconcileEngine.Reconcile(baseDoc, repo, external, _adapter.NormalizeBody, _adapter.NormalizeFields,
                _adapter.RepoOwnedStructure, _adapter.IsStaleConverterEcho, _adapter.RepresentableScalarKeys);
    }

    private ReconcileResult ReconcileAlignedProjectedBodies(SyncDoc baseDoc, SyncDoc repo, SyncDoc external)
    {
        var aligned = new DynaDocs.Sync.Projection.DualBodyBase(repo.Body, external.Body);
        var result = ReconcileEngine.ReconcileProjected(baseDoc, aligned, repo, external,
            _adapter.NormalizeFields, _adapter.RepresentableScalarKeys);
        if (result.NewBodyBase != null)
            return result;
        var normalized = _adapter.NormalizeFields(repo);
        return new ReconcileResult
        {
            LocalId = repo.LocalId,
            Action = ReconcileAction.None,
            NewBase = new SyncDoc
            {
                LocalId = repo.LocalId, ExternalId = external.ExternalId, Fields = normalized.Fields,
                Body = repo.Body, SourcePath = repo.SourcePath,
            },
            NewBodyBase = aligned,
        };
    }

    /// <summary>
    /// Pending body writes fence ordinary reconciliation until their outcome is provable. Creates deliberately
    /// stay fenced: binding an id requires Slice 4's exact operation identity, never a title/body guess. Updates
    /// and resolutions may be recovered only when their journaled page now exposes the intended bytes exactly.
    /// </summary>
    private HashSet<string> RecoverPendingWrites(IReadOnlyDictionary<string, SyncDoc> repo,
        IReadOnlyDictionary<string, SyncDoc> external, IReadOnlySet<string> ambiguousPendingCreates,
        out HashSet<string> retryCreates)
    {
        var fenced = new HashSet<string>();
        retryCreates = [];
        foreach (var localId in _base.LocalIds)
        {
            var intent = _base.GetPendingBodyWrite(localId);
            if (intent == null)
                continue;
            fenced.Add(localId);
            if (intent.Kind == BodyWriteOperationKind.Create)
            {
                RecoverPendingCreate(intent, repo, external, ambiguousPendingCreates, fenced, retryCreates);
                continue;
            }
            if (intent.ExternalId == null
                || !external.TryGetValue(localId, out var observed) || observed.ExternalId != intent.ExternalId)
                continue;
            repo.TryGetValue(localId, out var currentRepo);
            if (intent.Kind == BodyWriteOperationKind.Resolution && currentRepo != null
                && ThreeWayTextMerge.ContainsConflictMarkers(currentRepo.Body))
            {
                ShadowPendingProjection(currentRepo.Body, currentRepo, observed, localId);
                continue;
            }
            if (currentRepo?.Body == intent.IntendedLocalBody && observed.Body == intent.PriorExternalBody)
            {
                fenced.Remove(localId); // proved not to have landed: re-send the SAME journaled operation.
                continue;
            }
            switch (intent.Kind)
            {
                case BodyWriteOperationKind.Update:
                    RecoverUpdate(intent, currentRepo, observed, localId);
                    break;
                case BodyWriteOperationKind.Resolution:
                    RecoverResolution(intent, currentRepo, observed, localId);
                    break;
            }
        }
        return fenced;
    }

    private void RecoverPendingCreate(BodyWriteIntent intent, IReadOnlyDictionary<string, SyncDoc> repo,
        IReadOnlyDictionary<string, SyncDoc> external, IReadOnlySet<string> ambiguousCreates,
        HashSet<string> fenced, HashSet<string> retryCreates)
    {
        repo.TryGetValue(intent.LocalId, out var createdRepo);
        if (ambiguousCreates.Contains(intent.LocalId))
        {
            ShadowAmbiguousPendingCreate(intent, createdRepo);
            return;
        }
        if (!external.TryGetValue(intent.LocalId, out var created))
        {
            fenced.Remove(intent.LocalId); // Exact id has no live page: retry the same durable operation.
            retryCreates.Add(intent.LocalId);
            return;
        }
        _base.SetDualBodyBase(new SyncDoc
        {
            LocalId = intent.LocalId, ExternalId = created.ExternalId,
            Fields = created.Fields,
            Body = createdRepo?.Body ?? intent.IntendedLocalBody,
            SourcePath = createdRepo?.SourcePath ?? _repoPathFor(intent.LocalId, created.Fields, null),
        }, new DynaDocs.Sync.Projection.DualBodyBase(intent.IntendedLocalBody, created.Body));
        _base.RemovePendingBodyWrite(intent.LocalId);
        // Binding establishes the observed remote baseline, but this tick must not also treat a post-crash
        // local property/relation edit as synced. Leave it fenced until the next tick reconciles those deltas.
    }

    private void ShadowAmbiguousPendingCreate(BodyWriteIntent intent, SyncDoc? repo)
    {
        if (_conflictShadowPathFor == null)
            return;
        var path = _conflictShadowPathFor(intent.LocalId);
        if (File.Exists(path))
            return;
        SyncDocFile.Write(path, new SyncDoc
        {
            LocalId = intent.LocalId,
            Fields = repo?.Fields ?? [],
            Body = $"<<<<<<< repo\n{repo?.Body ?? intent.IntendedLocalBody}\n=======\nexternal create identity is ambiguous: dydo-write-id '{intent.OperationId}' matched multiple pages\n>>>>>>> external",
            SourcePath = path,
        });
    }

    private void RecoverUpdate(BodyWriteIntent intent, SyncDoc? repo, SyncDoc external, string localId) =>
        RecoverProvenProjectedWrite(intent, repo, external, localId);

    private void RecoverResolution(BodyWriteIntent intent, SyncDoc? repo, SyncDoc external, string localId)
    {
        RecoverProvenProjectedWrite(intent, repo, external, localId);
    }

    private void RecoverProvenProjectedWrite(BodyWriteIntent intent, SyncDoc? repo, SyncDoc external, string localId)
    {
        var current = repo?.Body ?? intent.IntendedLocalBody;
        var merged = DynaDocs.Sync.Projection.ProjectedMarkdownMerge.Merge(
            new DynaDocs.Sync.Projection.DualBodyBase(intent.PriorLocalBody, intent.PriorExternalBody),
            intent.PriorLocalBody, external.Body);
        if (repo?.Body == intent.IntendedLocalBody && merged.IsSuccess && merged.Body == intent.IntendedLocalBody)
        {
            var baseDoc = _base.Get(localId)!;
            _base.SetDualBodyBase(new SyncDoc
            {
                LocalId = localId, ExternalId = external.ExternalId, Fields = baseDoc.Fields,
                Body = repo.Body, SourcePath = repo.SourcePath,
            }, new DynaDocs.Sync.Projection.DualBodyBase(intent.IntendedLocalBody, external.Body));
            _base.RemovePendingBodyWrite(localId);
            return;
        }
        ShadowPendingProjection(current, repo, external, localId);
    }

    private void ShadowPendingProjection(string localBody, SyncDoc? repo, SyncDoc external, string localId)
    {
        if (_conflictShadowPathFor == null)
            return;
        var path = _conflictShadowPathFor(localId);
        if (!File.Exists(path))
        {
            var fields = repo?.Fields ?? external.Fields;
            SyncDocFile.Write(path, new SyncDoc
            {
                LocalId = localId, ExternalId = external.ExternalId, Fields = fields,
                Body = $"<<<<<<< repo\n{localBody}\n=======\n{external.Body}\n>>>>>>> external",
                SourcePath = path,
            });
        }
    }

    private static Dictionary<string, SyncBodyReadStatus> MapBodyReadStatuses(IReadOnlyList<SyncRecord> records,
        IReadOnlyDictionary<string, SyncDoc> external)
    {
        var externalIdToLocalId = external.Values.ToDictionary(doc => doc.ExternalId!, doc => doc.LocalId);
        var statuses = new Dictionary<string, SyncBodyReadStatus>();
        foreach (var record in records)
        {
            if (externalIdToLocalId.TryGetValue(record.ExternalId, out var localId))
                statuses[localId] = record.BodyReadStatus;
        }
        return statuses;
    }

    private static ReconcileResult TruncatedResult(string localId, SyncDoc? baseDoc, SyncDoc? repo, SyncDoc? external)
    {
        var localCandidate = repo?.Body ?? "(no canonical file)";
        var fields = repo?.Fields ?? external?.Fields ?? [];
        var source = repo?.SourcePath ?? external?.SourcePath ?? "";
        return new ReconcileResult
        {
            LocalId = localId,
            Action = ReconcileAction.Conflict,
            RepoWrite = new SyncDoc
            {
                LocalId = localId,
                ExternalId = baseDoc?.ExternalId ?? external?.ExternalId,
                Fields = fields,
                Body = $"<<<<<<< repo\n{localCandidate}\n=======\nexternal body unavailable: truncated export\n>>>>>>> external",
                SourcePath = source,
            },
            StructuredConflictReason = "external body unavailable: truncated export",
            UnhandledProjection = true,
        };
    }

    private Dictionary<string, string> PrepareBodyWriteIntents(IEnumerable<ReconcileResult> results,
        IReadOnlySet<string> shadowed)
    {
        var operations = new Dictionary<string, string>();
        foreach (var result in results)
        {
            if (shadowed.Contains(result.LocalId) || !result.WriteBody || result.ExternalWrite == null
                || result.NewBodyBase == null)
                continue;
            var prior = _base.GetDualBodyBase(result.LocalId)
                ?? new DynaDocs.Sync.Projection.DualBodyBase("", "");
            var existing = _base.GetPendingBodyWrite(result.LocalId);
            var operationId = existing?.OperationId ?? Guid.NewGuid().ToString();
            if (existing == null)
                _base.WritePendingBodyWrite(new BodyWriteIntent
            {
                OperationId = operationId,
                Kind = result.BodyWriteKind ?? (result.ExternalWrite.ExternalId == null
                    ? BodyWriteOperationKind.Create : BodyWriteOperationKind.Update),
                LocalId = result.LocalId,
                ExternalId = result.ExternalWrite.ExternalId,
                PriorLocalBody = prior.LocalBody,
                PriorExternalBody = prior.ExternalBody,
                IntendedLocalBody = result.NewBodyBase.LocalBody,
            });
            operations[result.LocalId] = operationId;
        }
        return operations;
    }

    /// <summary>The mass-delete fuse predicate (sprint decision, ns-2): trip when a reconcile would locally delete
    /// MORE than 5 records AND more than 20% of the type's tracked base records. Both arms must fire, so a handful
    /// of deletions on a large board and a tiny board's whole contents both pass; the <c>* 5</c> compare is the
    /// exact 20% test in integers.</summary>
    private static bool FuseTrips(int deletions, int trackedCount) =>
        deletions > 5 && deletions * 5 > trackedCount;

    /// <summary>Divert a conflicted body to the shadow tree instead of the canonical repo file (DR 035 §4/§5 —
    /// the docs mirror). Active only when a shadow-path resolver was supplied; the spine passes none and this is
    /// a no-op. Two things route a body here, both caught by one check on the body about to be persisted: a
    /// genuine two-sided <see cref="ReconcileAction.Conflict"/> whose merged body carries this run's conflict
    /// sentinels, and — the safety-rail backstop — ANY result whose body about to be written bears them. The
    /// conflicted body is written to the shadow path; the canonical file is left untouched at its last-good
    /// state, the external push is skipped, and the base is NOT advanced (see <see cref="CommitBase"/>), so the
    /// two-sided edit is re-detected until a human resolves the shadow file (promoted on the next sync). This is
    /// the hard invariant behind issue 0235: the sync can never corrupt a canonical doc with conflict markers.
    /// Returns true when the result was shadowed, so the caller skips <see cref="ApplyResult"/> for it.</summary>
    private bool RouteConflictToShadow(ReconcileResult result, HashSet<string> shadowed)
    {
        if (_conflictShadowPathFor == null || result.RepoWrite is not { } repoWrite)
            return false;
        if (result.StructuredConflictReason == null && !ThreeWayTextMerge.ContainsConflictMarkers(repoWrite.Body))
            return false;

        var shadowPath = _conflictShadowPathFor(result.LocalId);
        // Never clobber an existing shadow — mark the result shadowed (base un-advanced) and leave the file be. Two
        // cases both demand this: a marker-BEARING shadow is a human's in-progress resolution the previous tick
        // recorded, and a marker-FREE shadow is one the human already RESOLVED but the promote pass has not yet
        // consumed (a fast daemon tick re-detecting the conflict before promotion, or a promotion that could not
        // align its base) — overwriting either with fresh markers would discard the human's work (ns-13 F3). Only a
        // genuinely absent shadow is written afresh.
        if (!File.Exists(shadowPath))
            SyncDocFile.Write(shadowPath, repoWrite);
        shadowed.Add(result.LocalId);
        return true;
    }

    /// <summary>Advance the base snapshot after Apply. A create records its base only once its external
    /// id is confirmed in <paramref name="assigned"/> — a create that failed leaves no base entry, so it
    /// is retried (not seen as an external delete) and never duplicated. A delete that pushed an external
    /// archive advances its base only once that archive is confirmed in <paramref name="deleted"/> — the
    /// same per-item gate — so a transient/auth archive failure (or a tolerated archived-ancestor skip)
    /// leaves the entry for retry instead of dropping tracking for a still-live page (issue 0221).
    /// Non-create, non-delete advances commit only when the whole batch applied, so a failed tick
    /// self-heals on retry.</summary>
    private void CommitBase(List<ReconcileResult> results, IReadOnlySet<string> shadowed,
        IReadOnlyDictionary<string, string> assigned, IReadOnlySet<string> deleted,
        IReadOnlySet<string> emptyBodied, IReadOnlyList<BodyWriteReceipt> receipts,
        IReadOnlyDictionary<string, string> operations, bool applied)
    {
        var receiptByOperation = receipts.ToDictionary(receipt => receipt.OperationId);
        foreach (var result in results)
        {
            // A shadowed conflict (DR 035 §4) was neither written to the canonical file nor pushed, so its base
            // must NOT advance — leaving it lets the next tick re-detect the two-sided edit until a human resolves
            // the shadow file. Advancing here would record the un-persisted conflict body as synced and lose it.
            if (shadowed.Contains(result.LocalId))
                continue;
            if (CommitProjectedBase(result, receiptByOperation, operations, applied))
                continue;
            CommitLegacyBase(result, assigned, deleted, emptyBodied, applied);
        }
    }

    private bool CommitProjectedBase(ReconcileResult result,
        IReadOnlyDictionary<string, BodyWriteReceipt> receiptByOperation,
        IReadOnlyDictionary<string, string> operations, bool applied)
    {
        if (result.NewBodyBase == null)
            return false;
        if (!result.WriteBody || result.ExternalWrite == null)
        {
            if (applied)
                _base.SetDualBodyBase(result.NewBase!, result.NewBodyBase);
            return true;
        }
        if (!operations.TryGetValue(result.LocalId, out var operationId)
            || !receiptByOperation.TryGetValue(operationId, out var receipt))
            return true;

        var baseDoc = result.NewBase!;
        baseDoc.ExternalId = receipt.ExternalId;
        _base.SetDualBodyBase(baseDoc, new DynaDocs.Sync.Projection.DualBodyBase(
            result.NewBodyBase.LocalBody, receipt.ObservedExternalBody));
        _base.RemovePendingBodyWrite(result.LocalId);
        return true;
    }

    private void CommitLegacyBase(ReconcileResult result, IReadOnlyDictionary<string, string> assigned,
        IReadOnlySet<string> deleted, IReadOnlySet<string> emptyBodied, bool applied)
    {
        switch (result.Action)
        {
            case ReconcileAction.None:
                return;

            case ReconcileAction.Retire:
                // Both sides are gone, so nothing was pushed for this object — dropping its stale base entry
                // is safe regardless of whether the batch applied (slice brief §2).
                _base.Remove(result.LocalId);
                return;

            case ReconcileAction.Delete:
                // A delete that archived an external page drops its base only if THAT archive landed
                // (issue 0221): a swallowed archived-ancestor skip or a propagated transient/auth failure
                // leaves the entry for retry, never orphaning a live page. A delete with only a repo-file
                // removal and no external archive still gates on the batch applying.
                if (result.ExternalDelete != null ? deleted.Contains(result.ExternalDelete) : applied)
                    _base.Remove(result.LocalId);
                return;
        }

        // An upsert with no external id is a create: record its base only with the assigned id.
        if (result.ExternalWrite is { ExternalId: null })
        {
            if (assigned.TryGetValue(result.LocalId, out var externalId))
            {
                var newBase = result.NewBase!;
                newBase.ExternalId = externalId;
                _base.Set(emptyBodied.Contains(result.LocalId) ? WithBody(newBase, "") : newBase);
            }
            return;
        }
        if (applied)
            _base.Set(result.NewBase!);
    }

    /// <summary>Maintain the base snapshot's last-activity for a repo-backed object (DR 030 §3), timestamped
    /// from the repo file's mtime — the moment the change landed on disk — captured BEFORE Apply so it
    /// reflects the human/agent edit, not the engine's own subsequent rewrite of a merged file.
    /// <para>Two cases stamp it: a genuine repo-side change (<see cref="ReconcileResult.RepoChanged"/>), which
    /// bumps it every time; and the FIRST sight of a doc the store has never stamped — an object provisioned
    /// before this slice, one new this tick, or one created FROM the external side (its repo file does not
    /// exist yet, so the effective doc is <see cref="ReconcileResult.RepoWrite"/> and the stamp falls back to
    /// now) — which is SEEDED even on a no-op tick so an already-stalled loop can still go stale rather than
    /// reading null forever. An engine-performed external-to-repo write (RepoChanged false) on an object that
    /// already has an activity date is deliberately left untouched, so a mass sync never falsifies activity.
    /// Seeding writes only engine-internal store state — never the doc's fields — so a no-op tick stays a
    /// no-op on both sides and can never provoke an edit loop.</para></summary>
    private void RecordActivity(ReconcileResult result, SyncDoc? repo)
    {
        var doc = repo ?? result.RepoWrite;
        if (doc == null)
            return;
        if (!result.RepoChanged && _base.GetLastActivity(result.LocalId) != null)
            return;
        var mtime = !string.IsNullOrEmpty(doc.SourcePath) && File.Exists(doc.SourcePath)
            ? File.GetLastWriteTimeUtc(doc.SourcePath)
            : DateTime.UtcNow;
        _base.SetLastActivity(result.LocalId, mtime.ToString("yyyy-MM-dd"));
    }

    /// <summary>Push a seeded or drifted engine-computed value onto its page when this tick's action carried
    /// no upsert to ride along with (finding 1, DR 030 §3). A create-to-external or any push/merge already
    /// writes engine-computed properties via its upsert, so those are skipped here; a delete is skipped so a
    /// page about to be archived is never stamped. For every other case with an external page and a recorded
    /// last-activity, a refresh is enqueued — the adapter then writes only if the page is not already in sync,
    /// so repeated no-op ticks issue no write. Gated on the adapter actually maintaining engine-computed
    /// properties, so a plain view is never handed a refresh it would ignore.
    /// <para>The external id is taken ONLY from this tick's external read, never from the base snapshot: a
    /// refresh is legitimate only against a page present in the current read. Falling back to the base id
    /// would enqueue a property write against a page that vanished from the read — one archived/trashed
    /// between ticks. Real Notion rejects a property write on an archived page with 400, throwing mid-Apply
    /// before the base advances, permanently wedging the type's sync with no self-heal (finding F1). When the
    /// repo file is also gone, ReconcileEngine.BothGone returns Retire (wave 4a), which removes the base entry
    /// AND its last-activity — so no orphaned refresh is even considered for that object the next tick.</para></summary>
    private void EnqueueEngineComputedRefresh(string localId, ReconcileResult result, SyncDoc? external, SyncChangeSet changes)
    {
        if (!_adapter.WritesEngineComputed || result.ExternalWrite != null || result.Action == ReconcileAction.Delete)
            return;
        var externalId = external?.ExternalId;
        if (externalId == null || _base.GetLastActivity(localId) == null)
            return;
        changes.EngineComputedRefreshes.Add(new SyncEngineComputedRefresh { LocalId = localId, ExternalId = externalId });
    }

    private void ApplyResult(string localId, ReconcileResult result, SyncDoc? repo, SyncChangeSet changes, string? operationId)
    {
        switch (result.Action)
        {
            case ReconcileAction.None:
            case ReconcileAction.Retire:
                break;

            case ReconcileAction.PushToExternal:
            case ReconcileAction.Merged:
            case ReconcileAction.Conflict:
            case ReconcileAction.Create:
                if (result.ExternalWrite != null)
                    changes.Upserts.Add(ToUpsert(result.ExternalWrite, result.ClearedKeys,
                        result.NewBodyBase == null || result.WriteBody, operationId));
                // RepoWrite rewrites the file; else (a pure push, or a create-to-external) the repo doc is
                // unchanged and only its folder may need to move to match a status change.
                var docToPlace = result.RepoWrite ?? repo;
                if (docToPlace != null)
                    PlaceRepoFile(localId, docToPlace, rewrite: result.RepoWrite != null,
                        result.PatchFields, result.PatchBody, repo);
                break;

            case ReconcileAction.WriteToRepo:
                PlaceRepoFile(localId, result.RepoWrite!, rewrite: true, result.PatchFields, result.PatchBody, repo);
                break;

            case ReconcileAction.Delete:
                if (result.ExternalDelete != null)
                    changes.Deletes.Add(result.ExternalDelete);
                if (result.RepoDelete != null && File.Exists(result.RepoDelete))
                    File.Delete(result.RepoDelete);
                break;
        }
    }

    /// <summary>
    /// File a doc at its canonical path, honoring status-driven folder routing (slice brief §3). The path
    /// is derived from the doc's fields and its current path, so a status that maps to a subfolder re-files
    /// it there while a status with no folder mapping keeps the file where it is (finding 1); the local id
    /// (filename stem) is unchanged, so the base still keys the same object and a move is never seen as
    /// delete+create. When <paramref name="rewrite"/> the merged content is written to the new path and the
    /// old file removed; otherwise the on-disk content is already current and the file is only moved if its
    /// folder changed. A no-op when it is already at its canonical path.
    /// </summary>
    private void PlaceRepoFile(string localId, SyncDoc doc, bool rewrite, bool patchFields = false, bool patchBody = false,
        SyncDoc? current = null)
    {
        var oldPath = doc.SourcePath;
        var newPath = _repoPathFor(localId, doc.Fields, string.IsNullOrEmpty(oldPath) ? null : oldPath);
        var moved = !string.IsNullOrEmpty(oldPath) && !SamePath(oldPath, newPath) && File.Exists(oldPath);

        if (rewrite)
        {
            if (current != null && File.Exists(oldPath) && (patchFields || patchBody))
            {
                SyncDocFile.PatchExisting(oldPath, current, doc, patchFields, patchBody);
                if (moved)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);
                    File.Move(oldPath, newPath, overwrite: true);
                }
            }
            else
            {
                SyncDocFile.Write(newPath, doc);
                if (moved)
                    File.Delete(oldPath);
            }
        }
        else if (moved)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);
            File.Move(oldPath, newPath);
        }
    }

    private static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    private static SyncUpsert ToUpsert(SyncDoc doc, IReadOnlyList<string> clearedKeys, bool writeBody, string? operationId) => new()
    {
        LocalId = doc.LocalId,
        ExternalId = doc.ExternalId,
        Fields = doc.Fields,
        Body = doc.Body,
        WriteBody = writeBody,
        OperationId = operationId,
        ClearedKeys = clearedKeys,
    };

    /// <summary>
    /// Pair external records to local ids: by the base snapshot's recorded externalId where
    /// known, else by the record's reserved <see cref="LocalIdField"/> for objects created
    /// externally, else the external id itself as a last-resort stable key.
    /// </summary>
    private Dictionary<string, SyncDoc> MapExternalToLocalId(IReadOnlyList<SyncRecord> records,
        out HashSet<string> ambiguousPendingCreates)
    {
        var externalIdToLocalId = new Dictionary<string, string>();
        foreach (var localId in _base.LocalIds)
        {
            var snap = _base.Get(localId)!;
            if (snap.ExternalId != null)
                externalIdToLocalId[snap.ExternalId] = localId;
        }

        var claimedByOperation = FindPendingCreateClaims(records, out ambiguousPendingCreates,
            out var excluded, out var pendingCreateLocalIds);

        var result = new Dictionary<string, SyncDoc>();
        foreach (var record in records)
        {
            if (excluded.Contains(record.ExternalId))
                continue;
            // The base mapping is our own trusted id; a record's carried local-id (or its external id
            // fallback) is external input and becomes a repo file path, so sanitize it first (§6).
            var localId = claimedByOperation.TryGetValue(record.ExternalId, out var claimed) ? claimed
                : externalIdToLocalId.TryGetValue(record.ExternalId, out var known)
                ? known
                : SanitizeLocalId(record.Fields.FirstOrDefault(f => f.Key == LocalIdField)?.Value ?? record.ExternalId);

            // A pending create owns no trusted external id yet. Its ordinary local-id field cannot bind it:
            // only the durable operation id may do that, otherwise an unrelated page could be adopted.
            if (!claimedByOperation.ContainsKey(record.ExternalId) && pendingCreateLocalIds.Contains(localId))
                continue;

            result[localId] = new SyncDoc
            {
                LocalId = localId,
                ExternalId = record.ExternalId,
                Fields = record.Fields,
                Body = record.Body,
                SourcePath = _repoPathFor(localId, record.Fields, null),
            };
        }

        return result;
    }

    private Dictionary<string, string> FindPendingCreateClaims(IReadOnlyList<SyncRecord> records,
        out HashSet<string> ambiguousPendingCreates, out HashSet<string> excluded,
        out HashSet<string> pendingCreateLocalIds)
    {
        ambiguousPendingCreates = [];
        excluded = [];
        pendingCreateLocalIds = [];
        var claimedByOperation = new Dictionary<string, string>();
        foreach (var localId in _base.LocalIds)
        {
            var intent = _base.GetPendingBodyWrite(localId);
            if (intent?.Kind != BodyWriteOperationKind.Create)
                continue;
            pendingCreateLocalIds.Add(localId);
            var matches = records.Where(record => record.OperationId == intent.OperationId).ToList();
            if (matches.Count == 1)
                claimedByOperation[matches[0].ExternalId] = localId;
            else if (matches.Count > 1)
            {
                ambiguousPendingCreates.Add(localId);
                excluded.UnionWith(matches.Select(match => match.ExternalId));
            }
        }
        return claimedByOperation;
    }

    /// <summary>
    /// Reduce an externally-supplied local id to a bare, safe filename before it is combined into a repo
    /// path (coding-standards §6 — validate at boundaries). An external view is a trust boundary: a value
    /// like <c>../../evil</c>, <c>/etc/passwd</c> or <c>C:\x</c> must never escape the object type's
    /// canonical directory. Directory components and drive prefixes are stripped to the final segment; a
    /// value that reduces to nothing usable (empty, <c>.</c> or <c>..</c>) is rejected.
    /// </summary>
    internal static string SanitizeLocalId(string localId)
    {
        var name = localId.Replace('\\', '/');
        var slash = name.LastIndexOf('/');
        if (slash >= 0)
            name = name[(slash + 1)..];
        var colon = name.LastIndexOf(':'); // drop a drive prefix that survives when there is no separator
        if (colon >= 0)
            name = name[(colon + 1)..];
        name = name.Trim();

        if (name.Length == 0 || name == "." || name == "..")
            throw new SyncSecurityException($"external local id '{localId}' does not reduce to a safe file name");
        return name;
    }
}
