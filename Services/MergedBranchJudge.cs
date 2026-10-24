namespace DynaDocs.Services;

/// <summary>
/// Decides whether a removed worktree's local branch may go: only when its tip is reachable from
/// <c>origin/&lt;default&gt;</c>. A real run fetches with <c>--prune</c> once, before the first verdict, and
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

        var merged = GitRunner.Run(repository, "merge-base", "--is-ancestor", tip, _target).Ok;
        return merged
            ? (true, $"merged into {_target}{Staleness}")
            : (false, $"not merged into {_target}{Staleness}");
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
