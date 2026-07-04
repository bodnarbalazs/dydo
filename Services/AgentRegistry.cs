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
    private const int StaleWorkingMinutes = 5;

    /// <summary>
    /// When set, IsLauncherAlive uses this instead of scanning processes.
    /// Enables testing the stale-dispatch reclaim gate without real process lookups.
    /// </summary>
    internal static Func<string, bool>? IsLauncherAliveOverride { get; set; }

    /// <summary>
    /// When set, IsSessionPidAlive uses this instead of reading .session and probing the OS.
    /// Enables testing the stale-working reclaim gate without real process lookups.
    /// </summary>
    internal static Func<string, bool>? IsSessionPidAliveOverride { get; set; }

    private readonly string _basePath;
    private readonly IConfigService _configService;
    private readonly IFolderScaffolder _folderScaffolder;
    private readonly DydoConfig? _config;
    private readonly Dictionary<string, (List<string> Writable, List<string> ReadOnly)> _rolePermissions;
    private readonly Dictionary<string, RoleDefinition> _roleDefinitions;
    private readonly InboxMetadataReader _inboxReader;
    private readonly AgentSessionManager _sessionManager;

    public AgentRegistry(string? basePath = null, IConfigService? configService = null, IFolderScaffolder? folderScaffolder = null)
    {
        _basePath = basePath ?? PathUtils.FindProjectRoot() ?? Environment.CurrentDirectory;
        _configService = configService ?? new ConfigService();
        _folderScaffolder = folderScaffolder ?? new FolderScaffolder();
        _config = _configService.LoadConfig(_basePath);
        _inboxReader = new InboxMetadataReader(GetAgentWorkspace);
        _sessionManager = new AgentSessionManager(
            GetAgentWorkspace, WorkspacePath, AgentNames, IsValidAgentName, GetAgentState);

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
            // No role files on disk — use built-in base definitions with a warning.
            // Skill-only (planner) and workflow-only (sprint-auditor) roles are not claimable
            // identities, so they are excluded here exactly as WriteBaseRoleDefinitions
            // excludes them from the on-disk roster.
            Console.Error.WriteLine("[dydo] WARNING: No role files found at dydo/_system/roles/. Run 'dydo roles reset' to generate them. Using built-in defaults.");
            var baseRoles = RoleDefinitionService.GetBaseRoleDefinitions()
                .Where(r => !RoleDefinitionService.NonClaimableRoles.Contains(r.Name))
                .ToList();
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

    public bool IsWorktreeStale(string worktreeId)
    {
        var dydoRoot = _configService.GetDydoRoot(_basePath) ?? _basePath;
        return !Directory.Exists(Path.Combine(dydoRoot, "_system", ".local", "worktrees", worktreeId));
    }

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

            if (!IsReservable(state))
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
        (state.Status is AgentStatus.Dispatched
            && IsStaleDispatch(state)
            && !IsLauncherAlive(state.Name)) ||
        IsStaleWorking(state);

    // Strict gate for the reservation path (decisions 017, 018):
    // IsEffectivelyFree keeps stale-working permissive so display/claim-auto
    // surface reclaim candidates; ReserveAgent adds the session-pid probe
    // here to avoid double-claiming a live Claude.
    private bool IsReservable(AgentState state) =>
        IsEffectivelyFree(state) &&
        !(IsStaleWorking(state) && IsSessionPidAlive(state.Name));

    private static bool IsStaleDispatch(AgentState state) =>
        state.Status is AgentStatus.Dispatched &&
        state.Since.HasValue &&
        (DateTime.UtcNow - state.Since.Value.ToUniversalTime()).TotalMinutes > StaleDispatchMinutes;

    private static bool IsStaleWorking(AgentState state) =>
        state.Status == AgentStatus.Working &&
        state.Since.HasValue &&
        (DateTime.UtcNow - state.Since.Value.ToUniversalTime()).TotalMinutes > StaleWorkingMinutes;

    // #0207 part 2 companion: true while a watchdog auto-resume is within its warmup window.
    // The HandleExistingSession stale-working reclaim path uses this to refuse a concurrent
    // manual claim during the resume warmup — closing the window in Proof A of
    // dydo/agents/Charlie/plan-f11-guard-side.md. SaturateResumeAttempts clears
    // LastResumeLaunchedAt, so a watchdog give-up exits this state immediately.
    private static bool ResumeInFlight(AgentState state)
    {
        if (state.LastResumeLaunchedAt is not { } launchedAt) return false;
        var gate = WatchdogService.ResumeWarmupGateOverride ?? WatchdogService.ResumeWarmupGate;
        return DateTime.UtcNow - launchedAt < gate;
    }

    // Stale-working reclaim gate (decision 018 + #0207 part 2 companion): the agent is
    // Working past the threshold AND its session PID is dead AND no watchdog resume is
    // currently in flight. Extracted so HandleExistingSession's CC stays under the
    // gap_check T1 threshold.
    private bool IsReclaimableStaleWorking(string agentName, AgentState? state) =>
        state != null
        && IsStaleWorking(state)
        && !IsSessionPidAlive(agentName)
        && !ResumeInFlight(state);

    // PID to persist in .session so a later claim can probe Claude-tab liveness.
    // Prefer the nearest Claude ancestor (survives shell/subshell churn). On Windows
    // claude ships as a Node script, so FindClaudeAncestor also accepts "node" (#0151).
    // If the claim isn't running under a Claude tab (e.g. CLI tests), fall back to the
    // immediate parent shell, which at least dies with the current terminal.
    private static int? ResolveClaimedPid() =>
        ProcessUtils.FindClaudeAncestor() ??
        ProcessUtils.GetParentPid(Environment.ProcessId);

    private void RefreshClaimedPid(string agentName, AgentSession existingSession)
    {
        var newPid = ResolveClaimedPid();
        if (newPid == existingSession.ClaimedPid) return;
        WriteClaimedPid(agentName, existingSession, newPid);
    }

    // Writes a .session with ClaimedPid mutated to newPid, preserving Agent/SessionId/Claimed.
    // Extracted from RefreshClaimedPid so RefreshResumedAgentSession can write a PID it has
    // already validated against FindClaudeAncestor under the lock — without re-running
    // ResolveClaimedPid, which would fall back to a non-claude parent shell PID if the
    // ancestor briefly vanished after the validation step.
    private void WriteClaimedPid(string agentName, AgentSession existingSession, int? newPid)
    {
        var refreshed = new AgentSession
        {
            Agent = existingSession.Agent,
            SessionId = existingSession.SessionId,
            Claimed = existingSession.Claimed,
            ClaimedPid = newPid
        };
        var sessionPath = Path.Combine(GetAgentWorkspace(agentName), ".session");
        File.WriteAllText(sessionPath, JsonSerializer.Serialize(refreshed, DydoDefaultJsonContext.Default.AgentSession));
    }

    // Caller is responsible for holding the per-agent .claim.lock — UpdateAgentState
    // does not re-acquire it. Used by the same-session reclaim path inside ClaimAgent
    // (which already holds the lock) to zero the resume budget after a successful resume.
    private void ResetResumeBookkeeping(string agentName)
    {
        UpdateAgentState(agentName, s =>
        {
            s.ResumeAttempts = 0;
            s.LastResumeLaunchedAt = null;
            s.PreResumePid = null;
            s.LaunchedPid = null;
        });
    }

    // #0207 part 2: guard-side ClaimedPid auto-refresh. On a resumed claude session's
    // first guarded tool call, rewrites .session.ClaimedPid from the dead pre-resume PID
    // to the live claude ancestor — deterministic, no prompt dependence. Plays the same
    // bookkeeping reset (#0153) + recovery audit emit role that HandleExistingSession's
    // same-session reclaim branch did, but co-located with the proof of resume success
    // (an actual guarded tool call executing under the new claude) so neither responsibility
    // can be missed. The whole body is in try/catch: a guard hook MUST NEVER break a tool
    // call. See dydo/agents/Charlie/plan-f11-guard-side.md for the trigger derivation,
    // edge-case enumeration, and proofs. The HandleExistingSession same-session reclaim
    // branch stays — the two paths are both reachable (explicit re-claim vs first guarded
    // call) and the lock makes their interaction idempotent (Proof B).
    //
    // Steps 1–5 (the trigger predicate) live in RecoveryClassifier.ShouldRefreshResumedPid;
    // splitting keeps this method's cyclomatic complexity under the gap_check T1 threshold.
    internal void RefreshResumedAgentSession(string? sessionId)
    {
        try
        {
            var decision = RecoveryClassifier.ShouldRefreshResumedPid(
                sessionId, GetCurrentAgent, GetSession);
            if (!decision.ShouldRefresh) return;
            RefreshResumedAgentSessionUnderLock(
                sessionId!, decision.AgentName!, decision.LivePid);
        }
        catch
        {
            // A guard hook MUST NEVER break a tool call. Silent no-op on any failure;
            // the next guarded call will retry. Discipline matches LogAuditEvent /
            // RunDailyValidationIfDue in GuardCommand.
        }
    }

    // Steps 6–11 of RefreshResumedAgentSession's pseudocode: bounded-retry lock,
    // double-check under the lock, write the live PID, reset bookkeeping, emit audit.
    // Separated from the entry point so each half stays under the gap_check CRAP threshold.
    private void RefreshResumedAgentSessionUnderLock(
        string sessionId, string agentName, int livePid)
    {
        // 6. bounded-retry lock acquisition (3× / 50 ms) — TryAcquireLock is fail-fast.
        //    A once-per-resume hot path can afford ~150 ms; if a process is genuinely
        //    stuck on the lock, the next guarded call retries idempotently.
        var acquired = false;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (TryAcquireLock(agentName, out _)) { acquired = true; break; }
            if (attempt < 2) Thread.Sleep(50);
        }
        if (!acquired) return;

        try
        {
            // 7. re-read .session under the lock — another guard call may have refreshed
            //    first. If ClaimedPid is now live, skip (we lose the race; idempotent).
            var fresh = GetSession(agentName);
            if (fresh == null || fresh.SessionId != sessionId) return;
            if (fresh.ClaimedPid is not int freshPid) return;
            if (ProcessUtils.IsProcessRunning(freshPid)) return;

            // 8. re-validate status under the lock. ResetResumeBookkeeping below routes
            //    through UpdateAgentState whose `?? new AgentState{Name}` fallback would
            //    silently destroy a role-bearing agent's state.md if we proceeded past
            //    a non-Working state — the watchdog only resumes Working agents, so a
            //    genuinely resumed agent is always Working here.
            var priorState = GetAgentState(agentName);
            if (priorState == null || priorState.Status != AgentStatus.Working) return;

            // 9. write the already-validated livePid directly. Do NOT call RefreshClaimedPid
            //    (which re-runs ResolveClaimedPid and could fall back to a non-claude
            //    parent shell PID if the ancestor vanished between predicate eval and now).
            WriteClaimedPid(agentName, fresh, livePid);

            // 10. zero the resume bookkeeping (#0153). Order 9 → 10 → 11 is deliberate:
            //     refresh is the highest-value op (it stops the watchdog firing another
            //     redundant resume and unblocks F11), so it goes first under the lock.
            ResetResumeBookkeeping(agentName);

            // 11. emit watchdog resume_outcome log entry. EmitAutoRecovery self-gates
            //     on priorState.LastResumeLaunchedAt != null — a user-driven claude --resume
            //     (no watchdog launch) refreshes the PID but emits nothing.
            RecoveryClassifier.EmitAutoRecovery(_basePath, agentName, fresh, priorState);
        }
        finally
        {
            ReleaseLock(agentName);
        }
    }

    // Guards the stale-dispatch reclaim path: if the original dispatch's
    // "{agent} --inbox" launcher process is still alive, the first Claude is
    // merely slow to boot — reclaiming would strand it and double-launch.
    private static bool IsLauncherAlive(string agentName) =>
        IsLauncherAliveOverride != null
            ? IsLauncherAliveOverride(agentName)
            : ProcessUtils.FindProcessesByCommandLine($"{agentName} --inbox").Count > 0;

    // Guards the stale-working reclaim path (decision 018): reads .session,
    // checks the stored ClaimedPid against the OS. Missing/unparseable session
    // or absent PID is treated as dead — the claim path already archives
    // the workspace, so a malformed session is not a reason to keep a
    // zombie-working agent unclaimable.
    private bool IsSessionPidAlive(string agentName)
    {
        if (IsSessionPidAliveOverride != null)
            return IsSessionPidAliveOverride(agentName);

        var session = GetSession(agentName);
        if (session?.ClaimedPid is not { } pid) return false;
        return ProcessUtils.IsProcessRunning(pid);
    }

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
            if (IsIdempotentReclaim(existingSession, state, sessionId))
                return true;

            // Capture the prior session/state BEFORE SetupAgentWorkspace overwrites .session and
            // resets state.md — the recovery_kind classification needs the pre-reset values.
            // Closes the PR3 instrumentation half of the agent-crash-fixes batch.
            SetupAgentWorkspace(agentName, sessionId, human, IsDispatched(state),
                priorSession: existingSession, priorState: state);
            return true;
        }
        finally
        {
            ReleaseLock(agentName);
        }
    }

    private static bool IsIdempotentReclaim(AgentSession? existingSession, AgentState? state, string sessionId) =>
        existingSession?.SessionId == sessionId && !IsUnclaimedStatus(state?.Status);

    private static bool IsUnclaimedStatus(AgentStatus? status) =>
        status is null or AgentStatus.Free or AgentStatus.Dispatched;

    private static bool IsDispatched(AgentState? state) =>
        state?.Status == AgentStatus.Dispatched;

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
        {
            // #0143: refresh ClaimedPid before short-circuiting. After watchdog auto-resume,
            // .session still points at the dead pre-resume PID; without this update, the
            // watchdog's next dead-PID check fires another resume and produces duplicate
            // terminals. Identity preserved per Decision 022 — only the PID changes.
            RefreshClaimedPid(agentName, existingSession);

            // #0153: a same-session reclaim is a successful resume — clear the resume
            // bookkeeping so the next crash episode starts from a clean budget. Without
            // this, the cap accumulates across crashes and long-lived agents eventually
            // become silently un-resumable. Decision 022 reads "claim" as inclusive of
            // same-session reclaims; the spec wording is updated alongside this commit.
            // The local `state` snapshot (from GetAgentState above) is unaffected by the
            // disk-side reset, so RecoveryClassifier can read its pre-reset values for
            // the recovery_kind="auto" Claim audit + resume_outcome=succeeded log.
            ResetResumeBookkeeping(agentName);
            RecoveryClassifier.EmitAutoRecovery(_basePath, agentName, existingSession, state);
            return true;
        }

        // Stale-working reclaim (decision 018, issue #103): prior Claude's
        // session PID is dead and Status has been Working past the threshold.
        // SetupAgentWorkspace will archive the old workspace and regenerate.
        // Predicate is extracted so this method's cyclomatic complexity stays
        // under the gap_check T1 threshold after the #0207 part 2 companion clause.
        if (IsReclaimableStaleWorking(agentName, state))
        {
            Console.Error.WriteLine(
                $"[dydo] Note: reclaimed agent {agentName} from an interrupted session. " +
                "Check 'git status' for uncommitted work from the prior Claude.");
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

    private void SetupAgentWorkspace(string agentName, string sessionId, string? human, bool wasDispatched,
        AgentSession? priorSession = null, AgentState? priorState = null)
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
            Claimed = DateTime.UtcNow,
            ClaimedPid = ResolveClaimedPid()
        };

        var sessionPath = Path.Combine(workspace, ".session");
        File.WriteAllText(sessionPath, JsonSerializer.Serialize(session, DydoDefaultJsonContext.Default.AgentSession));

        UpdateAgentState(agentName, s =>
        {
            s.Status = AgentStatus.Working;
            s.Since = DateTime.UtcNow;
            s.AssignedHuman = human;
            s.ResumeAttempts = 0;
            s.LastResumeLaunchedAt = null;
            s.PreResumePid = null;
            s.LaunchedPid = null;
            if (!wasDispatched)
            {
                s.WindowId = null;
                s.AutoClose = false;
            }
        });

        // Anchor the watchdog to this claim's claude ancestor. RegisterMainAnchor
        // routes through PathUtils.FindMainDydoRoot so the anchor always lands in
        // the MAIN dydo root, never a worktree's — the watchdog only reads main.
        // Pass _basePath so the worktree-walkback seed is the registry's project
        // root rather than the process CWD (matters when the claim runs inside a
        // worktree dispatcher whose CWD is the worktree dir).
        // Closes #0154 (anchor-on-claim) and #0174 (worktree-claim wrong-dir).
        try { WatchdogService.RegisterMainAnchor(ProcessUtils.FindClaudeAncestor(), _basePath); }
        catch { /* anchoring is best-effort; never fail a claim because of it */ }

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
            string? releasedTask = null;
            UpdateAgentState(agent.Name, s =>
            {
                // Capture the task before nulling it: releasing is a "cause disappeared" transition
                // for the needs-human flag (Decision 030 §1), so we clear the flag AND its task-file
                // mirror here while the task name is still known. Without this, an orphan-crash flag
                // that mirrored `needs-human: true` into the task file would be stranded — Release
                // nulls s.Task, and the later watchdog sweep's SetNeedsHuman then sees s.Task == null
                // and cannot reach the task file to reconcile it.
                releasedTask = s.Task;
                s.Status = AgentStatus.Free;
                s.Role = null;
                s.Task = null;
                s.NeedsHuman = false;
                s.Since = null;
                s.WritablePaths = [];
                s.ReadOnlyPaths = [];
                s.UnreadMustReads = [];
                s.UnreadMessages = [];
                s.ResumeAttempts = 0;
                s.LastResumeLaunchedAt = null;
                s.PreResumePid = null;
                s.LaunchedPid = null;
                // Leave AutoClose untouched. The watchdog needs `free + auto-close: true`
                // post-release to kill claude (Services/WatchdogService.cs:359). The
                // redispatch race that earlier motivated clearing this here is closed by
                // the per-agent .claim.lock in PollAndCleanupForAgent (06512de).
            });
            MirrorNeedsHumanToTask(releasedTask, false);

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

        // `_`-prefixed wait markers are sentinels (e.g. _general-wait) — they signal
        // background-wait state, not "waiting for a response on this task". Real
        // task-channel waits are the only ones that should block release. Sentinels
        // are torn down by CleanupAfterRelease's ClearAllWaitMarkers below.
        var waitMarkers = GetWaitMarkers(agentName).Where(m => !m.Task.StartsWith('_')).ToList();
        if (waitMarkers.Count > 0)
        {
            var tasks = string.Join(", ", waitMarkers.Select(m => m.Task));
            error = $"Cannot release: waiting for response on: {tasks}.\n" +
                    "Cancel with: dydo wait --task <name> --cancel";
            return false;
        }

        var needsMergePath = Path.Combine(workspace, ".needs-merge");
        if (File.Exists(needsMergePath))
        {
            var mergeTask = File.ReadAllText(needsMergePath).Trim();
            error = $"Cannot release: review passed in worktree but merge not dispatched.\n" +
                    $"Dispatch a code-writer to merge the worktree branch:\n" +
                    $"  dydo dispatch --auto-close --role code-writer --task {mergeTask}-merge --brief \"Merge worktree branch into base. See .merge-source and .worktree-base markers in your workspace.\"";
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

        try { new GuardLiftService().ClearLift(agentName); } catch { }

        foreach (var marker in Directory.GetFiles(workspace, ".role-nudge-*"))
            File.Delete(marker);

        foreach (var marker in Directory.GetFiles(workspace, ".no-launch-nudge-*"))
            File.Delete(marker);

        foreach (var marker in Directory.GetFiles(workspace, ".no-wait-nudge-*"))
            File.Delete(marker);

        foreach (var marker in Directory.GetFiles(workspace, ".nudge-*"))
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

        var mustReads = ComputeUnreadMustReads(agent.Name, role, task);
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

    // Closes #0183 (F1): the env-var fast paths in GetSessionContext / GetCurrentAgent used
    // to trust DYDO_AGENT unconditionally, so a parent shell that set DYDO_AGENT=X could
    // make a `dydo` subprocess in a different agent's claude tab impersonate X. Now we
    // require the named agent's ClaimedPid to match either this process or its claude
    // ancestor. Honest dispatched-terminal callers pass (their .session.ClaimedPid is the
    // claude PID their terminal spawned). Plain-shell attackers fail (no claude ancestor;
    // PID mismatch). Override hook on ProcessUtils.FindClaudeAncestor lets tests inject.
    internal static bool IsOwnedByCaller(AgentSession session)
    {
        if (session.ClaimedPid is not int claimedPid) return false;
        if (Environment.ProcessId == claimedPid) return true;
        var claude = ProcessUtils.FindClaudeAncestor();
        return claude.HasValue && claude.Value == claimedPid;
    }

    /// <summary>
    /// Verifies the calling process actually owns the named agent — used at the wait-marker
    /// callsite (#0195/F11) so an attacker with stale DYDO_AGENT can't register a marker that
    /// holds another agent's general-wait slot. Returns false when no session exists for
    /// the agent. Closes #0195.
    /// </summary>
    public bool VerifyCallerOwnsAgent(string agentName)
    {
        var session = GetSession(agentName);
        return session != null && IsOwnedByCaller(session);
    }

    // Fastest path of GetCurrentAgent — extracted so the outer method's cyclomatic complexity
    // stays under tier T1's CRAP gate. The env fast-path returns the agent only when DYDO_AGENT
    // names a valid claimed agent whose session matches AND IsOwnedByCaller (post-F1).
    private AgentState? TryResolveCurrentAgentFromEnvVar(string sessionId)
    {
        var envAgent = Environment.GetEnvironmentVariable("DYDO_AGENT");
        if (string.IsNullOrEmpty(envAgent) || !IsValidAgentName(envAgent))
            return null;

        var envSession = GetSession(envAgent);
        if (envSession?.SessionId == sessionId && IsOwnedByCaller(envSession))
            return GetAgentState(envAgent);

        return null;
    }

    /// <summary>
    /// Gets the current agent for a given session ID.
    /// Uses a hint file to avoid scanning all agents when possible.
    /// </summary>
    public AgentState? GetCurrentAgent(string? sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
            return null;

        var envHit = TryResolveCurrentAgentFromEnvVar(sessionId);
        if (envHit != null) return envHit;

        // Fast path: check agent hint file
        var hintPath = GetAgentHintPath();
        if (File.Exists(hintPath))
        {
            try
            {
                var hint = FileReadRetry.Read(hintPath)?.Trim();
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

    private string GetAgentHintPath() =>
        Path.Combine(WorkspacePath, ".session-agent");


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
            var sessionId = FileReadRetry.Read(path)?.Trim();
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
    /// DYDO_AGENT env var bypasses the shared file entirely (set in dispatched terminals).
    /// </summary>
    public string? GetSessionContext()
    {
        var agentName = Environment.GetEnvironmentVariable("DYDO_AGENT");
        if (!string.IsNullOrEmpty(agentName) && IsValidAgentName(agentName))
        {
            var session = GetSession(agentName);
            if (session != null && IsOwnedByCaller(session)) return session.SessionId;
        }

        return _sessionManager.GetSessionContext();
    }

    /// <summary>
    /// Stores the session ID to context file, optionally with the agent name
    /// for cross-terminal race detection.
    /// Called by the guard hook before allowing dydo commands.
    /// </summary>
    public void StoreSessionContext(string sessionId, string? agentName = null)
    {
        _sessionManager.StoreSessionContext(sessionId, agentName);
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

    /// <summary>
    /// Atomically writes a wait marker with Listening=true and the given Pid in a single
    /// temp-then-rename file write. If a marker already exists for the task, its Target
    /// and Since fields are preserved (so callers can flip a dispatcher-pre-created marker
    /// to listening without losing dispatch context).
    ///
    /// Replaces the previous CreateWaitMarker + UpdateWaitMarkerListening sequence used by
    /// dydo wait — issue #0133 traced the orchestrator general-wait deadlock to the window
    /// between those two writes, where guard checks could see Listening=false.
    /// </summary>
    public void CreateListeningWaitMarker(string agentName, string task, string targetAgent, int pid)
    {
        var dir = GetWaitingDir(agentName);
        Directory.CreateDirectory(dir);

        var sanitized = PathUtils.SanitizeForFilename(task);
        var path = Path.Combine(dir, $"{sanitized}.json");

        var resolvedTarget = targetAgent;
        var since = DateTime.UtcNow;

        if (File.Exists(path))
        {
            try
            {
                var existing = JsonSerializer.Deserialize(File.ReadAllText(path), DydoDefaultJsonContext.Default.WaitMarker);
                if (existing != null)
                {
                    resolvedTarget = existing.Target;
                    since = existing.Since;
                }
            }
            catch
            {
                // Corrupt or unreadable existing marker — overwrite with caller-provided values.
            }
        }

        var marker = new WaitMarker
        {
            Target = resolvedTarget,
            Task = task,
            Since = since,
            Listening = true,
            Pid = pid,
        };

        var json = JsonSerializer.Serialize(marker, DydoDefaultJsonContext.Default.WaitMarker);
        var tempPath = $"{path}.tmp.{Environment.ProcessId}.{Guid.NewGuid():N}";
        File.WriteAllText(tempPath, json);
        try
        {
            File.Move(tempPath, path, overwrite: true);
        }
        catch
        {
            try { File.Delete(tempPath); } catch { }
            throw;
        }
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

    public AgentSession? GetSession(string agentName)
    {
        var sessionPath = Path.Combine(GetAgentWorkspace(agentName), ".session");
        if (!File.Exists(sessionPath))
            return null;

        try
        {
            var json = FileReadRetry.Read(sessionPath);
            if (json == null) return null;
            return JsonSerializer.Deserialize(json, DydoDefaultJsonContext.Default.AgentSession);
        }
        catch
        {
            return null;
        }
    }

    public RoleDefinition? GetRoleDefinition(string roleName)
    {
        return _roleDefinitions.GetValueOrDefault(roleName);
    }

    public bool IsValidAgentName(string name) =>
        AgentNames.Contains(name, StringComparer.OrdinalIgnoreCase);

    public string? GetAgentNameFromLetter(char letter) =>
        PresetAgentNames.GetNameFromLetter(letter);

    public void SetDispatchMetadata(string agentName, string? windowId, bool autoClose)
    {
        UpdateAgentState(agentName, s =>
        {
            s.WindowId = windowId;
            s.AutoClose = autoClose;
        });
    }

    public int IncrementResumeAttempts(string agentName, int? preResumePid = null, int? launchedPid = null)
    {
        if (!TryAcquireLock(agentName, out _))
            return -1;
        try
        {
            var state = GetAgentState(agentName) ?? new AgentState { Name = agentName };
            state.ResumeAttempts += 1;
            state.LastResumeLaunchedAt = DateTime.UtcNow;
            state.PreResumePid = preResumePid;
            state.LaunchedPid = launchedPid;
            WriteStateFile(agentName, state);
            return state.ResumeAttempts;
        }
        finally
        {
            ReleaseLock(agentName);
        }
    }

    /// <summary>
    /// Updates only LaunchedPid for an agent without incrementing the resume counter.
    /// Used by the watchdog when the launcher returns a non-zero PID after the
    /// counter has already been bumped — the launched PID needs to land in state.md
    /// before the next watchdog tick reads it for liveness checking. Returns false
    /// if the per-agent lock is contended (caller may retry on the next tick;
    /// LaunchedPid stays null until then, falling back to the wall-clock gate).
    /// </summary>
    public bool RecordResumeLaunch(string agentName, int launchedPid)
    {
        if (!TryAcquireLock(agentName, out _))
            return false;
        try
        {
            var state = GetAgentState(agentName) ?? new AgentState { Name = agentName };
            state.LaunchedPid = launchedPid;
            WriteStateFile(agentName, state);
            return true;
        }
        finally
        {
            ReleaseLock(agentName);
        }
    }

    /// <summary>
    /// Sets ResumeAttempts to the cap value directly (without incrementing) AND clears
    /// LastResumeLaunchedAt — both signal "this resume episode is over, no more launches."
    /// Used by the watchdog's bad-session-ID fail-fast path (<c>WatchdogService.IsBadSessionFailFast</c>)
    /// and by PR3's gave_up tick-check, which is idempotent because clearing
    /// LastResumeLaunchedAt makes the predicate <c>LastResumeLaunchedAt != null &amp;&amp;
    /// ResumeAttempts &gt;= cap</c> false until the next launch. Reset semantics still apply:
    /// dydo agent claim or release clears the saturation.
    /// </summary>
    public bool SaturateResumeAttempts(string agentName, int cap)
    {
        if (!TryAcquireLock(agentName, out _))
            return false;
        try
        {
            var state = GetAgentState(agentName) ?? new AgentState { Name = agentName };
            state.ResumeAttempts = cap;
            state.LastResumeLaunchedAt = null;
            WriteStateFile(agentName, state);
            return true;
        }
        finally
        {
            ReleaseLock(agentName);
        }
    }

    /// <summary>
    /// Sets or clears the derived needs-human attention flag (Decision 030 §1) on an agent's state and
    /// mirrors it to the agent's current task file frontmatter, so the signal is durable and syncable.
    /// The state write is skipped when the flag is already at <paramref name="value"/>, but the task
    /// mirror always runs so a stale or missing frontmatter key is reconciled. Returns false only on
    /// lock contention.
    /// </summary>
    public bool SetNeedsHuman(string agentName, bool value) => SetNeedsHuman(agentName, value, null);

    /// <summary>
    /// As <see cref="SetNeedsHuman(string, bool)"/>, but with a <paramref name="taskHint"/> for the
    /// task-file mirror. A caller that captured the agent's task from state BEFORE acquiring the lock
    /// (e.g. the watchdog sweep) passes it here so the mirror is still reconciled if a concurrent
    /// Release nulled <c>state.Task</c> in the meantime — otherwise a stale <c>needs-human: true</c>
    /// in the task file would be stranded.
    /// </summary>
    public bool SetNeedsHuman(string agentName, bool value, string? taskHint)
    {
        if (!TryAcquireLock(agentName, out _))
            return false;
        try
        {
            var state = GetAgentState(agentName) ?? new AgentState { Name = agentName };
            if (state.NeedsHuman != value)
            {
                state.NeedsHuman = value;
                WriteStateFile(agentName, state);
            }
            MirrorNeedsHumanToTask(string.IsNullOrEmpty(state.Task) ? taskHint : state.Task, value);
            return true;
        }
        finally
        {
            ReleaseLock(agentName);
        }
    }

    private void MirrorNeedsHumanToTask(string? task, bool value)
    {
        if (string.IsNullOrEmpty(task)) return;
        try
        {
            var taskFile = Path.Combine(_configService.GetTasksPath(_basePath), $"{task}.md");
            if (!File.Exists(taskFile)) return;
            var content = File.ReadAllText(taskFile);
            var updated = FrontmatterParser.UpsertField(content, "needs-human", value ? "true" : "false");
            if (updated != content)
                File.WriteAllText(taskFile, updated);
        }
        catch
        {
            // Best-effort mirror: the canonical flag is agent state; a task-file IO failure must not
            // fail the state write that already succeeded.
        }
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
            needs-human: {state.NeedsHuman.ToString().ToLowerInvariant()}
            window-id: {state.WindowId ?? "null"}
            auto-close: {state.AutoClose.ToString().ToLowerInvariant()}
            resume-attempts: {state.ResumeAttempts}
            last-resume-launched-at: {(state.LastResumeLaunchedAt.HasValue ? state.LastResumeLaunchedAt.Value.ToString("o") : "null")}
            pre-resume-pid: {state.PreResumePid?.ToString() ?? "null"}
            launched-pid: {state.LaunchedPid?.ToString() ?? "null"}
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

        // Atomic replace: write to a same-directory temp file then rename over the destination.
        // On POSIX this is rename(2) (always atomic). On NTFS, File.Move(..., overwrite: true)
        // uses MoveFileEx(MOVEFILE_REPLACE_EXISTING). Same-directory is required: cross-volume
        // rename is non-atomic on every OS we support. The temp suffix combines PID and a Guid
        // to avoid collisions if multiple agents are written from the same process under load.
        // (Within an agent's lifecycle the per-agent .claim.lock already serializes writers; the
        // PID+Guid suffix is defence-in-depth for cross-agent or unlocked-call paths.)
        var tempPath = $"{statePath}.tmp.{Environment.ProcessId}.{Guid.NewGuid():N}";
        File.WriteAllText(tempPath, content);
        try
        {
            File.Move(tempPath, statePath, overwrite: true);
        }
        catch
        {
            // Best-effort cleanup of the orphaned temp file if the rename failed (e.g. a Windows
            // sharing violation from a concurrent unlocked reader). The next call writes a fresh
            // temp anyway; we don't want a partially-named file lingering.
            try { File.Delete(tempPath); } catch { }
            throw;
        }
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
        ["needs-human"] = (s, v) => s.NeedsHuman = v == "true",
        ["resume-attempts"] = (s, v) => s.ResumeAttempts = int.TryParse(v, out var n) ? n : 0,
        ["last-resume-launched-at"] = (s, v) =>
        {
            if (v is "null" or "") return;
            if (DateTime.TryParse(v, null, System.Globalization.DateTimeStyles.RoundtripKind, out var ts))
                s.LastResumeLaunchedAt = ts;
        },
        ["pre-resume-pid"] = (s, v) =>
        {
            if (v is "null" or "") return;
            if (int.TryParse(v, out var p)) s.PreResumePid = p;
        },
        ["launched-pid"] = (s, v) =>
        {
            if (v is "null" or "") return;
            if (int.TryParse(v, out var p)) s.LaunchedPid = p;
        },
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
    private List<string> ComputeUnreadMustReads(string agentName, string role, string? task = null)
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

        // Conditional must-reads (Decision 013: data-driven via role JSON)
        var conditionalMustReads = _roleDefinitions.TryGetValue(role, out var roleDef)
            ? roleDef.ConditionalMustReads : [];
        MustReadTracker.AddConditionalMustReads(mustReads, workspace, task, projectRoot,
            agentName, conditionalMustReads, _inboxReader);

        // Read-completion is tracked live by the guard (state-based); return the full list.
        return mustReads.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
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

    #endregion

    #region Lock File Support

    private record ClaimLock(int Pid, DateTime Acquired);

    [JsonSerializable(typeof(ClaimLock))]
    private partial class RegistryLockJsonContext : JsonSerializerContext { }

    private string GetLockFilePath(string agentName) =>
        Path.Combine(GetAgentWorkspace(agentName), ".claim.lock");

    /// <summary>
    /// Attempts to acquire an exclusive lock at the given path.
    /// Handles stale locks from crashed processes.
    /// agentName is used for error messages only; lockPath determines the actual file.
    /// </summary>
    internal static bool TryAcquireLockAtPath(string lockPath, string agentName, out string error, int retryCount = 0)
    {
        error = string.Empty;

        // Ensure workspace directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);

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
                return TryAcquireLockAtPath(lockPath, agentName, out error, retryCount + 1);
            }
            catch (JsonException)
            {
                // Corrupt lock file - treat as stale, delete and retry
                // If delete fails, retry will handle it (either succeeds or fails with proper error)
                try { File.Delete(lockPath); } catch (IOException) { }
                return TryAcquireLockAtPath(lockPath, agentName, out error, retryCount + 1);
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
    /// Releases the lock file at the given path.
    /// </summary>
    internal static void ReleaseLockAtPath(string lockPath)
    {
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

    /// <summary>
    /// Attempts to acquire an exclusive lock for claiming/releasing an agent.
    /// </summary>
    private bool TryAcquireLock(string agentName, out string error, int retryCount = 0) =>
        TryAcquireLockAtPath(GetLockFilePath(agentName), agentName, out error, retryCount);

    /// <summary>
    /// Releases the lock file for an agent.
    /// </summary>
    private void ReleaseLock(string agentName) =>
        ReleaseLockAtPath(GetLockFilePath(agentName));

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
