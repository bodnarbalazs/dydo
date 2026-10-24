namespace DynaDocs.Services;

/// <summary>
/// Work inside a worktree's initialized submodules that removing the tree would lose. The archive reads
/// only the superproject, and a linked worktree keeps its submodules' git directories inside its own
/// admin folder, so <c>dydo clean</c> keeps any tree with a submodule that has staged, unstaged or
/// untracked changes, or a commit on any local ref (HEAD, a branch, a tag, the stash) that none of that
/// submodule's remote refs reach. Clean, pushed submodules go with the tree.
/// </summary>
internal static class SubmoduleWork
{
    /// <summary>Why the tree must be kept, naming the submodule, or null when there is nothing to lose.</summary>
    public static string? Find(string worktree)
    {
        var status = GitRunner.Run(worktree, "submodule", "status", "--recursive");
        if (!status.Ok)
            return $"git could not check its submodules ({status.Error}); check them yourself, then remove it";
        foreach (var path in InitializedPaths(status.Text))
        {
            var problem = Problem(Path.Combine(worktree, path));
            if (problem != null)
                return $"submodule {path} {problem}; commit and push it, or clean it, first";
        }

        return null;
    }

    private static string? Problem(string submodule)
    {
        var changes = GitRunner.Run(submodule, "status", "--porcelain", "--untracked-files=normal", "--ignore-submodules=none");
        if (!changes.Ok)
            return $"could not be checked ({changes.Error})";
        if (changes.Text.Length > 0)
            return "has uncommitted or untracked changes";
        var unpushed = GitRunner.Run(submodule, "rev-list", "-n", "1", "HEAD", "--all", "--not", "--remotes");
        if (!unpushed.Ok)
            return $"could not be checked ({unpushed.Error})";
        return unpushed.Text.Trim().Length > 0 ? "has commits on no remote ref" : null;
    }

    // git submodule status prints "<state><sha> <path>[ (<describe>)]"; state '-' is not initialized.
    private static IEnumerable<string> InitializedPaths(string status)
    {
        foreach (var line in status.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith('-'))
                continue;
            var afterSha = line.IndexOf(' ');
            if (afterSha < 0)
                continue;
            var path = line[(afterSha + 1)..];
            var describe = path.LastIndexOf(" (", StringComparison.Ordinal);
            yield return describe > 0 && path.EndsWith(')') ? path[..describe] : path;
        }
    }
}
