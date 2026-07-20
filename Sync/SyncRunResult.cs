namespace DynaDocs.Sync;

/// <summary>Outcome of a sync tick: the per-object reconcile results, with conflict counts.</summary>
public sealed class SyncRunResult
{
    public required List<ReconcileResult> Results { get; init; }

    public int ConflictCount => Results.Count(r => r.Conflicted);

    public IEnumerable<string> ConflictedLocalIds =>
        Results.Where(r => r.Conflicted).Select(r => r.LocalId);

    /// <summary>Local ids whose conflicted body was diverted to the shadow tree instead of the canonical repo
    /// file (DR 035 §4/§5). Empty unless the runner was given a shadow-path resolver (the docs mirror). The
    /// canonical file was left at its last-good state for each of these; the caller warns and the human
    /// resolves the shadow file, promoted on the next sync.</summary>
    public List<string> ShadowedLocalIds { get; init; } = [];

    /// <summary>Whether the mass-delete fuse aborted this run before applying anything (slice ns-2). When true the
    /// adapter's Apply was NOT called, no repo file was deleted, no external change pushed, and the base was not
    /// advanced or saved — the reconcile would have locally deleted a large share of the type's tracked records, so
    /// the caller maps it to a tool error and the operator re-runs with <c>--allow-mass-delete</c> if it is intended.</summary>
    public bool FuseTripped { get; init; }

    /// <summary>The repo file paths this run would have deleted had the mass-delete fuse not tripped, for the abort
    /// message. Empty unless <see cref="FuseTripped"/>.</summary>
    public List<string> WouldDeletePaths { get; init; } = [];
}
