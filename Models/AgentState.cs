namespace DynaDocs.Models;

public class AgentState
{
    public required string Name { get; init; }
    public string? Role { get; set; }
    public string? Task { get; set; }
    public AgentStatus Status { get; set; } = AgentStatus.Free;
    public DateTime? Since { get; set; }
    public List<string> AllowedPaths { get; set; } = [];
    public List<string> DeniedPaths { get; set; } = [];
    public string? AssignedHuman { get; set; }

    /// <summary>
    /// Tracks which roles this agent has held on which tasks.
    /// Key: task name, Value: list of roles held (e.g., ["planner", "code-writer"])
    /// Used to prevent self-review (code-writer cannot become reviewer on same task).
    /// </summary>
    public Dictionary<string, List<string>> TaskRoleHistory { get; set; } = new();

    /// <summary>
    /// Project-relative paths of must-read files that haven't been read yet.
    /// Populated on role set, cleared as agent reads each file.
    /// </summary>
    public List<string> UnreadMustReads { get; set; } = [];
}
