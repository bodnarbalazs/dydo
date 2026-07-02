namespace DynaDocs.Sync;

using DynaDocs.Models;

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
    /// Apply a change set to the external view. Each created upsert's assigned external id is recorded
    /// into <paramref name="assigned"/> (keyed by <c>LocalId</c>) as soon as that page is created — so
    /// if a later create in the same batch throws, the caller still holds the ids of everything already
    /// created and can persist them, preventing duplicate re-creation on the next tick (slice brief §3).
    /// </summary>
    void Apply(SyncChangeSet changes, IDictionary<string, string> assigned);

    /// <summary>
    /// The body this view echoes back when the given body is written and re-read. Views that convert
    /// bodies lossily (e.g. Notion block conversion drops blank lines) override this so the engine
    /// treats a normalization-only difference as "no change" and stays idempotent (slice brief §4).
    /// Views that store bodies verbatim use the identity default.
    /// </summary>
    string NormalizeBody(string body) => body;

    /// <summary>
    /// The fields this view echoes back after the given doc is written and re-read. Views that round-trip
    /// some fields lossily (e.g. Notion drops a relation whose target it cannot resolve to a page id, or a
    /// field whose property it cannot write, so both read back empty/absent) override this so the engine
    /// compares fields modulo the loss and does not mistake adapter-lossiness for a real external edit —
    /// which would silently blank the repo value (slice brief §1). A genuine value change still differs
    /// under the map, so real edits are still detected. Views that round-trip fields verbatim use the
    /// identity default.
    /// </summary>
    SyncDoc NormalizeFields(SyncDoc doc) => doc;
}
