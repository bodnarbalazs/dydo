namespace DynaDocs.Sync;

/// <summary>A batch of changes to apply to the external view in one tick.</summary>
public sealed class SyncChangeSet
{
    public List<SyncUpsert> Upserts { get; } = [];

    /// <summary>External ids to remove from the external view.</summary>
    public List<string> Deletes { get; } = [];

    public bool IsEmpty => Upserts.Count == 0 && Deletes.Count == 0;
}
