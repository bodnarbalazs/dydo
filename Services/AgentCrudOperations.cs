namespace DynaDocs.Services;

using System.Text.RegularExpressions;
using DynaDocs.Models;

public class AgentCrudOperations
{
    private readonly string _basePath;
    private readonly IConfigService _configService;
    private readonly IFolderScaffolder _folderScaffolder;
    private readonly DydoConfig? _config;
    private readonly Func<string, AgentState?> _getAgentState;
    private readonly Func<string, AgentSession?> _getSession;

    public AgentCrudOperations(
        string basePath,
        IConfigService configService,
        IFolderScaffolder folderScaffolder,
        DydoConfig? config,
        Func<string, AgentState?> getAgentState,
        Func<string, AgentSession?> getSession)
    {
        _basePath = basePath;
        _configService = configService;
        _folderScaffolder = folderScaffolder;
        _config = config;
        _getAgentState = getAgentState;
        _getSession = getSession;
    }

    public bool CreateAgent(string name, string human, out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(name))
        {
            error = "Agent name cannot be empty.";
            return false;
        }

        if (!Regex.IsMatch(name, @"^[A-Za-z][A-Za-z0-9-]*$"))
        {
            error = "Agent name must start with a letter and contain only letters, numbers, and hyphens.";
            return false;
        }

        if (name.Length > 9)
        {
            error = "Agent name cannot exceed 9 characters.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(human))
        {
            error = "Human name cannot be empty.";
            return false;
        }

        name = NormalizeToPascalCase(name);

        var configPath = _configService.FindConfigFile(_basePath);
        if (configPath == null)
        {
            error = "No dydo.json found. Run 'dydo init' first.";
            return false;
        }

        var config = _configService.LoadConfig(_basePath);
        if (config == null)
        {
            error = "Failed to load dydo.json.";
            return false;
        }

        if (config.Agents.Pool.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            error = $"Agent '{name}' already exists in the pool.";
            return false;
        }

        config.Agents.Pool.Add(name);

        if (!config.Agents.Assignments.ContainsKey(human))
        {
            config.Agents.Assignments[human] = new List<string>();
        }
        config.Agents.Assignments[human].Add(name);

        _configService.SaveConfig(config, configPath);

        var agentsPath = _configService.GetAgentsPath(_basePath);
        _folderScaffolder.ScaffoldAgentWorkspace(agentsPath, name);

        var workspace = Path.Combine(agentsPath, name);
        var statePath = Path.Combine(workspace, "state.md");
        var stateContent = $"""
            ---
            agent: {name}
            role: null
            task: null
            status: free
            assigned: {human}
            started: null
            writable-paths: []
            readonly-paths: []
            ---

            # {name} — Session State

            ## Current Task

            (No active task)

            ## Progress

            - [ ] (No items)

            ## Decisions Made

            (None yet)

            ## Blockers

            (None)

            ---

            <!--
            This file is managed by dydo. Manual edits may be overwritten.
            -->
            """;
        File.WriteAllText(statePath, stateContent);

        return true;
    }

    public bool RenameAgent(string oldName, string newName, out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(newName))
        {
            error = "New agent name cannot be empty.";
            return false;
        }

        if (!Regex.IsMatch(newName, @"^[A-Za-z][A-Za-z0-9-]*$"))
        {
            error = "Agent name must start with a letter and contain only letters, numbers, and hyphens.";
            return false;
        }

        if (newName.Length > 9)
        {
            error = "Agent name cannot exceed 9 characters.";
            return false;
        }

        oldName = NormalizeFromPool(oldName);
        newName = NormalizeToPascalCase(newName);

        var (config, configPath, findError) = LoadConfigSafe();
        if (config == null)
        {
            error = findError!;
            return false;
        }

        var existingName = config.Agents.Pool.FirstOrDefault(n =>
            n.Equals(oldName, StringComparison.OrdinalIgnoreCase));

        if (existingName == null)
        {
            error = $"Agent '{oldName}' does not exist in the pool.";
            return false;
        }

        if (config.Agents.Pool.Contains(newName, StringComparer.OrdinalIgnoreCase))
        {
            error = $"Agent '{newName}' already exists in the pool.";
            return false;
        }

        if (!IsAgentFree(existingName))
        {
            error = $"Agent '{existingName}' is currently claimed. Release it first.";
            return false;
        }

        // Update pool
        var poolIndex = config.Agents.Pool.FindIndex(n =>
            n.Equals(existingName, StringComparison.OrdinalIgnoreCase));
        config.Agents.Pool[poolIndex] = newName;

        // Update assignments
        foreach (var assignment in config.Agents.Assignments)
        {
            var agentIndex = assignment.Value.FindIndex(n =>
                n.Equals(existingName, StringComparison.OrdinalIgnoreCase));
            if (agentIndex >= 0)
            {
                assignment.Value[agentIndex] = newName;
            }
        }

        _configService.SaveConfig(config, configPath);

        // Rename workspace folder
        var agentsPath = _configService.GetAgentsPath(_basePath);
        var oldWorkspace = Path.Combine(agentsPath, existingName);
        var newWorkspace = Path.Combine(agentsPath, newName);
        if (Directory.Exists(oldWorkspace))
        {
            Directory.Move(oldWorkspace, newWorkspace);

            var statePath = Path.Combine(newWorkspace, "state.md");
            if (File.Exists(statePath))
            {
                var content = File.ReadAllText(statePath);
                content = Regex.Replace(content, $@"^agent:\s*{Regex.Escape(existingName)}\s*$",
                    $"agent: {newName}", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                content = Regex.Replace(content, $@"^# {Regex.Escape(existingName)} —",
                    $"# {newName} —", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                File.WriteAllText(statePath, content);
            }
        }

        _folderScaffolder.RegenerateAgentFiles(agentsPath, newName,
            _config?.Paths.Source, _config?.Paths.Tests);

        return true;
    }

    public bool RemoveAgent(string name, out string error)
    {
        error = string.Empty;

        var (config, configPath, findError) = LoadConfigSafe();
        if (config == null)
        {
            error = findError!;
            return false;
        }

        var existingName = config.Agents.Pool.FirstOrDefault(n =>
            n.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (existingName == null)
        {
            error = $"Agent '{name}' does not exist in the pool.";
            return false;
        }

        if (!IsAgentFree(existingName))
        {
            error = $"Agent '{existingName}' is currently claimed. Release it first.";
            return false;
        }

        config.Agents.Pool.RemoveAll(n => n.Equals(existingName, StringComparison.OrdinalIgnoreCase));

        foreach (var assignment in config.Agents.Assignments)
        {
            assignment.Value.RemoveAll(n => n.Equals(existingName, StringComparison.OrdinalIgnoreCase));
        }

        _configService.SaveConfig(config, configPath);

        var workspace = Path.Combine(_configService.GetAgentsPath(_basePath), existingName);
        if (Directory.Exists(workspace))
        {
            Directory.Delete(workspace, recursive: true);
        }

        return true;
    }

    public bool ReassignAgent(string name, string newHuman, out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(newHuman))
        {
            error = "Human name cannot be empty.";
            return false;
        }

        var (config, configPath, findError) = LoadConfigSafe();
        if (config == null)
        {
            error = findError!;
            return false;
        }

        var existingName = config.Agents.Pool.FirstOrDefault(n =>
            n.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (existingName == null)
        {
            error = $"Agent '{name}' does not exist in the pool.";
            return false;
        }

        if (!IsAgentFree(existingName))
        {
            error = $"Agent '{existingName}' is currently claimed. Release it first.";
            return false;
        }

        var currentHuman = config.Agents.GetHumanForAgent(existingName);

        if (currentHuman != null && currentHuman.Equals(newHuman, StringComparison.OrdinalIgnoreCase))
        {
            error = $"Agent '{existingName}' is already assigned to '{newHuman}'.";
            return false;
        }

        if (currentHuman != null && config.Agents.Assignments.ContainsKey(currentHuman))
        {
            config.Agents.Assignments[currentHuman].RemoveAll(n =>
                n.Equals(existingName, StringComparison.OrdinalIgnoreCase));
        }

        if (!config.Agents.Assignments.ContainsKey(newHuman))
        {
            config.Agents.Assignments[newHuman] = new List<string>();
        }
        config.Agents.Assignments[newHuman].Add(existingName);

        _configService.SaveConfig(config, configPath);

        var workspace = Path.Combine(_configService.GetAgentsPath(_basePath), existingName);
        var statePath = Path.Combine(workspace, "state.md");
        if (File.Exists(statePath))
        {
            var content = File.ReadAllText(statePath);
            content = Regex.Replace(content, @"^assigned:\s*\S+\s*$",
                $"assigned: {newHuman}", RegexOptions.Multiline);
            File.WriteAllText(statePath, content);
        }

        return true;
    }

    private (DydoConfig? config, string configPath, string? error) LoadConfigSafe()
    {
        var configPath = _configService.FindConfigFile(_basePath);
        if (configPath == null)
            return (null, "", "No dydo.json found. Run 'dydo init' first.");

        var config = _configService.LoadConfig(_basePath);
        if (config == null)
            return (null, "", "Failed to load dydo.json.");

        return (config, configPath, null);
    }

    private bool IsAgentFree(string name)
    {
        var state = _getAgentState(name);
        var session = _getSession(name);
        return !((state?.Status != AgentStatus.Free && session != null) || state?.Status == AgentStatus.Dispatched || state?.Status == AgentStatus.Queued);
    }

    private string NormalizeFromPool(string name)
    {
        var match = _config?.Agents.Pool.FirstOrDefault(n => n.Equals(name, StringComparison.OrdinalIgnoreCase));
        return match ?? name;
    }

    private static string NormalizeToPascalCase(string name)
    {
        return name.Length > 1
            ? char.ToUpperInvariant(name[0]) + name[1..].ToLowerInvariant()
            : name.ToUpperInvariant();
    }
}
