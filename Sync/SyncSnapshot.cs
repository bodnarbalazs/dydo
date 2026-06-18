namespace DynaDocs.Sync;

/// <summary>
/// The persisted last-synced state for one object — the "base" of the 3-way merge. Held in a
/// gitignored shadow store (never part of the canonical synced tree) so the shadow itself
/// never syncs or gets committed.
/// </summary>
public sealed class SyncSnapshot
{
    public required string LocalId { get; set; }
    public string? ExternalId { get; set; }
    public required List<SyncFieldEntry> Fields { get; set; }
    public required string Body { get; set; }
}
