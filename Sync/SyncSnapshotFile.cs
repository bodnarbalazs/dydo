namespace DynaDocs.Sync;

/// <summary>The on-disk shape of the base snapshot store: every object's last-synced state, plus the
/// engine-derived per-object last-activity dates (DR 030 §3) — kept beside the snapshots rather than
/// inside them so a create's activity can be recorded before its snapshot exists, and so an old file
/// without the map still loads (the map defaults empty).</summary>
public sealed class SyncSnapshotFile
{
    public List<SyncSnapshot> Objects { get; set; } = [];

    /// <summary>Local id → ISO-8601 date of the object's last genuine repo-side change (DR 030 §3).</summary>
    public Dictionary<string, string> LastActivity { get; set; } = new();
}
