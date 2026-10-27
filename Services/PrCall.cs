namespace DynaDocs.Services;

/// <summary>
/// One PR-opening gh call in a command line. <see cref="Repo"/> is the repository it acts on when
/// it names one, picked as gh picks it: a PR URL, else the last <c>-R</c>/<c>--repo</c>, else an
/// inline <c>GH_REPO</c>, else the inherited one (for <c>gh api</c>, the REST endpoint's repository);
/// null when it acts on the directory's own. A host it names is kept (<c>HOST/OWNER/REPO</c>).
/// <see cref="Branch"/> is the head branch it names, or null when it takes the directory's own;
/// <see cref="BranchForm"/> is the text that named it. <see cref="Wrapper"/> is the shell, eval or
/// program hiding the call, or null.
/// </summary>
internal sealed record PrCall(string? Repo, string? Branch = null, string? BranchForm = null, string? Wrapper = null);
