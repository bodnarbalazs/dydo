namespace DynaDocs.Services;

/// <summary>
/// What a worktree holds that removal would lose: tracked changes against HEAD (staged or not),
/// untracked files git does not ignore, and dydo's ignored evidence under <c>&lt;dydo root&gt;/agents/</c>.
/// Paths are worktree-relative with forward slashes. A path that is a link, or sits under one, is listed in
/// <see cref="Links"/> and never copied, so archiving never reads through a link out of the tree.
/// </summary>
internal sealed record ArchivePlan(
    IReadOnlyList<string> Tracked,
    IReadOnlyList<string> Untracked,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<string> Links)
{
    public bool IsEmpty => Tracked.Count + Untracked.Count + Evidence.Count + Links.Count == 0;

    /// <summary>Reads the plan from git; null with <paramref name="error"/> set when git cannot answer.</summary>
    public static ArchivePlan? Collect(string worktree, string dydoRoot, out string? error)
    {
        var tracked = GitRunner.Run(worktree, "diff", "HEAD", "--name-only", "-z");
        var untracked = GitRunner.Run(worktree, "ls-files", "--others", "--exclude-standard", "-z");
        var evidence = GitRunner.Run(worktree, "ls-files", "--others", "--ignored", "--exclude-standard", "-z",
            "--", $"{dydoRoot}/agents/");
        var failed = new[] { tracked, untracked, evidence }.FirstOrDefault(result => !result.Ok);
        if (failed != null)
        {
            error = $"git could not list its changes: {failed.Error}";
            return null;
        }

        error = null;
        var links = new List<string>();
        return new ArchivePlan(
            tracked.Entries,
            WithoutLinks(worktree, untracked.Entries, links),
            WithoutLinks(worktree, evidence.Entries, links),
            links);
    }

    private static List<string> WithoutLinks(string worktree, string[] paths, List<string> links)
    {
        var kept = new List<string>();
        foreach (var path in paths)
        {
            if (CrossesLink(worktree, path))
                links.Add(path);
            else
                kept.Add(path);
        }

        return kept;
    }

    private static bool CrossesLink(string worktree, string relativePath)
    {
        var current = worktree;
        foreach (var segment in relativePath.Split('/'))
        {
            current = Path.Combine(current, segment);
            // A missing path reports every attribute bit (-1), so existence is checked first.
            var info = new FileInfo(LinkSafeDelete.Extended(current));
            if ((int)info.Attributes != -1 && LinkSafeDelete.IsLink(info))
                return true;
        }

        return false;
    }
}
