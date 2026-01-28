namespace DynaDocs.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Root configuration object for dydo.json
/// </summary>
public class DydoConfig
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("structure")]
    public StructureConfig Structure { get; set; } = new();

    [JsonPropertyName("agents")]
    public AgentsConfig Agents { get; set; } = new();

    [JsonPropertyName("integrations")]
    public Dictionary<string, bool> Integrations { get; set; } = new();
}

/// <summary>
/// Configuration for folder structure
/// </summary>
public class StructureConfig
{
    [JsonPropertyName("root")]
    public string Root { get; set; } = "dydo";

    [JsonPropertyName("tasks")]
    public string Tasks { get; set; } = "project/tasks";
}

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

/// <summary>
/// Preset agent name sets
/// </summary>
public static class PresetAgentNames
{
    /// <summary>
    /// Primary set of 26 agent names (A-Z)
    /// </summary>
    public static readonly IReadOnlyList<string> Set1 = new[]
    {
        "Adele", "Brian", "Charlie", "Dexter", "Emma", "Frank",
        "Grace", "Henry", "Iris", "Jack", "Kate", "Leo",
        "Mia", "Noah", "Olivia", "Paul", "Quinn", "Rose",
        "Sam", "Tara", "Uma", "Victor", "Wendy", "Xavier",
        "Yara", "Zack"
    };

    /// <summary>
    /// Overflow set of 26 more agent names
    /// </summary>
    public static readonly IReadOnlyList<string> Set2 = new[]
    {
        "Alfred", "Bella", "Carla", "Dylan", "Ethan", "Fiona",
        "George", "Holly", "Ivan", "Julia", "Kevin", "Luna",
        "Marcus", "Nadia", "Oscar", "Penny", "Quentin", "Rita",
        "Steve", "Tina", "Ulrich", "Vera", "Walter", "Xena",
        "Yuri", "Zara"
    };

    /// <summary>
    /// Get agent names up to the specified count
    /// </summary>
    public static List<string> GetNames(int count)
    {
        var names = new List<string>();

        // Add from Set1 first
        for (int i = 0; i < Math.Min(count, Set1.Count); i++)
            names.Add(Set1[i]);

        // Add from Set2 if needed
        if (count > Set1.Count)
        {
            var remaining = count - Set1.Count;
            for (int i = 0; i < Math.Min(remaining, Set2.Count); i++)
                names.Add(Set2[i]);
        }

        return names;
    }

    /// <summary>
    /// Get agent name from letter (A=Adele, B=Brian, etc.)
    /// </summary>
    public static string? GetNameFromLetter(char letter)
    {
        letter = char.ToUpperInvariant(letter);
        var index = letter - 'A';

        if (index >= 0 && index < Set1.Count)
            return Set1[index];

        return null;
    }

    /// <summary>
    /// Get letter from agent name
    /// </summary>
    public static char? GetLetterFromName(string name)
    {
        var index = Set1.ToList().FindIndex(n =>
            n.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (index >= 0)
            return (char)('A' + index);

        return null;
    }
}
