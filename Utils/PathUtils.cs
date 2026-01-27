namespace DynaDocs.Utils;

using System.Text.RegularExpressions;

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

    public static string ResolvePath(string sourcePath, string relativePath)
    {
        var sourceDir = Path.GetDirectoryName(sourcePath) ?? "";
        var resolved = Path.GetFullPath(Path.Combine(sourceDir, relativePath));
        return NormalizePath(resolved);
    }

    public static string? FindDocsFolder(string startPath)
    {
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
