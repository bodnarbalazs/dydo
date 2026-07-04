namespace DynaDocs.Sync;

/// <summary>A batch of changes to apply to the external view in one tick.</summary>
public sealed class SyncChangeSet
{
    public List<SyncUpsert> Upserts { get; } = [];

    /// <summary>External ids to remove from the external view.</summary>
    public List<string> Deletes { get; } = [];

    /// <summary>Engine-computed-only property writes onto existing pages that no upsert already covers
    /// (finding 1, DR 030 §3) — carries no canonical fields and never rewrites the body.</summary>
    public List<SyncEngineComputedRefresh> EngineComputedRefreshes { get; } = [];

    public bool IsEmpty => Upserts.Count == 0 && Deletes.Count == 0 && EngineComputedRefreshes.Count == 0;
}
