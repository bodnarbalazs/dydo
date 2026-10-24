namespace DynaDocs.Services;

/// <summary>
/// What a worktree holds that removal would lose: tracked changes against HEAD (staged or not),
/// untracked files git does not ignore, and dydo's ignored evidence under <c>&lt;dydo root&gt;/agents/</c>.
/// Paths are worktree-relative with forward slashes. A link among those paths, or one a path sits under,
/// is never read through: it goes to <see cref="Links"/> once, with its target, so the archive records it
/// without copying anything out of the tree. Ignored links outside the evidence paths, such as skill
/// links, are regenerable and never listed.
/// </summary>
internal sealed record ArchivePlan(
    IReadOnlyList<string> Tracked,
    IReadOnlyList<string> Untracked,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<ArchiveLink> Links)
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
        var links = new List<ArchiveLink>();
        return new ArchivePlan(
            tracked.Entries,
            WithoutLinks(worktree, untracked.Entries, links),
            WithoutLinks(worktree, evidence.Entries, links),
            links);
    }

    private static List<string> WithoutLinks(string worktree, string[] paths, List<ArchiveLink> links)
    {
        var kept = new List<string>();
        foreach (var path in paths)
        {
            var link = LinkOnPath(worktree, path);
            if (link == null)
                kept.Add(path);
            else if (links.All(known => known.Path != link.Path))
                links.Add(link);
        }

        return kept;
    }

    // The first link along relativePath, read without following it.
    private static ArchiveLink? LinkOnPath(string worktree, string relativePath)
    {
        var current = worktree;
        var walked = new List<string>();
        foreach (var segment in relativePath.TrimEnd('/').Split('/'))
        {
            current = Path.Combine(current, segment);
            walked.Add(segment);
            // A missing path reports every attribute bit (-1), so existence is checked first.
            var info = new FileInfo(LinkSafeDelete.Extended(current));
            if ((int)info.Attributes != -1 && LinkSafeDelete.IsLink(info))
                return new ArchiveLink(string.Join('/', walked),
                    info.Attributes.HasFlag(FileAttributes.Directory) ? "directory" : "file",
                    info.LinkTarget ?? "(unreadable)");
        }

        return null;
    }
}

/// <summary>A link inside a worktree: its worktree-relative path, whether it links a directory or a file, and its target as stored.</summary>
internal sealed record ArchiveLink(string Path, string LinksTo, string Target);
