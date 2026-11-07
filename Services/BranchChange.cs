namespace DynaDocs.Services;

/// <summary>
/// A branch change in a line (DYD-887): <see cref="Branch"/> is the word that names the branch HEAD
/// is left on, which git resolves in the change's tree, or a branch the line itself
/// <see cref="Creates"/>, or <see cref="MergeRoute.UnreadableBranch"/>. The tree is
/// <see cref="Directory"/> (git -C's, as bash reads it) or <see cref="AsWritten"/> when that differs.
/// <see cref="Chained"/>: only <c>&amp;&amp;</c> separates it from the push, which so runs only after it succeeds.
/// </summary>
internal sealed record BranchChange(string Branch, bool Creates = false, string? Directory = null, string? AsWritten = null,
    bool Chained = false);
