namespace DynaDocs.Models;

public class ValidationIssue
{
    public required string Severity { get; init; } // "error" | "warning"
    public required string File { get; init; }
    public required string Message { get; init; }
}
