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
    private List<string> _whitelistPatterns = [];
    private List<Regex> _whitelistCompiled = [];
    private string? _basePath;
    private readonly IConfigService _configService;

    public IReadOnlyList<string> Patterns => _patterns.AsReadOnly();
    public IReadOnlyList<string> WhitelistPatterns => _whitelistPatterns.AsReadOnly();

    public OffLimitsService(IConfigService? configService = null)
    {
        _configService = configService ?? new ConfigService();
    }

    public void LoadPatterns(string? basePath = null)
    {
        _basePath = basePath ?? Environment.CurrentDirectory;
        _patterns.Clear();
        _compiledPatterns.Clear();
        _whitelistPatterns.Clear();
        _whitelistCompiled.Clear();

        var offLimitsPath = GetOffLimitsPath(_basePath);
        if (offLimitsPath == null || !File.Exists(offLimitsPath))
            return;

        var content = File.ReadAllText(offLimitsPath);
        var (patterns, whitelist) = ParsePatternsWithWhitelist(content);

        _patterns = patterns;
        _compiledPatterns = _patterns.Select(CompileGlobToRegex).ToList();
        _whitelistPatterns = whitelist;
        _whitelistCompiled = _whitelistPatterns.Select(CompileGlobToRegex).ToList();
    }

    public bool OffLimitsFileExists(string? basePath = null)
    {
        var path = GetOffLimitsPath(basePath ?? _basePath ?? Environment.CurrentDirectory);
        return path != null && File.Exists(path);
    }

    public string? IsPathOffLimits(string path)
    {
        var normalizedPath = NormalizePath(path);

        // Check whitelist first - if whitelisted, allow
        if (IsWhitelisted(normalizedPath))
            return null;

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

    private bool IsWhitelisted(string normalizedPath)
    {
        // Check full path
        foreach (var regex in _whitelistCompiled)
        {
            if (regex.IsMatch(normalizedPath))
                return true;
        }

        // Check filename for simple patterns
        var fileName = Path.GetFileName(normalizedPath);
        if (!string.IsNullOrEmpty(fileName) && fileName != normalizedPath)
        {
            for (int i = 0; i < _whitelistPatterns.Count; i++)
            {
                if (!_whitelistPatterns[i].Contains('/') && !_whitelistPatterns[i].Contains("**"))
                {
                    if (_whitelistCompiled[i].IsMatch(fileName))
                        return true;
                }
            }
        }

        return false;
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
    /// Parse patterns from markdown content, supporting both off-limits and whitelist sections.
    /// Supports patterns in code blocks (```...```) or list items (- or *).
    /// Section is determined by headers containing "whitelist"/"exception" or "off-limits".
    /// </summary>
    private static (List<string> patterns, List<string> whitelist) ParsePatternsWithWhitelist(string content)
    {
        var patterns = new List<string>();
        var whitelist = new List<string>();
        var lines = content.Split('\n');
        var inCodeBlock = false;
        var currentSection = "off-limits"; // Default section

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            // Detect section headers
            if (line.StartsWith("## ") || line.StartsWith("# "))
            {
                var headerLower = line.ToLowerInvariant();
                if (headerLower.Contains("whitelist") || headerLower.Contains("exception"))
                    currentSection = "whitelist";
                else if (headerLower.Contains("off-limits") || headerLower.Contains("off limits"))
                    currentSection = "off-limits";
                continue;
            }

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
            {
                if (currentSection == "whitelist")
                    whitelist.Add(patternCandidate);
                else
                    patterns.Add(patternCandidate);
            }
        }

        return (patterns, whitelist);
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

    /// <summary>
    /// Validate the format of the off-limits file.
    /// Returns a list of validation issues found.
    /// </summary>
    public IEnumerable<FormatValidationIssue> ValidateFormat(string? basePath = null)
    {
        var path = GetOffLimitsPath(basePath ?? _basePath ?? Environment.CurrentDirectory);
        if (path == null || !File.Exists(path))
            yield break;

        var content = File.ReadAllText(path);
        var lines = content.Split('\n');
        var codeBlockCount = 0;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith("```"))
                codeBlockCount++;
        }

        // Check for unclosed code blocks
        if (codeBlockCount % 2 != 0)
        {
            yield return new FormatValidationIssue(
                "Unclosed code block (``` without closing ```)",
                IsError: true
            );
        }

        // Check for no patterns
        if (_patterns.Count == 0 && _whitelistPatterns.Count == 0)
        {
            yield return new FormatValidationIssue(
                "No off-limits patterns defined",
                IsError: false
            );
        }

        // Check for duplicate patterns
        var seenPatterns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pattern in _patterns.Concat(_whitelistPatterns))
        {
            if (!seenPatterns.Add(pattern))
            {
                yield return new FormatValidationIssue(
                    $"Duplicate pattern: {pattern}",
                    IsError: false
                );
            }
        }

        // Check for overly broad whitelist patterns
        var broadPatterns = new[] { "*", "**", "**/*", "**/.*" };
        foreach (var pattern in _whitelistPatterns)
        {
            if (broadPatterns.Contains(pattern) || IsBroadWhitelistPattern(pattern))
            {
                yield return new FormatValidationIssue(
                    $"Whitelist pattern '{pattern}' is too broad and may defeat security",
                    IsError: false
                );
            }
        }
    }

    /// <summary>
    /// Check if a whitelist pattern is too broad.
    /// Patterns like "**/secrets*" or "**/*.json" are too broad for whitelist.
    /// </summary>
    private static bool IsBroadWhitelistPattern(string pattern)
    {
        if (!pattern.StartsWith("**/"))
            return false;

        var afterPrefix = pattern[3..];
        // If everything after **/ is wildcards + extension, it's too broad
        return afterPrefix.StartsWith('*') || afterPrefix.StartsWith('.');
    }
}
