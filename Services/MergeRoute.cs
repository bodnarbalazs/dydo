namespace DynaDocs.Services;

/// <summary>
/// One route around <c>dydo merge</c> in a command line (DYD-425). <see cref="Form"/> is the text
/// that names it. A <see cref="MergeRouteKind.Push"/> carries the remote branch it updates as
/// <see cref="Destination"/>: a name with <c>refs/heads/</c> dropped, possibly a glob, or
/// <see cref="CurrentBranch"/> or <see cref="Upstream"/> for git to resolve, read in
/// <see cref="Directory"/> (git -C's path, relative to the call's directory) when it names one.
/// </summary>
internal sealed record MergeRoute(MergeRouteKind Kind, string Form, string? Destination = null, string? Directory = null)
{
    /// <summary>The branch HEAD has checked out.</summary>
    public const string CurrentBranch = "HEAD";

    /// <summary>The branch a bare push updates: the current branch's upstream.</summary>
    public const string Upstream = "@{upstream}";
}
