namespace DynaDocs.Services;

/// <summary>
/// <c>dydo pr-ready</c>: a PR shows what its branch holds on the remote, so the tree must be clean and
/// HEAD must equal its upstream before a PR is opened or readied. The comparison reads the local
/// remote-tracking ref; nothing is fetched. The guard runs these tree checks on the
/// PR-opening gh calls <see cref="PrCallScanner"/> finds.
/// </summary>
internal static class PrReadyCheck
{
    private const int ListedPaths = 5;

    /// <summary>
    /// Every reason the tree at <paramref name="directory"/> is not ready, or null when it is not a git
    /// work tree (or git cannot run).
    /// </summary>
    public static List<string>? TreeProblems(string directory)
    {
        if (!GitRunner.Run(directory, "rev-parse", "--is-inside-work-tree").Text.StartsWith("true", StringComparison.Ordinal))
            return null;

        var problems = StatusProblems(GitRunner.Run(directory, "status", "--porcelain=v1", "-z", "--untracked-files=all"));
        problems.Add(UpstreamProblem(directory));
        return [.. problems.OfType<string>()];
    }

    private static List<string?> StatusProblems(GitResult status)
    {
        if (!status.Ok)
            return [$"git status failed: {status.Error}"];

        List<string> staged = [], unstaged = [], untracked = [];
        var entries = status.Entries;
        for (var i = 0; i < entries.Length; i++)
        {
            var (x, y, path) = (entries[i][0], entries[i][1], entries[i][3..]);
            if (x == '?')
            {
                untracked.Add(path);
                continue;
            }
            if (x != ' ') staged.Add(path);
            if (y != ' ') unstaged.Add(path);
            // A rename or copy carries its source path as the next entry.
            if (x is 'R' or 'C') i++;
        }

        return [Listed("staged changes", staged), Listed("unstaged changes", unstaged), Listed("untracked files", untracked)];
    }

    private static string? Listed(string what, List<string> paths)
    {
        if (paths.Count == 0)
            return null;
        var more = paths.Count > ListedPaths ? $" and {paths.Count - ListedPaths} more" : "";
        return $"{what}: {string.Join(", ", paths.Take(ListedPaths))}{more}";
    }

    private static string? UpstreamProblem(string directory)
    {
        var upstream = GitRunner.Run(directory, "rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{upstream}");
        if (!upstream.Ok)
            return "HEAD has no upstream; push the branch with git push -u";

        var name = upstream.Text.Trim();
        var counts = GitRunner.Run(directory, "rev-list", "--left-right", "--count", "HEAD...@{upstream}").Text.Split();
        return counts is ["0", "0", ..]
            ? null
            : $"HEAD differs from its upstream {name} ({counts[0]} ahead, {counts[1]} behind); push or pull until they match";
    }
}
