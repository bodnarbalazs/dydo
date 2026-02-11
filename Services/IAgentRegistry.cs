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
    /// Claims an agent for the current session.
    /// Requires a pending session ID from the guard hook.
    /// </summary>
    bool ClaimAgent(string agentName, out string error);

    /// <summary>
    /// Claims the first free agent assigned to the current human.
    /// </summary>
    bool ClaimAuto(out string claimedAgent, out string error);

    /// <summary>
    /// Releases the agent claimed by the given session.
    /// </summary>
    bool ReleaseAgent(string? sessionId, out string error);

    /// <summary>
    /// Sets the role for the current agent.
    /// </summary>
    bool SetRole(string? sessionId, string role, string? task, out string error);

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
    /// Gets the current agent for the given session ID.
    /// Returns null if no agent is claimed for this session.
    /// </summary>
    AgentState? GetCurrentAgent(string? sessionId);

    /// <summary>
    /// Gets the session for an agent.
    /// </summary>
    AgentSession? GetSession(string agentName);

    /// <summary>
    /// Checks if a path is allowed for the current agent's role.
    /// </summary>
    bool IsPathAllowed(string? sessionId, string path, string action, out string error);

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

    /// <summary>
    /// Marks a must-read file as read, removing it from the agent's unread list.
    /// </summary>
    void MarkMustReadComplete(string? sessionId, string relativePath);

    /// <summary>
    /// Stores a pending session ID for an agent.
    /// Called by the guard hook when it intercepts a claim command.
    /// </summary>
    void StorePendingSessionId(string agentName, string sessionId);

    /// <summary>
    /// Gets and clears the pending session ID for an agent.
    /// Used during claim to retrieve the session ID stored by the guard hook.
    /// </summary>
    string? GetPendingSessionId(string agentName);

    /// <summary>
    /// Gets the current session ID from context file.
    /// Used by commands that run as subprocesses to identify the session.
    /// </summary>
    string? GetSessionContext();

    /// <summary>
    /// Stores the session ID to context file.
    /// Called by the guard hook before allowing dydo commands.
    /// </summary>
    void StoreSessionContext(string sessionId);
}
