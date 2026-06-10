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
    // dydo/_system/** (machine-managed state: audit, watchdog, worktree markers, role defs) and
    // dydo.json (config, incl. security nudges) are agent-untouchable now that per-role RBAC,
    // which used to gate them, is gone (Decision 024).
    private static readonly (string Pattern, Regex Compiled)[] SystemOffLimits =
    [
        ("dydo/agents/*/.guard-lift.json", CompileGlobToRegex("dydo/agents/*/.guard-lift.json")),
        ("dydo/_system/**", CompileGlobToRegex("dydo/_system/**")),
        ("dydo.json", CompileGlobToRegex("dydo.json")),
    ];

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
        // Claude Code delivers absolute tool paths; off-limits patterns are repo-relative.
        // Relativize to the project root first, or none of the path-anchored patterns
        // (dydo/_system/**, dydo/agents/*/state.md, …) would ever match in production.
        var normalizedPath = PathUtils.NormalizeForPattern(RelativizeToProjectRoot(path));

        // Hardcoded system patterns — always enforced, not whitelistable
        foreach (var (pattern, compiled) in SystemOffLimits)
        {
            if (compiled.IsMatch(normalizedPath))
                return pattern;
        }

        // Whitelist — if matched, allow past user-defined off-limits patterns
        if (FindMatchingPattern(normalizedPath, _whitelistPatterns, _whitelistCompiled) != null)
            return null;

        return FindMatchingPattern(normalizedPath, _patterns, _compiledPatterns);
    }

    /// <summary>
    /// Maps an absolute tool path back to a project-root-relative path so it can match
    /// repo-relative off-limits patterns. Worktree paths use the main project root.
    /// Relative paths and paths outside the project are returned unchanged.
    /// </summary>
    private string RelativizeToProjectRoot(string path)
    {
        if (!Path.IsPathRooted(path))
            return path;

        var basePath = _basePath ?? Environment.CurrentDirectory;
        var root = PathUtils.GetMainProjectRoot(basePath) ?? _configService.GetProjectRoot(basePath);
        if (root == null)
            return path;

        var relative = Path.GetRelativePath(root, path);
        // Outside the project root (GetRelativePath yields a rooted path or a leading '..'
        // segment): leave it absolute so it simply won't match any repo-relative pattern.
        // Match '..' as a whole segment only — a dir literally named '..foo' is inside the root.
        var firstSegment = relative.Replace('\\', '/').Split('/', 2)[0];
        if (Path.IsPathRooted(relative) || firstSegment == "..")
            return path;

        return relative;
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
