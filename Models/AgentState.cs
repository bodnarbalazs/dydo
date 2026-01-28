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
}

public enum AgentStatus
{
    Free,
    Working,
    Reviewing
}
