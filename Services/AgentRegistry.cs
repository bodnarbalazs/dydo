namespace DynaDocs.Services;

using System.Text.RegularExpressions;
using DynaDocs.Models;
using DynaDocs.Utils;

/// <summary>
/// Read-only view over the agent workspaces and project config that the surviving KEEP code
/// needs (guard nudges/git-safety, worktree status/merge/cleanup/prune, validation). The
/// claim / roster / identity / session / wait / message / resume machinery was carved out in
/// the 2.1.0 simplification campaign (DR-041): identity is assigned at spawn now, not claimed,
/// so nothing writes agent state at runtime — this type only enumerates workspaces and parses
/// any state.md files that already exist.
/// </summary>
public class AgentRegistry
{
    private readonly string _basePath;
    private readonly IConfigService _configService;
    private readonly DydoConfig? _config;

    public AgentRegistry(string? basePath = null, IConfigService? configService = null)
    {
        _basePath = basePath ?? PathUtils.FindProjectRoot() ?? Environment.CurrentDirectory;
        _configService = configService ?? new ConfigService();
        _config = _configService.LoadConfig(_basePath);
    }

    public DydoConfig? Config => _config;

    public IReadOnlyList<string> AgentNames =>
        _config?.Agents.Pool ?? PresetAgentNames.Set1.ToList();

    public string WorkspacePath =>
        _configService.GetAgentsPath(_basePath);

    public string GetAgentWorkspace(string agentName) =>
        Path.Combine(WorkspacePath, agentName);

    public string? GetWorktreeId(string agentName)
    {
        var marker = Path.Combine(GetAgentWorkspace(agentName), ".worktree");
        return File.Exists(marker) ? File.ReadAllText(marker).Trim() : null;
    }

    public string? GetHumanForAgent(string agentName) =>
        _config?.Agents.GetHumanForAgent(agentName);

    public bool IsValidAgentName(string name) =>
        AgentNames.Contains(name, StringComparer.OrdinalIgnoreCase);

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

    public List<AgentState> GetAllAgentStates() =>
        AgentNames.Select(name => GetAgentState(name) ?? new AgentState { Name = name }).ToList();

    private static readonly Dictionary<string, Action<AgentState, string>> StateFieldParsers = new()
    {
        ["role"] = (s, v) => s.Role = NullableString(v),
        ["task"] = (s, v) => s.Task = NullableString(v),
        ["status"] = (s, v) => s.Status = ParseStatus(v),
        ["assigned"] = (s, v) => s.AssignedHuman = v is "unassigned" or "null" ? null : v,
        ["dispatched-by"] = (s, v) => s.DispatchedBy = NullableString(v),
        ["dispatched-by-role"] = (s, v) => s.DispatchedByRole = NullableString(v),
        ["window-id"] = (s, v) => s.WindowId = NullableString(v),
        ["auto-close"] = (s, v) => s.AutoClose = v == "true",
        ["needs-human"] = (s, v) => s.NeedsHuman = v == "true",
        ["needs-human-source"] = (s, v) => s.NeedsHumanSource = v switch
        {
            "derived" => Models.NeedsHumanSource.Derived,
            "explicit" => Models.NeedsHumanSource.Explicit,
            _ => null
        },
        ["started"] = (s, v) => { if (v != "null" && DateTime.TryParse(v, out var dt)) s.Since = dt; },
        ["writable-paths"] = (s, v) => s.WritablePaths = ParsePathList(v),
        ["readonly-paths"] = (s, v) => s.ReadOnlyPaths = ParsePathList(v),
        ["unread-must-reads"] = (s, v) => s.UnreadMustReads = ParsePathList(v),
        ["task-role-history"] = (s, v) => s.TaskRoleHistory = ParseTaskRoleHistory(v),
    };

    private static string? NullableString(string value) => value == "null" ? null : value;

    private static AgentStatus ParseStatus(string value) => value switch
    {
        "dispatched" => AgentStatus.Dispatched,
        "working" => AgentStatus.Working,
        "reviewing" => AgentStatus.Reviewing,
        _ => AgentStatus.Free
    };

    private AgentState? ParseStateFile(string agentName, string statePath)
    {
        try
        {
            var content = FileReadRetry.Read(statePath);
            if (content == null)
                return new AgentState { Name = agentName };

            var rawFields = FrontmatterParser.ParseFields(content);
            if (rawFields == null)
                return new AgentState { Name = agentName };

            var state = new AgentState { Name = agentName };

            foreach (var (key, value) in rawFields)
            {
                if (StateFieldParsers.TryGetValue(key, out var parser))
                    parser(state, value);
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
