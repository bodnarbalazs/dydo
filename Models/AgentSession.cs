namespace DynaDocs.Models;

public class AgentSession
{
    public required string Agent { get; init; }
    public int TerminalPid { get; set; }
    public int ClaudePid { get; set; }
    public DateTime Claimed { get; set; }
}
