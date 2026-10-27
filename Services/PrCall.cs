namespace DynaDocs.Services;

/// <summary>
/// One PR-opening gh call in a command line. <see cref="Repos"/> are the repositories it names
/// (<c>-R</c>, <c>--repo</c>, <c>GH_REPO</c>, a REST endpoint). <see cref="Branch"/> is the head
/// branch it names, or null when it takes the directory's own; <see cref="BranchForm"/> is the
/// text that named it. <see cref="Wrapper"/> is the shell or eval hiding the call, or null.
/// </summary>
internal sealed record PrCall(IReadOnlyList<string> Repos, string? Branch = null, string? BranchForm = null, string? Wrapper = null);
