namespace DynaDocs.Services;

/// <summary>
/// Service for managing globally off-limits file patterns.
/// These patterns apply to ALL agents regardless of role and block ALL operations (read, write, delete).
/// </summary>
public interface IOffLimitsService
{
    /// <summary>
    /// Load patterns from files-off-limits.md in the dydo folder.
    /// </summary>
    void LoadPatterns(string? basePath = null);

    /// <summary>
    /// Check if a path matches any off-limits pattern.
    /// Returns the matched pattern if blocked, null if allowed.
    /// </summary>
    string? IsPathOffLimits(string path);

    /// <summary>
    /// Check if a command contains any off-limits file references.
    /// Used for Bash command analysis.
    /// </summary>
    (bool IsBlocked, string? MatchedPath, string? MatchedPattern) CheckCommand(string command);

    /// <summary>
    /// Get all configured patterns (for diagnostics).
    /// </summary>
    IReadOnlyList<string> Patterns { get; }

    /// <summary>
    /// Validate that literal paths (without wildcards) in off-limits file exist.
    /// Returns list of missing paths.
    /// </summary>
    IEnumerable<string> ValidateLiteralPaths(string basePath);

    /// <summary>
    /// Check if the off-limits file exists.
    /// </summary>
    bool OffLimitsFileExists(string? basePath = null);
}
