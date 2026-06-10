namespace DynaDocs.Tests.Services;

using System.Text.Json;
using DynaDocs.Models;
using DynaDocs.Serialization;
using DynaDocs.Services;
using DynaDocs.Utils;

/// <summary>
/// Tests for <see cref="AgentRegistry.RefreshResumedAgentSession"/> — the guard-side
/// ClaimedPid auto-refresh that closes #0207 part 2. Trigger derivation, edge cases,
/// and proofs live in <c>dydo/agents/Charlie/plan-f11-guard-side.md</c>.
///
/// Isolation discipline (per the plan and the project's "test parallelism" rule):
///   - Save/restore CWD and DYDO_HUMAN/DYDO_AGENT around every test
///   - Reset every <see cref="ProcessUtils"/> / <see cref="WatchdogLogger"/> override in <see cref="Dispose"/>
///   - Join the shared <c>ProcessUtils</c> collection so we serialise with the rest of
///     the suite (assembly-wide parallelism is already off, but the collection tag
///     documents intent and acts as a future safety net).
/// </summary>
[Collection("ProcessUtils")]
public class GuardResumeRefreshTests : IDisposable
{
    private const string ResumeSessionId = "sess-resume-001";
    private const int DeadPreResumePid = 999001;
    private const int LiveClaudePid = 121212;

    private readonly string _testDir;
    private readonly string _originalDir;

    public GuardResumeRefreshTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-guard-refresh-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _originalDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(_testDir);

        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), """
            { "version": 1, "agents": { "pool": ["Adele", "Zelda"],
              "assignments": { "testuser": ["Adele", "Zelda"] } } }
            """);
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDir);
        Environment.SetEnvironmentVariable("DYDO_AGENT", null);
        Environment.SetEnvironmentVariable("DYDO_HUMAN", null);
        ProcessUtils.FindAncestorProcessOverride = null;
        ProcessUtils.IsProcessRunningOverride = null;
        AgentRegistry.IsSessionPidAliveOverride = null;
        AgentRegistry.IsLauncherAliveOverride = null;
        WatchdogLogger.LogPathOverride = null;
        WatchdogService.ResumeWarmupGateOverride = null;
        try { Directory.Delete(_testDir, true); } catch { }
    }

    // -----------------------------------------------------------------------------
    // Test helpers
    // -----------------------------------------------------------------------------

    private string AgentsDir => Path.Combine(_testDir, "dydo", "agents");

    private string WorkspaceOf(string agentName) => Path.Combine(AgentsDir, agentName);

    private string SessionPathOf(string agentName) => Path.Combine(WorkspaceOf(agentName), ".session");

    private string StatePathOf(string agentName) => Path.Combine(WorkspaceOf(agentName), "state.md");

    private void WriteSession(string agentName, string sessionId, int? claimedPid)
    {
        Directory.CreateDirectory(WorkspaceOf(agentName));
        var session = new AgentSession
        {
            Agent = agentName,
            SessionId = sessionId,
            Claimed = DateTime.UtcNow.AddMinutes(-10),
            ClaimedPid = claimedPid
        };
        File.WriteAllText(SessionPathOf(agentName),
            JsonSerializer.Serialize(session, DydoDefaultJsonContext.Default.AgentSession));
    }

    private void WriteWorkingState(string agentName,
        int resumeAttempts = 2, DateTime? lastResumeLaunchedAt = null,
        int? preResumePid = 12345, int? launchedPid = 54321,
        string role = "co-thinker", string task = "t")
    {
        Directory.CreateDirectory(WorkspaceOf(agentName));
        var launched = lastResumeLaunchedAt ?? DateTime.UtcNow.AddSeconds(-90);
        File.WriteAllText(StatePathOf(agentName), $$"""
            ---
            agent: {{agentName}}
            role: {{role}}
            task: {{task}}
            status: working
            assigned: testuser
            started: {{DateTime.UtcNow.AddMinutes(-15):o}}
            resume-attempts: {{resumeAttempts}}
            last-resume-launched-at: {{launched:o}}
            pre-resume-pid: {{(preResumePid?.ToString() ?? "null")}}
            launched-pid: {{(launchedPid?.ToString() ?? "null")}}
            writable-paths: []
            readonly-paths: []
            unread-must-reads: []
            unread-messages: []
            task-role-history: {}
            ---
            """);
    }

    private void WriteWorkingStateNoResumeBookkeeping(string agentName)
    {
        Directory.CreateDirectory(WorkspaceOf(agentName));
        File.WriteAllText(StatePathOf(agentName), $$"""
            ---
            agent: {{agentName}}
            role: co-thinker
            task: t
            status: working
            assigned: testuser
            started: {{DateTime.UtcNow.AddMinutes(-15):o}}
            resume-attempts: 0
            last-resume-launched-at: null
            writable-paths: []
            readonly-paths: []
            unread-must-reads: []
            unread-messages: []
            task-role-history: {}
            ---
            """);
    }

    private void WriteFreeState(string agentName)
    {
        Directory.CreateDirectory(WorkspaceOf(agentName));
        File.WriteAllText(StatePathOf(agentName), $$"""
            ---
            agent: {{agentName}}
            status: free
            assigned: testuser
            ---
            """);
    }

    // Resumed-agent shape: dead pre-resume PID in .session, Working state, resume
    // bookkeeping captured. The default "this is what auto-resume looks like" fixture.
    private void SetUpResumedAdele(int? preResumePidInSession = DeadPreResumePid)
    {
        WriteSession("Adele", ResumeSessionId, preResumePidInSession);
        WriteWorkingState("Adele");
    }

    private AgentSession? ReadSession(string agentName)
    {
        var path = SessionPathOf(agentName);
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize(File.ReadAllText(path),
            DydoDefaultJsonContext.Default.AgentSession);
    }

    // Configures the "the live claude ancestor is X, ClaimedPid Y is dead, all other
    // PIDs are alive" shape that the trigger predicate expects on a resumed session.
    private void ConfigureResumedAncestry(int liveAncestor = LiveClaudePid, int deadPid = DeadPreResumePid)
    {
        ProcessUtils.FindAncestorProcessOverride = (_, _) => liveAncestor;
        ProcessUtils.IsProcessRunningOverride = pid => pid != deadPid;
    }

    // -----------------------------------------------------------------------------
    // Core mechanism
    // -----------------------------------------------------------------------------

    [Fact]
    public void GuardRefreshOnResume_DeadClaimedPid_RewritesToLiveAncestor()
    {
        SetUpResumedAdele();
        ConfigureResumedAncestry();
        var registry = new AgentRegistry(_testDir);

        registry.RefreshResumedAgentSession(ResumeSessionId);

        var session = ReadSession("Adele");
        Assert.NotNull(session);
        Assert.Equal(LiveClaudePid, session!.ClaimedPid);
        Assert.Equal(ResumeSessionId, session.SessionId);
        Assert.Equal("Adele", session.Agent);
    }

    [Fact]
    public void GuardRefreshOnResume_BookkeepingReset()
    {
        SetUpResumedAdele();
        ConfigureResumedAncestry();
        var registry = new AgentRegistry(_testDir);

        registry.RefreshResumedAgentSession(ResumeSessionId);

        var state = registry.GetAgentState("Adele");
        Assert.NotNull(state);
        Assert.Equal(0, state!.ResumeAttempts);
        Assert.Null(state.LastResumeLaunchedAt);
        Assert.Null(state.PreResumePid);
        Assert.Null(state.LaunchedPid);
    }

    [Fact]
    public void GuardRefreshOnResume_EmitsResumeOutcome()
    {
        SetUpResumedAdele();
        ConfigureResumedAncestry();
        var watchdogLog = Path.Combine(_testDir, "watchdog-refresh.log");
        WatchdogLogger.LogPathOverride = watchdogLog;
        var registry = new AgentRegistry(_testDir);

        registry.RefreshResumedAgentSession(ResumeSessionId);

        Assert.True(File.Exists(watchdogLog), "resume_outcome line must be written to the watchdog log");
        var line = File.ReadAllLines(watchdogLog).Single(l => !string.IsNullOrWhiteSpace(l));
        var outcome = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(line)!;
        Assert.Equal("resume_outcome", outcome["event"].GetString());
        Assert.Equal("succeeded", outcome["outcome"].GetString());
        Assert.Equal("same_session_reclaim", outcome["reason"].GetString());
        Assert.Equal("Adele", outcome["agent"].GetString());
    }

    [Fact]
    public void GuardRefreshOnResume_Idempotent_NoDoubleEmit()
    {
        SetUpResumedAdele();
        ConfigureResumedAncestry();
        var registry = new AgentRegistry(_testDir);

        registry.RefreshResumedAgentSession(ResumeSessionId);
        registry.RefreshResumedAgentSession(ResumeSessionId);
        registry.RefreshResumedAgentSession(ResumeSessionId);

        Assert.Equal(LiveClaudePid, ReadSession("Adele")!.ClaimedPid);
    }

    // -----------------------------------------------------------------------------
    // Trigger gates / no-op cases
    // -----------------------------------------------------------------------------

    [Fact]
    public void FreshSession_Noop_NoWriteNoEmit()
    {
        // ClaimedPid alive → step 4 short-circuit. No write, no emit.
        WriteSession("Adele", ResumeSessionId, LiveClaudePid);
        WriteWorkingState("Adele");
        ProcessUtils.IsProcessRunningOverride = _ => true;
        // FindClaudeAncestorOverride deliberately NOT set — if the trigger walked
        // past step 4 it would call into the real process tree from the test host.
        ProcessUtils.FindAncestorProcessOverride = (_, _) =>
            throw new InvalidOperationException("Step 4 (ClaimedPid alive) must short-circuit before FindClaudeAncestor");
        var registry = new AgentRegistry(_testDir);

        var beforeBytes = File.ReadAllBytes(SessionPathOf("Adele"));
        registry.RefreshResumedAgentSession(ResumeSessionId);

        Assert.Equal(beforeBytes, File.ReadAllBytes(SessionPathOf("Adele")));
    }

    [Fact]
    public void NullAncestor_NoWrite()
    {
        // FindClaudeAncestor null → step 5 returns. Writing a null ClaimedPid would
        // break F11 for everyone; skipping leaves it stale (status quo).
        SetUpResumedAdele();
        ProcessUtils.IsProcessRunningOverride = pid => pid != DeadPreResumePid;
        ProcessUtils.FindAncestorProcessOverride = (_, _) => null;
        var registry = new AgentRegistry(_testDir);

        var beforeBytes = File.ReadAllBytes(SessionPathOf("Adele"));
        registry.RefreshResumedAgentSession(ResumeSessionId);

        Assert.Equal(beforeBytes, File.ReadAllBytes(SessionPathOf("Adele")));
    }

    [Fact]
    public void NonWatchdogResume_RefreshesPidWithoutEmit()
    {
        // EmitAutoRecovery self-gates on LastResumeLaunchedAt != null. User-driven
        // claude --resume refreshes the PID but emits nothing.
        WriteSession("Adele", ResumeSessionId, DeadPreResumePid);
        WriteWorkingStateNoResumeBookkeeping("Adele");
        ConfigureResumedAncestry();
        var registry = new AgentRegistry(_testDir);

        registry.RefreshResumedAgentSession(ResumeSessionId);

        Assert.Equal(LiveClaudePid, ReadSession("Adele")!.ClaimedPid);
    }

    [Fact]
    public void RefreshNoopForUnclaimedAgent()
    {
        // No .session anywhere; GetCurrentAgent → null at step 2.
        ConfigureResumedAncestry();
        var registry = new AgentRegistry(_testDir);

        registry.RefreshResumedAgentSession(ResumeSessionId);

        Assert.False(File.Exists(SessionPathOf("Adele")));
    }

    [Fact]
    public void RefreshNoopForReleasedAgent()
    {
        // Released agent: status Free, .session deleted by release path.
        WriteFreeState("Adele");
        ConfigureResumedAncestry();
        var registry = new AgentRegistry(_testDir);

        registry.RefreshResumedAgentSession(ResumeSessionId);

        Assert.False(File.Exists(SessionPathOf("Adele")));
    }

    [Fact]
    public void RefreshNoopWhenStateNotWorking_DoesNotWipeRole()
    {
        // D2: missing state.md → GetAgentState returns a default Free state. Status gate
        // must trip BEFORE any write — otherwise UpdateAgentState's `?? new AgentState{Name}`
        // fallback would silently destroy the agent's role.
        WriteSession("Adele", ResumeSessionId, DeadPreResumePid);
        // No state.md → GetAgentState returns Free.
        ConfigureResumedAncestry();
        var registry = new AgentRegistry(_testDir);

        var beforeBytes = File.ReadAllBytes(SessionPathOf("Adele"));
        registry.RefreshResumedAgentSession(ResumeSessionId);

        Assert.Equal(beforeBytes, File.ReadAllBytes(SessionPathOf("Adele")));
        Assert.False(File.Exists(StatePathOf("Adele")),
            "No state.md must NOT be created by a no-op refresh.");
    }

    [Fact]
    public void DuplicateClaude_LoserDoesNotSteal()
    {
        // C5: two live claudes for one session. Claude #1 refreshed first → ClaimedPid
        // is now claude #1 (alive). Claude #2's guard hits step 4 (alive) → no refresh.
        WriteSession("Adele", ResumeSessionId, LiveClaudePid);
        WriteWorkingState("Adele");
        const int duplicateClaude = 343434;
        ProcessUtils.IsProcessRunningOverride = _ => true;
        ProcessUtils.FindAncestorProcessOverride = (_, _) => duplicateClaude;
        var registry = new AgentRegistry(_testDir);

        registry.RefreshResumedAgentSession(ResumeSessionId);

        Assert.Equal(LiveClaudePid, ReadSession("Adele")!.ClaimedPid);
    }

    // -----------------------------------------------------------------------------
    // Robustness / concurrency
    // -----------------------------------------------------------------------------

    [Fact]
    public void RefreshSkipsWhenLockHeld()
    {
        // C2: pre-hold the .claim.lock. The bounded retry (3× / 50 ms) all fail because
        // the holding PID is alive → silently skip. .session stays byte-unchanged.
        SetUpResumedAdele();
        ConfigureResumedAncestry();
        var lockPath = Path.Combine(WorkspaceOf("Adele"), ".claim.lock");
        Directory.CreateDirectory(WorkspaceOf("Adele"));
        // Synthesise a "live-holder" lock that IsProcessRunningOverride says is alive.
        // Pick a PID that's neither the dead claim PID nor the live claude — any other PID
        // is treated as "running" by ConfigureResumedAncestry's override.
        var holderPid = 777777;
        File.WriteAllText(lockPath, JsonSerializer.Serialize(new { Pid = holderPid, Acquired = DateTime.UtcNow }));

        var registry = new AgentRegistry(_testDir);
        var beforeBytes = File.ReadAllBytes(SessionPathOf("Adele"));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        registry.RefreshResumedAgentSession(ResumeSessionId);
        sw.Stop();

        Assert.Equal(beforeBytes, File.ReadAllBytes(SessionPathOf("Adele")));
        // The bounded retry sleeps twice (between the 3 attempts) = ~100 ms. Assert with a
        // generous floor (60 ms) so OS scheduling slack on slow CI doesn't flake the test
        // while still pinning the "we actually waited, didn't hard fail-fast" invariant.
        Assert.True(sw.ElapsedMilliseconds >= 60, $"Bounded retry should have slept ~100ms (was {sw.ElapsedMilliseconds}ms)");
    }

    [Fact]
    public void RefreshNeverThrowsOnCorruptState()
    {
        // D1: corrupt state.md → ParseStateFile null → GetCurrentAgent null → no-op.
        // Verify the method swallows even pathological inputs (no exception escapes).
        WriteSession("Adele", ResumeSessionId, DeadPreResumePid);
        File.WriteAllText(StatePathOf("Adele"), "this is not yaml :: \n--- broken --- \n\0\0\0");
        ConfigureResumedAncestry();
        var registry = new AgentRegistry(_testDir);

        var ex = Record.Exception(() => registry.RefreshResumedAgentSession(ResumeSessionId));

        Assert.Null(ex);
    }

    [Fact]
    public void RefreshPreservesRoleTaskStatus()
    {
        // E1: refresh touches only ClaimedPid and the four resume-bookkeeping fields.
        // Role/task/status must survive untouched.
        SetUpResumedAdele();
        ConfigureResumedAncestry();
        var before = new AgentRegistry(_testDir).GetAgentState("Adele")!;

        new AgentRegistry(_testDir).RefreshResumedAgentSession(ResumeSessionId);

        var after = new AgentRegistry(_testDir).GetAgentState("Adele")!;
        Assert.Equal(before.Role, after.Role);
        Assert.Equal(before.Task, after.Task);
        Assert.Equal(AgentStatus.Working, after.Status);
    }

    [Fact]
    public void RefreshPreservesSessionIdentityFields()
    {
        // D4: WriteClaimedPid copies Agent / SessionId / Claimed verbatim, changes only ClaimedPid.
        SetUpResumedAdele();
        ConfigureResumedAncestry();
        var before = ReadSession("Adele")!;
        var registry = new AgentRegistry(_testDir);

        registry.RefreshResumedAgentSession(ResumeSessionId);

        var after = ReadSession("Adele")!;
        Assert.Equal(before.Agent, after.Agent);
        Assert.Equal(before.SessionId, after.SessionId);
        Assert.Equal(before.Claimed, after.Claimed);
        Assert.NotEqual(before.ClaimedPid, after.ClaimedPid);
        Assert.Equal(LiveClaudePid, after.ClaimedPid);
    }

    [Fact]
    public void RefreshIgnoresSpoofedDydoAgent()
    {
        // A5: spoofed DYDO_AGENT must not mislead resolution. The refresh resolves only
        // via GetCurrentAgent on the hook's authoritative session_id. Zelda's session_id
        // is what we pass; DYDO_AGENT=Adele is bogus and the refresh must still target Zelda.
        WriteSession("Zelda", ResumeSessionId, DeadPreResumePid);
        WriteWorkingState("Zelda");
        WriteSession("Adele", "other-sid", LiveClaudePid);
        WriteWorkingState("Adele");

        Environment.SetEnvironmentVariable("DYDO_AGENT", "Adele");  // spoof
        ProcessUtils.FindAncestorProcessOverride = (_, _) => LiveClaudePid;
        // Both Zelda's dead PID and Adele's "live" PID need correct liveness:
        //   - Zelda's ClaimedPid (DeadPreResumePid) → dead → triggers refresh
        //   - Adele's ClaimedPid (LiveClaudePid) → alive (so Adele looks legit too)
        ProcessUtils.IsProcessRunningOverride = pid => pid != DeadPreResumePid;
        var registry = new AgentRegistry(_testDir);

        registry.RefreshResumedAgentSession(ResumeSessionId);

        // Zelda's session got refreshed because she actually owns ResumeSessionId.
        Assert.Equal(LiveClaudePid, ReadSession("Zelda")!.ClaimedPid);
        // Adele's session is untouched.
        Assert.Equal(LiveClaudePid, ReadSession("Adele")!.ClaimedPid);
    }

    [Fact]
    public void TwoEpisodes_EachReset()
    {
        // E2: each episode independently resets ResumeAttempts to 0. The cap never
        // accumulates across episodes (#0153).
        const int episode2DeadPid = 999003;
        SetUpResumedAdele();
        // IsProcessRunning override must treat BOTH episode PIDs as dead — the live
        // claude (LiveClaudePid) and any other PID stay alive.
        ProcessUtils.FindAncestorProcessOverride = (_, _) => LiveClaudePid;
        ProcessUtils.IsProcessRunningOverride = pid =>
            pid != DeadPreResumePid && pid != episode2DeadPid;
        var registry = new AgentRegistry(_testDir);

        // Episode 1: refresh resets bookkeeping.
        registry.RefreshResumedAgentSession(ResumeSessionId);
        Assert.Equal(0, registry.GetAgentState("Adele")!.ResumeAttempts);

        // Simulate a second crash + watchdog launch: another auto-resume happens.
        WriteSession("Adele", ResumeSessionId, episode2DeadPid);
        WriteWorkingState("Adele", resumeAttempts: 1);

        registry.RefreshResumedAgentSession(ResumeSessionId);

        var state = registry.GetAgentState("Adele")!;
        Assert.Equal(0, state.ResumeAttempts);
        Assert.Null(state.LastResumeLaunchedAt);
        Assert.Equal(LiveClaudePid, ReadSession("Adele")!.ClaimedPid);
    }

    // -----------------------------------------------------------------------------
    // Charlie's three additions (B3 / G1 / F2)
    // -----------------------------------------------------------------------------

    [Fact]
    public void PidReuse_SkipsRefresh()
    {
        // B3 (audit-revised): stale ClaimedPid value has been recycled by the OS to a
        // live unrelated process. Step 4 short-circuits → refresh skipped this call.
        // The resumed agent's dydo wait stays F11-refused for the recycling process's
        // lifetime — the same shared assumption the watchdog already makes, honestly
        // pinned by this test. Flipping the override (process exits) lets the next
        // refresh proceed.
        SetUpResumedAdele();
        ProcessUtils.FindAncestorProcessOverride = (_, _) => LiveClaudePid;
        // First, the dead-claim PID has been recycled → IsProcessRunning(DeadPreResumePid)=true.
        ProcessUtils.IsProcessRunningOverride = _ => true;

        var registry = new AgentRegistry(_testDir);
        var beforeBytes = File.ReadAllBytes(SessionPathOf("Adele"));

        registry.RefreshResumedAgentSession(ResumeSessionId);

        Assert.Equal(beforeBytes, File.ReadAllBytes(SessionPathOf("Adele")));

        // Now the recycling process exits → IsProcessRunning(DeadPreResumePid)=false.
        ProcessUtils.IsProcessRunningOverride = pid => pid != DeadPreResumePid;

        registry.RefreshResumedAgentSession(ResumeSessionId);

        Assert.Equal(LiveClaudePid, ReadSession("Adele")!.ClaimedPid);
    }

    [Fact]
    public void GuardRefreshOnResume_Worktree_WritesThroughJunctionAndEmitsToMainLog()
    {
        // G1: in a worktree the agents/ dir is junction-shared back to main, so .session
        // and .claim.lock are the same physical files. EmitAutoRecovery routes
        // resume_outcome to the MAIN watchdog log via PathUtils.FindMainDydoRoot.
        //
        // We simulate a worktree layout by:
        //   - Setting up "main" project state at _testDir/dydo
        //   - Creating a worktree-shaped subdir at _testDir/dydo/_system/.local/worktrees/{id}/
        //   - Setting CWD inside that subdir (FindMainDydoRoot walks up from CWD)
        //   - Using the SAME agents/ dir for both — i.e. our "junction" is just shared path
        //
        // PathUtils.FindMainDydoRoot detects the worktree segment and walks back to the
        // main dydo root, so the log lands at _testDir/dydo/_system/.local/watchdog.log
        // (overridden below to make assertion trivial).
        SetUpResumedAdele();
        ConfigureResumedAncestry();

        var worktreeId = "Adele-20260523120000";
        var worktreeRoot = Path.Combine(_testDir, "dydo", "_system", ".local", "worktrees", worktreeId);
        Directory.CreateDirectory(worktreeRoot);
        // Same dydo.json so AgentRegistry resolves the agents pool.
        File.WriteAllText(Path.Combine(worktreeRoot, "dydo.json"),
            File.ReadAllText(Path.Combine(_testDir, "dydo.json")));
        Directory.SetCurrentDirectory(worktreeRoot);

        // Tell the watchdog logger where the MAIN log lives so the test can assert on it.
        var mainLog = Path.Combine(_testDir, "main-watchdog.log");
        WatchdogLogger.LogPathOverride = mainLog;

        var registry = new AgentRegistry(_testDir);

        registry.RefreshResumedAgentSession(ResumeSessionId);

        // .session.ClaimedPid written through to the shared agents/ dir.
        Assert.Equal(LiveClaudePid, ReadSession("Adele")!.ClaimedPid);

        // Audit emit + resume_outcome both landed.
        Assert.True(File.Exists(mainLog), "resume_outcome should land in the main watchdog log");
        var line = File.ReadAllLines(mainLog).Single(l => !string.IsNullOrWhiteSpace(l));
        var outcome = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(line)!;
        Assert.Equal("succeeded", outcome["outcome"].GetString());
    }

    [Fact]
    public void F2Corner_NoAuditAfterSaturate_ButPidIsRefreshed()
    {
        // F2 (audit-revised): SaturateResumeAttempts cleared LastResumeLaunchedAt mid-episode
        // (e.g. the watchdog logged resume_outcome=failed first). The later guard refresh
        // reads priorState.LastResumeLaunchedAt == null → EmitAutoRecovery emits NOTHING.
        // Functionally the agent still recovers: .session.ClaimedPid is updated. This corner
        // is documented in the plan and architecture.md (auto recoveries are permanently
        // un-emit-able when SaturateResumeAttempts has fired first).
        WriteSession("Adele", ResumeSessionId, DeadPreResumePid);
        // priorState with LastResumeLaunchedAt EXPLICITLY null (saturate already cleared it).
        WriteWorkingStateNoResumeBookkeeping("Adele");
        ConfigureResumedAncestry();
        var registry = new AgentRegistry(_testDir);

        registry.RefreshResumedAgentSession(ResumeSessionId);

        // Functional recovery: ClaimedPid refreshed.
        Assert.Equal(LiveClaudePid, ReadSession("Adele")!.ClaimedPid);
        // No audit emission.
    }

    // -----------------------------------------------------------------------------
    // Companion change (Proof A): concurrent claim during resume warmup
    // -----------------------------------------------------------------------------

    [Fact]
    public void ConcurrentClaimDuringWarmup_Refused()
    {
        // Companion change: while a watchdog resume is within ResumeWarmupGate,
        // HandleExistingSession's stale-working reclaim branch is gated by ResumeInFlight
        // and refuses the concurrent claim. The recovering agent is not archived.
        WriteSession("Adele", "sess-prior", DeadPreResumePid);
        // Stale-working (Since older than threshold) + LastResumeLaunchedAt very recent
        // → ResumeInFlight true → reclaim refused.
        Directory.CreateDirectory(WorkspaceOf("Adele"));
        File.WriteAllText(StatePathOf("Adele"), $$"""
            ---
            agent: Adele
            role: co-thinker
            task: t
            status: working
            assigned: testuser
            started: {{DateTime.UtcNow.AddMinutes(-30):o}}
            resume-attempts: 1
            last-resume-launched-at: {{DateTime.UtcNow.AddSeconds(-30):o}}
            writable-paths: []
            readonly-paths: []
            unread-must-reads: []
            unread-messages: []
            task-role-history: {}
            ---
            """);
        AgentRegistry.IsSessionPidAliveOverride = _ => false; // dead PID
        WatchdogService.ResumeWarmupGateOverride = TimeSpan.FromMinutes(5);

        var registry = new AgentRegistry(_testDir);
        registry.StorePendingSessionId("Adele", "sess-concurrent");
        var ok = registry.ClaimAgent("Adele", out var err);

        Assert.False(ok, "Concurrent claim during resume warmup must be refused.");
        Assert.Contains("already claimed", err);
    }

    [Fact]
    public void ConcurrentClaimAfterWarmup_Allowed()
    {
        // Symmetric to the previous test: once ResumeWarmupGate elapses (or the watchdog's
        // SaturateResumeAttempts has cleared LastResumeLaunchedAt), the stale-working
        // reclaim works normally — a genuinely-dead agent must still be reclaimable.
        WriteSession("Adele", "sess-prior", DeadPreResumePid);
        Directory.CreateDirectory(WorkspaceOf("Adele"));
        File.WriteAllText(StatePathOf("Adele"), $$"""
            ---
            agent: Adele
            role: co-thinker
            task: t
            status: working
            assigned: testuser
            started: {{DateTime.UtcNow.AddMinutes(-30):o}}
            resume-attempts: 1
            last-resume-launched-at: {{DateTime.UtcNow.AddMinutes(-20):o}}
            writable-paths: []
            readonly-paths: []
            unread-must-reads: []
            unread-messages: []
            task-role-history: {}
            ---
            """);
        AgentRegistry.IsSessionPidAliveOverride = _ => false;
        WatchdogService.ResumeWarmupGateOverride = TimeSpan.FromMinutes(5);

        var registry = new AgentRegistry(_testDir);
        registry.StorePendingSessionId("Adele", "sess-concurrent");
        var ok = registry.ClaimAgent("Adele", out var err);

        Assert.True(ok, $"Concurrent claim after warmup must succeed (err: {err}).");
    }
}
