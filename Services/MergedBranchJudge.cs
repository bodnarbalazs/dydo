namespace DynaDocs.Services;

/// <summary>
/// Decides whether a removed worktree's local branch may go: when its tip is reachable from
/// <c>origin/&lt;default&gt;</c>, or when merging the tip into it changes nothing, as after a squash or rebase
/// merge (<c>git merge-tree --write-tree</c> prints exactly origin's tree). That merge runs in a scratch bare
/// repository that borrows the real object store, without the repository's config or attributes or any inherited
/// <c>GIT_*</c> variable, so no merge driver, builtin or configured, decides and no verdict writes an object into the repository; a git without
/// <c>merge-tree --write-tree</c>, or one that fails, keeps the branch. A real run fetches with <c>--prune</c> once, before the first verdict, and
/// keeps every branch when the fetch fails; a dry run fetches nothing and judges the last fetch. The
/// default branch is the one origin advertises now (<c>git ls-remote --symref origin HEAD</c>), so a
/// stale local <c>origin/HEAD</c> never decides; only when origin cannot answer does the local
/// <c>origin/HEAD</c>, then <c>origin/main</c> or <c>origin/master</c>, stand in.
/// </summary>
internal sealed class MergedBranchJudge(string repository, bool dryRun)
{
    private string? _target;
    private string? _unavailable;
    private bool _prepared;

    /// <summary>The verdict and its reason, such as <c>merged into origin/master</c>.</summary>
    public (bool Delete, string Reason) Judge(string tip)
    {
        Prepare();
        if (_target == null)
            return (false, _unavailable!);

        if (GitRunner.Run(repository, "merge-base", "--is-ancestor", tip, _target).Ok)
            return (true, $"merged into {_target}{Staleness}");
        var unavailable = MergeChangesNothing(tip, out var contentMerged);
        if (unavailable != null)
            return (false, $"not merged into {_target}{Staleness}; the squash check failed: {unavailable}");
        return contentMerged
            ? (true, $"no changes left to merge into {_target}{Staleness}")
            : (false, $"not merged into {_target}{Staleness}");
    }

    // Whether merging tip (a full hash) into the target yields the target's own tree; the reason when git cannot tell.
    private string? MergeChangesNothing(string tip, out bool unchanged)
    {
        unchanged = false;
        var target = GitRunner.Run(repository, "rev-parse", "--verify", "--quiet", $"{_target}^{{commit}}");
        var targetTree = GitRunner.Run(repository, "rev-parse", "--verify", "--quiet", $"{_target}^{{tree}}");
        var store = GitRunner.Run(repository, "rev-parse", "--path-format=absolute", "--git-path", "objects");
        var failed = new[] { target, targetTree, store }.FirstOrDefault(result => !result.Ok);
        if (failed != null)
            return failed.Error;

        var scratch = Path.Combine(Path.GetTempPath(), "dydo-merge-tree-" + Guid.NewGuid().ToString("N"));
        try
        {
            var merge = IsolatedMergeTree(scratch, store.Text.Trim(), target.Text.Trim(), tip);
            // Exit 1 is a conflicted merge: the tip holds changes origin does not.
            if (merge.ExitCode is not (0 or 1))
                return string.IsNullOrEmpty(merge.Error) ? $"git exited {merge.ExitCode}" : merge.Error;
            // A clean merge with --no-messages prints only the merged tree.
            unchanged = merge.ExitCode == 0 && merge.Text.Trim() == targetTree.Text.Trim();
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"its scratch repository {scratch} failed: {ex.Message}";
        }
        finally
        {
            DeleteScratch(scratch);
        }
    }

    // git merge-tree in a scratch bare repository that borrows the real object store as an alternate, so
    // the objects it writes go with the scratch directory, and no config or attributes but its own reach it:
    // a merge driver, even a builtin one such as union that needs no config, cannot resolve the tip's changes away.
    // Every source git consults, and how it is shut:
    // - environment: every inherited GIT_* is dropped (GIT_CONFIG_PARAMETERS and GIT_CONFIG_COUNT command-line
    //   config, GIT_CONFIG, GIT_CONFIG_SYSTEM, GIT_ATTR_SOURCE, GIT_TEMPLATE_DIR, GIT_DIR and the like); no -c is passed.
    // - system config ($(prefix)/etc/gitconfig, and Git for Windows' ProgramData config): GIT_CONFIG_NOSYSTEM=1.
    // - system attributes ($(prefix)/etc/gitattributes): GIT_ATTR_NOSYSTEM=1.
    // - global config (~/.gitconfig, $XDG_CONFIG_HOME/git/config): GIT_CONFIG_GLOBAL names an empty file in scratch.
    // - core.attributesFile: no config read sets it, and its default $XDG_CONFIG_HOME/git/attributes is in scratch,
    //   where no git directory exists.
    // - local config: only what git init writes into the scratch repository; worktree config is never read, as
    //   extensions.worktreeConfig is unset; includes come only from config files, and every one read is empty or init's.
    // - templates: --template= copies none, so no hooks, info/exclude or info/attributes land in $GIT_DIR/info.
    // - in-tree .gitattributes: the repository is bare with an unborn HEAD and no index, and attr.tree is unset,
    //   so no tree, index or worktree supplies attributes.
    private static GitResult IsolatedMergeTree(string scratch, string store, string target, string tip)
    {
        var bare = Path.Combine(scratch, "repo.git");
        Directory.CreateDirectory(scratch);
        var isolated = ScratchEnvironment(scratch);
        File.WriteAllText(isolated["GIT_CONFIG_GLOBAL"], "");
        var init = GitRunner.RunIsolated(scratch, isolated, "init", "--bare", "--quiet", "--template=", bare);
        if (!init.Ok)
            return init;
        // One path per line; quoted, a path holding a quote, a backslash or a newline survives intact.
        File.WriteAllText(Path.Combine(bare, "objects", "info", "alternates"), CQuoted(store) + "\n");
        isolated["GIT_DIR"] = bare;
        return GitRunner.RunIsolated(bare, isolated, "merge-tree", "--write-tree", "--no-messages", target, tip);
    }

    /// <summary>The variables the scratch git runs with, on top of an environment stripped of every <c>GIT_*</c>.</summary>
    internal static Dictionary<string, string> ScratchEnvironment(string scratch) => new()
    {
        ["GIT_CONFIG_NOSYSTEM"] = "1",
        ["GIT_ATTR_NOSYSTEM"] = "1",
        ["GIT_CONFIG_GLOBAL"] = Path.Combine(scratch, "config"),
        ["XDG_CONFIG_HOME"] = scratch
    };

    private static string CQuoted(string path) =>
        "\"" + path.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";

    // A scratch directory left behind holds only objects already judged; it never changes the verdict.
    private static void DeleteScratch(string scratch)
    {
        try
        {
            LinkSafeDelete.DeleteTree(scratch);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private string Staleness => dryRun ? " as of the last fetch" : "";

    private void Prepare()
    {
        if (_prepared)
            return;
        _prepared = true;

        if (!GitRunner.Run(repository, "remote", "get-url", "origin").Ok)
        {
            _unavailable = "no origin remote";
            return;
        }

        if (!dryRun)
        {
            var fetch = GitRunner.Run(repository, "fetch", "--prune", "origin");
            if (!fetch.Ok)
            {
                _unavailable = $"git fetch --prune origin failed: {fetch.Error}";
                return;
            }
        }

        _target = DefaultBranch();
        if (_target == null)
            _unavailable = "origin has no default branch (origin/HEAD, origin/main or origin/master)";
    }

    private string? DefaultBranch()
    {
        var advertised = GitRunner.Run(repository, "ls-remote", "--symref", "origin", "HEAD");
        if (advertised.Ok)
            return AdvertisedHead(advertised.Text);
        var head = GitRunner.Run(repository, "symbolic-ref", "--quiet", "--short", "refs/remotes/origin/HEAD");
        if (head.Ok)
            return head.Text.Trim();
        return new[] { "origin/main", "origin/master" }
            .FirstOrDefault(Exists);
    }

    // "ref: refs/heads/main<TAB>HEAD" names the default; origin answering without it has none.
    private string? AdvertisedHead(string listing)
    {
        const string prefix = "ref: refs/heads/";
        const string suffix = "\tHEAD";
        var line = listing.ReplaceLineEndings("\n").Split('\n')
            .FirstOrDefault(entry => entry.StartsWith(prefix, StringComparison.Ordinal) && entry.EndsWith(suffix, StringComparison.Ordinal));
        if (line == null)
            return null;
        var branch = "origin/" + line[prefix.Length..^suffix.Length];
        return Exists(branch) ? branch : null;
    }

    private bool Exists(string reference) =>
        GitRunner.Run(repository, "rev-parse", "--verify", "--quiet", reference).Ok;
}
