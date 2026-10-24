namespace DynaDocs.Services;

/// <summary>One worktree from <c>git worktree list --porcelain</c>; the first listed is the main checkout.</summary>
internal sealed record WorktreeEntry(string Path, string? Head, string? Branch, bool Locked, bool IsMain)
{
    public string Label => Branch ?? $"detached-{Head?[..Math.Min(7, Head.Length)]}";

    public static List<WorktreeEntry> Parse(string porcelain)
    {
        var entries = new List<WorktreeEntry>();
        foreach (var block in porcelain.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            string? path = null, head = null, branch = null;
            var locked = false;
            foreach (var line in block.Split('\n'))
            {
                if (line.StartsWith("worktree ", StringComparison.Ordinal))
                    path = System.IO.Path.GetFullPath(line["worktree ".Length..]);
                else if (line.StartsWith("HEAD ", StringComparison.Ordinal))
                    head = line["HEAD ".Length..];
                else if (line.StartsWith("branch refs/heads/", StringComparison.Ordinal))
                    branch = line["branch refs/heads/".Length..];
                else if (line == "locked" || line.StartsWith("locked ", StringComparison.Ordinal))
                    locked = true;
            }

            if (path != null)
                entries.Add(new WorktreeEntry(path, head, branch, locked, entries.Count == 0));
        }

        return entries;
    }
}
