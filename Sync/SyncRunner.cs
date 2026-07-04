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
    private readonly Func<string, IReadOnlyList<SyncField>, string> _repoPathFor;

    /// <param name="repoPathFor">The canonical repo path for a doc, given its local id and fields. Fields
    /// are passed so folder placement can derive from status (slice brief §3); a flat layout simply
    /// ignores them. See <see cref="RepoFolderLayout"/>.</param>
    public SyncRunner(ISyncAdapter adapter, BaseSnapshotStore baseStore, Func<string, IReadOnlyList<SyncField>, string> repoPathFor)
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
        var repoByLocalId = repoDocs.ToDictionary(d => d.LocalId);

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

    public SyncRunResult Run(IReadOnlyList<SyncDoc> repoDocs)
    {
        var externalByLocalId = MapExternalToLocalId(_adapter.ReadExternalState());
        var repoByLocalId = repoDocs.ToDictionary(d => d.LocalId);

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
            ApplyResult(localId, result, repo, changes);
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

    private void ApplyResult(string localId, ReconcileResult result, SyncDoc? repo, SyncChangeSet changes)
    {
        switch (result.Action)
        {
            case ReconcileAction.None:
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
    /// is derived from the doc's fields, so a status change re-files it into the matching subfolder; the
    /// local id (filename stem) is unchanged, so the base still keys the same object and a move is never
    /// seen as delete+create. When <paramref name="rewrite"/> the merged content is written to the new path
    /// and the old file removed; otherwise the on-disk content is already current and the file is only moved
    /// if its folder changed. A no-op when the doc is null or already at its canonical path.
    /// </summary>
    private void PlaceRepoFile(string localId, SyncDoc? doc, bool rewrite)
    {
        if (doc == null)
            return;

        var newPath = _repoPathFor(localId, doc.Fields);
        var oldPath = doc.SourcePath;
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
                SourcePath = _repoPathFor(localId, record.Fields),
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
