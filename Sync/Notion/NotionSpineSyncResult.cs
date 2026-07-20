namespace DynaDocs.Sync.Notion;

/// <summary>Outcome of one <see cref="NotionSpineSync.Run"/> tick across every object type: the types whose
/// reconcile the mass-delete fuse aborted (slice ns-2). Empty on a clean run. The fuse is a per-type RESULT,
/// not an exception, so a tripped type never blocks its siblings — every other type still reconciles — and this
/// lets <see cref="NotionSyncService"/> map any trip to a tool-error exit after the whole run completes.</summary>
public sealed class NotionSpineSyncResult
{
    public List<string> FuseTrippedTypes { get; init; } = [];

    public bool FuseTripped => FuseTrippedTypes.Count > 0;
}
