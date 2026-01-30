namespace DynaDocs.Utils;

using System.Text.RegularExpressions;
using DynaDocs.Services;

public static partial class PathUtils
{
    private static readonly HashSet<string> ExemptFiles = ["CLAUDE.md", ".gitkeep"];

    public static bool IsKebabCase(string name)
    {
        if (ExemptFiles.Contains(name)) return true;

        var nameWithoutExt = Path.GetFileNameWithoutExtension(name);
        if (nameWithoutExt.StartsWith('_')) nameWithoutExt = nameWithoutExt[1..];

        if (string.IsNullOrEmpty(nameWithoutExt)) return false;

        return KebabCaseRegex().IsMatch(nameWithoutExt);
    }

    /// <summary>
    /// Find the dydo root directory by looking for dydo.json.
    /// Returns the dydo/ folder path if found, null otherwise.
    /// </summary>
    public static string? FindDydoRoot(string? startPath = null)
    {
        var configService = new ConfigService();
        var projectRoot = configService.GetProjectRoot(startPath);

        if (projectRoot == null)
            return null;

        return configService.GetDydoRoot(startPath);
    }

    /// <summary>
    /// Find the project root directory (where dydo.json lives).
    /// </summary>
    public static string? FindProjectRoot(string? startPath = null)
    {
        var configService = new ConfigService();
        return configService.GetProjectRoot(startPath);
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

    public static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
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

    public static string ResolvePath(string sourcePath, string relativePath)
    {
        var sourceDir = Path.GetDirectoryName(sourcePath) ?? "";
        var resolved = Path.GetFullPath(Path.Combine(sourceDir, relativePath));
        return NormalizePath(resolved);
    }

    public static string? FindDocsFolder(string startPath)
    {
        // First try the new structure: look for dydo.json and use its root
        var configService = new ConfigService();
        var projectRoot = configService.GetProjectRoot(startPath);

        if (projectRoot != null)
        {
            var dydoRoot = configService.GetDydoRoot(startPath);
            if (Directory.Exists(dydoRoot))
            {
                var indexPath = Path.Combine(dydoRoot, "index.md");
                if (File.Exists(indexPath))
                    return dydoRoot;
            }
        }

        // Fall back to legacy structure: docs/ folder
        var docsPath = Path.Combine(startPath, "docs");
        if (Directory.Exists(docsPath))
        {
            var indexPath = Directory.GetFiles(docsPath, "index.md", SearchOption.TopDirectoryOnly)
                .FirstOrDefault();
            if (indexPath != null) return docsPath;

            foreach (var subDir in Directory.GetDirectories(docsPath))
            {
                indexPath = Directory.GetFiles(subDir, "index.md", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault();
                if (indexPath != null) return subDir;
            }
        }

        return null;
    }

    public static string GetRelativePath(string fromPath, string toPath)
    {
        var fromDir = Path.GetDirectoryName(fromPath) ?? "";
        var fromParts = fromDir.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        var toParts = toPath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);

        var commonLength = 0;
        for (int i = 0; i < Math.Min(fromParts.Length, toParts.Length - 1); i++)
        {
            if (fromParts[i].Equals(toParts[i], StringComparison.OrdinalIgnoreCase))
                commonLength++;
            else
                break;
        }

        var upCount = fromParts.Length - commonLength;
        var relativeParts = new List<string>();

        for (int i = 0; i < upCount; i++)
            relativeParts.Add("..");

        for (int i = commonLength; i < toParts.Length; i++)
            relativeParts.Add(toParts[i]);

        if (relativeParts.Count == 0 || !relativeParts[0].StartsWith(".."))
            relativeParts.Insert(0, ".");

        return string.Join("/", relativeParts);
    }

    [GeneratedRegex(@"^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex KebabCaseRegex();

    [GeneratedRegex(@"([a-z])([A-Z])")]
    private static partial Regex PascalCaseRegex();

    [GeneratedRegex(@"-+")]
    private static partial Regex MultipleHyphensRegex();
}
