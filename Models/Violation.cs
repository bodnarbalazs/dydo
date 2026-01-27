namespace DynaDocs.Models;

public enum ViolationSeverity
{
    Error,
    Warning
}

public record Violation(
    string FilePath,
    string RuleName,
    string Message,
    ViolationSeverity Severity,
    int? LineNumber = null,
    bool IsAutoFixable = false,
    string? SuggestedFix = null
);
