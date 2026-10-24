namespace DynaDocs.Services;

using System.Text.Json;
using DynaDocs.Serialization;

/// <summary>
/// What a worktree holds that removal would lose: tracked changes against HEAD (staged or not),
/// untracked files git does not ignore, and dydo's ignored evidence under <c>&lt;dydo root&gt;/agents/</c>
/// for every root in <see cref="EvidenceRoots"/>. Build output an agent left there (a <c>bin</c>, <c>obj</c>,
/// <c>node_modules</c> or <c>.vs</c> folder below <c>agents/</c>, in any case) is regenerable: it is not
/// evidence and only counted in <see cref="SkippedBuildOutput"/>.
/// Paths are worktree-relative with forward slashes. A link among those paths, or one a path sits under,
/// is never read through: it goes to <see cref="Links"/> once, with its target, so the archive records it
/// without copying anything out of the tree. Ignored links outside the evidence paths, such as skill
/// links, are regenerable and never listed.
/// </summary>
internal sealed record ArchivePlan(
    IReadOnlyList<string> Tracked,
    IReadOnlyList<string> Untracked,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<ArchiveLink> Links,
    int SkippedBuildOutput)
{
    private static readonly string[] BuildFolders = ["bin", "obj", "node_modules", ".vs"];

    public bool IsEmpty => Tracked.Count + Untracked.Count + Evidence.Count + Links.Count == 0;

    /// <summary>Reads the plan from git; null with <paramref name="error"/> set when git cannot answer.</summary>
    public static ArchivePlan? Collect(string worktree, IReadOnlyList<string> dydoRoots, out string? error)
    {
        // git diff HEAD would rewrite the index even with optional locks off; status refreshes in memory only.
        var tracked = GitRunner.Run(worktree, "status", "--porcelain=v1", "-z", "--no-renames", "--untracked-files=no");
        var untracked = GitRunner.Run(worktree, "ls-files", "--others", "--exclude-standard", "-z");
        var evidence = GitRunner.Run(worktree, ["ls-files", "--others", "--ignored", "--exclude-standard", "-z",
            "--", .. dydoRoots.Select(root => $"{root}/agents/")]);
        var failed = new[] { tracked, untracked, evidence }.FirstOrDefault(result => !result.Ok);
        if (failed != null)
        {
            error = $"git could not list its changes: {failed.Error}";
            return null;
        }

        error = null;
        var links = new List<ArchiveLink>();
        var kept = evidence.Entries.Where(path => !IsBuildOutput(path, dydoRoots)).ToArray();
        return new ArchivePlan(
            [.. tracked.Entries.Select(entry => entry[3..])],
            WithoutLinks(worktree, untracked.Entries, links),
            WithoutLinks(worktree, kept, links),
            links,
            evidence.Entries.Length - kept.Length);
    }

    // A path below <root>/agents/ with a build folder among its folders.
    private static bool IsBuildOutput(string path, IReadOnlyList<string> dydoRoots) =>
        dydoRoots.Select(root => $"{root}/agents/")
            .Where(agents => path.StartsWith(agents, StringComparison.OrdinalIgnoreCase))
            .Any(agents => path[agents.Length..].Split('/').SkipLast(1)
                .Any(folder => BuildFolders.Contains(folder, StringComparer.OrdinalIgnoreCase)));

    /// <summary>
    /// The dydo roots whose <c>agents/</c> may hold evidence: the one <c>dydo.json</c> names at
    /// <paramref name="head"/>, the one the working tree's <c>dydo.json</c> names, and the default,
    /// de-duplicated. A root changed in the tree leaves the old one's ignored files behind, so every root
    /// is searched. Null with <paramref name="reason"/> set when a <c>dydo.json</c> exists but cannot be
    /// read or parsed: which evidence the tree holds is then unknown. Reads only; HEAD's copy comes from
    /// <c>git show</c>, which leaves the index alone.
    /// </summary>
    public static IReadOnlyList<string>? EvidenceRoots(string worktree, string? head, out string? reason)
    {
        var texts = ConfigTexts(worktree, head, out reason);
        if (reason != null)
            return null;
        var roots = new List<string> { ConfigService.DefaultRoot };
        foreach (var (source, json) in texts)
        {
            var root = RootIn(json, source, out reason);
            if (reason != null)
                return null;
            if (root != null && !roots.Contains(root))
                roots.Add(root);
        }

        return roots;
    }

    // HEAD's dydo.json and the working tree's, each where it exists; reason is set when one exists but
    // cannot be read.
    private static List<(string Source, string Json)> ConfigTexts(string worktree, string? head, out string? reason)
    {
        reason = null;
        var texts = new List<(string Source, string Json)>();
        var name = ConfigService.ConfigFileName;
        var atHead = head == null ? null : ReadAt(worktree, head, name);
        if (atHead is { Ok: false })
        {
            reason = $"git could not read {name} at HEAD ({atHead.Error}), so the evidence folders to archive are unknown";
            return texts;
        }

        if (atHead != null)
            texts.Add(($"{name} at HEAD", atHead.Text));
        var path = Path.Combine(worktree, name);
        try
        {
            if (File.Exists(path))
                texts.Add(($"the working tree's {name}", File.ReadAllText(path)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            reason = $"the working tree's {name} could not be read ({ex.Message}), so the evidence folders to archive are unknown";
        }

        return texts;
    }

    // The file at commit, or null when the commit has none. Both reads leave the index alone.
    private static GitResult? ReadAt(string worktree, string commit, string name)
    {
        var listed = GitRunner.Run(worktree, "ls-tree", "-z", commit, "--", name);
        if (!listed.Ok)
            return listed;
        return listed.Output.Length == 0 ? null : GitRunner.Run(worktree, "show", $"{commit}:{name}");
    }

    // The structure root a dydo.json names, with forward slashes and no edge slashes; null when it names none.
    private static string? RootIn(string json, string source, out string? reason)
    {
        reason = null;
        try
        {
            var config = JsonSerializer.Deserialize(json.TrimStart('\uFEFF'), DydoConfigJsonContext.Default.DydoConfig)
                ?? throw new JsonException("it holds no object");
            var root = config.Structure?.Root?.Replace('\\', '/').Trim('/');
            return string.IsNullOrEmpty(root) ? null : root;
        }
        catch (JsonException ex)
        {
            reason = $"{source} cannot be parsed ({ex.Message}), so the evidence folders to archive are unknown";
            return null;
        }
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
