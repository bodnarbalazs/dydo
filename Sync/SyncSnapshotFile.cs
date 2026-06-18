namespace DynaDocs.Sync;

/// <summary>The on-disk shape of the base snapshot store: every object's last-synced state.</summary>
public sealed class SyncSnapshotFile
{
    public List<SyncSnapshot> Objects { get; set; } = [];
}
