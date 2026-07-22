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

    /// <summary>Keys a prior sync recorded that this UPDATE now clears — a scalar the base held non-empty and the
    /// repo now carries empty-or-absent (issue 0299, F5). The adapter emits an explicit clear payload for these on
    /// an update so the board value is removed (a blank scalar is otherwise omitted and the board keeps the old
    /// value, silently reverting the local clear next tick). Empty on a create — "blank means unset".</summary>
    public IReadOnlyList<string> ClearedKeys { get; init; } = [];
}
