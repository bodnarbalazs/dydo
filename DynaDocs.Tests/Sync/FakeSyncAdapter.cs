namespace DynaDocs.Sync;

using DynaDocs.Models;

/// <summary>
/// An in-memory <see cref="ISyncAdapter"/> for exercising the engine bidirectionally without
/// Notion. Tests seed/edit external records directly via <see cref="Seed"/> / <see cref="Edit"/>
/// / <see cref="DeleteExternal"/> and assert what the engine pushes back through <see cref="Apply"/>.
///
/// Test-only: this lives in the test assembly (namespaced <c>DynaDocs.Sync</c> so it sits beside the
/// engine it fakes) so the Native-AOT production binary never ships it (slice brief finding 5).
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

    /// <summary>When true, <see cref="Apply"/> throws before touching any record — drives the partial-tick
    /// tests where a failing Apply holds base advances back (a Retire must still commit, finding 7).</summary>
    public bool FailApply { get; set; }

    public IReadOnlyList<SyncRecord> ReadExternalState() => _records.Values.ToList();

    public void Apply(SyncChangeSet changes, IDictionary<string, string> assigned) =>
        Apply(changes, assigned, new HashSet<string>(), new HashSet<string>());

    public void Apply(SyncChangeSet changes, IDictionary<string, string> assigned, ICollection<string> deleted)
        => Apply(changes, assigned, deleted, new HashSet<string>());

    public void Apply(SyncChangeSet changes, IDictionary<string, string> assigned, ICollection<string> deleted,
        ICollection<string> emptyBodied)
    {
        if (FailApply)
            throw new InvalidOperationException("simulated Apply failure");

        foreach (var upsert in changes.Upserts)
        {
            var id = upsert.ExternalId ?? $"ext-{_nextId++}";
            if (upsert.ExternalId == null)
                assigned[upsert.LocalId] = id;
            _records[id] = new SyncRecord { ExternalId = id, Fields = upsert.Fields, Body = upsert.Body };
        }

        foreach (var id in changes.Deletes)
        {
            _records.Remove(id);
            deleted.Add(id);
        }
    }
}
