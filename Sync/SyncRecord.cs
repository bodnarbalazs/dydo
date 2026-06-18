namespace DynaDocs.Sync;

using DynaDocs.Models;

/// <summary>
/// The external view's state for one object, in the engine's neutral terms: an external id,
/// an ordered field map, and a markdown body. The adapter converts its own representation
/// (Notion properties + blocks, or anything else) to/from this; the engine never sees the
/// adapter's native types.
/// </summary>
public sealed class SyncRecord
{
    public required string ExternalId { get; init; }
    public required List<SyncField> Fields { get; init; }
    public required string Body { get; init; }
}
