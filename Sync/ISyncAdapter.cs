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
    /// Every delete that actually LANDS is recorded into <paramref name="deleted"/> (by external id) the
    /// instant its archive succeeds — the same pattern as <paramref name="assigned"/>, so the caller
    /// advances a delete's base only for archives that truly happened. A delete an adapter deliberately
    /// tolerates and skips (the docs mirror's archived-ancestor case, issue 0221) is NOT recorded, so its
    /// base entry is retained for the next tick rather than orphaning a still-live page. A create that exists but
    /// still needs its body written is recorded in <paramref name="emptyBodied"/> so the caller persists its base
    /// with an empty body until the write succeeds.
    /// </summary>
    void Apply(SyncChangeSet changes, IDictionary<string, string> assigned, ICollection<string> deleted,
        ICollection<string> emptyBodied);

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

    /// <summary>
    /// Whether this view maintains engine-computed properties (last-activity, DR 030 §3) that the engine
    /// writes one-way. When true, the runner enqueues an engine-computed refresh for objects a tick would
    /// otherwise leave un-pushed (a no-op, an external-to-repo write, or an externally-created page), so a
    /// seeded or drifted value still reaches the page (finding 1). Views without engine-computed properties
    /// use the default, and the runner never enqueues a refresh they would ignore.
    /// </summary>
    bool WritesEngineComputed => false;

    /// <summary>
    /// Whether the given external body is merely the PREVIOUS converter version's degraded projection of the base
    /// body — a one-time migration artifact, not a genuine board edit (ns-7). A board synced before a converter
    /// upgrade holds blocks the old converter pushed; read back and normalized under the new converter they diverge,
    /// which the reconcile would otherwise mistake for an external edit and use to overwrite the canonical file. An
    /// adapter that upgrades its converter overrides this to recognise that projection so the engine treats it as
    /// unchanged and force-pushes the repo body to upgrade the board instead. Default false — no migration in play.
    /// </summary>
    bool IsStaleConverterEcho(string externalBody, string baseBody) => false;

    /// <summary>
    /// Whether this view's TREE STRUCTURE is repo-owned one-way (DR 033 §2 — the docs nested-page mirror),
    /// as opposed to the fully bidirectional spine. When true, a page missing from the external read while its
    /// repo doc is still present is NEVER a deletion — it is listing eventual-consistency lag, or a colleague's
    /// stray archive — so the engine re-creates it from the repo instead of deleting the repo file or archiving
    /// the page. The invariant: a present repo doc's page is never archived; archive fires only when the repo
    /// doc is genuinely gone. The default (false) keeps the spine's delete/modify semantics, where an
    /// external-side deletion is a meaningful signal.
    /// </summary>
    bool RepoOwnedStructure => false;
}
