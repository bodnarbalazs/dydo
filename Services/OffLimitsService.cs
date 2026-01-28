namespace DynaDocs.Services;

using System.Text.RegularExpressions;

/// <summary>
/// Service for managing globally off-limits file patterns.
/// Loads patterns from dydo/files-off-limits.md and provides path matching.
/// </summary>
public class OffLimitsService : IOffLimitsService
{
    public const string OffLimitsFileName = "files-off-limits.md";

    private List<string> _patterns = [];
    private List<Regex> _compiledPatterns = [];
    private string? _basePath;
    private readonly IConfigService _configService;

    public IReadOnlyList<string> Patterns => _patterns.AsReadOnly();

    public OffLimitsService(IConfigService? configService = null)
    {
        _configService = configService ?? new ConfigService();
    }

    public void LoadPatterns(string? basePath = null)
    {
        _basePath = basePath ?? Environment.CurrentDirectory;
        _patterns.Clear();
        _compiledPatterns.Clear();

        var offLimitsPath = GetOffLimitsPath(_basePath);
        if (offLimitsPath == null || !File.Exists(offLimitsPath))
            return;

        var content = File.ReadAllText(offLimitsPath);
        _patterns = ParsePatterns(content);
        _compiledPatterns = _patterns.Select(CompileGlobToRegex).ToList();
    }

    public bool OffLimitsFileExists(string? basePath = null)
    {
        var path = GetOffLimitsPath(basePath ?? _basePath ?? Environment.CurrentDirectory);
        return path != null && File.Exists(path);
    }

    public string? IsPathOffLimits(string path)
    {
        var normalizedPath = NormalizePath(path);

        for (int i = 0; i < _patterns.Count; i++)
        {
            if (_compiledPatterns[i].IsMatch(normalizedPath))
                return _patterns[i];
        }

        // Also check the filename alone for patterns like ".env"
        var fileName = Path.GetFileName(normalizedPath);
        if (!string.IsNullOrEmpty(fileName) && fileName != normalizedPath)
        {
            for (int i = 0; i < _patterns.Count; i++)
            {
                // Only match simple patterns (no path separators) against filename
                if (!_patterns[i].Contains('/') && !_patterns[i].Contains("**"))
                {
                    if (_compiledPatterns[i].IsMatch(fileName))
                        return _patterns[i];
                }
            }
        }

        return null;
    }

    public (bool IsBlocked, string? MatchedPath, string? MatchedPattern) CheckCommand(string command)
    {
        var paths = ExtractPathsFromCommand(command);
        foreach (var path in paths)
        {
            var pattern = IsPathOffLimits(path);
            if (pattern != null)
                return (true, path, pattern);
        }
        return (false, null, null);
    }

    public IEnumerable<string> ValidateLiteralPaths(string basePath)
    {
        var missing = new List<string>();
        foreach (var pattern in _patterns)
        {
            // Only check patterns that are literal paths (no wildcards)
            if (!ContainsWildcard(pattern))
            {
                var fullPath = Path.Combine(basePath, pattern);
                if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
                    missing.Add(pattern);
            }
        }
        return missing;
    }

    private string? GetOffLimitsPath(string basePath)
    {
        var projectRoot = _configService.GetProjectRoot(basePath);
        if (projectRoot == null)
            return null;

        var dydoRoot = _configService.GetDydoRoot(basePath);
        return Path.Combine(dydoRoot, OffLimitsFileName);
    }

    /// <summary>
    /// Parse patterns from markdown content.
    /// Supports patterns in code blocks (```...```) or list items (- or *).
    /// </summary>
    private static List<string> ParsePatterns(string content)
    {
        var patterns = new List<string>();
        var lines = content.Split('\n');
        var inCodeBlock = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            // Track code block state
            if (line.StartsWith("```"))
            {
                inCodeBlock = !inCodeBlock;
                continue;
            }

            string? patternCandidate = null;

            if (inCodeBlock)
            {
                patternCandidate = line;
            }
            else if (line.StartsWith("- ") || line.StartsWith("* "))
            {
                patternCandidate = line.TrimStart('-', '*', ' ');
            }

            if (patternCandidate == null)
                continue;

            // Skip comments and empty lines
            if (string.IsNullOrWhiteSpace(patternCandidate) || patternCandidate.StartsWith('#'))
                continue;

            // Handle inline comments
            var commentIndex = patternCandidate.IndexOf(" #", StringComparison.Ordinal);
            if (commentIndex > 0)
                patternCandidate = patternCandidate[..commentIndex].Trim();

            if (!string.IsNullOrWhiteSpace(patternCandidate))
                patterns.Add(patternCandidate);
        }

        return patterns;
    }

    /// <summary>
    /// Convert a glob pattern to a compiled regex.
    /// Supports: ** (any path), * (within segment), ? (single char)
    /// </summary>
    private static Regex CompileGlobToRegex(string pattern)
    {
        // Normalize to forward slashes for matching
        pattern = pattern.Replace('\\', '/');

        // Build regex pattern
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace(@"\*\*/", "(.*/)?")     // **/ matches any path or empty
            .Replace(@"\*\*", ".*")          // ** matches anything
            .Replace(@"\*", "[^/]*")         // * matches within a segment
            .Replace(@"\?", ".")             // ? matches single char
            + "$";

        return new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    /// <summary>
    /// Normalize a path for matching: forward slashes, no leading slash.
    /// </summary>
    private static string NormalizePath(string path)
    {
        // Convert to forward slashes
        var normalized = path.Replace('\\', '/');

        // Remove leading ./
        if (normalized.StartsWith("./"))
            normalized = normalized[2..];

        // Remove leading /
        normalized = normalized.TrimStart('/');

        return normalized;
    }

    private static bool ContainsWildcard(string pattern)
    {
        return pattern.Contains('*') || pattern.Contains('?');
    }

    /// <summary>
    /// Extract potential file paths from a shell command.
    /// This is a simplified extraction for quick checking.
    /// </summary>
    private static IEnumerable<string> ExtractPathsFromCommand(string command)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Match quoted strings
        var quotedMatches = Regex.Matches(command, @"['""]([^'""]+)['""]");
        foreach (Match m in quotedMatches)
        {
            var path = m.Groups[1].Value;
            if (LooksLikePath(path))
                paths.Add(path);
        }

        // Match paths after common read/write commands
        // Handle commands with flags (e.g., tail -f file.txt)
        var commandPatterns = new[]
        {
            @"(?:cat|head|tail|less|more|type|Get-Content|gc)\s+(?:-[^\s]+\s+)*([^\s|;&>-][^\s|;&>]*)",
            @"(?:>|>>)\s*([^\s|;&]+)",
            @"(?:<)\s*([^\s|;&]+)",
            @"(?:rm|del|Remove-Item|ri)\s+(?:-[^\s]+\s+)*([^\s|;&>-][^\s|;&>]*)",
            @"(?:cp|mv|copy|move|Copy-Item|Move-Item)\s+(?:-[^\s]+\s+)*([^\s|;&>-][^\s|;&>]*)",
            @"(?:chmod|chown|icacls)\s+(?:[^\s]+\s+)*([^\s|;&>]+)",
            @"(?:touch|truncate|tee)\s+([^\s|;&>]+)",
            @"echo\s+[^>]*>>\s*([^\s|;&]+)",  // echo with append
            @"echo\s+[^>]*>\s*([^\s|;&]+)",   // echo with write
        };

        foreach (var pattern in commandPatterns)
        {
            var matches = Regex.Matches(command, pattern, RegexOptions.IgnoreCase);
            foreach (Match m in matches)
            {
                var path = m.Groups[1].Value.Trim('"', '\'');
                if (LooksLikePath(path))
                    paths.Add(path);
            }
        }

        return paths;
    }

    /// <summary>
    /// Heuristic to determine if a string looks like a file path.
    /// </summary>
    private static bool LooksLikePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        // Skip flags and shell operators
        if (value.StartsWith('-') || value.StartsWith("&"))
            return false;

        // Skip common shell builtins/commands
        var builtins = new[] { "echo", "printf", "true", "false", "exit", "return" };
        if (builtins.Contains(value.ToLowerInvariant()))
            return false;

        // Has file extension or path separator - likely a path
        if (value.Contains('.') || value.Contains('/') || value.Contains('\\'))
            return true;

        // Single word that could be a filename
        if (!value.Contains(' '))
            return true;

        return false;
    }
}
