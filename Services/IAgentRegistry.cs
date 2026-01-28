namespace DynaDocs.Services;

using DynaDocs.Models;

public interface IAgentRegistry
{
    /// <summary>
    /// All 26 agent names (A-Z).
    /// </summary>
    IReadOnlyList<string> AgentNames { get; }

    /// <summary>
    /// Gets the workspace root path (.workspace folder).
    /// </summary>
    string WorkspacePath { get; }

    /// <summary>
    /// Gets an agent's workspace path.
    /// </summary>
    string GetAgentWorkspace(string agentName);

    /// <summary>
    /// Claims an agent for the current terminal.
    /// </summary>
    bool ClaimAgent(string agentName, out string error);

    /// <summary>
    /// Releases the currently claimed agent.
    /// </summary>
    bool ReleaseAgent(out string error);

    /// <summary>
    /// Sets the role for the current agent.
    /// </summary>
    bool SetRole(string role, string? task, out string error);

    /// <summary>
    /// Gets the agent state for a specific agent.
    /// </summary>
    AgentState? GetAgentState(string agentName);

    /// <summary>
    /// Gets all agent states.
    /// </summary>
    List<AgentState> GetAllAgentStates();

    /// <summary>
    /// Gets free agents.
    /// </summary>
    List<AgentState> GetFreeAgents();

    /// <summary>
    /// Gets the current agent based on calling process PID.
    /// </summary>
    AgentState? GetCurrentAgent();

    /// <summary>
    /// Gets the session for an agent.
    /// </summary>
    AgentSession? GetSession(string agentName);

    /// <summary>
    /// Checks if a path is allowed for the current agent's role.
    /// </summary>
    bool IsPathAllowed(string path, string action, out string error);

    /// <summary>
    /// Validates that an agent name is valid.
    /// </summary>
    bool IsValidAgentName(string name);

    /// <summary>
    /// Gets agent name from letter (A -> Adele).
    /// </summary>
    string? GetAgentNameFromLetter(char letter);
}
