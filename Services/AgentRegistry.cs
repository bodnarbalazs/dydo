namespace DynaDocs.Services;

using System.Text.Json;
using System.Text.RegularExpressions;
using DynaDocs.Models;

public class AgentRegistry : IAgentRegistry
{
    private static readonly Dictionary<char, string> AgentMap = new()
    {
        ['A'] = "Adele", ['B'] = "Brian", ['C'] = "Charlie", ['D'] = "Dexter",
        ['E'] = "Emma", ['F'] = "Frank", ['G'] = "Grace", ['H'] = "Henry",
        ['I'] = "Iris", ['J'] = "Jack", ['K'] = "Kate", ['L'] = "Leo",
        ['M'] = "Mia", ['N'] = "Noah", ['O'] = "Olivia", ['P'] = "Paul",
        ['Q'] = "Quinn", ['R'] = "Rose", ['S'] = "Sam", ['T'] = "Tara",
        ['U'] = "Uma", ['V'] = "Victor", ['W'] = "Wendy", ['X'] = "Xavier",
        ['Y'] = "Yara", ['Z'] = "Zack"
    };

    private static readonly Dictionary<string, (List<string> Allowed, List<string> Denied)> RolePermissions = new()
    {
        ["code-writer"] = (["src/**", "tests/**"], ["docs/**", "project/**"]),
        ["reviewer"] = ([], ["**"]),
        ["docs-writer"] = (["docs/**"], ["src/**", "tests/**", "project/**"]),
        ["interviewer"] = ([".workspace/{self}/**"], ["**"]),
        ["planner"] = ([".workspace/{self}/**", "project/tasks/**"], ["src/**", "docs/**"])
    };

    private readonly string _basePath;

    public AgentRegistry(string? basePath = null)
    {
        _basePath = basePath ?? Environment.CurrentDirectory;
    }

    public IReadOnlyList<string> AgentNames => AgentMap.Values.ToList();

    public string WorkspacePath => Path.Combine(_basePath, ".workspace");

    public string GetAgentWorkspace(string agentName) =>
        Path.Combine(WorkspacePath, agentName);

    public bool ClaimAgent(string agentName, out string error)
    {
        error = string.Empty;

        if (!IsValidAgentName(agentName))
        {
            error = $"Invalid agent name: {agentName}";
            return false;
        }

        var state = GetAgentState(agentName);
        if (state?.Status != AgentStatus.Free)
        {
            var session = GetSession(agentName);
            if (session != null && ProcessUtils.IsProcessRunning(session.TerminalPid))
            {
                error = $"Agent {agentName} is already claimed by terminal PID {session.TerminalPid}";
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
        });

        return true;
    }

    public bool ReleaseAgent(out string error)
    {
        error = string.Empty;

        var agent = GetCurrentAgent();
        if (agent == null)
        {
            error = "No agent claimed for this terminal";
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
            error = "No agent claimed for this terminal";
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
                Status = AgentStatus.Free
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
            error = "No agent claimed for this terminal";
            return false;
        }

        if (string.IsNullOrEmpty(agent.Role))
        {
            error = $"Agent {agent.Name} has no role set";
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
                    error = $"Agent {agent.Name} ({agent.Role}) cannot {action} {relativePath}";
                    return false;
                }
            }
        }

        // If no allowed paths, nothing is allowed
        if (agent.AllowedPaths.Count == 0)
        {
            error = $"Agent {agent.Name} ({agent.Role}) has no write permissions";
            return false;
        }

        // Check if path matches any allowed pattern
        var allowed = agent.AllowedPaths.Any(pattern => MatchesGlob(relativePath, pattern));
        if (!allowed)
        {
            error = $"Agent {agent.Name} ({agent.Role}) cannot {action} {relativePath}";
            return false;
        }

        return true;
    }

    public bool IsValidAgentName(string name) =>
        AgentNames.Contains(name, StringComparer.OrdinalIgnoreCase);

    public string? GetAgentNameFromLetter(char letter)
    {
        letter = char.ToUpperInvariant(letter);
        return AgentMap.TryGetValue(letter, out var name) ? name : null;
    }

    private string GetRelativePath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            var relative = Path.GetRelativePath(_basePath, path);
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
}
