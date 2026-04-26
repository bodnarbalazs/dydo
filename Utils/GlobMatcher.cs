namespace DynaDocs.Utils;

using System.Collections.Concurrent;
using System.Text.RegularExpressions;

/// <summary>
/// Shared glob-to-regex conversion for consistent pattern matching across the codebase.
/// Supports: ** (any path), * (within segment), ? (single char), **/ (optional prefix).
/// </summary>
public static class GlobMatcher
{
    private static readonly ConcurrentDictionary<string, Regex> _cache = new();

    public static bool IsMatch(string path, string pattern)
    {
        path = PathUtils.NormalizePath(path);
        var regex = CompileGlob(pattern);
        return regex.IsMatch(path);
    }

    public static Regex CompileGlob(string pattern)
    {
        pattern = PathUtils.NormalizePath(pattern);
        return _cache.GetOrAdd(pattern, BuildRegex);
    }

    private static Regex BuildRegex(string normalizedPattern)
    {
        var regexPattern = "^" + Regex.Escape(normalizedPattern)
            .Replace(@"\*\*/", "(.*/)?")
            .Replace(@"\*\*", ".*")
            .Replace(@"\*", "[^/]*")
            .Replace(@"\?", ".")
            + "$";

        return new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }
}
