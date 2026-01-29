namespace DynaDocs.Services;

using System.Text.Json;
using System.Text.RegularExpressions;
using DynaDocs.Models;

public class AgentRegistry : IAgentRegistry
{
    private static readonly Dictionary<string, (List<string> Allowed, List<string> Denied)> RolePermissions = new()
    {
        ["code-writer"] = (["src/**", "tests/**"], ["dydo/**", "project/**"]),
        ["reviewer"] = ([], ["**"]),
        ["docs-writer"] = (["dydo/**"], ["dydo/agents/**", "src/**", "tests/**"]),
        ["interviewer"] = (["dydo/agents/{self}/**"], ["**"]),
        ["planner"] = (["dydo/agents/{self}/**", "dydo/project/tasks/**"], ["src/**"])
    };

    private readonly string _basePath;
    private readonly IConfigService _configService;
    private readonly IFolderScaffolder _folderScaffolder;
    private readonly DydoConfig? _config;

    public AgentRegistry(string? basePath = null, IConfigService? configService = null, IFolderScaffolder? folderScaffolder = null)
    {
        _basePath = basePath ?? Environment.CurrentDirectory;
        _configService = configService ?? new ConfigService();
        _folderScaffolder = folderScaffolder ?? new FolderScaffolder();
        _config = _configService.LoadConfig(_basePath);
    }

    public DydoConfig? Config => _config;

    public IReadOnlyList<string> AgentNames =>
        _config?.Agents.Pool ?? PresetAgentNames.Set1.ToList();

    public string WorkspacePath =>
        _configService.GetAgentsPath(_basePath);

    public string GetAgentWorkspace(string agentName) =>
        Path.Combine(WorkspacePath, agentName);

    public string? GetCurrentHuman() =>
        _configService.GetHumanFromEnv();

    public string? GetHumanForAgent(string agentName) =>
        _config?.Agents.GetHumanForAgent(agentName);

    public List<string> GetAgentsForHuman(string human) =>
        _config?.Agents.GetAgentsForHuman(human) ?? new List<string>();

    public bool ClaimAgent(string agentName, out string error)
    {
        error = string.Empty;

        if (!IsValidAgentName(agentName))
        {
            error = $"Invalid agent name: {agentName}";
            return false;
        }

        // Validate human assignment
        var human = GetCurrentHuman();
        var (canClaim, claimError) = _configService.ValidateAgentClaim(agentName, human, _config);
        if (!canClaim)
        {
            error = claimError!;
            return false;
        }

        var state = GetAgentState(agentName);
        if (state?.Status != AgentStatus.Free)
        {
            var session = GetSession(agentName);
            if (session != null && ProcessUtils.IsProcessRunning(session.TerminalPid))
            {
                error = $"Agent {agentName} is already claimed (terminal PID {session.TerminalPid}).";
                if (_config != null && human != null)
                {
                    var claimable = GetFreeAgentsForHuman(human);
                    if (claimable.Count > 0)
                        error += $"\nClaimable agents for human '{human}': {string.Join(", ", claimable.Select(a => a.Name))}";
                }
                error += "\nUse 'dydo agent claim auto' to claim the first available.";
                return false;
            }
            // Stale claim - terminal died, allow reclaim
        }

        var (terminalPid, claudePid) = ProcessUtils.GetProcessAncestors();
        if (terminalPid <= 0)
        {
            error = "Could not determine terminal PID";
            return false;
        }

        // Check if this terminal already has an agent
        var existingAgent = GetCurrentAgent();
        if (existingAgent != null && existingAgent.Name != agentName)
        {
            error = $"This terminal already has agent {existingAgent.Name} claimed. Release first.";
            return false;
        }

        // Write session file
        var workspace = GetAgentWorkspace(agentName);
        Directory.CreateDirectory(workspace);

        var session2 = new AgentSession
        {
            Agent = agentName,
            TerminalPid = terminalPid,
            ClaudePid = claudePid,
            Claimed = DateTime.UtcNow
        };

        var sessionPath = Path.Combine(workspace, ".session");
        File.WriteAllText(sessionPath, JsonSerializer.Serialize(session2, new JsonSerializerOptions { WriteIndented = true }));

        // Update state
        UpdateAgentState(agentName, s =>
        {
            s.Status = AgentStatus.Working;
            s.Since = DateTime.UtcNow;
            s.AssignedHuman = human;
        });

        return true;
    }

    public bool ClaimAuto(out string claimedAgent, out string error)
    {
        claimedAgent = string.Empty;
        error = string.Empty;

        var human = GetCurrentHuman();
        if (string.IsNullOrEmpty(human))
        {
            error = "DYDO_HUMAN environment variable not set.\nSet it to identify which human is operating this terminal:\n  export DYDO_HUMAN=your_name";
            return false;
        }

        // Get free agents for this human
        var freeAgents = GetFreeAgentsForHuman(human);
        if (freeAgents.Count == 0)
        {
            var assignedAgents = GetAgentsForHuman(human);
            if (assignedAgents.Count == 0)
            {
                error = $"No agents assigned to human '{human}' in dydo.json.";
            }
            else
            {
                var agentStatuses = assignedAgents.Select(a =>
                {
                    var s = GetAgentState(a);
                    return $"{a} ({s?.Status.ToString().ToLowerInvariant() ?? "unknown"})";
                });
                error = $"No free agents available for human '{human}'.\nAgents assigned to {human}: {string.Join(", ", agentStatuses)}";
            }
            return false;
        }

        // Claim first free agent
        var agentToClaim = freeAgents.First().Name;
        if (!ClaimAgent(agentToClaim, out error))
            return false;

        claimedAgent = agentToClaim;
        return true;
    }

    public bool ReleaseAgent(out string error)
    {
        error = string.Empty;

        var agent = GetCurrentAgent();
        if (agent == null)
        {
            error = "No agent identity assigned to this process.";
            return false;
        }

        var workspace = GetAgentWorkspace(agent.Name);
        var sessionPath = Path.Combine(workspace, ".session");

        if (File.Exists(sessionPath))
            File.Delete(sessionPath);

        UpdateAgentState(agent.Name, s =>
        {
            s.Status = AgentStatus.Free;
            s.Role = null;
            s.Task = null;
            s.Since = null;
            s.AllowedPaths = [];
            s.DeniedPaths = [];
        });

        return true;
    }

    public bool SetRole(string role, string? task, out string error)
    {
        error = string.Empty;

        var agent = GetCurrentAgent();
        if (agent == null)
        {
            error = "No agent identity assigned to this process. Run 'dydo agent claim auto' first.";
            return false;
        }

        if (!RolePermissions.ContainsKey(role))
        {
            error = $"Invalid role: {role}. Valid roles: {string.Join(", ", RolePermissions.Keys)}";
            return false;
        }

        var (allowed, denied) = RolePermissions[role];

        // Replace {self} placeholder with agent name
        allowed = allowed.Select(p => p.Replace("{self}", agent.Name)).ToList();
        denied = denied.Select(p => p.Replace("{self}", agent.Name)).ToList();

        UpdateAgentState(agent.Name, s =>
        {
            s.Role = role;
            s.Task = task;
            s.AllowedPaths = allowed;
            s.DeniedPaths = denied;
        });

        return true;
    }

    public AgentState? GetAgentState(string agentName)
    {
        if (!IsValidAgentName(agentName))
            return null;

        var statePath = Path.Combine(GetAgentWorkspace(agentName), "state.md");
        if (!File.Exists(statePath))
        {
            return new AgentState
            {
                Name = agentName,
                Status = AgentStatus.Free,
                AssignedHuman = GetHumanForAgent(agentName)
            };
        }

        return ParseStateFile(agentName, statePath);
    }

    public List<AgentState> GetAllAgentStates()
    {
        return AgentNames.Select(name => GetAgentState(name) ?? new AgentState { Name = name }).ToList();
    }

    public List<AgentState> GetFreeAgents()
    {
        return GetAllAgentStates().Where(a => a.Status == AgentStatus.Free).ToList();
    }

    public List<AgentState> GetFreeAgentsForHuman(string human)
    {
        var assignedAgents = GetAgentsForHuman(human);
        return GetAllAgentStates()
            .Where(a => a.Status == AgentStatus.Free &&
                       assignedAgents.Contains(a.Name, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    public AgentState? GetCurrentAgent()
    {
        var (terminalPid, claudePid) = ProcessUtils.GetProcessAncestors();
        if (terminalPid <= 0 && claudePid <= 0)
            return null;

        foreach (var name in AgentNames)
        {
            var session = GetSession(name);
            if (session == null) continue;

            // Match by terminal PID or Claude PID
            if (session.TerminalPid == terminalPid || session.ClaudePid == claudePid)
            {
                if (ProcessUtils.IsProcessRunning(session.TerminalPid))
                    return GetAgentState(name);
            }
        }

        return null;
    }

    public AgentSession? GetSession(string agentName)
    {
        var sessionPath = Path.Combine(GetAgentWorkspace(agentName), ".session");
        if (!File.Exists(sessionPath))
            return null;

        try
        {
            var json = File.ReadAllText(sessionPath);
            return JsonSerializer.Deserialize<AgentSession>(json);
        }
        catch
        {
            return null;
        }
    }

    public bool IsPathAllowed(string path, string action, out string error)
    {
        error = string.Empty;

        var agent = GetCurrentAgent();
        if (agent == null)
        {
            error = "No agent identity assigned to this process. Run 'dydo agent claim auto' first.";
            return false;
        }

        if (string.IsNullOrEmpty(agent.Role))
        {
            error = $"Agent {agent.Name} has no role set. Run 'dydo agent role <role>' first.";
            return false;
        }

        // Normalize path
        var relativePath = GetRelativePath(path);

        // Check denied first
        foreach (var pattern in agent.DeniedPaths)
        {
            if (pattern == "**" || MatchesGlob(relativePath, pattern))
            {
                // Check if explicitly allowed
                var isAllowed = agent.AllowedPaths.Any(ap => MatchesGlob(relativePath, ap));
                if (!isAllowed)
                {
                    error = $"Agent {agent.Name} ({agent.Role}) cannot {action} {relativePath}. {GetRoleRestrictionMessage(agent.Role)}";
                    return false;
                }
            }
        }

        // If no allowed paths, nothing is allowed
        if (agent.AllowedPaths.Count == 0)
        {
            error = $"Agent {agent.Name} ({agent.Role}) has no write permissions.";
            return false;
        }

        // Check if path matches any allowed pattern
        var allowed = agent.AllowedPaths.Any(pattern => MatchesGlob(relativePath, pattern));
        if (!allowed)
        {
            error = $"Agent {agent.Name} ({agent.Role}) cannot {action} {relativePath}. {GetRoleRestrictionMessage(agent.Role)}";
            return false;
        }

        return true;
    }

    private static string GetRoleRestrictionMessage(string role)
    {
        return role switch
        {
            "reviewer" => "Reviewer role has no write permissions.",
            "code-writer" => "Code-writer role can only edit src/** and tests/**.",
            "docs-writer" => "Docs-writer role can only edit dydo/** (except agents/).",
            "interviewer" => "Interviewer role can only edit own workspace.",
            "planner" => "Planner role can only edit own workspace and tasks.",
            _ => ""
        };
    }

    public bool IsValidAgentName(string name) =>
        AgentNames.Contains(name, StringComparer.OrdinalIgnoreCase);

    public string? GetAgentNameFromLetter(char letter) =>
        PresetAgentNames.GetNameFromLetter(letter);

    private string GetRelativePath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            var projectRoot = _configService.GetProjectRoot(_basePath) ?? _basePath;
            var relative = Path.GetRelativePath(projectRoot, path);
            return relative.Replace('\\', '/');
        }
        return path.Replace('\\', '/');
    }

    private static bool MatchesGlob(string path, string pattern)
    {
        // Simple glob matching: ** matches any path, * matches within segment
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace(@"\*\*", ".*")
            .Replace(@"\*", "[^/]*") + "$";

        return Regex.IsMatch(path, regexPattern, RegexOptions.IgnoreCase);
    }

    private void UpdateAgentState(string agentName, Action<AgentState> update)
    {
        var state = GetAgentState(agentName) ?? new AgentState { Name = agentName };
        update(state);
        WriteStateFile(agentName, state);
    }

    private void WriteStateFile(string agentName, AgentState state)
    {
        var workspace = GetAgentWorkspace(agentName);
        Directory.CreateDirectory(workspace);

        var statePath = Path.Combine(workspace, "state.md");
        var content = $"""
            ---
            agent: {agentName}
            role: {state.Role ?? "null"}
            task: {state.Task ?? "null"}
            status: {state.Status.ToString().ToLowerInvariant()}
            assigned: {state.AssignedHuman ?? GetHumanForAgent(agentName) ?? "unassigned"}
            started: {(state.Since.HasValue ? state.Since.Value.ToString("o") : "null")}
            allowed-paths: [{string.Join(", ", state.AllowedPaths.Select(p => $"\"{p}\""))}]
            denied-paths: [{string.Join(", ", state.DeniedPaths.Select(p => $"\"{p}\""))}]
            ---

            # {agentName} — Session State

            ## Current Task

            {(string.IsNullOrEmpty(state.Task) ? "(No active task)" : state.Task)}

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

        File.WriteAllText(statePath, content);
    }

    private AgentState? ParseStateFile(string agentName, string statePath)
    {
        try
        {
            var content = File.ReadAllText(statePath);
            if (!content.StartsWith("---"))
                return new AgentState { Name = agentName };

            var endIndex = content.IndexOf("---", 3);
            if (endIndex < 0)
                return new AgentState { Name = agentName };

            var yaml = content[3..endIndex].Trim();
            var state = new AgentState { Name = agentName };

            foreach (var line in yaml.Split('\n'))
            {
                var colonIndex = line.IndexOf(':');
                if (colonIndex < 0) continue;

                var key = line[..colonIndex].Trim();
                var value = line[(colonIndex + 1)..].Trim();

                switch (key)
                {
                    case "role":
                        state.Role = value == "null" ? null : value;
                        break;
                    case "task":
                        state.Task = value == "null" ? null : value;
                        break;
                    case "status":
                        state.Status = value switch
                        {
                            "working" => AgentStatus.Working,
                            "reviewing" => AgentStatus.Reviewing,
                            _ => AgentStatus.Free
                        };
                        break;
                    case "assigned":
                        state.AssignedHuman = value == "unassigned" || value == "null" ? null : value;
                        break;
                    case "started":
                        if (value != "null" && DateTime.TryParse(value, out var dt))
                            state.Since = dt;
                        break;
                    case "allowed-paths":
                        state.AllowedPaths = ParsePathList(value);
                        break;
                    case "denied-paths":
                        state.DeniedPaths = ParsePathList(value);
                        break;
                }
            }

            return state;
        }
        catch
        {
            return new AgentState { Name = agentName };
        }
    }

    private static List<string> ParsePathList(string value)
    {
        // Parse ["path1", "path2"] format
        if (string.IsNullOrEmpty(value) || value == "[]")
            return [];

        var match = Regex.Match(value, @"\[(.*)\]");
        if (!match.Success)
            return [];

        return Regex.Matches(match.Groups[1].Value, @"""([^""]+)""")
            .Select(m => m.Groups[1].Value)
            .ToList();
    }

    /// <summary>
    /// Creates a new agent: adds to pool, assigns to human, creates workspace and workflow file.
    /// </summary>
    public bool CreateAgent(string name, string human, out string error)
    {
        error = string.Empty;

        // Validate name format
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

        // Validate human name
        if (string.IsNullOrWhiteSpace(human))
        {
            error = "Human name cannot be empty.";
            return false;
        }

        // Normalize name to PascalCase (first letter uppercase)
        name = char.ToUpperInvariant(name[0]) + name[1..].ToLowerInvariant();

        // Load fresh config
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

        // Check if agent already exists
        if (config.Agents.Pool.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            error = $"Agent '{name}' already exists in the pool.";
            return false;
        }

        // Add to pool
        config.Agents.Pool.Add(name);

        // Add to human's assignments
        if (!config.Agents.Assignments.ContainsKey(human))
        {
            config.Agents.Assignments[human] = new List<string>();
        }
        config.Agents.Assignments[human].Add(name);

        // Save config
        _configService.SaveConfig(config, configPath);

        // Create agent workspace with workflow and mode files
        var agentsPath = _configService.GetAgentsPath(_basePath);
        _folderScaffolder.ScaffoldAgentWorkspace(agentsPath, name);

        // Create initial state file with assigned human
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
            allowed-paths: []
            denied-paths: []
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

    /// <summary>
    /// Renames an agent: updates pool, assignments, workspace folder, and regenerates workflow/mode files.
    /// </summary>
    public bool RenameAgent(string oldName, string newName, out string error)
    {
        error = string.Empty;

        // Validate new name format
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

        // Normalize names
        oldName = NormalizeName(oldName);
        newName = char.ToUpperInvariant(newName[0]) + newName[1..].ToLowerInvariant();

        // Load fresh config
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

        // Find the actual name in the pool (case-insensitive match)
        var existingName = config.Agents.Pool.FirstOrDefault(n =>
            n.Equals(oldName, StringComparison.OrdinalIgnoreCase));

        if (existingName == null)
        {
            error = $"Agent '{oldName}' does not exist in the pool.";
            return false;
        }

        // Check if new name already exists
        if (config.Agents.Pool.Contains(newName, StringComparer.OrdinalIgnoreCase))
        {
            error = $"Agent '{newName}' already exists in the pool.";
            return false;
        }

        // Check agent is not claimed
        var state = GetAgentState(existingName);
        var session = GetSession(existingName);
        if (state?.Status != AgentStatus.Free && session != null && ProcessUtils.IsProcessRunning(session.TerminalPid))
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

        // Save config
        _configService.SaveConfig(config, configPath);

        // Rename workspace folder
        var agentsPath = _configService.GetAgentsPath(_basePath);
        var oldWorkspace = Path.Combine(agentsPath, existingName);
        var newWorkspace = Path.Combine(agentsPath, newName);
        if (Directory.Exists(oldWorkspace))
        {
            Directory.Move(oldWorkspace, newWorkspace);

            // Update state file with new name
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

        // Regenerate workflow and mode files with new name
        _folderScaffolder.RegenerateAgentFiles(agentsPath, newName);

        return true;
    }

    /// <summary>
    /// Removes an agent: deletes from pool and assignments, removes entire workspace.
    /// </summary>
    public bool RemoveAgent(string name, out string error)
    {
        error = string.Empty;

        // Load fresh config
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

        // Find the actual name in the pool
        var existingName = config.Agents.Pool.FirstOrDefault(n =>
            n.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (existingName == null)
        {
            error = $"Agent '{name}' does not exist in the pool.";
            return false;
        }

        // Check agent is not claimed
        var state = GetAgentState(existingName);
        var session = GetSession(existingName);
        if (state?.Status != AgentStatus.Free && session != null && ProcessUtils.IsProcessRunning(session.TerminalPid))
        {
            error = $"Agent '{existingName}' is currently claimed. Release it first.";
            return false;
        }

        // Remove from pool
        config.Agents.Pool.RemoveAll(n => n.Equals(existingName, StringComparison.OrdinalIgnoreCase));

        // Remove from all assignments
        foreach (var assignment in config.Agents.Assignments)
        {
            assignment.Value.RemoveAll(n => n.Equals(existingName, StringComparison.OrdinalIgnoreCase));
        }

        // Save config
        _configService.SaveConfig(config, configPath);

        // Delete workspace folder (includes workflow.md and modes/)
        var workspace = Path.Combine(_configService.GetAgentsPath(_basePath), existingName);
        if (Directory.Exists(workspace))
        {
            Directory.Delete(workspace, recursive: true);
        }

        return true;
    }

    /// <summary>
    /// Reassigns an agent to a different human.
    /// </summary>
    public bool ReassignAgent(string name, string newHuman, out string error)
    {
        error = string.Empty;

        // Validate human name
        if (string.IsNullOrWhiteSpace(newHuman))
        {
            error = "Human name cannot be empty.";
            return false;
        }

        // Load fresh config
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

        // Find the actual name in the pool
        var existingName = config.Agents.Pool.FirstOrDefault(n =>
            n.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (existingName == null)
        {
            error = $"Agent '{name}' does not exist in the pool.";
            return false;
        }

        // Check agent is not claimed
        var state = GetAgentState(existingName);
        var session = GetSession(existingName);
        if (state?.Status != AgentStatus.Free && session != null && ProcessUtils.IsProcessRunning(session.TerminalPid))
        {
            error = $"Agent '{existingName}' is currently claimed. Release it first.";
            return false;
        }

        // Find current assignment
        var currentHuman = config.Agents.GetHumanForAgent(existingName);

        // Check if already assigned to the new human
        if (currentHuman != null && currentHuman.Equals(newHuman, StringComparison.OrdinalIgnoreCase))
        {
            error = $"Agent '{existingName}' is already assigned to '{newHuman}'.";
            return false;
        }

        // Remove from current human's assignments
        if (currentHuman != null && config.Agents.Assignments.ContainsKey(currentHuman))
        {
            config.Agents.Assignments[currentHuman].RemoveAll(n =>
                n.Equals(existingName, StringComparison.OrdinalIgnoreCase));
        }

        // Add to new human's assignments
        if (!config.Agents.Assignments.ContainsKey(newHuman))
        {
            config.Agents.Assignments[newHuman] = new List<string>();
        }
        config.Agents.Assignments[newHuman].Add(existingName);

        // Save config
        _configService.SaveConfig(config, configPath);

        // Update state file if it exists
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

    /// <summary>
    /// Normalizes an agent name (finds exact match in pool).
    /// </summary>
    private string NormalizeName(string name)
    {
        var match = AgentNames.FirstOrDefault(n => n.Equals(name, StringComparison.OrdinalIgnoreCase));
        return match ?? name;
    }
}
