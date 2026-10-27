namespace DynaDocs.Commands;

using DynaDocs.Services;
using DynaDocs.Utils;

/// <summary>
/// The PR-ready rule (DYD-374, DYD-391): a PR shows the branch as the remote holds it, so a
/// PR-opening gh call (see <see cref="PrCallScanner"/>) is refused while the tree it shows is dirty
/// or HEAD differs from its upstream. That tree is the call's directory (the host's per-call cwd or
/// workdir, else the session's, else the project root), or for a named branch (<c>--head</c>,
/// <c>gh pr ready BRANCH</c>, a REST <c>head</c> field) every worktree with that branch checked out.
/// A call naming another repository is refused, since its trees are not here, and a call wrapped in a
/// shell or eval is refused unread. It stops an accident, not a deliberate bypass. A directory
/// outside git is not this rule's business.
/// </summary>
public static partial class GuardCommand
{
    internal static int? CheckPrReady(string command, string? cwd, GuardEnv env)
    {
        var calls = PrCallScanner.Find(command);
        if (calls.Count == 0)
            return null;

        if (calls.FirstOrDefault(c => c.Wrapper != null) is { } wrapped)
            return RefusePr($"BLOCKED: This command runs a PR call through {wrapped.Wrapper}, which the PR check does not read.",
                "  Wrapped PR calls are refused unread: run gh directly so the PR check can see it.");

        var directory = cwd ?? env.ProjectRoot ?? Environment.CurrentDirectory;
        var directoryProblems = PrReadyCheck.TreeProblems(directory);
        if (directoryProblems == null)
            return null;

        var origin = RepositoryName(GitRunner.Run(directory, "remote", "get-url", "origin").Text);
        if (calls.SelectMany(c => c.Repos).FirstOrDefault(r => RepositoryName(r) is not { } named || named != origin) is { } foreign)
            return RefusePr($"BLOCKED: This PR names another repository, {foreign}; {directory} is a checkout of {origin ?? "no origin"}.",
                "  Its branch is not in these trees, so run it from that repo's checkout.");

        var lines = TreeLines(calls, directory, directoryProblems, cwd == null);
        return lines.Count == 0
            ? null
            : RefusePr(["BLOCKED: This PR would not match the branch in your tree.", .. lines, "  Run dydo pr-ready there and fix what it reports."]);
    }

    private static List<string> TreeLines(List<PrCall> calls, string directory, List<string> directoryProblems, bool hostSentNoCwd)
    {
        List<string> lines = [];
        List<string> trees = [];
        var worktrees = calls.Any(c => c.Branch != null)
            ? WorktreeEntry.Parse(GitRunner.Run(directory, "worktree", "list", "--porcelain").Text)
            : [];
        foreach (var call in calls)
        {
            if (call.Branch == null)
            {
                trees.Add(directory);
                continue;
            }
            var holders = worktrees.Where(w => w.Branch == call.Branch).Select(w => w.Path).ToList();
            if (holders.Count == 0)
                lines.Add($"  {call.BranchForm} names branch {call.Branch}, which no worktree of this repository has checked out; check it out in a worktree and push it, then retry");
            trees.AddRange(holders);
        }

        foreach (var tree in trees.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var problems = tree == directory ? directoryProblems : PrReadyCheck.TreeProblems(tree) ?? [];
            if (problems.Count == 0)
                continue;
            lines.AddRange(problems.Select(p => $"  {p}"));
            lines.Add(tree == directory && hostSentNoCwd
                ? $"  Checked: {tree} (the project root; the host sent no cwd)"
                : $"  Checked: {tree}");
        }
        return lines;
    }

    private static int RefusePr(params string[] lines)
    {
        foreach (var line in lines)
            Console.Error.WriteLine(line);
        return ExitCodes.ToolError;
    }

    // OWNER/REPO in lower case from OWNER/REPO, HOST/OWNER/REPO or a remote URL; null when it names none.
    private static string? RepositoryName(string value)
    {
        var name = value.Trim().TrimEnd('/');
        if (name.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            name = name[..^4];
        var parts = name.Replace(':', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"{parts[^2]}/{parts[^1]}".ToLowerInvariant() : null;
    }
}
