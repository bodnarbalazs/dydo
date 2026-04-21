namespace DynaDocs.Utils;

using DynaDocs.Services;

public static partial class PathUtils
{
    /// <summary>
    /// Find the dydo root directory by looking for dydo.json.
    /// Returns the dydo/ folder path if found, null otherwise.
    /// </summary>
    public static string? FindDydoRoot(string? startPath = null)
    {
        var configService = new ConfigService();
        var projectRoot = configService.GetProjectRoot(startPath);
        if (projectRoot == null) return null;
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

    /// <summary>
    /// Find the main project root even when called from inside a worktree.
    /// Resolves via the <c>dydo/_system/.local/worktrees/</c> marker; falls back
    /// to <see cref="FindProjectRoot"/> when not inside a worktree.
    /// Used by the watchdog so its PID file and CWD never land inside a worktree.
    /// </summary>
    public static string? FindMainProjectRoot(string? startPath = null)
    {
        var start = startPath ?? Environment.CurrentDirectory;
        var worktreeMainRoot = GetMainProjectRoot(start);
        if (worktreeMainRoot != null) return worktreeMainRoot;
        return FindProjectRoot(start);
    }

    /// <summary>
    /// Returns the main project's dydo folder (the one with agents/, _system/, ...),
    /// resolved from the main project root rather than the nearest one.
    /// </summary>
    public static string? FindMainDydoRoot(string? startPath = null)
    {
        var projectRoot = FindMainProjectRoot(startPath);
        if (projectRoot == null) return null;
        var configService = new ConfigService();
        return configService.GetDydoRoot(projectRoot);
    }

    public static string ResolvePath(string sourcePath, string relativePath)
    {
        var sourceDir = Path.GetDirectoryName(sourcePath) ?? "";
        var resolved = Path.GetFullPath(Path.Combine(sourceDir, relativePath));
        return NormalizePath(resolved);
    }

    public static string? FindDocsFolder(string startPath)
    {
        var configService = new ConfigService();
        var projectRoot = configService.GetProjectRoot(startPath);

        if (projectRoot != null)
        {
            var dydoRoot = configService.GetDydoRoot(startPath);
            if (Directory.Exists(dydoRoot) && File.Exists(Path.Combine(dydoRoot, "index.md")))
                return dydoRoot;
        }

        return FindLegacyDocsFolder(startPath);
    }

    private static string? FindLegacyDocsFolder(string startPath)
    {
        var docsPath = Path.Combine(startPath, "docs");
        if (!Directory.Exists(docsPath)) return null;

        var indexPath = Directory.GetFiles(docsPath, "index.md", SearchOption.TopDirectoryOnly)
            .FirstOrDefault();
        if (indexPath != null) return docsPath;

        foreach (var subDir in Directory.GetDirectories(docsPath))
        {
            indexPath = Directory.GetFiles(subDir, "index.md", SearchOption.TopDirectoryOnly)
                .FirstOrDefault();
            if (indexPath != null) return subDir;
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
}
