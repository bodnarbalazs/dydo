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
}
