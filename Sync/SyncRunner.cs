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

    /// <param name="repoPathFor">The canonical repo path for a doc, given its local id, fields, and current
    /// on-disk path (null when the doc has none yet). Fields are passed so folder placement can derive from
    /// status (slice brief §3); the current path lets an unmapped status keep an existing doc in place rather
    /// than pulling it to the root (finding 1). A flat layout simply ignores both. See <see cref="RepoFolderLayout"/>.</param>
    public SyncRunner(ISyncAdapter adapter, BaseSnapshotStore baseStore, Func<string, IReadOnlyList<SyncField>, string?, string> repoPathFor)
    {
        _adapter = adapter;
        _base = baseStore;
        _repoPathFor = repoPathFor;
    }

    /// <summary>
    /// Compute the reconcile decisions for this tick without writing anything — no repo files, no
    /// external change set, no base advance. Used by <c>notion sync --dry-run</c> to preview the plan.
    /// </summary>
    public IReadOnlyList<ReconcileResult> Plan(IReadOnlyList<SyncDoc> repoDocs)
    {
        var externalByLocalId = MapExternalToLocalId(_adapter.ReadExternalState());
        var repoByLocalId = IndexByLocalId(repoDocs);

        var localIds = new HashSet<string>(_base.LocalIds);
        localIds.UnionWith(repoByLocalId.Keys);
        localIds.UnionWith(externalByLocalId.Keys);

        var results = new List<ReconcileResult>();
        foreach (var localId in localIds.OrderBy(x => x))
        {
            var baseDoc = _base.Get(localId);
            repoByLocalId.TryGetValue(localId, out var repo);
            externalByLocalId.TryGetValue(localId, out var external);
            results.Add(ReconcileEngine.Reconcile(baseDoc, repo, external, _adapter.NormalizeBody, _adapter.NormalizeFields));
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

    public SyncRunResult Run(IReadOnlyList<SyncDoc> repoDocs)
    {
        var externalByLocalId = MapExternalToLocalId(_adapter.ReadExternalState());
        var repoByLocalId = IndexByLocalId(repoDocs);

        var localIds = new HashSet<string>(_base.LocalIds);
        localIds.UnionWith(repoByLocalId.Keys);
        localIds.UnionWith(externalByLocalId.Keys);

        var changes = new SyncChangeSet();
        var results = new List<ReconcileResult>();

        foreach (var localId in localIds.OrderBy(x => x))
        {
            var baseDoc = _base.Get(localId);
            repoByLocalId.TryGetValue(localId, out var repo);
            externalByLocalId.TryGetValue(localId, out var external);

            var result = ReconcileEngine.Reconcile(baseDoc, repo, external, _adapter.NormalizeBody, _adapter.NormalizeFields);
            results.Add(result);
            RecordActivity(result, repo);
            ApplyResult(localId, result, repo, changes);
            EnqueueEngineComputedRefresh(localId, result, external, changes);
        }

        // Apply is non-atomic; the base advance is deferred until Apply reports back so a mid-batch
        // throw never leaves the base claiming un-pushed work is synced (slice brief §3). The finally
        // still records the ids of pages already created and persists the base, so a crashed tick that
        // created some pages does not re-create them (duplicate them) on retry.
        var assigned = new Dictionary<string, string>();
        var applied = false;
        try
        {
            _adapter.Apply(changes, assigned);
            applied = true;
        }
        finally
        {
            CommitBase(results, assigned, applied);
            // A completed tick has confirmed every create's external id, so any last-activity with no base
            // entry is a genuine orphan (a crashed create's seed, or a doc whose base never existed) that
            // Retire can never reach — sweep it before saving (finding 7). A partial tick leaves it for the
            // retry, when the pending create either confirms (base entry appears) or is swept then.
            if (applied)
                _base.PruneOrphanLastActivity();
            _base.Save();
        }

        return new SyncRunResult { Results = results };
    }

    /// <summary>Advance the base snapshot after Apply. A create records its base only once its external
    /// id is confirmed in <paramref name="assigned"/> — a create that failed leaves no base entry, so it
    /// is retried (not seen as an external delete) and never duplicated. Non-create advances commit only
    /// when the whole batch applied, so a failed tick self-heals on retry.</summary>
    private void CommitBase(List<ReconcileResult> results, IReadOnlyDictionary<string, string> assigned, bool applied)
    {
        foreach (var result in results)
        {
            switch (result.Action)
            {
                case ReconcileAction.None:
                    break;

                case ReconcileAction.Retire:
                    // Both sides are gone, so nothing was pushed for this object — dropping its stale base entry
                    // is safe regardless of whether the batch applied (slice brief §2).
                    _base.Remove(result.LocalId);
                    break;

                case ReconcileAction.Delete:
                    if (applied)
                        _base.Remove(result.LocalId);
                    break;

                default:
                    // An upsert with no external id is a create: record its base only with the assigned id.
                    if (result.ExternalWrite is { ExternalId: null })
                    {
                        if (assigned.TryGetValue(result.LocalId, out var externalId))
                        {
                            result.NewBase!.ExternalId = externalId;
                            _base.Set(result.NewBase);
                        }
                    }
                    else if (applied)
                    {
                        _base.Set(result.NewBase!);
                    }
                    break;
            }
        }
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

    private void ApplyResult(string localId, ReconcileResult result, SyncDoc? repo, SyncChangeSet changes)
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
                    changes.Upserts.Add(ToUpsert(result.ExternalWrite));
                // RepoWrite rewrites the file; else (a pure push, or a create-to-external) the repo doc is
                // unchanged and only its folder may need to move to match a status change.
                PlaceRepoFile(localId, result.RepoWrite ?? repo, rewrite: result.RepoWrite != null);
                break;

            case ReconcileAction.WriteToRepo:
                PlaceRepoFile(localId, result.RepoWrite!, rewrite: true);
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
    /// folder changed. A no-op when the doc is null or already at its canonical path.
    /// </summary>
    private void PlaceRepoFile(string localId, SyncDoc? doc, bool rewrite)
    {
        if (doc == null)
            return;

        var oldPath = doc.SourcePath;
        var newPath = _repoPathFor(localId, doc.Fields, string.IsNullOrEmpty(oldPath) ? null : oldPath);
        var moved = !string.IsNullOrEmpty(oldPath) && !SamePath(oldPath, newPath) && File.Exists(oldPath);

        if (rewrite)
        {
            SyncDocFile.Write(newPath, doc);
            if (moved)
                File.Delete(oldPath);
        }
        else if (moved)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);
            File.Move(oldPath, newPath);
        }
    }

    private static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    private static SyncUpsert ToUpsert(SyncDoc doc) => new()
    {
        LocalId = doc.LocalId,
        ExternalId = doc.ExternalId,
        Fields = doc.Fields,
        Body = doc.Body,
    };

    /// <summary>
    /// Pair external records to local ids: by the base snapshot's recorded externalId where
    /// known, else by the record's reserved <see cref="LocalIdField"/> for objects created
    /// externally, else the external id itself as a last-resort stable key.
    /// </summary>
    private Dictionary<string, SyncDoc> MapExternalToLocalId(IReadOnlyList<SyncRecord> records)
    {
        var externalIdToLocalId = new Dictionary<string, string>();
        foreach (var localId in _base.LocalIds)
        {
            var snap = _base.Get(localId)!;
            if (snap.ExternalId != null)
                externalIdToLocalId[snap.ExternalId] = localId;
        }

        var result = new Dictionary<string, SyncDoc>();
        foreach (var record in records)
        {
            // The base mapping is our own trusted id; a record's carried local-id (or its external id
            // fallback) is external input and becomes a repo file path, so sanitize it first (§6).
            var localId = externalIdToLocalId.TryGetValue(record.ExternalId, out var known)
                ? known
                : SanitizeLocalId(record.Fields.FirstOrDefault(f => f.Key == LocalIdField)?.Value ?? record.ExternalId);

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
