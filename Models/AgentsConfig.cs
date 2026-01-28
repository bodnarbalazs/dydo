namespace DynaDocs.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Configuration for agents and assignments
/// </summary>
public class AgentsConfig
{
    [JsonPropertyName("pool")]
    public List<string> Pool { get; set; } = new();

    [JsonPropertyName("assignments")]
    public Dictionary<string, List<string>> Assignments { get; set; } = new();

    /// <summary>
    /// Get the human assigned to a specific agent
    /// </summary>
    public string? GetHumanForAgent(string agentName)
    {
        foreach (var (human, agents) in Assignments)
        {
            if (agents.Contains(agentName, StringComparer.OrdinalIgnoreCase))
                return human;
        }
        return null;
    }

    /// <summary>
    /// Get all agents assigned to a specific human
    /// </summary>
    public List<string> GetAgentsForHuman(string human)
    {
        if (Assignments.TryGetValue(human, out var agents))
            return agents;

        // Case-insensitive lookup
        var match = Assignments.FirstOrDefault(kvp =>
            kvp.Key.Equals(human, StringComparison.OrdinalIgnoreCase));

        return match.Value ?? new List<string>();
    }

    /// <summary>
    /// Check if an agent is assigned to a specific human
    /// </summary>
    public bool IsAgentAssignedTo(string agentName, string human)
    {
        var assignedHuman = GetHumanForAgent(agentName);
        return assignedHuman != null &&
               assignedHuman.Equals(human, StringComparison.OrdinalIgnoreCase);
    }
}
