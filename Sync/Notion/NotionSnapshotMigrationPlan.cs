namespace DynaDocs.Sync.Notion;

using DynaDocs.Models;
using DynaDocs.Sync.Projection;

/// <summary>Pure classification output for one legacy snapshot pass. The caller owns when its staged base upgrades
/// and operator shadows become durable.</summary>
internal sealed class NotionSnapshotMigrationPlan
{
    public Dictionary<string, (SyncDoc Base, DualBodyBase Bodies)> Adoptions { get; } = [];
    public Dictionary<string, SyncDoc> Shadows { get; } = [];
}
