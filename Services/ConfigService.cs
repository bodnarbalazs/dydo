namespace DynaDocs.Services;

using System.Text.Json;
using DynaDocs.Models;
using DynaDocs.Serialization;

public class ConfigService : IConfigService
{
    public const string ConfigFileName = "dydo.json";
    public const string HumanEnvVar = "DYDO_HUMAN";
    public const string DefaultRoot = "dydo";

    // Cache keyed by startPath to avoid repeated directory walks within the same instance
    private readonly Dictionary<string, string?> _configFileCache = new();

    /// <summary>
    /// Find dydo.json by walking up the directory tree
    /// </summary>
    public string? FindConfigFile(string? startPath = null)
    {
        var cacheKey = startPath ?? Environment.CurrentDirectory;
        if (_configFileCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var result = ConfigFileLocator.WalkUpForFile(cacheKey, ConfigFileName);
        _configFileCache[cacheKey] = result;
        return result;
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
            return JsonSerializer.Deserialize(json, DydoConfigJsonContext.Default.DydoConfig);
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
        var json = JsonSerializer.Serialize(config, DydoConfigJsonContext.Default.DydoConfig);
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
        var baseDir = GetProjectRoot(startPath) ?? startPath ?? Environment.CurrentDirectory;
        var rootFolder = LoadConfig(startPath)?.Structure.Root ?? DefaultRoot;
        return Path.Combine(baseDir, rootFolder);
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
    /// Get the audit folder path (dydo/_system/audit/)
    /// </summary>
    public string GetAuditPath(string? startPath = null)
    {
        return Path.Combine(GetDydoRoot(startPath), "_system", "audit");
    }

    /// <summary>
    /// Get the issues folder path
    /// </summary>
    public string GetIssuesPath(string? startPath = null)
    {
        var dydoRoot = GetDydoRoot(startPath);
        var config = LoadConfig(startPath);
        var issuesPath = config?.Structure.Issues ?? "project/issues";
        return Path.Combine(dydoRoot, issuesPath);
    }

    /// <summary>
    /// Get the changelog folder path (dydo/project/changelog/)
    /// </summary>
    public string GetChangelogPath(string? startPath = null)
    {
        return Path.Combine(GetDydoRoot(startPath), "project", "changelog");
    }

    public (bool CanClaim, string? Error) ValidateAgentClaim(string agentName, string? humanName, DydoConfig? config)
        => AgentClaimValidator.Validate(agentName, humanName, config);

    public string? GetFirstFreeAgent(string humanName, DydoConfig config, Func<string, bool> isAgentFree)
        => AgentClaimValidator.FindFirstFree(humanName, config, isAgentFree);
}
