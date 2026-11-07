namespace DynaDocs.Commands;

using System.Text.RegularExpressions;
using DynaDocs.Services;

/// <summary>
/// The merge-route hard blocks (DYD-425, DR 061 decision 7): agents merge only through dydo merge,
/// into a feature/* branch, and the default branch is the user's. Every caller, top-level or
/// sub-agent, is refused a raw PR merge (<see cref="PrCallScanner.FindMergeRoutes"/>; gh's base is not
/// readable offline, so every base), a default-branch change, and a git push that updates
/// <c>master</c>, <c>main</c> (any case) or the name the local <c>origin/HEAD</c> points at. A push
/// names its branch, or HEAD (the current branch), or none (the current branch's upstream, else the
/// current branch); git state is read offline in the call's directory, the PR check's, or the
/// directory git -C names there. State git cannot read resolves nothing, so it never blocks on its own.
/// A push of the current branch after the line's own branch change in the same worktree is also
/// judged on the branch that change leaves HEAD on, and refused when git or the line cannot say (DYD-887).
/// </summary>
public static partial class GuardCommand
{
    private static readonly string[] DefaultBranchNames = ["master", "main"];

    internal static int? BlockIfMergeRoute(string command, IReadOnlyList<string>? argv, string? cwd, ShellDialect shell, GuardEnv env)
    {
        var routes = argv != null ? PrCallScanner.FindMergeRoutesInArgv(argv) : PrCallScanner.FindMergeRoutes(command, shell);
        if (routes.FirstOrDefault(r => r.Kind == MergeRouteKind.Merge) is { } merge)
            return RefusePr("BLOCKED: Agents merge PRs only through dydo merge, never with gh or the GitHub API (DR 061).",
                $"  Detected: {merge.Form}",
                "  Run dydo merge <pr> --match-head-commit <reviewed sha40> --match-base-commit <feature-tip sha40>,",
                "  which merges a reviewed PR into its feature/* branch. Read the feature tip from the branch ref",
                "  (git ls-remote origin refs/heads/<base>), never from the PR's baseRefOid;",
                "  a merge into the default branch is the user's click.");
        if (routes.FirstOrDefault(r => r.Kind == MergeRouteKind.DefaultBranchChange) is { } change)
            return RefusePr("BLOCKED: Agents do not change the repository's default branch; the default branch is the user's (DR 061).",
                $"  Detected: {change.Form}",
                "  Integrate reviewed work into its feature/* branch with dydo merge instead.");

        var directory = CallDirectory(cwd, env);
        return routes.Select(push => RefusePush(push, directory)).FirstOrDefault(refusal => refusal != null);
    }

    // A push is refused when any branch HEAD may be on as it runs is unreadable, or makes it update a default branch.
    private static int? RefusePush(MergeRoute push, string directory)
    {
        var tree = Path.GetFullPath(Path.Combine(directory, push.Directory ?? ""));
        foreach (var current in CurrentBranches(push, tree, directory))
        {
            if (current == MergeRoute.UnreadableBranch)
                return RefusePr("BLOCKED: This git push follows a branch change in the same line, and the guard cannot read which branch that change leaves HEAD on, so the push may update the default branch, which is the user's (DR 061).",
                    $"  Detected: {push.Form}",
                    "  Push your own branch by name (git push origin <branch>), then integrate it into its feature/* branch",
                    "  with dydo merge, or with a PR from it into that branch.");
            if (PushedBranch(push.Destination!, current, tree) is { } branch && DefaultBranchMatch(branch, tree) is { } target)
                return RefusePr($"BLOCKED: This git push updates {target}, the default branch, and the default branch is the user's (DR 061).",
                    $"  Detected: {push.Form}",
                    "  Push your own branch and integrate it into its feature/* branch with dydo merge.");
        }
        return null;
    }

    /// <summary>The directory a shell call runs in: the host's per-call cwd or workdir, else the session's, else the project root.</summary>
    private static string CallDirectory(string? cwd, GuardEnv env) =>
        Path.GetFullPath(cwd ?? env.ProjectRoot ?? Environment.CurrentDirectory);

    // The branch name a push destination stands for in tree, given the branch HEAD is on, or null when git cannot say.
    private static string? PushedBranch(string destination, string? current, string tree)
    {
        if (destination is not (MergeRoute.CurrentBranch or MergeRoute.Upstream))
            return destination;
        if (current == null || destination == MergeRoute.CurrentBranch)
            return current;
        var upstream = GitText(tree, "config", "--get", $"branch.{current}.merge");
        return upstream?.StartsWith("refs/heads/", StringComparison.Ordinal) == true ? upstream["refs/heads/".Length..] : current;
    }

    // The branches HEAD may be on when a push of the current branch runs (DYD-887): HEAD's at hook time
    // and the branch of each change before the push in its worktree, or only the last such change when
    // nothing but && separates it from the push, which so runs only after it succeeds. A change in a
    // worktree git cannot settle is kept, and never taken as the last. A push naming its branch has one: none.
    private static IEnumerable<string?> CurrentBranches(MergeRoute push, string tree, string directory)
    {
        if (push.Destination is not (MergeRoute.CurrentBranch or MergeRoute.Upstream))
            return [null];
        var top = Toplevel(tree);
        List<(bool Same, bool Chained, string Branch)> changes = [];
        foreach (var change in push.Changes.Items)
        {
            var (same, home) = Worktree(change, top, directory);
            if (same != false && ChangedBranch(change, home) is { } branch)
                changes.Add((same == true, change.Chained, branch));
        }
        var last = changes.FindLastIndex(change => change.Same);
        return last >= 0 && changes[last].Chained
            ? changes.Skip(last).Select(change => (string?)change.Branch)
            : changes.Select(change => (string?)change.Branch).Prepend(GitText(tree, "symbolic-ref", "--quiet", "--short", "HEAD"));
    }

    // Whether a change runs in the push's worktree, whose top is top: true or false when git reads both,
    // else null (fail closed); and a tree of the change git reads, or null.
    private static (bool? Same, string? Home) Worktree(BranchChange change, string? top, string directory)
    {
        List<string> trees = [.. new[] { change.Directory, change.AsWritten ?? change.Directory }.Distinct()
            .Select(tree => Path.GetFullPath(Path.Combine(directory, tree ?? "")))];
        List<(string Tree, string Top)> read = [.. trees.Select(tree => (Tree: tree, Top: Toplevel(tree)))
            .Where(r => r.Top != null).Select(r => (r.Tree, r.Top!))];
        if (top != null && read.FirstOrDefault(r => SamePath(r.Top, top)).Tree is { } home)
            return (true, home);
        return top == null || read.Count < trees.Count ? (null, read.FirstOrDefault().Tree) : (false, null);
    }

    // The branch a change leaves HEAD on, read in its tree: one it creates; a name git has as a local
    // branch, or as a remote's branch it then creates locally; unreadable for a commit it detaches onto
    // (a SHA, a tag, a remote-tracking ref) or a tree git cannot read; null for paths, which move no HEAD.
    private static string? ChangedBranch(BranchChange change, string? home)
    {
        if (change.Creates || change.Branch == MergeRoute.UnreadableBranch)
            return change.Branch;
        if (home == null)
            return MergeRoute.UnreadableBranch;
        if (GitText(home, "rev-parse", "--verify", "--quiet", $"refs/heads/{change.Branch}") != null)
            return change.Branch;
        if (GitText(home, "rev-parse", "--verify", "--quiet", $"{change.Branch}^{{commit}}") != null)
            return MergeRoute.UnreadableBranch;
        var remoteBranches = GitText(home, "for-each-ref", "--format=%(refname:lstrip=3)", "refs/remotes/")?.Split('\n') ?? [];
        return remoteBranches.Any(name => name.Trim() == change.Branch) ? change.Branch : null;
    }

    private static string? Toplevel(string tree) => GitText(tree, "rev-parse", "--show-toplevel");

    private static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    // The default name branch is, or matches as a glob: master, main, or what origin/HEAD names in tree.
    private static string? DefaultBranchMatch(string branch, string tree)
    {
        var originHead = GitText(tree, "symbolic-ref", "--quiet", "--short", "refs/remotes/origin/HEAD");
        var names = originHead?.StartsWith("origin/", StringComparison.Ordinal) == true
            ? [.. DefaultBranchNames, originHead["origin/".Length..]]
            : DefaultBranchNames;
        var glob = new Regex($"^{Regex.Escape(branch).Replace(@"\*", ".*")}$", RegexOptions.IgnoreCase);
        return names.FirstOrDefault(glob.IsMatch);
    }

    private static string? GitText(string tree, params string[] args)
    {
        var result = GitRunner.Run(tree, args);
        var text = result.Text.Trim();
        return result.Ok && text.Length > 0 ? text : null;
    }
}
