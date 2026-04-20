namespace DynaDocs.Services;

using DynaDocs.Models;
using DynaDocs.Utils;

public enum SuspiciousCategory { TaskFile, Source, Test, Other }

public record JunkFile(string GitStatusLine, string Path, string MatchedPattern);
public record SuspiciousFile(string GitStatusLine, string Path, SuspiciousCategory Category);

public record ClassificationResult(
    IReadOnlyList<JunkFile> Junk,
    IReadOnlyList<SuspiciousFile> Suspicious);

/// <summary>
/// Classifies `git status --porcelain` output into junk (ignorable generated artifacts)
/// and suspicious (real uncommitted work that should block a merge).
/// </summary>
public static class WorktreeMergeSafety
{
    public static readonly IReadOnlyList<string> BuiltInIgnoreDefaults =
    [
        "dydo/_system/audit/**",
        "dydo/_system/.local/**",
        "**/__pycache__/**",
        "**/*.pyc",
        "**/bin/**",
        "**/obj/**",
        "**/node_modules/**",
        "**/*.log",
    ];

    public static IReadOnlyList<string> EffectiveIgnorePatterns(DydoConfig config)
    {
        var ms = config.Worktree.MergeSafety;
        var result = new List<string>();
        if (ms.IgnoreDefaults)
            result.AddRange(BuiltInIgnoreDefaults);
        result.AddRange(ms.Ignore);
        return result;
    }

    public static ClassificationResult Classify(string porcelainOutput, DydoConfig config)
    {
        var junk = new List<JunkFile>();
        var suspicious = new List<SuspiciousFile>();

        if (string.IsNullOrWhiteSpace(porcelainOutput))
            return new ClassificationResult(junk, suspicious);

        var ignorePatterns = EffectiveIgnorePatterns(config);
        var tasksPrefix = BuildTasksPrefix(config);

        foreach (var rawLine in porcelainOutput.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length < 3) continue;

            var path = ExtractPath(line);
            if (string.IsNullOrEmpty(path)) continue;

            var matched = FirstMatchingPattern(path, ignorePatterns);
            if (matched != null)
            {
                junk.Add(new JunkFile(line, path, matched));
                continue;
            }

            var category = Categorize(path, config, tasksPrefix);
            suspicious.Add(new SuspiciousFile(line, path, category));
        }

        return new ClassificationResult(junk, suspicious);
    }

    private static string BuildTasksPrefix(DydoConfig config)
    {
        var root = (config.Structure.Root ?? "dydo").Trim('/');
        var tasks = (config.Structure.Tasks ?? "project/tasks").Trim('/');
        return $"{root}/{tasks}/".Replace('\\', '/');
    }

    /// <summary>
    /// Extract the filesystem path from a porcelain line. Handles renames (picks the new path)
    /// and C-style quoted paths. Returns empty string if the line is malformed.
    /// </summary>
    internal static string ExtractPath(string line)
    {
        // Porcelain: "XY PATH" — 2-char status, space, path. Rename/copy: "XY OLD -> NEW".
        if (line.Length < 3 || line[2] != ' ') return string.Empty;
        var rest = line[3..];

        const string renameSep = " -> ";
        var sepIdx = rest.IndexOf(renameSep, StringComparison.Ordinal);
        var pathPart = sepIdx >= 0 ? rest[(sepIdx + renameSep.Length)..] : rest;

        pathPart = Unquote(pathPart);
        return PathUtils.NormalizePath(pathPart);
    }

    private static string Unquote(string path)
    {
        if (path.Length < 2 || path[0] != '"' || path[^1] != '"')
            return path;

        var inner = path[1..^1];
        var sb = new System.Text.StringBuilder(inner.Length);
        for (var i = 0; i < inner.Length; i++)
        {
            if (inner[i] != '\\' || i + 1 >= inner.Length)
            {
                sb.Append(inner[i]);
                continue;
            }
            var next = inner[++i];
            sb.Append(next switch
            {
                'n' => '\n',
                't' => '\t',
                'r' => '\r',
                '"' => '"',
                '\\' => '\\',
                _ => next,
            });
        }
        return sb.ToString();
    }

    private static string? FirstMatchingPattern(string path, IReadOnlyList<string> patterns)
    {
        foreach (var pattern in patterns)
        {
            if (GlobMatcher.IsMatch(path, pattern))
                return pattern;
        }
        return null;
    }

    private static SuspiciousCategory Categorize(string path, DydoConfig config, string tasksPrefix)
    {
        if (path.StartsWith(tasksPrefix, StringComparison.OrdinalIgnoreCase))
            return SuspiciousCategory.TaskFile;

        foreach (var pattern in config.Paths.Source)
        {
            if (GlobMatcher.IsMatch(path, pattern))
                return SuspiciousCategory.Source;
        }
        foreach (var pattern in config.Paths.Tests)
        {
            if (GlobMatcher.IsMatch(path, pattern))
                return SuspiciousCategory.Test;
        }
        return SuspiciousCategory.Other;
    }
}
