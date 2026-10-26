namespace DynaDocs.Commands;

using DynaDocs.Utils;

/// <summary>
/// New-file nudges (<c>"match": "new-file"</c>): the pattern is matched against the
/// project-relative, forward-slash path of each file a write tool is about to create.
/// A target that already exists as a file or directory never fires, so edits to existing
/// files pass. Shell-created files are not seen here; they stay with command nudges.
/// </summary>
public static partial class GuardCommand
{
    internal static int? CheckNewFileNudges(IReadOnlyList<string> targets, string? cwd, GuardEnv env)
    {
        var nudges = env.Config?.Nudges.Where(n => n.IsNewFileMatch).ToList() ?? [];
        if (nudges.Count == 0 || env.ProjectRoot == null)
            return null;

        var newPaths = NewProjectPaths(targets, cwd ?? Environment.CurrentDirectory, env.ProjectRoot);
        var warns = new List<WarnStep>();
        foreach (var nudge in nudges)
        {
            var regex = CompileNudgePattern(nudge.Pattern);
            var match = regex == null ? null : newPaths.Select(p => regex.Match(p)).FirstOrDefault(m => m.Success);
            if (match == null) continue;

            // Warns wait until no block refuses the write. A distinct marker prefix: a command nudge
            // with the same pattern hashes to ".nudge-<hash>", so neither lane spends the other's.
            if (IsWarn(nudge))
            {
                warns.Add(new WarnStep(nudge, match, $".nudge-new-file-{ComputeNudgeHash(nudge.Pattern)}",
                    "Retry the same write to proceed anyway."));
                continue;
            }

            var verdict = EnforceNudge(nudge, match);
            if (verdict != null) return verdict;
        }

        return EnforceWarns(warns, env);
    }

    private static List<string> NewProjectPaths(IEnumerable<string> targets, string cwd, string projectRoot)
    {
        var paths = new List<string>();
        foreach (var target in targets)
        {
            var absolute = PathUtils.CollapseRelativeSegments(Path.Combine(cwd, target));
            if (File.Exists(absolute) || Directory.Exists(absolute))
                continue;

            var relative = ProjectRelativePath(absolute, projectRoot);
            if (relative != null)
                paths.Add(relative);
        }

        return paths;
    }

    /// <summary>
    /// The forward-slash path of <paramref name="absolute"/> relative to the project root, or
    /// null when it lies outside (another drive, or a leading '..').
    /// </summary>
    internal static string? ProjectRelativePath(string absolute, string projectRoot)
    {
        var relative = Path.GetRelativePath(projectRoot, absolute).Replace('\\', '/');
        var outside = Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith("../", StringComparison.Ordinal);
        return outside ? null : relative;
    }
}
