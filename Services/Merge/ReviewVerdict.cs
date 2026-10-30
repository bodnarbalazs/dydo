namespace DynaDocs.Services.Merge;

/// <summary>
/// One reviewer verdict line from a PR body: PASS or FAIL, its rubric, and the candidate and base SHAs
/// it names (lower-case, possibly abbreviated; null when the line names none). <see cref="Line"/> is the
/// line as written.
/// </summary>
internal sealed record ReviewVerdict(bool Pass, string Rubric, string? Candidate, string? Base, string Line);
