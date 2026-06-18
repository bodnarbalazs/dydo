namespace DynaDocs.Sync;

/// <summary>
/// The generic, Notion-agnostic seam the sync engine talks to (Decision 025 §1). Any external
/// view — Notion today, a custom UI tomorrow — implements this; all view-specific knowledge
/// lives behind it. The acceptance test for the whole design: delete the adapter and the repo
/// is still whole.
/// </summary>
public interface ISyncAdapter
{
    /// <summary>
    /// Read the current external state of all synced objects as neutral records. The engine
    /// diffs these against its base snapshot, so an adapter that cannot report deltas may simply
    /// return the full current state each tick.
    /// </summary>
    IReadOnlyList<SyncRecord> ReadExternalState();

    /// <summary>
    /// Apply a change set to the external view. Returns the external ids assigned to created
    /// upserts, keyed by <c>LocalId</c>, so the engine can record the local↔external mapping.
    /// </summary>
    IReadOnlyDictionary<string, string> Apply(SyncChangeSet changes);
}
