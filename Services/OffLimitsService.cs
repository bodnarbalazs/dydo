namespace DynaDocs.Services;

using System.Text.RegularExpressions;
using DynaDocs.Utils;

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

    // Hardcoded off-limits patterns for system-critical files that must never be agent-writable,
    // regardless of what's in files-off-limits.md. Prevents self-escalation attacks.
    private static readonly (string Pattern, Regex Compiled)[] SystemOffLimits =
        [("dydo/agents/*/.guard-lift.json", CompileGlobToRegex("dydo/agents/*/.guard-lift.json"))];

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
        var normalizedPath = PathUtils.NormalizeForPattern(path);

        // Check whitelist first - if whitelisted, allow
        if (FindMatchingPattern(normalizedPath, _whitelistPatterns, _whitelistCompiled) != null)
            return null;

        // Hardcoded system patterns — always enforced, not whitelistable
        foreach (var (pattern, compiled) in SystemOffLimits)
        {
            if (compiled.IsMatch(normalizedPath))
                return pattern;
        }

        return FindMatchingPattern(normalizedPath, _patterns, _compiledPatterns);
    }

    private static string? FindMatchingPattern(string normalizedPath, List<string> patterns, List<Regex> compiled)
    {
        for (int i = 0; i < patterns.Count; i++)
        {
            if (compiled[i].IsMatch(normalizedPath))
                return patterns[i];
        }

        var fileName = Path.GetFileName(normalizedPath);
        if (!string.IsNullOrEmpty(fileName) && fileName != normalizedPath)
        {
            for (int i = 0; i < patterns.Count; i++)
            {
                if (!patterns[i].Contains('/') && !patterns[i].Contains("**"))
                {
                    if (compiled[i].IsMatch(fileName))
                        return patterns[i];
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
        var currentSection = "off-limits";

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            if (line.StartsWith("## ") || line.StartsWith("# "))
            {
                currentSection = ClassifySection(line, currentSection);
                continue;
            }

            if (line.StartsWith("```"))
            {
                inCodeBlock = !inCodeBlock;
                continue;
            }

            var candidate = ExtractPatternCandidate(line, inCodeBlock);
            if (candidate == null)
                continue;

            if (currentSection == "whitelist")
                whitelist.Add(candidate);
            else
                patterns.Add(candidate);
        }

        return (patterns, whitelist);
    }

    private static string ClassifySection(string headerLine, string currentSection)
    {
        var lower = headerLine.ToLowerInvariant();
        if (lower.Contains("whitelist") || lower.Contains("exception"))
            return "whitelist";
        if (lower.Contains("off-limits") || lower.Contains("off limits"))
            return "off-limits";
        return currentSection;
    }

    private static string? ExtractPatternCandidate(string line, bool inCodeBlock)
    {
        string? candidate = null;

        if (inCodeBlock)
            candidate = line;
        else if (line.StartsWith("- ") || line.StartsWith("* "))
            candidate = line.TrimStart('-', '*', ' ');

        if (candidate == null)
            return null;

        if (string.IsNullOrWhiteSpace(candidate) || candidate.StartsWith('#'))
            return null;

        if (!inCodeBlock && !LooksLikePattern(candidate))
            return null;

        var commentIndex = candidate.IndexOf(" #", StringComparison.Ordinal);
        if (commentIndex > 0)
            candidate = candidate[..commentIndex].Trim();

        return string.IsNullOrWhiteSpace(candidate) ? null : candidate;
    }

    private static Regex CompileGlobToRegex(string pattern) => GlobMatcher.CompileGlob(pattern);

    private static bool ContainsWildcard(string pattern)
    {
        return pattern.Contains('*') || pattern.Contains('?');
    }

    /// <summary>
    /// Check if text looks like a file pattern rather than descriptive text.
    /// Patterns typically contain: path separators, wildcards, dots (extensions), or start with special chars.
    /// </summary>
    private static bool LooksLikePattern(string text)
    {
        // If text contains multiple spaces, it's likely a descriptive sentence, not a pattern
        // e.g., "Glob patterns supported: `*` matches within directory"
        var spaceCount = text.Count(c => c == ' ');
        if (spaceCount >= 2)
            return false;

        // Patterns contain: path separators, wildcards, or look like filenames
        return text.Contains('/') ||
               text.Contains('\\') ||
               text.Contains('*') ||
               text.Contains('?') ||
               text.StartsWith('.') ||  // .env, .gitignore, etc.
               (text.Contains('.') && !text.Contains(' '));  // file.ext but not "a sentence."
    }

    /// <summary>
    /// Extract potential file paths from a shell command.
    /// This is a simplified extraction for quick checking.
    /// </summary>
    private static readonly string[] CommandPathPatterns =
    [
        @"['""]([^'""]+)['""]",
        @"(?:cat|head|tail|less|more|type|Get-Content|gc)\s+(?:-[^\s]+\s+)*([^\s|;&>-][^\s|;&>]*)",
        @"(?:>|>>)\s*([^\s|;&]+)",
        @"(?:<)\s*([^\s|;&]+)",
        @"(?:rm|del|Remove-Item|ri)\s+(?:-[^\s]+\s+)*([^\s|;&>-][^\s|;&>]*)",
        @"(?:cp|mv|copy|move|Copy-Item|Move-Item)\s+(?:-[^\s]+\s+)*([^\s|;&>-][^\s|;&>]*)",
        @"(?:chmod|chown|icacls)\s+(?:[^\s]+\s+)*([^\s|;&>]+)",
        @"(?:touch|truncate|tee)\s+([^\s|;&>]+)",
        @"echo\s+[^>]*>>\s*([^\s|;&]+)",
        @"echo\s+[^>]*>\s*([^\s|;&]+)",
    ];

    private static IEnumerable<string> ExtractPathsFromCommand(string command)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pattern in CommandPathPatterns)
        {
            foreach (var path in GetFirstMatchedGroup(command, pattern))
            {
                if (LooksLikePath(path))
                    paths.Add(path);
            }
        }

        return paths;
    }

    private static IEnumerable<string> GetFirstMatchedGroup(string input, string pattern)
    {
        var matches = Regex.Matches(input, pattern, RegexOptions.IgnoreCase);
        foreach (Match m in matches)
        {
            // Find the first captured group that has a value
            for (int g = 1; g < m.Groups.Count; g++)
            {
                if (m.Groups[g].Success)
                {
                    yield return m.Groups[g].Value.Trim('"', '\'');
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Heuristic to determine if a string looks like a file path.
    /// </summary>
    private static readonly HashSet<string> ShellBuiltins = new(StringComparer.OrdinalIgnoreCase)
        { "echo", "printf", "true", "false", "exit", "return" };

    private static bool LooksLikePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (value.StartsWith('-') || value.StartsWith("&"))
            return false;

        if (ShellBuiltins.Contains(value))
            return false;

        if (value.Contains('.') || value.Contains('/') || value.Contains('\\'))
            return true;

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

        foreach (var issue in ValidateCodeBlocks(content))
            yield return issue;
        foreach (var issue in ValidatePatternPresence())
            yield return issue;
        foreach (var issue in ValidateDuplicates())
            yield return issue;
        foreach (var issue in ValidateWhitelistBreadth())
            yield return issue;
    }

    private static IEnumerable<FormatValidationIssue> ValidateCodeBlocks(string content)
    {
        var codeBlockCount = content.Split('\n').Count(l => l.Trim().StartsWith("```"));
        if (codeBlockCount % 2 != 0)
            yield return new FormatValidationIssue("Unclosed code block (``` without closing ```)", IsError: true);
    }

    private IEnumerable<FormatValidationIssue> ValidatePatternPresence()
    {
        if (_patterns.Count == 0 && _whitelistPatterns.Count == 0)
            yield return new FormatValidationIssue("No off-limits patterns defined", IsError: false);
    }

    private IEnumerable<FormatValidationIssue> ValidateDuplicates()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pattern in _patterns.Concat(_whitelistPatterns))
        {
            if (!seen.Add(pattern))
                yield return new FormatValidationIssue($"Duplicate pattern: {pattern}", IsError: false);
        }
    }

    private static readonly string[] BroadPatternLiterals = ["*", "**", "**/*"];

    private IEnumerable<FormatValidationIssue> ValidateWhitelistBreadth()
    {
        foreach (var pattern in _whitelistPatterns)
        {
            if (BroadPatternLiterals.Contains(pattern) || IsBroadWhitelistPattern(pattern))
                yield return new FormatValidationIssue($"Whitelist pattern '{pattern}' is too broad and may defeat security", IsError: false);
        }
    }

    /// <summary>
    /// Check if a whitelist pattern is too broad.
    /// Patterns like "**/secrets*" or "**/*.json" are too broad for whitelist.
    /// Note: **/.*  is specifically allowed as it matches dotfiles (reasonably scoped).
    /// </summary>
    private static bool IsBroadWhitelistPattern(string pattern)
    {
        if (!pattern.StartsWith("**/"))
            return false;

        var afterPrefix = pattern[3..];

        // **/.*  matches only dotfiles (files starting with .) - this is specific enough
        if (afterPrefix == ".*")
            return false;

        // If everything after **/ is wildcards + extension, it's too broad
        return afterPrefix.StartsWith('*') || afterPrefix.StartsWith('.');
    }
}
