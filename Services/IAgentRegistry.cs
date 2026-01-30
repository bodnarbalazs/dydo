namespace DynaDocs.Services;

using DynaDocs.Models;

public interface IAgentRegistry
{
    /// <summary>
    /// All agent names from the configured pool.
    /// </summary>
    IReadOnlyList<string> AgentNames { get; }

    /// <summary>
    /// Gets the workspace root path (dydo/agents folder).
    /// </summary>
    string WorkspacePath { get; }

    /// <summary>
    /// Gets an agent's workspace path.
    /// </summary>
    string GetAgentWorkspace(string agentName);

    /// <summary>
    /// Claims an agent for the current terminal.
    /// Validates against human assignment if config exists.
    /// </summary>
    bool ClaimAgent(string agentName, out string error);

    /// <summary>
    /// Claims the first free agent assigned to the current human.
    /// </summary>
    bool ClaimAuto(out string claimedAgent, out string error);

    /// <summary>
    /// Releases the currently claimed agent.
    /// </summary>
    bool ReleaseAgent(out string error);

    /// <summary>
    /// Sets the role for the current agent.
    /// </summary>
    bool SetRole(string role, string? task, out string error);

    /// <summary>
    /// Checks if an agent can take a specific role on a task.
    /// Returns false if the agent was code-writer and is trying to become reviewer (no self-review).
    /// </summary>
    bool CanTakeRole(string agentName, string role, string task, out string reason);

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
    /// Gets free agents assigned to a specific human.
    /// </summary>
    List<AgentState> GetFreeAgentsForHuman(string human);

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

    /// <summary>
    /// Gets the current human from DYDO_HUMAN env var.
    /// </summary>
    string? GetCurrentHuman();

    /// <summary>
    /// Gets the human assigned to an agent.
    /// </summary>
    string? GetHumanForAgent(string agentName);

    /// <summary>
    /// Gets agents assigned to a human.
    /// </summary>
    List<string> GetAgentsForHuman(string human);

    /// <summary>
    /// Gets the loaded configuration.
    /// </summary>
    DydoConfig? Config { get; }

    /// <summary>
    /// Creates a new agent: adds to pool, assigns to human, creates workspace and workflow file.
    /// </summary>
    bool CreateAgent(string name, string human, out string error);

    /// <summary>
    /// Renames an agent: updates pool, assignments, workspace folder, and workflow file.
    /// </summary>
    bool RenameAgent(string oldName, string newName, out string error);

    /// <summary>
    /// Removes an agent: deletes from pool and assignments, removes workspace and workflow file.
    /// </summary>
    bool RemoveAgent(string name, out string error);

    /// <summary>
    /// Reassigns an agent to a different human.
    /// </summary>
    bool ReassignAgent(string name, string newHuman, out string error);
}
