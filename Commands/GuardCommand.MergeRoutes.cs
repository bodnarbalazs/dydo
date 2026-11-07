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
/// A push of the current branch after the line's own branch change is also judged on the branch that
/// change checks out, and refused when the line does not say which (DYD-887).
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
        foreach (var push in routes)
        {
            if (push.SwitchedTo == MergeRoute.UnreadableBranch)
                return RefusePr("BLOCKED: This git push follows a branch change in the same line, and the guard cannot read which branch that change checks out, so the push may update the default branch, which is the user's (DR 061).",
                    $"  Detected: {push.Form}",
                    "  Push your own branch by name (git push origin <branch>), then integrate it into its feature/* branch",
                    "  with dydo merge, or with a PR from it into that branch.");
            var tree = Path.GetFullPath(Path.Combine(directory, push.Directory ?? ""));
            if (PushedBranch(push, tree) is { } branch && DefaultBranchMatch(branch, tree) is { } target)
                return RefusePr($"BLOCKED: This git push updates {target}, the default branch, and the default branch is the user's (DR 061).",
                    $"  Detected: {push.Form}",
                    "  Push your own branch and integrate it into its feature/* branch with dydo merge.");
        }
        return null;
    }

    /// <summary>The directory a shell call runs in: the host's per-call cwd or workdir, else the session's, else the project root.</summary>
    private static string CallDirectory(string? cwd, GuardEnv env) =>
        Path.GetFullPath(cwd ?? env.ProjectRoot ?? Environment.CurrentDirectory);

    // The branch name a push destination stands for in tree, or null when git cannot say. The current
    // branch is the one the line's own branch change checks out, else HEAD's at hook time.
    private static string? PushedBranch(MergeRoute push, string tree)
    {
        var destination = push.Destination!;
        if (destination is not (MergeRoute.CurrentBranch or MergeRoute.Upstream))
            return destination;
        var current = push.SwitchedTo ?? GitText(tree, "symbolic-ref", "--quiet", "--short", "HEAD");
        if (current == null || destination == MergeRoute.CurrentBranch)
            return current;
        var upstream = GitText(tree, "config", "--get", $"branch.{current}.merge");
        return upstream?.StartsWith("refs/heads/", StringComparison.Ordinal) == true ? upstream["refs/heads/".Length..] : current;
    }

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
