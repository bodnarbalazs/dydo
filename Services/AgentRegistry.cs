namespace DynaDocs.Services;

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DynaDocs.Models;
using DynaDocs.Serialization;
using DynaDocs.Utils;

public partial class AgentRegistry : IAgentRegistry
{
    private const int StaleDispatchMinutes = 2;

    private readonly string _basePath;
    private readonly IConfigService _configService;
    private readonly IFolderScaffolder _folderScaffolder;
    private readonly IAuditService _auditService;
    private readonly DydoConfig? _config;
    private readonly Dictionary<string, (List<string> Writable, List<string> ReadOnly)> _rolePermissions;
    private readonly Dictionary<string, RoleDefinition> _roleDefinitions;
    private readonly InboxMetadataReader _inboxReader;

    public AgentRegistry(string? basePath = null, IConfigService? configService = null, IFolderScaffolder? folderScaffolder = null, IAuditService? auditService = null)
    {
        _basePath = basePath ?? Environment.CurrentDirectory;
        _configService = configService ?? new ConfigService();
        _folderScaffolder = folderScaffolder ?? new FolderScaffolder();
        _auditService = auditService ?? new AuditService(_configService, _basePath);
        _config = _configService.LoadConfig(_basePath);
        _inboxReader = new InboxMetadataReader(GetAgentWorkspace);

        var roleDefService = new RoleDefinitionService();
        var roles = roleDefService.LoadRoleDefinitions(_basePath);

        if (roles.Count > 0)
        {
            var pathSets = roleDefService.ResolvePathSets(_config);
            _rolePermissions = roleDefService.BuildPermissionMap(roles, pathSets);
            _roleDefinitions = roles.ToDictionary(r => r.Name);
        }
        else
        {
            // No role files on disk — use built-in base definitions with a warning
            Console.Error.WriteLine("[dydo] WARNING: No role files found at dydo/_system/roles/. Run 'dydo roles reset' to generate them. Using built-in defaults.");
            var baseRoles = RoleDefinitionService.GetBaseRoleDefinitions();
            var pathSets = roleDefService.ResolvePathSets(_config);
            _rolePermissions = roleDefService.BuildPermissionMap(baseRoles, pathSets);
            _roleDefinitions = baseRoles.ToDictionary(r => r.Name);
        }
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

    public bool IsWorktreeStale(string worktreeId) =>
        !Directory.Exists(Path.Combine(_basePath, "_system", ".local", "worktrees", worktreeId));

    public static string TruncateWorktreeId(string worktreeId)
    {
        // Format: AgentName-yyyyMMddHHmmss → AgentName-MMDD
        var dashIdx = worktreeId.LastIndexOf('-');
        if (dashIdx < 0 || worktreeId.Length - dashIdx - 1 < 8)
            return worktreeId;
        var timestamp = worktreeId[(dashIdx + 1)..];
        return worktreeId[..dashIdx] + "-" + timestamp[4..8];
    }

    public bool HasPendingInbox(string agentName)
    {
        var inboxPath = Path.Combine(GetAgentWorkspace(agentName), "inbox");
        return Directory.Exists(inboxPath) && Directory.GetFiles(inboxPath, "*.md").Length > 0;
    }

    public string? GetCurrentHuman() =>
        _configService.GetHumanFromEnv();

    public string? GetHumanForAgent(string agentName) =>
        _config?.Agents.GetHumanForAgent(agentName);

    public List<string> GetAgentsForHuman(string human) =>
        _config?.Agents.GetAgentsForHuman(human) ?? new List<string>();

    public bool ReserveAgent(string agentName, out string error)
    {
        error = string.Empty;

        if (!IsValidAgentName(agentName))
        {
            error = $"Agent '{agentName}' does not exist.";
            return false;
        }

        if (!TryAcquireLock(agentName, out error))
            return false;

        try
        {
            var state = GetAgentState(agentName);
            if (state == null)
            {
                error = $"Agent '{agentName}' not found.";
                return false;
            }

            if (!IsEffectivelyFree(state))
            {
                error = $"Agent '{agentName}' is not free (status: {state.Status.ToString().ToLowerInvariant()}).";
                return false;
            }

            UpdateAgentState(agentName, s =>
            {
                s.Status = AgentStatus.Dispatched;
                s.Since = DateTime.UtcNow;
            });

            return true;
        }
        finally
        {
            ReleaseLock(agentName);
        }
    }

    private bool IsEffectivelyFree(AgentState state) =>
        state.Status == AgentStatus.Free ||
        (state.Status == AgentStatus.Dispatched && IsStaleDispatch(state));

    private static bool IsStaleDispatch(AgentState state) =>
        state.Status == AgentStatus.Dispatched &&
        state.Since.HasValue &&
        (DateTime.UtcNow - state.Since.Value.ToUniversalTime()).TotalMinutes > StaleDispatchMinutes;

    public bool ClaimAgent(string agentName, out string error)
    {
        error = string.Empty;

        if (!IsValidAgentName(agentName))
        {
            error = $"Invalid agent name: {agentName}";
            return false;
        }

        var sessionId = ResolveSessionId(agentName);
        if (string.IsNullOrEmpty(sessionId))
        {
            error = "No session ID available. Claim must be initiated via hook.";
            return false;
        }

        if (!TryAcquireLock(agentName, out error))
            return false;

        try
        {
            var human = GetCurrentHuman();
            if (!ValidateClaimPreconditions(agentName, sessionId, human, out error))
                return false;

            var state = GetAgentState(agentName);
            var existingSession = GetSession(agentName);
            if (!HandleExistingSession(agentName, state, existingSession, sessionId, human, out error))
                return false;

            // HandleExistingSession returns true for both "proceed" and "idempotent reclaim";
            // if the error string is empty and we got a match, it was idempotent.
            if (existingSession?.SessionId == sessionId &&
                state?.Status != AgentStatus.Free && state?.Status != AgentStatus.Dispatched)
                return true;

            SetupAgentWorkspace(agentName, sessionId, human, state?.Status == AgentStatus.Dispatched);
            return true;
        }
        finally
        {
            ReleaseLock(agentName);
        }
    }

    private string? ResolveSessionId(string agentName)
    {
        var sessionId = GetPendingSessionId(agentName);
        if (string.IsNullOrEmpty(sessionId))
            sessionId = GetSessionContext();
        return sessionId;
    }

    private bool ValidateClaimPreconditions(string agentName, string sessionId, string? human, out string error)
    {
        error = string.Empty;

        var (canClaim, claimError) = _configService.ValidateAgentClaim(agentName, human, _config);
        if (!canClaim)
        {
            error = claimError!;
            return false;
        }

        var existingAgent = GetCurrentAgent(sessionId);
        if (existingAgent != null && existingAgent.Name != agentName)
        {
            error = $"This session already has agent {existingAgent.Name} claimed. Release first.";
            return false;
        }

        return true;
    }

    private bool HandleExistingSession(string agentName, AgentState? state, AgentSession? existingSession,
        string sessionId, string? human, out string error)
    {
        error = string.Empty;

        if (state?.Status == AgentStatus.Free || state?.Status == AgentStatus.Dispatched || existingSession == null)
            return true;

        if (existingSession.SessionId == sessionId)
            return true;

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

    private void SetupAgentWorkspace(string agentName, string sessionId, string? human, bool wasDispatched)
    {
        var workspace = GetAgentWorkspace(agentName);
        Directory.CreateDirectory(workspace);

        try { ArchiveWorkspace(workspace); PruneArchive(workspace); }
        catch { /* Archive failure should not block agent claim */ }

        _folderScaffolder.CopyBuiltInTemplates(_configService.GetDydoRoot(_basePath));
        _folderScaffolder.RegenerateAgentFiles(WorkspacePath, agentName,
            _config?.Paths.Source, _config?.Paths.Tests);

        var session = new AgentSession
        {
            Agent = agentName,
            SessionId = sessionId,
            Claimed = DateTime.UtcNow
        };

        var sessionPath = Path.Combine(workspace, ".session");
        File.WriteAllText(sessionPath, JsonSerializer.Serialize(session, DydoDefaultJsonContext.Default.AgentSession));

        UpdateAgentState(agentName, s =>
        {
            s.Status = AgentStatus.Working;
            s.Since = DateTime.UtcNow;
            s.AssignedHuman = human;
            if (!wasDispatched)
            {
                s.WindowId = null;
                s.AutoClose = false;
            }
        });

        ProjectSnapshot? snapshot = null;
        try
        {
            var snapshotService = new SnapshotService(_configService);
            snapshot = snapshotService.CaptureSnapshot(_basePath);
        }
        catch { /* Snapshot failure should not block agent claim */ }

        LogLifecycleEvent(sessionId, new AuditEvent
        {
            EventType = AuditEventType.Claim,
            AgentName = agentName
        }, agentName, human, snapshot);

        try { File.WriteAllText(GetAgentHintPath(), agentName); } catch { }

        try
        {
            var nudgePath = Path.Combine(WorkspacePath, $".claim-nudge-{sessionId}");
            if (File.Exists(nudgePath)) File.Delete(nudgePath);
        }
        catch { }
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

        // Nudge: dispatched agents with inbox items suggest this terminal was launched
        // for a specific agent — the agent should claim by name, not auto.
        var sessionId = GetSessionContext();
        if (!string.IsNullOrEmpty(sessionId))
        {
            var humanAgents = GetAgentsForHuman(human);
            var hasDispatchedWithInbox = GetAllAgentStates()
                .Any(a => a.Status == AgentStatus.Dispatched
                    && humanAgents.Contains(a.Name, StringComparer.OrdinalIgnoreCase)
                    && HasPendingInbox(a.Name));

            if (hasDispatchedWithInbox)
            {
                var markerPath = Path.Combine(WorkspacePath, $".claim-nudge-{sessionId}");
                if (!File.Exists(markerPath))
                {
                    File.WriteAllText(markerPath, DateTime.UtcNow.ToString("o"));
                    error = "There are dispatched agents waiting to be claimed. " +
                            "Check your prompt for your agent name — " +
                            "'dydo agent claim auto' is probably not meant for you. " +
                            "If you intentionally want auto-assignment, run the command again.";
                    return false;
                }
                File.Delete(markerPath);
            }
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

        if (!TryAcquireLock(agent.Name, out error))
            return false;

        try
        {
            var workspace = GetAgentWorkspace(agent.Name);

            if (!ValidateReleasePreconditions(agent.Name, workspace, out error))
                return false;

            var sessionPath = Path.Combine(workspace, ".session");
            if (File.Exists(sessionPath))
                File.Delete(sessionPath);

            try { var hintPath = GetAgentHintPath(); if (File.Exists(hintPath)) File.Delete(hintPath); } catch { }

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
                s.WritablePaths = [];
                s.ReadOnlyPaths = [];
                s.UnreadMustReads = [];
                s.UnreadMessages = [];
            });

            CleanupAfterRelease(agent.Name, workspace, sessionId);
            return true;
        }
        finally
        {
            ReleaseLock(agent.Name);
        }
    }

    private bool ValidateReleasePreconditions(string agentName, string workspace, out string error)
    {
        error = string.Empty;

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

        var waitMarkers = GetWaitMarkers(agentName);
        if (waitMarkers.Count > 0)
        {
            var tasks = string.Join(", ", waitMarkers.Select(m => m.Task));
            error = $"Cannot release: waiting for response on: {tasks}.\n" +
                    "Cancel with: dydo wait --task <name> --cancel";
            return false;
        }

        var replyMarkers = GetReplyPendingMarkers(agentName);
        if (replyMarkers.Count > 0)
        {
            var pending = string.Join(", ", replyMarkers.Select(m => $"'{m.Task}' to {m.To}"));
            error = $"Cannot release: pending reply on: {pending}.\n" +
                    "Send a message first: dydo msg --to <agent> --subject <task> --body \"...\"";
            return false;
        }

        // Data-driven release constraints (requires-dispatch)
        var state = GetAgentState(agentName);
        if (state != null && !string.IsNullOrEmpty(state.Role) && !string.IsNullOrEmpty(state.Task))
        {
            var evaluator = new RoleConstraintEvaluator(_roleDefinitions, AgentNames, GetAgentState);
            if (!evaluator.CanRelease(agentName, state.Role, state.Task,
                !string.IsNullOrEmpty(state.DispatchedBy),
                (t, r) => HasDispatchMarker(agentName, t, r), out error))
            {
                return false;
            }
        }

        var needsMergePath = Path.Combine(workspace, ".needs-merge");
        if (File.Exists(needsMergePath))
        {
            var mergeTask = File.ReadAllText(needsMergePath).Trim();
            error = $"Cannot release: review passed in worktree but merge not dispatched.\n" +
                    $"Dispatch a code-writer to merge the worktree branch:\n" +
                    $"  dydo dispatch --no-wait --role code-writer --task {mergeTask}-merge --brief \"Merge worktree branch\"";
            return false;
        }

        return true;
    }

    private void CleanupAfterRelease(string agentName, string workspace, string? sessionId)
    {
        var modesPath = Path.Combine(workspace, "modes");
        if (Directory.Exists(modesPath))
            Directory.Delete(modesPath, true);

        ClearAllWaitMarkers(agentName);
        ClearAllReplyPendingMarkers(agentName);
        ClearAllDispatchMarkers(agentName);

        try { new GuardLiftService().ClearLift(agentName); } catch { }

        foreach (var marker in Directory.GetFiles(workspace, ".role-nudge-*"))
            File.Delete(marker);

        foreach (var marker in Directory.GetFiles(workspace, ".no-launch-nudge-*"))
            File.Delete(marker);

        if (!string.IsNullOrEmpty(sessionId))
        {
            var claimNudgePath = Path.Combine(WorkspacePath, $".claim-nudge-{sessionId}");
            try { if (File.Exists(claimNudgePath)) File.Delete(claimNudgePath); } catch { }
        }
    }

    private string? GetDispatchedRole(string agentName, string task) =>
        _inboxReader.GetDispatchedRole(agentName, task);

    private string? GetDispatchedFrom(string agentName, string task) =>
        _inboxReader.GetDispatchedFrom(agentName, task);

    private string? GetDispatchedFromRole(string agentName, string task) =>
        _inboxReader.GetDispatchedFromRole(agentName, task);

    public bool SetRole(string? sessionId, string role, string? task, out string error)
    {
        error = string.Empty;

        var agent = GetCurrentAgent(sessionId);
        if (agent == null)
        {
            error = "No agent identity assigned to this session. Run 'dydo agent claim auto' first.";
            return false;
        }

        if (!_rolePermissions.ContainsKey(role))
        {
            error = $"Invalid role: {role}. Valid roles: {string.Join(", ", _rolePermissions.Keys)}";
            return false;
        }

        if (!string.IsNullOrEmpty(task) && !CanTakeRole(agent.Name, role, task, out var reason))
        {
            error = reason;
            return false;
        }

        if (!string.IsNullOrEmpty(task) && !HandleRoleNudge(agent.Name, task, role, out error))
            return false;

        var (writable, readOnly) = _rolePermissions[role];
        writable = writable.Select(p => p.Replace("{self}", agent.Name)).ToList();
        readOnly = readOnly.Select(p => p.Replace("{self}", agent.Name)).ToList();

        var mustReads = ComputeUnreadMustReads(agent.Name, role, sessionId);
        var dispatchedFrom = !string.IsNullOrEmpty(task) ? GetDispatchedFrom(agent.Name, task) : null;
        var dispatchedFromRole = !string.IsNullOrEmpty(task) ? GetDispatchedFromRole(agent.Name, task) : null;

        UpdateAgentState(agent.Name, s =>
        {
            s.Role = role;
            s.Task = task;
            s.WritablePaths = writable;
            s.ReadOnlyPaths = readOnly;
            s.UnreadMustReads = mustReads;
            s.DispatchedBy = dispatchedFrom;
            s.DispatchedByRole = dispatchedFromRole;

            if (!string.IsNullOrEmpty(task))
            {
                if (!s.TaskRoleHistory.ContainsKey(task))
                    s.TaskRoleHistory[task] = new List<string>();
                if (!s.TaskRoleHistory[task].Contains(role))
                    s.TaskRoleHistory[task].Add(role);
            }
        });

        if (!string.IsNullOrEmpty(task))
            AutoCreateTaskFile(task, agent.Name);

        var human = GetCurrentHuman();
        LogLifecycleEvent(sessionId, new AuditEvent
        {
            EventType = AuditEventType.Role,
            Role = role,
            Task = task
        }, agent.Name, human);

        return true;
    }

    private bool HandleRoleNudge(string agentName, string task, string role, out string error)
    {
        error = string.Empty;
        var dispatchedRole = GetDispatchedRole(agentName, task);
        var markerPath = Path.Combine(GetAgentWorkspace(agentName), $".role-nudge-{PathUtils.SanitizeForFilename(task)}");

        if (dispatchedRole != null && !string.Equals(dispatchedRole, role, StringComparison.OrdinalIgnoreCase))
        {
            var state = GetAgentState(agentName);
            var alreadyFulfilled = state != null
                && state.TaskRoleHistory.TryGetValue(task, out var history)
                && history.Contains(dispatchedRole, StringComparer.OrdinalIgnoreCase);

            if (!alreadyFulfilled)
            {
                if (!File.Exists(markerPath))
                {
                    File.WriteAllText(markerPath, role);
                    error = $"You were dispatched as '{dispatchedRole}' for this task. If '{role}' fits better, run the command again.";
                    return false;
                }
                File.Delete(markerPath);
            }
            else if (File.Exists(markerPath))
            {
                File.Delete(markerPath);
            }
        }
        else if (File.Exists(markerPath))
        {
            File.Delete(markerPath);
        }

        return true;
    }

    private void AutoCreateTaskFile(string task, string agentName)
    {
        try
        {
            var tasksPath = _configService.GetTasksPath(_basePath);
            var taskFilePath = Path.Combine(tasksPath, $"{task}.md");
            if (!File.Exists(taskFilePath))
            {
                Directory.CreateDirectory(tasksPath);
                var content = $"""
                    ---
                    area: general
                    name: {task}
                    status: pending
                    created: {DateTime.UtcNow:o}
                    assigned: {agentName}
                    ---

                    # Task: {task}

                    (No description)

                    ## Progress

                    - [ ] (Not started)

                    ## Files Changed

                    (None yet)

                    ## Review Summary

                    (Pending)
                    """;
                File.WriteAllText(taskFilePath, content);
            }
        }
        catch
        {
            // Non-blocking: task file creation is a convenience side-effect
        }
    }

    /// <summary>
    /// Checks if an agent can take a specific role on a task.
    /// Delegates to RoleConstraintEvaluator for data-driven constraint evaluation.
    /// </summary>
    public bool CanTakeRole(string agentName, string role, string task, out string reason)
    {
        var evaluator = new RoleConstraintEvaluator(_roleDefinitions, AgentNames, GetAgentState);
        return evaluator.CanTakeRole(agentName, role, task, out reason);
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

    public List<AgentState> GetActiveAgents()
    {
        return GetAllAgentStates().Where(a => a.Status == AgentStatus.Working).ToList();
    }

    public List<AgentState> GetActiveOversightAgents()
    {
        return GetAllAgentStates()
            .Where(a => a.Status == AgentStatus.Working
                && !string.IsNullOrEmpty(a.Role)
                && _roleDefinitions.TryGetValue(a.Role, out var def)
                && def.CanOrchestrate)
            .ToList();
    }

    public List<AgentState> GetFreeAgents()
    {
        return GetAllAgentStates().Where(a => IsEffectivelyFree(a)).ToList();
    }

    public List<AgentState> GetFreeAgentsForHuman(string human)
    {
        var assignedAgents = GetAgentsForHuman(human);
        return GetAllAgentStates()
            .Where(a => IsEffectivelyFree(a) &&
                       assignedAgents.Contains(a.Name, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Gets the current agent for a given session ID.
    /// Uses a hint file to avoid scanning all agents when possible.
    /// </summary>
    public AgentState? GetCurrentAgent(string? sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
            return null;

        // Fastest path: check DYDO_AGENT env var
        var envAgent = Environment.GetEnvironmentVariable("DYDO_AGENT");
        if (!string.IsNullOrEmpty(envAgent) && IsValidAgentName(envAgent))
        {
            var envSession = GetSession(envAgent);
            if (envSession?.SessionId == sessionId)
                return GetAgentState(envAgent);
        }

        // Fast path: check agent hint file
        var hintPath = GetAgentHintPath();
        if (File.Exists(hintPath))
        {
            try
            {
                var hint = FileReadWithRetry(hintPath)?.Trim();
                if (!string.IsNullOrEmpty(hint) && IsValidAgentName(hint))
                {
                    var session = GetSession(hint);
                    if (session?.SessionId == sessionId)
                        return GetAgentState(hint);
                }
            }
            catch { }
        }

        // Slow path: scan all agents with timeout guard
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            foreach (var name in AgentNames)
            {
                cts.Token.ThrowIfCancellationRequested();
                var session = GetSession(name);
                if (session?.SessionId == sessionId)
                {
                    // Cache hint for next call
                    try { File.WriteAllText(hintPath, name); } catch { }
                    return GetAgentState(name);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("dydo whoami timed out — likely filesystem contention from concurrent agents. Try again.");
        }

        return null;
    }

    #region Session Context Support

    private string GetPendingSessionPath(string agentName) =>
        Path.Combine(GetAgentWorkspace(agentName), ".pending-session");

    private string GetSessionContextPath() =>
        Path.Combine(WorkspacePath, ".session-context");

    private string GetAgentHintPath() =>
        Path.Combine(WorkspacePath, ".session-agent");

    /// <summary>
    /// Reads a file with FileShare.ReadWrite and retry on IOException.
    /// Prevents concurrent readers/writers from blocking each other.
    /// </summary>
    private static string? FileReadWithRetry(string path, int maxRetries = 3)
    {
        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs);
                return sr.ReadToEnd();
            }
            catch (IOException)
            {
                if (attempt < maxRetries - 1)
                    Thread.Sleep(50 * (int)Math.Pow(3, attempt));
            }
            catch (UnauthorizedAccessException)
            {
                if (attempt < maxRetries - 1)
                    Thread.Sleep(50 * (int)Math.Pow(3, attempt));
            }
        }

        return null;
    }

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
            var sessionId = FileReadWithRetry(path)?.Trim();
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
        var agentName = Environment.GetEnvironmentVariable("DYDO_AGENT");
        if (!string.IsNullOrEmpty(agentName))
        {
            var session = GetSession(agentName);
            if (session != null) return session.SessionId;
        }

        var path = GetSessionContextPath();
        if (!File.Exists(path)) return null;

        try
        {
            return FileReadWithRetry(path)?.Trim();
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

    #endregion

    #region Wait Markers

    private string GetWaitingDir(string agentName) =>
        Path.Combine(GetAgentWorkspace(agentName), ".waiting");

    public void CreateWaitMarker(string agentName, string task, string targetAgent)
    {
        var dir = GetWaitingDir(agentName);
        Directory.CreateDirectory(dir);

        var marker = new WaitMarker
        {
            Target = targetAgent,
            Task = task,
            Since = DateTime.UtcNow
        };

        var sanitized = PathUtils.SanitizeForFilename(task);
        var path = Path.Combine(dir, $"{sanitized}.json");
        var json = JsonSerializer.Serialize(marker, DydoDefaultJsonContext.Default.WaitMarker);
        File.WriteAllText(path, json);
    }

    public List<WaitMarker> GetWaitMarkers(string agentName)
    {
        var dir = GetWaitingDir(agentName);
        if (!Directory.Exists(dir))
            return [];

        var markers = new List<WaitMarker>();
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var marker = JsonSerializer.Deserialize(json, DydoDefaultJsonContext.Default.WaitMarker);
                if (marker != null)
                    markers.Add(marker);
            }
            catch { }
        }

        return markers;
    }

    public bool RemoveWaitMarker(string agentName, string task)
    {
        var dir = GetWaitingDir(agentName);
        if (!Directory.Exists(dir))
            return false;

        var sanitized = PathUtils.SanitizeForFilename(task);
        var path = Path.Combine(dir, $"{sanitized}.json");
        if (!File.Exists(path))
            return false;

        File.Delete(path);
        return true;
    }

    public void ClearAllWaitMarkers(string agentName)
    {
        var dir = GetWaitingDir(agentName);
        if (Directory.Exists(dir))
            Directory.Delete(dir, true);
    }

    public bool UpdateWaitMarkerListening(string agentName, string task, int pid)
    {
        var dir = GetWaitingDir(agentName);
        var sanitized = PathUtils.SanitizeForFilename(task);
        var path = Path.Combine(dir, $"{sanitized}.json");

        if (!File.Exists(path))
            return false;

        try
        {
            var json = File.ReadAllText(path);
            var marker = JsonSerializer.Deserialize(json, DydoDefaultJsonContext.Default.WaitMarker);
            if (marker == null)
                return false;

            marker.Listening = true;
            marker.Pid = pid;

            var updated = JsonSerializer.Serialize(marker, DydoDefaultJsonContext.Default.WaitMarker);
            File.WriteAllText(path, updated);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void ResetWaitMarkerListening(string agentName, string task)
    {
        var dir = GetWaitingDir(agentName);
        var sanitized = PathUtils.SanitizeForFilename(task);
        var path = Path.Combine(dir, $"{sanitized}.json");

        if (!File.Exists(path))
            return;

        try
        {
            var json = File.ReadAllText(path);
            var marker = JsonSerializer.Deserialize(json, DydoDefaultJsonContext.Default.WaitMarker);
            if (marker == null) return;

            marker.Listening = false;
            marker.Pid = null;

            var updated = JsonSerializer.Serialize(marker, DydoDefaultJsonContext.Default.WaitMarker);
            File.WriteAllText(path, updated);
        }
        catch { }
    }

    public List<WaitMarker> GetNonListeningWaitMarkers(string agentName)
    {
        return GetWaitMarkers(agentName).Where(m => !m.Listening).ToList();
    }

    #endregion

    #region Reply-Pending Markers

    private string GetReplyPendingDir(string agentName) =>
        Path.Combine(GetAgentWorkspace(agentName), ".reply-pending");

    public void CreateReplyPendingMarker(string agentName, string task, string replyTo)
    {
        var dir = GetReplyPendingDir(agentName);
        Directory.CreateDirectory(dir);

        var marker = new ReplyPendingMarker
        {
            To = replyTo,
            Task = task,
            Since = DateTime.UtcNow
        };

        var sanitized = PathUtils.SanitizeForFilename(task);
        var path = Path.Combine(dir, $"{sanitized}.json");
        var json = JsonSerializer.Serialize(marker, DydoDefaultJsonContext.Default.ReplyPendingMarker);
        File.WriteAllText(path, json);
    }

    public List<ReplyPendingMarker> GetReplyPendingMarkers(string agentName)
    {
        var dir = GetReplyPendingDir(agentName);
        if (!Directory.Exists(dir))
            return [];

        var markers = new List<ReplyPendingMarker>();
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var marker = JsonSerializer.Deserialize(json, DydoDefaultJsonContext.Default.ReplyPendingMarker);
                if (marker != null)
                    markers.Add(marker);
            }
            catch { }
        }

        return markers;
    }

    public bool RemoveReplyPendingMarker(string agentName, string task)
    {
        var dir = GetReplyPendingDir(agentName);
        if (!Directory.Exists(dir))
            return false;

        var sanitized = PathUtils.SanitizeForFilename(task);
        var path = Path.Combine(dir, $"{sanitized}.json");
        if (!File.Exists(path))
            return false;

        File.Delete(path);
        return true;
    }

    public void ClearAllReplyPendingMarkers(string agentName)
    {
        var dir = GetReplyPendingDir(agentName);
        if (Directory.Exists(dir))
            Directory.Delete(dir, true);
    }

    #endregion

    #region Dispatch Markers

    private string GetDispatchMarkersDir(string agentName) =>
        Path.Combine(GetAgentWorkspace(agentName), ".dispatch-markers");

    public void CreateDispatchMarker(string agentName, string task, string targetRole, string dispatchedTo)
    {
        var dir = GetDispatchMarkersDir(agentName);
        Directory.CreateDirectory(dir);

        var marker = new DispatchMarker
        {
            Task = task,
            TargetRole = targetRole,
            DispatchedTo = dispatchedTo,
            Since = DateTime.UtcNow
        };

        var sanitized = PathUtils.SanitizeForFilename(task);
        var path = Path.Combine(dir, $"{sanitized}-{targetRole}.json");
        var json = JsonSerializer.Serialize(marker, DydoDefaultJsonContext.Default.DispatchMarker);
        File.WriteAllText(path, json);
    }

    public bool HasDispatchMarker(string agentName, string task, string targetRole)
    {
        var dir = GetDispatchMarkersDir(agentName);
        if (!Directory.Exists(dir))
            return false;

        var sanitized = PathUtils.SanitizeForFilename(task);
        return File.Exists(Path.Combine(dir, $"{sanitized}-{targetRole}.json"));
    }

    public void ClearAllDispatchMarkers(string agentName)
    {
        var dir = GetDispatchMarkersDir(agentName);
        if (Directory.Exists(dir))
            Directory.Delete(dir, true);
    }

    #endregion

    public AgentSession? GetSession(string agentName)
    {
        var sessionPath = Path.Combine(GetAgentWorkspace(agentName), ".session");
        if (!File.Exists(sessionPath))
            return null;

        try
        {
            var json = FileReadWithRetry(sessionPath);
            if (json == null) return null;
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

        // Check read-only first
        foreach (var pattern in agent.ReadOnlyPaths)
        {
            if (pattern == "**" || MatchesGlob(relativePath, pattern))
            {
                // Check if explicitly allowed
                var isAllowed = agent.WritablePaths.Any(ap => MatchesGlob(relativePath, ap));
                if (!isAllowed)
                {
                    error = $"Agent {agent.Name} ({agent.Role}) cannot {action} {relativePath}. {GetRoleRestrictionMessage(agent.Role, relativePath)}";
                    return false;
                }
            }
        }

        // If no writable paths, nothing is allowed
        if (agent.WritablePaths.Count == 0)
        {
            error = $"Agent {agent.Name} ({agent.Role}) has no write permissions.";
            return false;
        }

        // Check if path matches any writable pattern
        var allowed = agent.WritablePaths.Any(pattern => MatchesGlob(relativePath, pattern));
        if (!allowed)
        {
            error = $"Agent {agent.Name} ({agent.Role}) cannot {action} {relativePath}. {GetRoleRestrictionMessage(agent.Role, relativePath)}";
            return false;
        }

        return true;
    }

    public RoleDefinition? GetRoleDefinition(string roleName)
    {
        return _roleDefinitions.GetValueOrDefault(roleName);
    }

    private string GetRoleRestrictionMessage(string role, string? relativePath = null)
    {
        var pathNudge = relativePath != null ? GetPathSpecificNudge(relativePath) : null;
        if (pathNudge != null)
            return pathNudge;

        return _roleDefinitions.TryGetValue(role, out var def) ? def.DenialHint ?? "" : "";
    }

    /// <summary>
    /// Returns a targeted nudge for known "wrong destination" paths, or null if no special case applies.
    /// </summary>
    private static string? GetPathSpecificNudge(string relativePath)
    {
        if (relativePath.StartsWith(".claude/plans/", StringComparison.OrdinalIgnoreCase) ||
            relativePath.StartsWith(".claude\\plans\\", StringComparison.OrdinalIgnoreCase))
        {
            return "Dydo agents don't use Claude Code's built-in plans. "
                 + "Switch to planner mode ('dydo agent role planner --task <name>') "
                 + "and write your plan to your workspace (dydo/agents/<you>/plan-<task>.md).";
        }

        return null;
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

    public void SetDispatchMetadata(string agentName, string? windowId, bool autoClose)
    {
        UpdateAgentState(agentName, s =>
        {
            s.WindowId = windowId;
            s.AutoClose = autoClose;
        });
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

    private static string FormatTaskRoleHistory(Dictionary<string, List<string>> history)
    {
        if (history.Count == 0)
            return "{}";

        // Format as compact JSON-like YAML: { "task1": ["role1", "role2"], "task2": ["role3"] }
        var entries = history.Select(kvp =>
            $"\"{kvp.Key}\": [{string.Join(", ", kvp.Value.Select(r => $"\"{r}\""))}]");
        return "{ " + string.Join(", ", entries) + " }";
    }

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
        ["started"] = (s, v) => { if (v != "null" && DateTime.TryParse(v, out var dt)) s.Since = dt; },
        ["writable-paths"] = (s, v) => s.WritablePaths = ParsePathList(v),
        ["readonly-paths"] = (s, v) => s.ReadOnlyPaths = ParsePathList(v),
        ["unread-must-reads"] = (s, v) => s.UnreadMustReads = ParsePathList(v),
        ["unread-messages"] = (s, v) => s.UnreadMessages = ParsePathList(v),
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
            var content = FileReadWithRetry(statePath);
            if (content == null || !content.StartsWith("---"))
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

        if (!ValidateAgentNameFormat(name, out error))
            return false;

        if (string.IsNullOrWhiteSpace(human))
        {
            error = "Human name cannot be empty.";
            return false;
        }

        name = NormalizeAgentName(name);

        if (!LoadConfigForCrud(out var configPath, out var config, out error))
            return false;

        if (config.Agents.Pool.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            error = $"Agent '{name}' already exists in the pool.";
            return false;
        }

        config.Agents.Pool.Add(name);

        if (!config.Agents.Assignments.ContainsKey(human))
            config.Agents.Assignments[human] = new List<string>();
        config.Agents.Assignments[human].Add(name);

        _configService.SaveConfig(config, configPath);

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

    private bool LoadConfigForCrud(out string configPath, out DydoConfig config, out string error)
    {
        error = string.Empty;
        configPath = string.Empty;

        var path = _configService.FindConfigFile(_basePath);
        if (path == null)
        {
            error = "No dydo.json found. Run 'dydo init' first.";
            config = null!;
            return false;
        }
        configPath = path;

        var loaded = _configService.LoadConfig(_basePath);
        if (loaded == null)
        {
            error = "Failed to load dydo.json.";
            config = null!;
            return false;
        }
        config = loaded;
        return true;
    }

    private static bool ValidateAgentNameFormat(string name, out string error)
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
        return true;
    }

    private static string NormalizeAgentName(string name) =>
        name.Length > 1
            ? char.ToUpperInvariant(name[0]) + name[1..].ToLowerInvariant()
            : name.ToUpperInvariant();

    private string? FindInPool(DydoConfig config, string name) =>
        config.Agents.Pool.FirstOrDefault(n => n.Equals(name, StringComparison.OrdinalIgnoreCase));

    private bool IsAgentActive(string agentName)
    {
        var state = GetAgentState(agentName);
        var session = GetSession(agentName);
        return (state?.Status != AgentStatus.Free && session != null) || state?.Status == AgentStatus.Dispatched;
    }

    /// <summary>
    /// Renames an agent: updates pool, assignments, workspace folder, and regenerates workflow/mode files.
    /// </summary>
    public bool RenameAgent(string oldName, string newName, out string error)
    {
        if (!ValidateAgentNameFormat(newName, out error))
        {
            error = error.Replace("Agent name", "New agent name");
            return false;
        }

        oldName = NormalizeName(oldName);
        newName = NormalizeAgentName(newName);

        if (!LoadConfigForCrud(out var configPath, out var config, out error))
            return false;

        var existingName = FindInPool(config, oldName);
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

        if (IsAgentActive(existingName))
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
                assignment.Value[agentIndex] = newName;
        }

        _configService.SaveConfig(config, configPath);

        // Rename workspace folder
        var agentsPath = _configService.GetAgentsPath(_basePath);
        var oldWorkspace = Path.Combine(agentsPath, existingName);
        var newWorkspace = Path.Combine(agentsPath, newName);
        if (Directory.Exists(oldWorkspace))
        {
            Directory.Move(oldWorkspace, newWorkspace);
            UpdateStateFileForRename(newWorkspace, existingName, newName);
        }

        _folderScaffolder.RegenerateAgentFiles(agentsPath, newName,
            _config?.Paths.Source, _config?.Paths.Tests);

        return true;
    }

    private static void UpdateStateFileForRename(string workspace, string oldName, string newName)
    {
        var statePath = Path.Combine(workspace, "state.md");
        if (!File.Exists(statePath)) return;

        var content = File.ReadAllText(statePath);
        content = Regex.Replace(content, $@"^agent:\s*{Regex.Escape(oldName)}\s*$",
            $"agent: {newName}", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        content = Regex.Replace(content, $@"^# {Regex.Escape(oldName)} —",
            $"# {newName} —", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        File.WriteAllText(statePath, content);
    }

    /// <summary>
    /// Removes an agent: deletes from pool and assignments, removes entire workspace.
    /// </summary>
    public bool RemoveAgent(string name, out string error)
    {
        if (!LoadConfigForCrud(out var configPath, out var config, out error))
            return false;

        var existingName = FindInPool(config, name);
        if (existingName == null)
        {
            error = $"Agent '{name}' does not exist in the pool.";
            return false;
        }

        if (IsAgentActive(existingName))
        {
            error = $"Agent '{existingName}' is currently claimed. Release it first.";
            return false;
        }

        config.Agents.Pool.RemoveAll(n => n.Equals(existingName, StringComparison.OrdinalIgnoreCase));
        foreach (var assignment in config.Agents.Assignments)
            assignment.Value.RemoveAll(n => n.Equals(existingName, StringComparison.OrdinalIgnoreCase));

        _configService.SaveConfig(config, configPath);

        var workspace = Path.Combine(_configService.GetAgentsPath(_basePath), existingName);
        if (Directory.Exists(workspace))
            Directory.Delete(workspace, recursive: true);

        return true;
    }

    /// <summary>
    /// Reassigns an agent to a different human.
    /// </summary>
    public bool ReassignAgent(string name, string newHuman, out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(newHuman))
        {
            error = "Human name cannot be empty.";
            return false;
        }

        if (!LoadConfigForCrud(out var configPath, out var config, out error))
            return false;

        var existingName = FindInPool(config, name);
        if (existingName == null)
        {
            error = $"Agent '{name}' does not exist in the pool.";
            return false;
        }

        if (IsAgentActive(existingName))
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
            config.Agents.Assignments[newHuman] = new List<string>();
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

    /// <summary>
    /// Normalizes an agent name (finds exact match in pool).
    /// </summary>
    private string NormalizeName(string name)
    {
        var match = AgentNames.FirstOrDefault(n => n.Equals(name, StringComparison.OrdinalIgnoreCase));
        return match ?? name;
    }

    #region Must-Read Enforcement

    /// <summary>
    /// Computes the list of must-read files for a given role by inspecting the mode file's links.
    /// Filters out files already read in the current audit session.
    /// </summary>
    private List<string> ComputeUnreadMustReads(string agentName, string role, string? sessionId)
    {
        var workspace = GetAgentWorkspace(agentName);
        var modeFilePath = Path.Combine(workspace, "modes", $"{role}.md");

        if (!File.Exists(modeFilePath))
            return [];

        var content = File.ReadAllText(modeFilePath);
        var parser = new MarkdownParser();
        var links = parser.ExtractLinks(content);

        var projectRoot = _configService.GetProjectRoot(_basePath) ?? _basePath;
        var mustReads = new List<string>();

        foreach (var link in links)
        {
            if (link.Type == LinkType.External) continue;
            if (string.IsNullOrEmpty(link.Target)) continue;

            var resolved = PathUtils.ResolvePath(modeFilePath, link.Target);

            if (!File.Exists(resolved)) continue;

            var targetContent = File.ReadAllText(resolved);
            var frontmatter = parser.ExtractFrontmatter(targetContent);

            if (frontmatter?.MustRead == true)
            {
                var relativePath = PathUtils.NormalizePath(Path.GetRelativePath(projectRoot, resolved));
                mustReads.Add(relativePath);
            }
        }

        // Add the mode file itself (always implicitly must-read)
        var modeRelative = PathUtils.NormalizePath(Path.GetRelativePath(projectRoot, modeFilePath));
        mustReads.Add(modeRelative);

        // Deduplicate
        mustReads = mustReads.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        // Filter out files already read in this session
        if (!string.IsNullOrEmpty(sessionId))
        {
            try
            {
                var session = _auditService.GetSession(sessionId);
                if (session != null)
                {
                    var readPaths = session.Events
                        .Where(e => e.EventType == AuditEventType.Read && !string.IsNullOrEmpty(e.Path))
                        .Select(e => NormalizeMustReadPath(e.Path!))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    mustReads.RemoveAll(p => readPaths.Contains(NormalizeMustReadPath(p)));
                }
            }
            catch
            {
                // Audit service failure should not block role setting
            }
        }

        return mustReads;
    }

    /// <summary>
    /// Marks a must-read file as read, removing it from the agent's unread list.
    /// </summary>
    public void MarkMustReadComplete(string? sessionId, string relativePath)
    {
        var agent = GetCurrentAgent(sessionId);
        if (agent == null) return;
        UpdateAgentState(agent.Name, s =>
        {
            s.UnreadMustReads.RemoveAll(p => p.Equals(relativePath, StringComparison.OrdinalIgnoreCase));
        });
    }

    public void AddUnreadMessage(string agentName, string messageId)
    {
        UpdateAgentState(agentName, s =>
        {
            if (!s.UnreadMessages.Contains(messageId))
                s.UnreadMessages.Add(messageId);
        });
    }

    public void MarkMessageRead(string? sessionId, string messageId)
    {
        var agent = GetCurrentAgent(sessionId);
        if (agent == null) return;
        UpdateAgentState(agent.Name, s =>
        {
            s.UnreadMessages.RemoveAll(id => id.Equals(messageId, StringComparison.OrdinalIgnoreCase));
        });
    }

    public void ClearAllUnreadMessages(string agentName)
    {
        UpdateAgentState(agentName, s => s.UnreadMessages.Clear());
    }

    private static string NormalizeMustReadPath(string path)
    {
        var normalized = PathUtils.NormalizeWorktreePath(path)?.Replace('\\', '/') ?? path.Replace('\\', '/');
        var dydoIndex = normalized.IndexOf("dydo/", StringComparison.OrdinalIgnoreCase);
        return dydoIndex >= 0 ? normalized[dydoIndex..] : normalized;
    }

    #endregion

    #region Lock File Support

    private record ClaimLock(int Pid, DateTime Acquired);

    [JsonSerializable(typeof(ClaimLock))]
    private partial class RegistryLockJsonContext : JsonSerializerContext { }

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
            var json = JsonSerializer.Serialize(lockInfo, RegistryLockJsonContext.Default.ClaimLock);
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
                var existingLock = JsonSerializer.Deserialize(existingJson, RegistryLockJsonContext.Default.ClaimLock);

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
        "workflow.md", "state.md", ".session", ".pending-session", ".claim.lock", "modes", "archive", "inbox",
        ".worktree", ".worktree-path", ".worktree-base", ".worktree-root", ".worktree-hold",
        ".merge-source", ".needs-merge"
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
