namespace DynaDocs.Services;

/// <summary>
/// One route around <c>dydo merge</c> in a command line (DYD-425). <see cref="Form"/> is the text
/// that names it. A <see cref="MergeRouteKind.Push"/> carries the remote branch it updates as
/// <see cref="Destination"/>: a name with <c>refs/heads/</c> dropped, possibly a glob, or
/// <see cref="CurrentBranch"/> or <see cref="Upstream"/> for git to resolve, read in
/// <see cref="Directory"/> (git -C's path, relative to the call's directory) when it names one.
/// A push to <see cref="CurrentBranch"/> or <see cref="Upstream"/> after a branch change earlier in
/// the line also comes once per such change, with <see cref="SwitchedTo"/> naming the branch that
/// change leaves HEAD on, or <see cref="UnreadableBranch"/> when the line does not say (DYD-887).
/// </summary>
internal sealed record MergeRoute(MergeRouteKind Kind, string Form, string? Destination = null, string? Directory = null,
    string? SwitchedTo = null)
{
    /// <summary>The branch HEAD has checked out.</summary>
    public const string CurrentBranch = "HEAD";

    /// <summary>The branch a bare push updates: the current branch's upstream.</summary>
    public const string Upstream = "@{upstream}";

    /// <summary>A branch change whose branch the line does not say for certain; git forbids <c>@{</c> in a branch name.</summary>
    public const string UnreadableBranch = "@{unreadable}";
}
