namespace DynaDocs.Services.Merge;

using DynaDocs.Utils;

/// <summary>
/// The located dydo project's checkout, the GitHub repository its origin names, and the branch its
/// local <c>origin/HEAD</c> names (null when unset). git runs isolated, so an inherited <c>GIT_DIR</c>
/// or <c>GIT_CONFIG_*</c> cannot point the merge at another repository.
/// </summary>
internal sealed record MergeCheckout(string Root, OriginRepository Origin, string? OriginHead)
{
    private const string NoProject = "no dydo project here; run dydo merge from a checkout of the project's repository";

    private static readonly Dictionary<string, string> NoEnvironment = [];

    public static MergeCheckout Locate(string currentDirectory)
    {
        var root = ConfigFileLocator.FindProjectRoot(currentDirectory);
        if (root == null || !IsProjectCheckout(currentDirectory, root))
            throw MergeStop.Refused(NoProject);

        var url = Git(root, "remote", "get-url", "origin");
        var origin = (url.Ok ? OriginRepository.Parse(url.Text.Trim()) : null)
            ?? throw MergeStop.Refused($"{root} has no origin remote naming HOST/OWNER/REPO");
        if (origin.IsSshAlias)
            throw MergeStop.ToolError($"origin's host {origin.Host} is an SSH alias dydo merge cannot resolve; "
                + $"point origin at the real host, such as git@github.com:{origin.FullName}.git");

        var head = Git(root, "symbolic-ref", "refs/remotes/origin/HEAD");
        const string remoteBranches = "refs/remotes/origin/";
        var originHead = head.Text.Trim();
        return new MergeCheckout(root, origin,
            head.Ok && originHead.StartsWith(remoteBranches, StringComparison.Ordinal) ? originHead[remoteBranches.Length..] : null);
    }

    // The repository here must be the project's own, rooted at dydo.json: not one nested inside the
    // project, nor one holding the project in a subfolder. Both paths come from git, so they compare as text.
    private static bool IsProjectCheckout(string currentDirectory, string root)
    {
        var here = Git(currentDirectory, "rev-parse", "--show-toplevel");
        var project = Git(root, "rev-parse", "--show-toplevel", "--show-prefix");
        return here.Ok && project.Ok
            && project.Text.Split('\n') is [var top, var prefix, ..]
            && top.Trim() == here.Text.Trim() && prefix.Trim().Length == 0;
    }

    private static GitResult Git(string directory, params string[] arguments) =>
        GitRunner.RunIsolated(directory, NoEnvironment, arguments);
}
