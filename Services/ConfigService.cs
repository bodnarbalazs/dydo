namespace DynaDocs.Services;

using System.Text.Json;
using DynaDocs.Models;

public interface IConfigService
{
    string? FindConfigFile(string? startPath = null);
    DydoConfig? LoadConfig(string? startPath = null);
    void SaveConfig(DydoConfig config, string path);
    string? GetHumanFromEnv();
    string? GetProjectRoot(string? startPath = null);
    string GetDydoRoot(string? startPath = null);
    string GetAgentsPath(string? startPath = null);
    string GetDocsPath(string? startPath = null);
    string GetTasksPath(string? startPath = null);
    (bool CanClaim, string? Error) ValidateAgentClaim(string agentName, string? humanName, DydoConfig? config);
}

public class ConfigService : IConfigService
{
    public const string ConfigFileName = "dydo.json";
    public const string HumanEnvVar = "DYDO_HUMAN";
    public const string DefaultRoot = "dydo";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Find dydo.json by walking up the directory tree
    /// </summary>
    public string? FindConfigFile(string? startPath = null)
    {
        var dir = startPath ?? Environment.CurrentDirectory;

        while (!string.IsNullOrEmpty(dir))
        {
            var configPath = Path.Combine(dir, ConfigFileName);
            if (File.Exists(configPath))
                return configPath;

            var parent = Directory.GetParent(dir);
            if (parent == null)
                break;

            dir = parent.FullName;
        }

        return null;
    }

    /// <summary>
    /// Load configuration from dydo.json
    /// </summary>
    public DydoConfig? LoadConfig(string? startPath = null)
    {
        var configPath = FindConfigFile(startPath);
        if (configPath == null)
            return null;

        try
        {
            var json = File.ReadAllText(configPath);
            return JsonSerializer.Deserialize<DydoConfig>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Save configuration to dydo.json
    /// </summary>
    public void SaveConfig(DydoConfig config, string path)
    {
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Get the human name from DYDO_HUMAN environment variable
    /// </summary>
    public string? GetHumanFromEnv()
    {
        return Environment.GetEnvironmentVariable(HumanEnvVar);
    }

    /// <summary>
    /// Get the project root directory (where dydo.json lives)
    /// </summary>
    public string? GetProjectRoot(string? startPath = null)
    {
        var configPath = FindConfigFile(startPath);
        if (configPath == null)
            return null;

        return Path.GetDirectoryName(configPath);
    }

    /// <summary>
    /// Get the dydo root folder path (e.g., /project/dydo/)
    /// </summary>
    public string GetDydoRoot(string? startPath = null)
    {
        var projectRoot = GetProjectRoot(startPath);
        var config = LoadConfig(startPath);
        var rootFolder = config?.Structure.Root ?? DefaultRoot;

        if (projectRoot != null)
            return Path.Combine(projectRoot, rootFolder);

        // Fall back to current directory
        return Path.Combine(startPath ?? Environment.CurrentDirectory, rootFolder);
    }

    /// <summary>
    /// Get the agents folder path (e.g., /project/dydo/agents/)
    /// </summary>
    public string GetAgentsPath(string? startPath = null)
    {
        return Path.Combine(GetDydoRoot(startPath), "agents");
    }

    /// <summary>
    /// Get the docs folder path (dydo root itself contains docs)
    /// </summary>
    public string GetDocsPath(string? startPath = null)
    {
        return GetDydoRoot(startPath);
    }

    /// <summary>
    /// Get the tasks folder path
    /// </summary>
    public string GetTasksPath(string? startPath = null)
    {
        var dydoRoot = GetDydoRoot(startPath);
        var config = LoadConfig(startPath);
        var tasksPath = config?.Structure.Tasks ?? "project/tasks";

        return Path.Combine(dydoRoot, tasksPath);
    }

    /// <summary>
    /// Create a default configuration for a new project
    /// </summary>
    public static DydoConfig CreateDefault(string humanName, int agentCount = 26)
    {
        var agentNames = PresetAgentNames.GetNames(agentCount);

        return new DydoConfig
        {
            Version = 1,
            Structure = new StructureConfig
            {
                Root = DefaultRoot,
                Tasks = "project/tasks"
            },
            Agents = new AgentsConfig
            {
                Pool = agentNames,
                Assignments = new Dictionary<string, List<string>>
                {
                    [humanName] = agentNames
                }
            },
            Integrations = new Dictionary<string, bool>()
        };
    }

    /// <summary>
    /// Add a new human to an existing configuration
    /// </summary>
    public static void AddHuman(DydoConfig config, string humanName, int agentCount)
    {
        // Find unassigned agents
        var assignedAgents = config.Agents.Assignments.Values
            .SelectMany(a => a)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var availableAgents = config.Agents.Pool
            .Where(a => !assignedAgents.Contains(a))
            .ToList();

        if (availableAgents.Count < agentCount)
        {
            // Need to add more agents to the pool
            var currentCount = config.Agents.Pool.Count;
            var newAgents = PresetAgentNames.GetNames(currentCount + agentCount)
                .Skip(currentCount)
                .ToList();

            config.Agents.Pool.AddRange(newAgents);
            availableAgents.AddRange(newAgents);
        }

        // Assign agents to the new human
        var toAssign = availableAgents.Take(agentCount).ToList();
        config.Agents.Assignments[humanName] = toAssign;
    }

    /// <summary>
    /// Validate that an agent can be claimed by a human
    /// </summary>
    public (bool CanClaim, string? Error) ValidateAgentClaim(string agentName, string? humanName, DydoConfig? config)
    {
        if (string.IsNullOrEmpty(humanName))
        {
            return (false, $"DYDO_HUMAN environment variable not set.\nSet it to identify which human is operating this terminal:\n  export DYDO_HUMAN=your_name");
        }

        if (config == null)
        {
            // No config - allow any claim (unconfigured project)
            return (true, null);
        }

        if (!config.Agents.Pool.Contains(agentName, StringComparer.OrdinalIgnoreCase))
        {
            return (false, $"Agent '{agentName}' is not in the configured agent pool.");
        }

        var assignedHuman = config.Agents.GetHumanForAgent(agentName);
        if (assignedHuman == null)
        {
            // Agent not assigned to anyone - allow claim
            return (true, null);
        }

        if (!assignedHuman.Equals(humanName, StringComparison.OrdinalIgnoreCase))
        {
            var claimableAgents = config.Agents.GetAgentsForHuman(humanName);
            var agentList = claimableAgents.Count > 0
                ? string.Join(", ", claimableAgents)
                : "(none assigned)";

            return (false, $"Agent {agentName} is assigned to human '{assignedHuman}', not '{humanName}'.\nClaimable agents for human '{humanName}': {agentList}\nUse 'dydo agent claim auto' to claim the first available.");
        }

        return (true, null);
    }

    /// <summary>
    /// Get the first available agent for a human
    /// </summary>
    public string? GetFirstFreeAgent(string humanName, DydoConfig config, Func<string, bool> isAgentFree)
    {
        var assignedAgents = config.Agents.GetAgentsForHuman(humanName);

        foreach (var agent in assignedAgents)
        {
            if (isAgentFree(agent))
                return agent;
        }

        return null;
    }
}
