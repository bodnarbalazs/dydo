namespace DynaDocs.Sync;

/// <summary>
/// A request to (re)write an EXISTING external page's engine-computed properties (last-activity, DR 030
/// §3) to match the engine's current value, without touching any canonical property or the body. Emitted
/// for an object whose tick produces no upsert — a no-op, an external-to-repo write, or a page created
/// from the external side — so a seeded or drifted last-activity still lands on the board (finding 1). The
/// adapter writes only the engine-computed value the engine derives (keyed by <see cref="LocalId"/>) onto
/// the page (<see cref="ExternalId"/>), and skips the write when the page already carries it, so a no-op
/// tick stays a no-op.
/// </summary>
public sealed class SyncEngineComputedRefresh
{
    public required string LocalId { get; init; }
    public required string ExternalId { get; init; }
}
