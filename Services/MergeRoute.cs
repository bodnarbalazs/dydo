namespace DynaDocs.Services;

/// <summary>
/// One route around <c>dydo merge</c> in a command line (DYD-425). <see cref="Form"/> is the text
/// that names it. A <see cref="MergeRouteKind.Push"/> carries the remote branch it updates as
/// <see cref="Destination"/>: a name with <c>refs/heads/</c> dropped, possibly a glob, or
/// <see cref="CurrentBranch"/> or <see cref="Upstream"/> for git to resolve, read in
/// <see cref="Directory"/> (git -C's path, relative to the call's directory) when it names one.
/// A push to <see cref="CurrentBranch"/> or <see cref="Upstream"/> also carries the line's
/// <see cref="Changes"/> before it, which may have moved HEAD by the time it runs (DYD-887).
/// </summary>
internal sealed record MergeRoute(MergeRouteKind Kind, string Form, string? Destination = null, string? Directory = null)
{
    /// <summary>The branch HEAD has checked out.</summary>
    public const string CurrentBranch = "HEAD";

    /// <summary>The branch a bare push updates: the current branch's upstream.</summary>
    public const string Upstream = "@{upstream}";

    /// <summary>A branch change whose branch the line does not say for certain; git forbids <c>@{</c> in a branch name.</summary>
    public const string UnreadableBranch = "@{unreadable}";

    /// <summary>The branch changes the line runs before this push of the current branch, in the order it runs them.</summary>
    public BranchChanges Changes { get; init; } = BranchChanges.None;

    /// <summary>
    /// The line changes branch and pushes, and a substitution holds a change or a push, so the line
    /// does not say which branch HEAD is on when this push runs (DYD-887).
    /// </summary>
    public bool Unreadable { get; init; }
}
