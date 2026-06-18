namespace DynaDocs.Sync;

using DynaDocs.Models;

/// <summary>
/// A create-or-update to push to the external view. <see cref="ExternalId"/> is null when the
/// object does not yet exist externally (a create); the adapter returns the assigned id.
/// </summary>
public sealed class SyncUpsert
{
    public required string LocalId { get; init; }
    public string? ExternalId { get; init; }
    public required List<SyncField> Fields { get; init; }
    public required string Body { get; init; }
}
