namespace DynaDocs.Services;

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DynaDocs.Models;
using DynaDocs.Serialization;
using DynaDocs.Utils;

public partial class AgentRegistry : IAgentRegistry
{
    private static readonly Dictionary<string, (List<string> Allowed, List<string> Denied)> RolePermissions = new()
    {
        ["code-writer"] = (["src/**", "tests/**", "dydo/agents/{self}/**"], ["dydo/**", "project/**"]),
        ["reviewer"] = (["dydo/agents/{self}/**"], ["**"]),
        ["co-thinker"] = (["dydo/agents/{self}/**", "dydo/project/decisions/**"], ["src/**", "tests/**"]),
        ["docs-writer"] = (["dydo/understand/**", "dydo/guides/**", "dydo/reference/**", "dydo/project/**", "dydo/_system/**", "dydo/_assets/**", "dydo/*.md", "dydo/agents/{self}/**"], ["src/**", "tests/**"]),
        ["interviewer"] = (["dydo/agents/{self}/**"], ["**"]),
        ["planner"] = (["dydo/agents/{self}/**", "dydo/project/tasks/**"], ["src/**"]),
        ["tester"] = (["dydo/agents/{self}/**", "tests/**", "dydo/project/pitfalls/**"], ["src/**"])
    };

    private readonly string _basePath;
    private readonly IConfigService _configService;
    private readonly IFolderScaffolder _folderScaffolder;
    private readonly IAuditService _auditService;
    private readonly DydoConfig? _config;

    public AgentRegistry(string? basePath = null, IConfigService? configService = null, IFolderScaffolder? folderScaffolder = null, IAuditService? auditService = null)
    {
        _basePath = basePath ?? Environment.CurrentDirectory;
        _configService = configService ?? new ConfigService();
        _folderScaffolder = folderScaffolder ?? new FolderScaffolder();
        _auditService = auditService ?? new AuditService(_configService, _basePath);
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

        // Get pending session_id from guard hook
        var sessionId = GetPendingSessionId(agentName);
        if (string.IsNullOrEmpty(sessionId))
        {
            error = "No session ID available. Claim must be initiated via hook.";
            return false;
        }

        // Acquire lock before any state checks to prevent race conditions
        if (!TryAcquireLock(agentName, out error))
            return false;

        try
        {
            // Validate human assignment
            var human = GetCurrentHuman();
            var (canClaim, claimError) = _configService.ValidateAgentClaim(agentName, human, _config);
            if (!canClaim)
            {
                error = claimError!;
                return false;
            }

            // Check if this session already has a different agent
            var existingAgent = GetCurrentAgent(sessionId);
            if (existingAgent != null && existingAgent.Name != agentName)
            {
                error = $"This session already has agent {existingAgent.Name} claimed. Release first.";
                return false;
            }

            // Check if agent is already claimed by another session
            var state = GetAgentState(agentName);
            var existingSession = GetSession(agentName);
            if (state?.Status != AgentStatus.Free && existingSession != null)
            {
                if (existingSession.SessionId == sessionId)
                {
                    // Same session reclaiming - idempotent, success
                    return true;
                }
                error = $"Agent {agentName} is already claimed by another session.";
                if (_config != null && human != null)
                {
                    var claimable = GetFreeAgentsForHuman(human);
                    if (claimable.Count > 0)
                        error += $"\nClaimable agents for human '{human}': {string.Join(", ", claimable.Select(a => a.Name))}";
                }
                error += "\nUse 'dydo agent claim auto' to claim the first available.";
                return false;
            }

            // Write session file
            var workspace = GetAgentWorkspace(agentName);
            Directory.CreateDirectory(workspace);

            // Archive user files from previous session (if any)
            try
            {
                ArchiveWorkspace(workspace);
                PruneArchive(workspace);
            }
            catch
            {
                // Archive failure should not block agent claim
            }

            // Regenerate mode files from templates (fresh start for each claim)
            _folderScaffolder.RegenerateAgentFiles(WorkspacePath, agentName);

            var session = new AgentSession
            {
                Agent = agentName,
                SessionId = sessionId,
                Claimed = DateTime.UtcNow
            };

            var sessionPath = Path.Combine(workspace, ".session");
            File.WriteAllText(sessionPath, JsonSerializer.Serialize(session, DydoDefaultJsonContext.Default.AgentSession));

            // Update state
            UpdateAgentState(agentName, s =>
            {
                s.Status = AgentStatus.Working;
                s.Since = DateTime.UtcNow;
                s.AssignedHuman = human;
            });

            // Capture project snapshot for audit visualization
            ProjectSnapshot? snapshot = null;
            try
            {
                var snapshotService = new SnapshotService(_configService);
                snapshot = snapshotService.CaptureSnapshot(_basePath);
            }
            catch
            {
                // Snapshot failure should not block agent claim
            }

            // Log claim event with snapshot
            LogLifecycleEvent(sessionId, new AuditEvent
            {
                EventType = AuditEventType.Claim,
                AgentName = agentName
            }, agentName, human, snapshot);

            return true;
        }
        finally
        {
            ReleaseLock(agentName);
        }
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

    public bool ReleaseAgent(string? sessionId, out string error)
    {
        error = string.Empty;

        var agent = GetCurrentAgent(sessionId);
        if (agent == null)
        {
            error = "No agent identity assigned to this session.";
            return false;
        }

        // Acquire lock to prevent race with concurrent claim attempts
        if (!TryAcquireLock(agent.Name, out error))
            return false;

        try
        {
            var workspace = GetAgentWorkspace(agent.Name);

            // Check for unprocessed inbox items
            var inboxPath = Path.Combine(workspace, "inbox");
            if (Directory.Exists(inboxPath))
            {
                var unprocessedItems = Directory.GetFiles(inboxPath, "*.md").Length;
                if (unprocessedItems > 0)
                {
                    error = $"Cannot release: {unprocessedItems} unprocessed inbox item(s).\n" +
                            "Process all inbox items, then run 'dydo inbox clear' before releasing.";
                    return false;
                }
            }

            var sessionPath = Path.Combine(workspace, ".session");

            if (File.Exists(sessionPath))
                File.Delete(sessionPath);

            // Log release event before clearing state
            var human = GetCurrentHuman();
            LogLifecycleEvent(sessionId, new AuditEvent
            {
                EventType = AuditEventType.Release,
                AgentName = agent.Name
            }, agent.Name, human);

            UpdateAgentState(agent.Name, s =>
            {
                s.Status = AgentStatus.Free;
                s.Role = null;
                s.Task = null;
                s.Since = null;
                s.AllowedPaths = [];
                s.DeniedPaths = [];
            });

            // Remove modes/ directory (regenerated fresh on next claim)
            var modesPath = Path.Combine(workspace, "modes");
            if (Directory.Exists(modesPath))
                Directory.Delete(modesPath, true);

            return true;
        }
        finally
        {
            ReleaseLock(agent.Name);
        }
    }

    public bool SetRole(string? sessionId, string role, string? task, out string error)
    {
        error = string.Empty;

        var agent = GetCurrentAgent(sessionId);
        if (agent == null)
        {
            error = "No agent identity assigned to this session. Run 'dydo agent claim auto' first.";
            return false;
        }

        if (!RolePermissions.ContainsKey(role))
        {
            error = $"Invalid role: {role}. Valid roles: {string.Join(", ", RolePermissions.Keys)}";
            return false;
        }

        // Check for self-review violation
        if (role == "reviewer" && !string.IsNullOrEmpty(task))
        {
            if (!CanTakeRole(agent.Name, role, task, out var reason))
            {
                error = reason;
                return false;
            }
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

            // Track role in task history
            if (!string.IsNullOrEmpty(task))
            {
                if (!s.TaskRoleHistory.ContainsKey(task))
                {
                    s.TaskRoleHistory[task] = new List<string>();
                }
                if (!s.TaskRoleHistory[task].Contains(role))
                {
                    s.TaskRoleHistory[task].Add(role);
                }
            }
        });

        // Log role change event
        var human = GetCurrentHuman();
        LogLifecycleEvent(sessionId, new AuditEvent
        {
            EventType = AuditEventType.Role,
            Role = role,
            Task = task
        }, agent.Name, human);

        return true;
    }

    /// <summary>
    /// Checks if an agent can take a specific role on a task.
    /// Returns false if the agent was code-writer and is trying to become reviewer (no self-review).
    /// </summary>
    public bool CanTakeRole(string agentName, string role, string task, out string reason)
    {
        reason = string.Empty;

        var state = GetAgentState(agentName);
        if (state == null)
        {
            reason = $"Agent {agentName} not found.";
            return false;
        }

        // Prevent self-review: code-writer cannot become reviewer on same task
        if (role == "reviewer" && state.TaskRoleHistory.TryGetValue(task, out var previousRoles))
        {
            if (previousRoles.Contains("code-writer"))
            {
                reason = $"Agent {agentName} was code-writer on task '{task}' and cannot be reviewer on the same task. Dispatch to a different agent for review.";
                return false;
            }
        }

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

    /// <summary>
    /// Gets the current agent for a given session ID.
    /// Returns null if no agent is claimed for this session.
    /// </summary>
    public AgentState? GetCurrentAgent(string? sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
            return null;

        foreach (var name in AgentNames)
        {
            var session = GetSession(name);
            if (session?.SessionId == sessionId)
                return GetAgentState(name);
        }

        return null;
    }

    #region Session Context Support

    private string GetPendingSessionPath(string agentName) =>
        Path.Combine(GetAgentWorkspace(agentName), ".pending-session");

    private string GetSessionContextPath() =>
        Path.Combine(WorkspacePath, ".session-context");

    /// <summary>
    /// Gets and clears the pending session ID for an agent.
    /// Used during claim to retrieve the session ID stored by the guard hook.
    /// </summary>
    public string? GetPendingSessionId(string agentName)
    {
        var path = GetPendingSessionPath(agentName);
        if (!File.Exists(path)) return null;

        try
        {
            var sessionId = File.ReadAllText(path).Trim();
            File.Delete(path);  // Clean up after reading
            return sessionId;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Stores a pending session ID for an agent.
    /// Called by the guard hook when it intercepts a claim command.
    /// </summary>
    public void StorePendingSessionId(string agentName, string sessionId)
    {
        var path = GetPendingSessionPath(agentName);
        var dir = Path.GetDirectoryName(path);
        if (dir != null) Directory.CreateDirectory(dir);

        // Retry on file access errors (concurrent access in tests)
        for (var i = 0; i < 3; i++)
        {
            try
            {
                File.WriteAllText(path, sessionId);
                return;
            }
            catch (IOException) when (i < 2)
            {
                Thread.Sleep(10 * (i + 1));
            }
        }
    }

    /// <summary>
    /// Gets the current session ID from context file.
    /// Used by commands that run as subprocesses to identify the session.
    /// </summary>
    public string? GetSessionContext()
    {
        var path = GetSessionContextPath();
        if (!File.Exists(path)) return null;

        try
        {
            return File.ReadAllText(path).Trim();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Stores the session ID to context file.
    /// Called by the guard hook before allowing dydo commands.
    /// </summary>
    public void StoreSessionContext(string sessionId)
    {
        var path = GetSessionContextPath();
        var dir = Path.GetDirectoryName(path);
        if (dir != null) Directory.CreateDirectory(dir);
        File.WriteAllText(path, sessionId);
    }

    #endregion

    public AgentSession? GetSession(string agentName)
    {
        var sessionPath = Path.Combine(GetAgentWorkspace(agentName), ".session");
        if (!File.Exists(sessionPath))
            return null;

        try
        {
            var json = File.ReadAllText(sessionPath);
            return JsonSerializer.Deserialize(json, DydoDefaultJsonContext.Default.AgentSession);
        }
        catch
        {
            return null;
        }
    }

    public bool IsPathAllowed(string? sessionId, string path, string action, out string error)
    {
        error = string.Empty;

        var agent = GetCurrentAgent(sessionId);
        if (agent == null)
        {
            error = "No agent identity assigned to this session. Run 'dydo agent claim auto' first.";
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
            "reviewer" => "Reviewer role can only edit own workspace.",
            "code-writer" => "Code-writer role can only edit src/**, tests/**, and own workspace.",
            "co-thinker" => "Co-thinker role can edit own workspace and decisions.",
            "docs-writer" => "Docs-writer role can only edit dydo/** (except other agents' workspaces) and own workspace.",
            "interviewer" => "Interviewer role can only edit own workspace.",
            "planner" => "Planner role can only edit own workspace and tasks.",
            "tester" => "Tester role can edit own workspace, tests, and pitfalls.",
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
            return PathUtils.NormalizePath(relative);
        }
        return PathUtils.NormalizePath(path);
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

        // Format task role history for YAML
        var historyYaml = FormatTaskRoleHistory(state.TaskRoleHistory);

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
            task-role-history: {historyYaml}
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

    private static string FormatTaskRoleHistory(Dictionary<string, List<string>> history)
    {
        if (history.Count == 0)
            return "{}";

        // Format as compact JSON-like YAML: { "task1": ["role1", "role2"], "task2": ["role3"] }
        var entries = history.Select(kvp =>
            $"\"{kvp.Key}\": [{string.Join(", ", kvp.Value.Select(r => $"\"{r}\""))}]");
        return "{ " + string.Join(", ", entries) + " }";
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
                    case "task-role-history":
                        state.TaskRoleHistory = ParseTaskRoleHistory(value);
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

    private static Dictionary<string, List<string>> ParseTaskRoleHistory(string value)
    {
        var history = new Dictionary<string, List<string>>();

        if (string.IsNullOrEmpty(value) || value == "{}")
            return history;

        // Parse format: { "task1": ["role1", "role2"], "task2": ["role3"] }
        // Find all task entries: "taskName": ["role1", "role2"]
        var taskMatches = Regex.Matches(value, @"""([^""]+)""\s*:\s*\[(.*?)\]");
        foreach (Match match in taskMatches)
        {
            var taskName = match.Groups[1].Value;
            var rolesStr = match.Groups[2].Value;
            var roles = Regex.Matches(rolesStr, @"""([^""]+)""")
                .Select(m => m.Groups[1].Value)
                .ToList();
            history[taskName] = roles;
        }

        return history;
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
        name = name.Length > 1
            ? char.ToUpperInvariant(name[0]) + name[1..].ToLowerInvariant()
            : name.ToUpperInvariant();

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
        newName = newName.Length > 1
            ? char.ToUpperInvariant(newName[0]) + newName[1..].ToLowerInvariant()
            : newName.ToUpperInvariant();

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
        if (state?.Status != AgentStatus.Free && session != null)
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
        if (state?.Status != AgentStatus.Free && session != null)
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
        if (state?.Status != AgentStatus.Free && session != null)
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

    #region Lock File Support

    private record ClaimLock(int Pid, DateTime Acquired);

    [JsonSerializable(typeof(ClaimLock))]
    private partial class ClaimLockJsonContext : JsonSerializerContext { }

    private string GetLockFilePath(string agentName) =>
        Path.Combine(GetAgentWorkspace(agentName), ".claim.lock");

    /// <summary>
    /// Attempts to acquire an exclusive lock for claiming/releasing an agent.
    /// Handles stale locks from crashed processes.
    /// </summary>
    private bool TryAcquireLock(string agentName, out string error, int retryCount = 0)
    {
        error = string.Empty;
        var lockPath = GetLockFilePath(agentName);
        var workspace = GetAgentWorkspace(agentName);

        // Ensure workspace directory exists
        Directory.CreateDirectory(workspace);

        try
        {
            // Try to create lock file exclusively
            using var stream = new FileStream(
                lockPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);

            // Write our PID and timestamp
            var lockInfo = new ClaimLock(Environment.ProcessId, DateTime.UtcNow);
            var json = JsonSerializer.Serialize(lockInfo, ClaimLockJsonContext.Default.ClaimLock);
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            stream.Write(bytes, 0, bytes.Length);

            return true;
        }
        catch (IOException) when (File.Exists(lockPath))
        {
            // Lock file exists - check if it's stale
            if (retryCount > 0)
            {
                error = $"Could not acquire claim lock for agent {agentName}. Try again.";
                return false;
            }

            try
            {
                var existingJson = File.ReadAllText(lockPath);
                var existingLock = JsonSerializer.Deserialize(existingJson, ClaimLockJsonContext.Default.ClaimLock);

                if (existingLock != null && ProcessUtils.IsProcessRunning(existingLock.Pid))
                {
                    error = $"Agent {agentName} claim in progress by another process (PID {existingLock.Pid}).";
                    return false;
                }

                // Stale lock - delete and retry once
                File.Delete(lockPath);
                return TryAcquireLock(agentName, out error, retryCount + 1);
            }
            catch (JsonException)
            {
                // Corrupt lock file - treat as stale, delete and retry
                // If delete fails, retry will handle it (either succeeds or fails with proper error)
                try { File.Delete(lockPath); } catch (IOException) { }
                return TryAcquireLock(agentName, out error, retryCount + 1);
            }
            catch (IOException)
            {
                // Another process grabbed it while we were checking
                error = $"Could not acquire claim lock for agent {agentName}. Try again.";
                return false;
            }
        }
        catch (Exception ex)
        {
            error = $"Failed to acquire lock for agent {agentName}: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Releases the lock file for an agent.
    /// </summary>
    private void ReleaseLock(string agentName)
    {
        var lockPath = GetLockFilePath(agentName);

        // Retry deletion - another process might briefly have the file open for reading
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (File.Exists(lockPath))
                    File.Delete(lockPath);
                return; // Success or file doesn't exist
            }
            catch (IOException) when (attempt < 4)
            {
                // File might be locked by another process reading it, wait briefly
                Thread.Sleep(20);
            }
            catch
            {
                // Other errors - give up, lock will be detected as stale later
                return;
            }
        }
    }

    #endregion

    #region Audit Logging

    /// <summary>
    /// Helper to log lifecycle events (claim, release, role) with proper error handling.
    /// </summary>
    private void LogLifecycleEvent(string? sessionId, AuditEvent @event, string? agentName, string? human, ProjectSnapshot? snapshot = null)
    {
        if (string.IsNullOrEmpty(sessionId))
            return;

        try
        {
            _auditService.LogEvent(sessionId, @event, agentName, human, snapshot);
        }
        catch
        {
            // Audit logging should never break agent operations
            // Silently ignore errors
        }
    }

    #endregion

    #region Workspace Archiving

    private static readonly HashSet<string> SystemManagedEntries = new(StringComparer.OrdinalIgnoreCase)
    {
        "workflow.md", "state.md", ".session", ".pending-session", ".claim.lock", "modes", "archive", "inbox"
    };

    /// <summary>
    /// Archives non-system files from a workspace into archive/{timestamp}/.
    /// Returns the snapshot path, or null if nothing to archive.
    /// </summary>
    public static string? ArchiveWorkspace(string workspace)
    {
        if (!Directory.Exists(workspace))
            return null;

        var entries = Directory.GetFileSystemEntries(workspace)
            .Where(e => !SystemManagedEntries.Contains(Path.GetFileName(e)))
            .ToList();

        if (entries.Count == 0)
            return null;

        var snapshotName = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var snapshotPath = Path.Combine(workspace, "archive", snapshotName);
        Directory.CreateDirectory(snapshotPath);

        foreach (var entry in entries)
        {
            var name = Path.GetFileName(entry);
            var dest = Path.Combine(snapshotPath, name);

            if (File.Exists(entry))
                File.Move(entry, dest);
            else if (Directory.Exists(entry))
                Directory.Move(entry, dest);
        }

        return snapshotPath;
    }

    /// <summary>
    /// Prunes the archive directory so total files across all snapshots stays within maxFiles.
    /// Deletes oldest snapshots first.
    /// </summary>
    public static void PruneArchive(string workspace, int maxFiles = 30)
    {
        var archivePath = Path.Combine(workspace, "archive");
        if (!Directory.Exists(archivePath))
            return;

        var snapshots = Directory.GetDirectories(archivePath)
            .Where(d => !string.Equals(Path.GetFileName(d), "inbox", StringComparison.OrdinalIgnoreCase))
            .OrderBy(d => Path.GetFileName(d))
            .ToList();

        if (snapshots.Count == 0)
            return;

        var totalFiles = snapshots.Sum(CountFilesRecursive);

        while (totalFiles > maxFiles && snapshots.Count > 0)
        {
            var oldest = snapshots[0];
            totalFiles -= CountFilesRecursive(oldest);
            Directory.Delete(oldest, recursive: true);
            snapshots.RemoveAt(0);
        }
    }

    private static int CountFilesRecursive(string directory)
    {
        try
        {
            return Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Length;
        }
        catch
        {
            return 0;
        }
    }

    #endregion
}
