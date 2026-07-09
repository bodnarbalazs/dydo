namespace DynaDocs.Services;

/// <summary>
/// Outcome of the dispatch preflight (<see cref="DispatchPreflight"/>). A failed result
/// carries an actionable message that names the missing prerequisite AND the fix — the
/// DR 037 §6 / issue 0239 shape — so a dispatch that cannot succeed fails in the
/// dispatcher's terminal instead of downstream.
/// </summary>
public sealed record PreflightResult(bool Ok, string? Error)
{
    public static PreflightResult Pass { get; } = new(true, null);

    public static PreflightResult Fail(string error) => new(false, error);
}
