namespace DynaDocs.Services;

public interface IBashCommandAnalyzer
{
    BashAnalysisResult Analyze(string command);

    (bool IsDangerous, string? Reason) CheckDangerousPatterns(string command);

    /// <summary>
    /// Detect needless cd+command compounds (e.g. "cd /path && git diff", "cd /path && grep ...").
    /// </summary>
    (bool IsMatch, string? CdPath, string? RestCommand) DetectNeedlessCd(string command);
}

public class BashAnalysisResult
{
    public List<FileOperation> Operations { get; } = [];
    public bool HasDangerousPattern { get; set; }
    public string? DangerousPatternReason { get; set; }
    public List<string> Warnings { get; } = [];
    /// <summary>
    /// True when command substitution, variable expansion, or other bypass techniques
    /// make the file operation analysis unreliable.
    /// </summary>
    public bool HasBypassAttempt { get; set; }
}

public class FileOperation
{
    public FileOperationType Type { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public bool IsUncertain { get; set; } // True if path might be from variable/substitution
    public bool IsCopySource { get; set; } // True for a copy's source path: the copy reads it and writes only its destination
}

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
