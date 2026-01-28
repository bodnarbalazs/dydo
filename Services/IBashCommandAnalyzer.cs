namespace DynaDocs.Services;

/// <summary>
/// Analyzes Bash/shell commands to detect file operations.
/// Supports bash, zsh, PowerShell, and cmd.
/// </summary>
public interface IBashCommandAnalyzer
{
    /// <summary>
    /// Analyze a command and return all detected file operations.
    /// </summary>
    BashAnalysisResult Analyze(string command);

    /// <summary>
    /// Check if a command contains dangerous patterns that should always be blocked.
    /// </summary>
    (bool IsDangerous, string? Reason) CheckDangerousPatterns(string command);
}

/// <summary>
/// Result of analyzing a shell command.
/// </summary>
public class BashAnalysisResult
{
    public List<FileOperation> Operations { get; } = [];
    public bool HasDangerousPattern { get; set; }
    public string? DangerousPatternReason { get; set; }
    public List<string> Warnings { get; } = [];
}

/// <summary>
/// A detected file operation within a command.
/// </summary>
public class FileOperation
{
    public FileOperationType Type { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public bool IsUncertain { get; set; } // True if path might be from variable/substitution
}

/// <summary>
/// Types of file operations that can be detected in shell commands.
/// </summary>
public enum FileOperationType
{
    Read,
    Write,
    Delete,
    Execute,
    PermissionChange,
    Copy,
    Move
}
