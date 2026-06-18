namespace DynaDocs.Sync;

using DynaDocs.Models;

/// <summary>
/// An in-memory <see cref="ISyncAdapter"/> for exercising the engine bidirectionally without
/// Notion. Tests seed/edit external records directly via <see cref="Seed"/> / <see cref="Edit"/>
/// / <see cref="DeleteExternal"/> and assert what the engine pushes back through <see cref="Apply"/>.
/// </summary>
public sealed class FakeSyncAdapter : ISyncAdapter
{
    private readonly Dictionary<string, SyncRecord> _records = new();
    private int _nextId = 1;

    /// <summary>Place or replace an external record under a chosen id (simulates external state).</summary>
    public void Seed(string externalId, List<SyncField> fields, string body)
    {
        _records[externalId] = new SyncRecord { ExternalId = externalId, Fields = fields, Body = body };
    }

    /// <summary>Simulate a colleague editing an existing external record.</summary>
    public void Edit(string externalId, List<SyncField> fields, string body) =>
        Seed(externalId, fields, body);

    public void DeleteExternal(string externalId) => _records.Remove(externalId);

    public IReadOnlyList<SyncRecord> ReadExternalState() => _records.Values.ToList();

    public IReadOnlyDictionary<string, string> Apply(SyncChangeSet changes)
    {
        var assigned = new Dictionary<string, string>();

        foreach (var upsert in changes.Upserts)
        {
            var id = upsert.ExternalId ?? $"ext-{_nextId++}";
            if (upsert.ExternalId == null)
                assigned[upsert.LocalId] = id;
            _records[id] = new SyncRecord { ExternalId = id, Fields = upsert.Fields, Body = upsert.Body };
        }

        foreach (var id in changes.Deletes)
            _records.Remove(id);

        return assigned;
    }
}
