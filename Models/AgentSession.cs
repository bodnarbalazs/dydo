namespace DynaDocs.Models;

/// <summary>
/// Represents an agent's session claim.
/// Identity is tracked via session_id provided by the coding tool's hook system (e.g., Claude Code).
/// </summary>
public class AgentSession
{
    public required string Agent { get; init; }
    public required string SessionId { get; init; }
    public DateTime Claimed { get; set; }
}
