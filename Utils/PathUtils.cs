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
    /// Lexically collapses '.' and '..' segments without touching the filesystem,
    /// preserving the path's form (drive prefix, Windows UNC or device root, leading slash,
    /// relative-ness). A '..' that would escape a relative root is kept verbatim so the result can
    /// never be mistaken for a path under a trusted root; a rooted '..' stops at the root, which
    /// for '//server/share' is the share. Used to defeat traversal evasion
    /// ('.../memory/../../secret') before any path-based guard check.
    /// </summary>
    public static string CollapseRelativeSegments(string path)
    {
        var p = path.Replace('\\', '/');

        var prefix = RootPrefix(p);
        p = p[prefix.Length..];

        var rooted = p.StartsWith('/') || prefix.StartsWith("//", StringComparison.Ordinal);
        var stack = new List<string>();
        foreach (var seg in p.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (seg == ".")
                continue;
            if (seg == "..")
            {
                if (stack.Count > 0 && stack[^1] != "..")
                    stack.RemoveAt(stack.Count - 1);
                else if (!rooted)
                    stack.Add("..");
                // rooted '..' at the top is dropped - can't escape the root
            }
            else
            {
                stack.Add(seg);
            }
        }

        return prefix + (rooted ? "/" : "") + string.Join('/', stack);
    }

    /// <summary>
    /// The part of a slash-normalised path that '..' cannot climb above and that is not a
    /// segment: a drive ('C:'); on Windows also a UNC share ('//server/share') or a device root
    /// ('//?/C:', '//?/UNC/server/share', '//.'). Elsewhere a leading '//' reads as '/'.
    /// </summary>
    private static string RootPrefix(string p)
    {
        if (IsDrive(p))
            return p[..2];
        if (!OperatingSystem.IsWindows() || p.Length < 3 || !p.StartsWith("//", StringComparison.Ordinal) || p[2] == '/')
            return "";

        var isDevice = p.Length >= 4 && (p[2] is '?' or '.') && p[3] == '/';
        if (!isDevice)
            return p[..(2 + ServerShareLength(p[2..]))];

        if (IsDrive(p[4..]))
            return p[..6];
        if (p[4..].StartsWith("UNC/", StringComparison.OrdinalIgnoreCase) && ServerShareLength(p[8..]) > 0)
            return p[..(8 + ServerShareLength(p[8..]))];
        return p[..3];
    }

    private static bool IsDrive(string p) => p.Length >= 2 && char.IsLetter(p[0]) && p[1] == ':';

    // Length of a leading 'server/share' in s: up to the share's end, or all of s; 0 without a server.
    private static int ServerShareLength(string s)
    {
        if (s.Length == 0 || s[0] == '/')
            return 0;
        var server = s.IndexOf('/');
        var share = server < 0 ? -1 : s.IndexOf('/', server + 1);
        return share < 0 ? s.Length : share;
    }

    /// <summary>
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
    /// Resolves a read-only command's <c>--root</c> option. Without it, the current directory
    /// (the default lookup, unchanged). With it, the nearest <c>dydo.json</c> at or above that
    /// directory, announced as <c>Root:</c>; null, after reporting the error, when there is none.
    /// </summary>
    public static string? ResolveCommandRoot(string? root)
    {
        if (root == null)
            return Environment.CurrentDirectory;

        string? start = null;
        if (File.Exists(root))
            start = Path.GetDirectoryName(Path.GetFullPath(root));
        else if (Directory.Exists(root))
            start = Path.GetFullPath(root);

        var projectRoot = start == null ? null : FindProjectRoot(start);
        if (projectRoot == null)
        {
            ConsoleOutput.WriteError($"No dydo project found at {root}");
            return null;
        }

        Console.WriteLine($"Root: {projectRoot}");
        return projectRoot;
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

    [GeneratedRegex(@"^[a-z0-9]+([.\-][a-z0-9]+)*$")]
    private static partial Regex KebabCaseRegex();

    [GeneratedRegex(@"([a-z])([A-Z])")]
    private static partial Regex PascalCaseRegex();

    [GeneratedRegex(@"-+")]
    private static partial Regex MultipleHyphensRegex();
}
