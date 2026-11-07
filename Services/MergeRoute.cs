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
}

/// <summary>
/// A branch change in a line (DYD-887): <see cref="Branch"/> is the word that names the branch HEAD
/// is left on, which git resolves in the change's tree, or a branch the line itself
/// <see cref="Creates"/>, or <see cref="MergeRoute.UnreadableBranch"/>. The tree is
/// <see cref="Directory"/> (git -C's, as bash reads it) or <see cref="AsWritten"/> when that differs.
/// <see cref="Chained"/>: only <c>&amp;&amp;</c> separates it from the push, which so runs only after it succeeds.
/// </summary>
internal sealed record BranchChange(string Branch, bool Creates = false, string? Directory = null, string? AsWritten = null,
    bool Chained = false);

/// <summary>A push's branch changes, equal when their items are, so routes keep their value equality.</summary>
internal sealed record BranchChanges(IReadOnlyList<BranchChange> Items)
{
    public static readonly BranchChanges None = new([]);

    public bool Equals(BranchChanges? other) => other != null && Items.SequenceEqual(other.Items);

    public override int GetHashCode() => Items.Aggregate(Items.Count, (hash, change) => HashCode.Combine(hash, change));
}
