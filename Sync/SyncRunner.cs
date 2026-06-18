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
    private readonly Func<string, string> _repoPathForLocalId;

    public SyncRunner(ISyncAdapter adapter, BaseSnapshotStore baseStore, Func<string, string> repoPathForLocalId)
    {
        _adapter = adapter;
        _base = baseStore;
        _repoPathForLocalId = repoPathForLocalId;
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

            var result = ReconcileEngine.Reconcile(baseDoc, repo, external);
            results.Add(result);
            ApplyResult(localId, result, changes);
        }

        var assignedIds = _adapter.Apply(changes);
        foreach (var (localId, externalId) in assignedIds)
        {
            var snap = _base.Get(localId);
            if (snap != null)
            {
                snap.ExternalId = externalId;
                _base.Set(snap);
            }
        }

        _base.Save();
        return new SyncRunResult { Results = results };
    }

    private void ApplyResult(string localId, ReconcileResult result, SyncChangeSet changes)
    {
        switch (result.Action)
        {
            case ReconcileAction.None:
                break;

            case ReconcileAction.PushToExternal:
            case ReconcileAction.Merged:
            case ReconcileAction.Conflict:
                if (result.RepoWrite != null)
                    SyncDocFile.Write(_repoPathForLocalId(localId), result.RepoWrite);
                if (result.ExternalWrite != null)
                    changes.Upserts.Add(ToUpsert(result.ExternalWrite));
                _base.Set(result.NewBase!);
                break;

            case ReconcileAction.WriteToRepo:
                SyncDocFile.Write(_repoPathForLocalId(localId), result.RepoWrite!);
                _base.Set(result.NewBase!);
                break;

            case ReconcileAction.Create:
                if (result.RepoWrite != null)
                    SyncDocFile.Write(_repoPathForLocalId(localId), result.RepoWrite);
                if (result.ExternalWrite != null)
                    changes.Upserts.Add(ToUpsert(result.ExternalWrite));
                _base.Set(result.NewBase!);
                break;

            case ReconcileAction.Delete:
                if (result.ExternalDelete != null)
                    changes.Deletes.Add(result.ExternalDelete);
                if (result.RepoDelete != null && File.Exists(result.RepoDelete))
                    File.Delete(result.RepoDelete);
                _base.Remove(localId);
                break;
        }
    }

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
            var localId = externalIdToLocalId.TryGetValue(record.ExternalId, out var known)
                ? known
                : record.Fields.FirstOrDefault(f => f.Key == LocalIdField)?.Value ?? record.ExternalId;

            result[localId] = new SyncDoc
            {
                LocalId = localId,
                ExternalId = record.ExternalId,
                Fields = record.Fields,
                Body = record.Body,
                SourcePath = _repoPathForLocalId(localId),
            };
        }

        return result;
    }
}
