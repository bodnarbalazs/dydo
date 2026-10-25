namespace DynaDocs.Services;

public record FormatValidationIssue(
    string Message,
    bool IsError
);

/// <summary>
/// Globally off-limits patterns: they apply to every agent and block reads, writes and deletes.
/// </summary>
public interface IOffLimitsService
{
    void LoadPatterns(string? basePath = null);

    /// <summary>
    /// Check if a path matches any off-limits pattern.
    /// Returns the matched pattern if blocked, null if allowed.
    /// </summary>
    string? IsPathOffLimits(string path);

    IReadOnlyList<string> Patterns { get; }

    IReadOnlyList<string> WhitelistPatterns { get; }

    /// <summary>
    /// Validate that literal paths (without wildcards) in off-limits file exist.
    /// Returns list of missing paths.
    /// </summary>
    IEnumerable<string> ValidateLiteralPaths(string basePath);

    bool OffLimitsFileExists(string? basePath = null);

    IEnumerable<FormatValidationIssue> ValidateFormat(string? basePath = null);
}
