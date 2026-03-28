namespace DynaDocs.Services;

using System.Text.RegularExpressions;
using DynaDocs.Models;

public class AgentStateStore
{
    private readonly Func<string, string> _getAgentWorkspace;
    private readonly Func<string, string?> _getHumanForAgent;
    private readonly IReadOnlyList<string> _agentNames;

    public AgentStateStore(
        Func<string, string> getAgentWorkspace,
        Func<string, string?> getHumanForAgent,
        IReadOnlyList<string> agentNames)
    {
        _getAgentWorkspace = getAgentWorkspace;
        _getHumanForAgent = getHumanForAgent;
        _agentNames = agentNames;
    }

    public AgentState? GetAgentState(string agentName, Func<string, bool> isValidName)
    {
        if (!isValidName(agentName))
            return null;

        var statePath = Path.Combine(_getAgentWorkspace(agentName), "state.md");
        if (!File.Exists(statePath))
        {
            return new AgentState
            {
                Name = agentName,
                Status = AgentStatus.Free,
                AssignedHuman = _getHumanForAgent(agentName)
            };
        }

        return ParseStateFile(agentName, statePath);
    }

    public List<AgentState> GetAllAgentStates(Func<string, bool> isValidName)
    {
        return _agentNames.Select(name => GetAgentState(name, isValidName) ?? new AgentState { Name = name }).ToList();
    }

    public void UpdateAgentState(string agentName, Action<AgentState> update, Func<string, bool> isValidName)
    {
        var state = GetAgentState(agentName, isValidName) ?? new AgentState { Name = agentName };
        update(state);
        WriteStateFile(agentName, state);
    }

    public void SetDispatchMetadata(string agentName, string? windowId, bool autoClose, Func<string, bool> isValidName)
    {
        UpdateAgentState(agentName, s =>
        {
            s.WindowId = windowId;
            s.AutoClose = autoClose;
        }, isValidName);
    }

    public void WriteStateFile(string agentName, AgentState state)
    {
        var workspace = _getAgentWorkspace(agentName);
        Directory.CreateDirectory(workspace);

        var historyYaml = FormatTaskRoleHistory(state.TaskRoleHistory);

        var statePath = Path.Combine(workspace, "state.md");
        var content = $"""
            ---
            agent: {agentName}
            role: {state.Role ?? "null"}
            task: {state.Task ?? "null"}
            status: {state.Status.ToString().ToLowerInvariant()}
            assigned: {state.AssignedHuman ?? _getHumanForAgent(agentName) ?? "unassigned"}
            dispatched-by: {state.DispatchedBy ?? "null"}
            dispatched-by-role: {state.DispatchedByRole ?? "null"}
            window-id: {state.WindowId ?? "null"}
            auto-close: {state.AutoClose.ToString().ToLowerInvariant()}
            started: {(state.Since.HasValue ? state.Since.Value.ToString("o") : "null")}
            writable-paths: [{string.Join(", ", state.WritablePaths.Select(p => $"\"{p}\""))}]
            readonly-paths: [{string.Join(", ", state.ReadOnlyPaths.Select(p => $"\"{p}\""))}]
            unread-must-reads: [{string.Join(", ", state.UnreadMustReads.Select(p => $"\"{p}\""))}]
            unread-messages: [{string.Join(", ", state.UnreadMessages.Select(p => $"\"{p}\""))}]
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

    public AgentState? ParseStateFile(string agentName, string statePath)
    {
        try
        {
            var content = AgentSessionManager.FileReadWithRetry(statePath);
            if (content == null)
                return new AgentState { Name = agentName };
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

                ApplyStateField(state, key, value);
            }

            return state;
        }
        catch
        {
            return new AgentState { Name = agentName };
        }
    }

    private static readonly Dictionary<string, Action<AgentState, string>> FieldAppliers = new()
    {
        ["role"] = (s, v) => s.Role = NullIfNull(v),
        ["task"] = (s, v) => s.Task = NullIfNull(v),
        ["status"] = (s, v) => s.Status = ParseStatus(v),
        ["assigned"] = (s, v) => s.AssignedHuman = v is "unassigned" or "null" ? null : v,
        ["dispatched-by"] = (s, v) => s.DispatchedBy = NullIfNull(v),
        ["dispatched-by-role"] = (s, v) => s.DispatchedByRole = NullIfNull(v),
        ["window-id"] = (s, v) => s.WindowId = NullIfNull(v),
        ["auto-close"] = (s, v) => s.AutoClose = v == "true",
        ["started"] = (s, v) => { if (v != "null" && DateTime.TryParse(v, out var dt)) s.Since = dt; },
        ["writable-paths"] = (s, v) => s.WritablePaths = ParsePathList(v),
        ["readonly-paths"] = (s, v) => s.ReadOnlyPaths = ParsePathList(v),
        ["unread-must-reads"] = (s, v) => s.UnreadMustReads = ParsePathList(v),
        ["unread-messages"] = (s, v) => s.UnreadMessages = ParsePathList(v),
        ["task-role-history"] = (s, v) => s.TaskRoleHistory = ParseTaskRoleHistory(v),
    };

    private static void ApplyStateField(AgentState state, string key, string value)
    {
        if (FieldAppliers.TryGetValue(key, out var applier))
            applier(state, value);
    }

    private static string? NullIfNull(string value) => value == "null" ? null : value;

    private static AgentStatus ParseStatus(string value) => value switch
    {
        "dispatched" => AgentStatus.Dispatched,
        "queued" => AgentStatus.Queued,
        "working" => AgentStatus.Working,
        "reviewing" => AgentStatus.Reviewing,
        _ => AgentStatus.Free
    };

    public static Dictionary<string, List<string>> ParseTaskRoleHistory(string value)
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

    public static List<string> ParsePathList(string value)
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

    public static string FormatTaskRoleHistory(Dictionary<string, List<string>> history)
    {
        if (history.Count == 0)
            return "{}";

        var entries = history.Select(kvp =>
            $"\"{kvp.Key}\": [{string.Join(", ", kvp.Value.Select(r => $"\"{r}\""))}]");
        return "{ " + string.Join(", ", entries) + " }";
    }
}
