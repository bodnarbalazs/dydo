namespace DynaDocs.Services;

using System.Text.RegularExpressions;
using DynaDocs.Utils;

/// <summary>
/// <c>dydo pr-ready</c>: a PR shows what its branch holds on the remote, so the tree must be clean and
/// HEAD must equal its upstream before a PR is opened or readied. The comparison reads the local
/// remote-tracking ref; nothing is fetched. A PR body must name its ticket and carry the reviewer's
/// <c>PASS</c> block under <c>## Independent review</c>. The guard runs the tree half on the
/// PR-opening gh calls <see cref="PrCallScanner"/> finds.
/// </summary>
internal static partial class PrReadyCheck
{
    private const int ListedPaths = 5;

    public static int Run(string directory, string? bodyFile)
    {
        var problems = TreeProblems(directory) ?? [$"{directory} is not inside a git work tree"];
        if (bodyFile != null)
            problems = [.. problems, .. BodyFileProblems(bodyFile)];

        foreach (var problem in problems)
            Console.Error.WriteLine($"not ready: {problem}");
        if (problems.Count > 0)
            return ExitCodes.ValidationErrors;

        Console.WriteLine(bodyFile == null
            ? $"ready: {directory} is clean and HEAD matches its upstream"
            : $"ready: {directory} is clean, HEAD matches its upstream, and the body names its ticket and holds a PASS review");
        return ExitCodes.Success;
    }

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

    private static List<string> BodyFileProblems(string bodyFile)
    {
        if (!File.Exists(bodyFile))
            return [$"body file {bodyFile} not found"];

        var body = File.ReadAllText(bodyFile);
        List<string> problems = [];
        if (!TicketKeyRegex().IsMatch(body))
            problems.Add("the body names no ticket key, such as DYD-123");
        var review = ReviewSectionRegex().Match(body);
        if (!review.Success)
            problems.Add("the body has no '## Independent review' section");
        else if (!PassBlockRegex().IsMatch(review.Groups["section"].Value))
            problems.Add("the '## Independent review' section holds no reviewer 'PASS - rubric/type: ...' block (a hyphen, en dash or em dash after PASS)");
        return problems;
    }

    [GeneratedRegex(@"\b[A-Z][A-Z0-9]*-\d+\b")]
    private static partial Regex TicketKeyRegex();

    // The section runs to the next level-one or level-two heading.
    [GeneratedRegex(@"^##[ \t]+Independent review[ \t]*\r?$(?<section>[\s\S]*?)(?=^#{1,2}[ \t]|\z)",
        RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex ReviewSectionRegex();

    // The dash may be a hyphen, an en dash or an em dash; downstream PR bodies use all three.
    [GeneratedRegex(@"^[ \t]*PASS[ \t]*[-–—][ \t]*rubric/type:", RegexOptions.Multiline)]
    private static partial Regex PassBlockRegex();
}
