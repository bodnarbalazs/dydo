namespace DynaDocs.Utils;

using System.Text.RegularExpressions;

public static partial class PathUtils
{
    /// <summary>
    /// Files exempt from kebab-case naming validation.
    /// - CLAUDE.md: Standard AI assistant entry point file (conventionally uppercase)
    /// - .gitkeep: Git placeholder file for empty directories (standard naming)
    /// </summary>
    private static readonly HashSet<string> ExemptFiles = ["CLAUDE.md", ".gitkeep"];

    public static bool IsKebabCase(string name)
    {
        if (ExemptFiles.Contains(name)) return true;

        var nameWithoutExt = Path.GetFileNameWithoutExtension(name);
        if (nameWithoutExt.StartsWith('_')) nameWithoutExt = nameWithoutExt[1..];

        if (string.IsNullOrEmpty(nameWithoutExt)) return false;

        return KebabCaseRegex().IsMatch(nameWithoutExt);
    }


    public static string ToKebabCase(string input)
    {
        var result = input.Replace(' ', '-');
        result = result.Replace('_', '-');
        result = PascalCaseRegex().Replace(result, "$1-$2");
        result = result.ToLowerInvariant();
        result = MultipleHyphensRegex().Replace(result, "-");
        result = result.Trim('-');
        return result;
    }

    /// <summary>
    /// Replace characters illegal in Windows filenames with dashes.
    /// Collapses runs of dashes, trims edges, and truncates to 100 chars.
    /// </summary>
    public static string SanitizeForFilename(string name)
    {
        var sanitized = name;
        foreach (var c in new[] { ':', '<', '>', '"', '|', '?', '*', '\\', '/' })
            sanitized = sanitized.Replace(c, '-');

        sanitized = MultipleHyphensRegex().Replace(sanitized, "-");
        sanitized = sanitized.Trim('-', ' ');

        if (sanitized.Length > 100)
            sanitized = sanitized[..100].TrimEnd('-');

        return sanitized;
    }

    public static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    /// <summary>
    /// Rewrites an absolute path inside a git worktree back to the equivalent main-project path.
    /// Detects the worktree marker <c>dydo/_system/.local/worktrees/</c>, identifies the worktree
    /// root via <c>dydo.json</c> presence, and returns <c>{mainRoot}/{projectContent}</c>.
    /// Returns the input unchanged if it's not a worktree path or the root can't be identified.
    /// </summary>
    public static string? NormalizeWorktreePath(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        var normalized = path.Replace('\\', '/');

        const string marker = "dydo/_system/.local/worktrees/";
        var markerIndex = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
            return path;

        var mainRoot = normalized[..markerIndex];
        var afterMarkerStart = markerIndex + marker.Length;
        var afterMarker = normalized[afterMarkerStart..];

        if (string.IsNullOrEmpty(afterMarker))
            return path;

        // Walk segments to find deepest worktree root (contains dydo.json)
        var bestSplitPos = -1;
        var pos = 0;

        while (pos < afterMarker.Length)
        {
            var slashPos = afterMarker.IndexOf('/', pos);
            if (slashPos < 0)
                break;

            var candidateDir = normalized[..(afterMarkerStart + slashPos)];
            if (File.Exists(candidateDir + "/dydo.json"))
                bestSplitPos = slashPos;

            pos = slashPos + 1;
        }

        if (bestSplitPos < 0)
            return path;

        var projectContent = afterMarker[(bestSplitPos + 1)..];

        if (string.IsNullOrEmpty(projectContent))
            return path;

        return mainRoot + projectContent;
    }

    /// <summary>
    /// Detects if a directory is inside a worktree and returns the main project root.
    /// Returns null if the directory is not inside a worktree.
    /// </summary>
    public static string? GetMainProjectRoot(string cwd)
    {
        var normalized = cwd.Replace('\\', '/');
        const string marker = "dydo/_system/.local/worktrees/";
        var markerIndex = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
            return null;
        return normalized[..markerIndex].TrimEnd('/');
    }

    /// <summary>
    /// Normalizes a path for pattern matching/comparison.
    /// Converts backslashes, removes leading './' and '/', but preserves case.
    /// Use for glob pattern matching, regex comparison with IgnoreCase.
    /// </summary>
    public static string NormalizeForPattern(string path)
    {
        var normalized = path.Replace('\\', '/');

        if (normalized.StartsWith("./"))
            normalized = normalized[2..];

        return normalized.TrimStart('/');
    }

    /// <summary>
    /// Normalizes a path for use as a dictionary key.
    /// Converts backslashes, removes leading './' and '/', lowercases.
    /// Use for dictionary keys where deterministic hashing is required.
    /// </summary>
    public static string NormalizeForKey(string path)
    {
        return NormalizeForPattern(path).ToLowerInvariant();
    }

    /// <summary>
    /// Ensures the dydo/_system/.local/ directory exists. Needed in worktrees where
    /// the gitignored .local/ directory is absent.
    /// </summary>
    public static void EnsureLocalDirExists(string dydoRoot)
    {
        var localDir = Path.Combine(dydoRoot, "_system", ".local");
        Directory.CreateDirectory(localDir);
    }

    /// <summary>
    /// Returns true if the given path (or CWD) is inside a dydo worktree.
    /// Detects the marker 'dydo/_system/.local/worktrees/' in the path.
    /// </summary>
    public static bool IsInsideWorktree(string? path = null)
    {
        var check = (path ?? Environment.CurrentDirectory).Replace('\\', '/');
        return check.Contains("dydo/_system/.local/worktrees/", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"^[a-z0-9]+([.\-][a-z0-9]+)*$")]
    private static partial Regex KebabCaseRegex();

    [GeneratedRegex(@"([a-z])([A-Z])")]
    private static partial Regex PascalCaseRegex();

    [GeneratedRegex(@"-+")]
    private static partial Regex MultipleHyphensRegex();
}
