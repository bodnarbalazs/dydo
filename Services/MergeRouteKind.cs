namespace DynaDocs.Services;

/// <summary>What a <see cref="MergeRoute"/> does that only the user or <c>dydo merge</c> may do (DR 061).</summary>
internal enum MergeRouteKind
{
    /// <summary>Merges a PR or a branch on GitHub: gh pr merge, a REST merge path, a GraphQL merge mutation.</summary>
    Merge,

    /// <summary>Changes the repository's default branch.</summary>
    DefaultBranchChange,

    /// <summary>A git push, judged by the branch it updates.</summary>
    Push,
}
