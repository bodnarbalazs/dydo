namespace DynaDocs.Tests.Commands;

using DynaDocs.Commands;
using DynaDocs.Models;
using DynaDocs.Services;

/// <summary>
/// The needs-human attention-flag runtime (Decision 030 §1): the derived flag on agent state, its
/// mirror to the task file, the guard's AskUserQuestion/Stop detection and self-heal, and the explicit
/// hand raise/lower command. Each test builds its own temp project; CWD is switched only for the
/// command-wiring cases and restored in Dispose.
/// </summary>
[Collection("Integration")]
public class NeedsHumanTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _dydoDir;
    private readonly string _originalDir;
    private readonly AgentRegistry _registry;

    public NeedsHumanTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-needshuman-" + Guid.NewGuid().ToString("N")[..8]);
        _dydoDir = Path.Combine(_testDir, "dydo");
        Directory.CreateDirectory(Path.Combine(_dydoDir, "agents"));
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), """
            { "version": 1, "structure": { "root": "dydo" },
              "agents": { "pool": ["Adele"], "assignments": { "testuser": ["Adele"] } } }
            """);
        _originalDir = Environment.CurrentDirectory;
        _registry = new AgentRegistry(_testDir);
    }

    public void Dispose()
    {
        Environment.CurrentDirectory = _originalDir;
        if (Directory.Exists(_testDir))
        {
            for (var i = 0; i < 3; i++)
            {
                try { Directory.Delete(_testDir, true); return; }
                catch (IOException) when (i < 2) { Thread.Sleep(50); }
            }
        }
    }

    private string AgentDir(string agent)
    {
        var dir = Path.Combine(_dydoDir, "agents", agent);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private void WriteState(string agent, string status = "working", string task = "my-task", bool needsHuman = false, string sessionId = "sess-1")
    {
        File.WriteAllText(Path.Combine(AgentDir(agent), "state.md"), $"""
            ---
            agent: {agent}
            role: code-writer
            task: {task}
            status: {status}
            assigned: testuser
            needs-human: {needsHuman.ToString().ToLowerInvariant()}
            ---
            """);
        File.WriteAllText(Path.Combine(AgentDir(agent), ".session"),
            $"{{\"Agent\":\"{agent}\",\"SessionId\":\"{sessionId}\",\"Claimed\":\"{DateTime.UtcNow:o}\",\"ClaimedPid\":123}}");
    }

    private string WriteTaskFile(string task)
    {
        var tasksDir = Path.Combine(_dydoDir, "project", "tasks");
        Directory.CreateDirectory(tasksDir);
        var path = Path.Combine(tasksDir, $"{task}.md");
        File.WriteAllText(path, $"""
            ---
            area: general
            name: {task}
            status: in-progress
            assigned: Adele
            ---

            # Task: {task}
            """);
        return path;
    }

    private string WriteTaskFileWithFlag(string task, bool needsHuman)
    {
        var tasksDir = Path.Combine(_dydoDir, "project", "tasks");
        Directory.CreateDirectory(tasksDir);
        var path = Path.Combine(tasksDir, $"{task}.md");
        File.WriteAllText(path, $"""
            ---
            area: general
            name: {task}
            status: in-progress
            assigned: Adele
            needs-human: {needsHuman.ToString().ToLowerInvariant()}
            ---

            # Task: {task}
            """);
        return path;
    }

    [Fact]
    public void SetNeedsHuman_PersistsFlagOnState_AndMirrorsToTaskFile()
    {
        WriteState("Adele", task: "my-task");
        var taskPath = WriteTaskFile("my-task");

        Assert.True(_registry.SetNeedsHuman("Adele", true));

        Assert.True(_registry.GetAgentState("Adele")!.NeedsHuman);
        Assert.Contains("needs-human: true", File.ReadAllText(taskPath));
    }

    [Fact]
    public void SetNeedsHuman_False_ClearsFlagAndMirror()
    {
        WriteState("Adele", needsHuman: true);
        var taskPath = WriteTaskFile("my-task");
        _registry.SetNeedsHuman("Adele", true);

        _registry.SetNeedsHuman("Adele", false);

        Assert.False(_registry.GetAgentState("Adele")!.NeedsHuman);
        Assert.Contains("needs-human: false", File.ReadAllText(taskPath));
    }

    [Fact]
    public void Release_ThenWatchdogSweep_ClearsFlagAndTaskFileMirror()
    {
        // Repro for the stranded-mirror bug: an orphan-crash flag mirrors `needs-human: true` into
        // the task file, then the agent is released without another guarded tool call. Release nulls
        // state.Task, so a later watchdog sweep can no longer reach the task file. Release must clear
        // BOTH the flag and its task-file mirror while the task name is still known, and the sweep
        // must then find nothing stale to leave behind.
        WriteState("Adele", status: "working", task: "my-task", needsHuman: false, sessionId: "sess-rel");
        var taskPath = WriteTaskFile("my-task");
        _registry.SetNeedsHuman("Adele", true);
        Assert.Contains("needs-human: true", File.ReadAllText(taskPath));

        Assert.True(_registry.ReleaseAgent("sess-rel", out var relErr), relErr);

        // Release cleared both the canonical flag and the task-file mirror.
        Assert.False(_registry.GetAgentState("Adele")!.NeedsHuman);
        Assert.Contains("needs-human: false", File.ReadAllText(taskPath));

        // The follow-up sweep sees a released agent with a cleared flag: a clean no-op. The task
        // file must stay cleared — no stranded `needs-human: true`.
        WatchdogService.PollNeedsHuman(_dydoDir);

        Assert.False(new AgentRegistry(_testDir).GetAgentState("Adele")!.NeedsHuman);
        Assert.Contains("needs-human: false", File.ReadAllText(taskPath));
        Assert.DoesNotContain("needs-human: true", File.ReadAllText(taskPath));
    }

    [Fact]
    public void SetNeedsHuman_WithTaskHint_ClearsTaskFileMirror_WhenStateTaskAlreadyNulledByRace()
    {
        // Wave-1 race fix (the taskHint branch of SetNeedsHuman). The watchdog sweep captured the
        // agent's task BEFORE taking the lock, but a concurrent Release nulled state.Task in between.
        // With state.Task now empty, the mirror must fall back to the captured hint — otherwise the
        // stranded `needs-human: true` in the task file is never cleared. The literal `task: null`
        // parses to a real null Task, so the ternary's IsNullOrEmpty branch is the one under test.
        WriteState("Adele", status: "free", task: "null", needsHuman: true);
        var taskPath = WriteTaskFileWithFlag("my-task", needsHuman: true);

        Assert.True(_registry.SetNeedsHuman("Adele", false, "my-task"));

        Assert.Contains("needs-human: false", File.ReadAllText(taskPath));
        Assert.DoesNotContain("needs-human: true", File.ReadAllText(taskPath));
    }

    [Fact]
    public void SetNeedsHuman_WithTaskHint_SetsTaskFileMirror_WhenStateTaskAlreadyNulledByRace()
    {
        // Symmetric set-true case: the hint fallback must reach the task file in both directions.
        WriteState("Adele", status: "free", task: "null", needsHuman: false);
        var taskPath = WriteTaskFileWithFlag("my-task", needsHuman: false);

        Assert.True(_registry.SetNeedsHuman("Adele", true, "my-task"));

        Assert.Contains("needs-human: true", File.ReadAllText(taskPath));
    }

    [Fact]
    public void NeedsHuman_RoundTripsThroughStateFile()
    {
        WriteState("Adele", needsHuman: false);
        _registry.SetNeedsHuman("Adele", true);

        // A fresh registry re-parses the on-disk state.md — the flag survives serialization.
        Assert.True(new AgentRegistry(_testDir).GetAgentState("Adele")!.NeedsHuman);
    }

    [Fact]
    public void Guard_AskUserQuestion_SetsFlag_ThenNextToolCallClearsIt()
    {
        WriteState("Adele", status: "working", task: "my-task", needsHuman: false);

        GuardCommand.ReconcileNeedsHuman("askuserquestion", "sess-1", _registry);
        Assert.True(_registry.GetAgentState("Adele")!.NeedsHuman);

        GuardCommand.ReconcileNeedsHuman("read", "sess-1", _registry);
        Assert.False(_registry.GetAgentState("Adele")!.NeedsHuman);
    }

    [Fact]
    public void Guard_NonAskToolCall_WhenFlagAlreadyClear_IsNoOp()
    {
        WriteState("Adele", needsHuman: false);
        GuardCommand.ReconcileNeedsHuman("bash", "sess-1", _registry);
        Assert.False(_registry.GetAgentState("Adele")!.NeedsHuman);
    }

    [Fact]
    public void Guard_Stop_WhileWorkingWithTask_SetsFlag()
    {
        WriteState("Adele", status: "working", task: "my-task", needsHuman: false);
        GuardCommand.ApplyStopSignal("sess-1", _registry);
        Assert.True(_registry.GetAgentState("Adele")!.NeedsHuman);
    }

    [Fact]
    public void Guard_Stop_WhenNotWorking_DoesNotSetFlag()
    {
        WriteState("Adele", status: "free", task: "my-task", needsHuman: false);
        GuardCommand.ApplyStopSignal("sess-1", _registry);
        Assert.False(_registry.GetAgentState("Adele")!.NeedsHuman);
    }

    [Fact]
    public void Guard_Stop_WhenNoTask_DoesNotSetFlag()
    {
        WriteState("Adele", status: "working", task: "null", needsHuman: false);
        GuardCommand.ApplyStopSignal("sess-1", _registry);
        Assert.False(_registry.GetAgentState("Adele")!.NeedsHuman);
    }

    [Fact]
    public void HandRaise_And_HandLower_SetAndClearTheFlag_ForNamedAgent()
    {
        WriteState("Adele", needsHuman: false);
        Environment.CurrentDirectory = _testDir;

        Assert.Equal(0, HandCommand.Create().Parse(["raise", "--agent", "Adele"]).Invoke());
        Assert.True(new AgentRegistry(_testDir).GetAgentState("Adele")!.NeedsHuman);

        Assert.Equal(0, HandCommand.Create().Parse(["lower", "--agent", "Adele"]).Invoke());
        Assert.False(new AgentRegistry(_testDir).GetAgentState("Adele")!.NeedsHuman);
    }

    [Fact]
    public void HandRaise_WithNoCurrentAgent_IsDefensiveNoOp_ReturnsSuccess()
    {
        // No --agent and no claimed session for this process: the escalation writer must not fail —
        // it prints a notice and exits 0 rather than throwing or blocking the pipeline.
        Environment.CurrentDirectory = _testDir;
        Assert.Equal(0, HandCommand.Create().Parse(["raise"]).Invoke());
    }

    [Fact]
    public void HandRaise_RecordsExplicitSource_ThatRoundTripsThroughStateFile()
    {
        WriteState("Adele", status: "working", task: "my-task", needsHuman: false);
        Environment.CurrentDirectory = _testDir;

        Assert.Equal(0, HandCommand.Create().Parse(["raise", "--agent", "Adele"]).Invoke());

        Assert.Equal(NeedsHumanSource.Explicit, new AgentRegistry(_testDir).GetAgentState("Adele")!.NeedsHumanSource);
    }

    [Fact]
    public void HandRaise_ExplicitFlag_SurvivesRaisersNextGuardedToolCall()
    {
        // The sticky-explicit fix: an explicit raise must NOT be erased by the raiser's very next
        // (non-Ask) guarded tool call — that self-heal is for DERIVED flags only.
        WriteState("Adele", status: "working", task: "my-task", needsHuman: false);
        Environment.CurrentDirectory = _testDir;
        HandCommand.Create().Parse(["raise", "--agent", "Adele"]).Invoke();

        GuardCommand.ReconcileNeedsHuman("read", "sess-1", _registry);

        Assert.True(_registry.GetAgentState("Adele")!.NeedsHuman);
        Assert.Equal(NeedsHumanSource.Explicit, _registry.GetAgentState("Adele")!.NeedsHumanSource);
    }

    [Fact]
    public void DerivedFlag_UpgradedByExplicitRaise_BecomesExplicit_AndStopsSelfHealing()
    {
        WriteState("Adele", status: "working", task: "my-task", needsHuman: false);
        Environment.CurrentDirectory = _testDir;

        // Derived detection (AskUserQuestion) sets a self-healing flag.
        GuardCommand.ReconcileNeedsHuman("askuserquestion", "sess-1", _registry);
        Assert.Equal(NeedsHumanSource.Derived, _registry.GetAgentState("Adele")!.NeedsHumanSource);

        // An explicit raise upgrades it; the next tool call no longer clears it.
        HandCommand.Create().Parse(["raise", "--agent", "Adele"]).Invoke();
        Assert.Equal(NeedsHumanSource.Explicit, _registry.GetAgentState("Adele")!.NeedsHumanSource);

        GuardCommand.ReconcileNeedsHuman("read", "sess-1", _registry);
        Assert.True(_registry.GetAgentState("Adele")!.NeedsHuman);
    }

    [Fact]
    public void HandLower_ClearsExplicitFlagAndSource()
    {
        WriteState("Adele", status: "working", task: "my-task", needsHuman: false);
        Environment.CurrentDirectory = _testDir;
        HandCommand.Create().Parse(["raise", "--agent", "Adele"]).Invoke();

        Assert.Equal(0, HandCommand.Create().Parse(["lower", "--agent", "Adele"]).Invoke());

        var state = new AgentRegistry(_testDir).GetAgentState("Adele")!;
        Assert.False(state.NeedsHuman);
        Assert.Null(state.NeedsHumanSource);
    }

    [Fact]
    public void Sweep_LeavesExplicitFlag_OnIdlePeer()
    {
        // An orchestrator flags an idle peer via `hand raise --agent`; the peer isn't
        // working-with-task, but the explicit flag must survive the reconcile sweep.
        WriteState("Adele", status: "free", task: "null", needsHuman: false);
        Environment.CurrentDirectory = _testDir;
        HandCommand.Create().Parse(["raise", "--agent", "Adele"]).Invoke();
        Assert.True(_registry.GetAgentState("Adele")!.NeedsHuman);

        WatchdogService.PollNeedsHuman(_dydoDir);

        Assert.True(new AgentRegistry(_testDir).GetAgentState("Adele")!.NeedsHuman);
    }

    [Fact]
    public void HandRaise_UnknownAgent_FailsNonZero_AndWritesNoState()
    {
        // Ghost-agent defect: an unknown --agent must be a clear error with a non-zero exit and
        // nothing written — no silently-fabricated state file, no false success.
        Environment.CurrentDirectory = _testDir;

        var code = HandCommand.Create().Parse(["raise", "--agent", "Ghost"]).Invoke();

        Assert.NotEqual(0, code);
        Assert.False(File.Exists(Path.Combine(_dydoDir, "agents", "Ghost", "state.md")));
    }

    [Fact]
    public void HandRaise_PathTraversalAgent_FailsNonZero_AndWritesNothingOutsideAgentsTree()
    {
        // Path-traversal defect: `--agent ../../evil` must not fabricate a state file outside the
        // agents tree. Validation rejects it before any filesystem touch.
        Environment.CurrentDirectory = _testDir;

        var code = HandCommand.Create().Parse(["raise", "--agent", "../../evil"]).Invoke();

        Assert.NotEqual(0, code);
        Assert.False(Directory.Exists(Path.Combine(_testDir, "evil")));
    }

    [Fact]
    public void DerivedClear_UnderLock_LeavesExplicitFlagUntouched()
    {
        // Finding 2: a machine derived-clear must decide stickiness from the state re-read UNDER the lock,
        // not a pre-lock snapshot. Seed an Explicit flag, then invoke the derived-clear path (the default
        // Derived source the watchdog sweep / guard reconcile use); the explicit flag must survive.
        WriteState("Adele", status: "working", task: "my-task", needsHuman: false);
        _registry.SetNeedsHuman("Adele", true, NeedsHumanSource.Explicit);
        Assert.Equal(NeedsHumanSource.Explicit, _registry.GetAgentState("Adele")!.NeedsHumanSource);

        Assert.True(_registry.SetNeedsHuman("Adele", false)); // derived-clear (Derived source)

        var state = _registry.GetAgentState("Adele")!;
        Assert.True(state.NeedsHuman);
        Assert.Equal(NeedsHumanSource.Explicit, state.NeedsHumanSource); // sticky explicit survives
    }

    [Fact]
    public void Sweep_CrashMidTask_FlagAlreadySet_StillRepairsStaleTaskMirror()
    {
        // Finding 8: a crashed agent (working + task, session dead) whose needs-human flag is ALREADY set
        // must still have its task-file mirror reconciled — a stale/missing mirror on a crash is repaired,
        // not skipped just because the flag value is unchanged.
        WriteState("Adele", status: "working", task: "my-task", needsHuman: true, sessionId: "sess-dead");
        var taskPath = WriteTaskFile("my-task"); // task file has NO needs-human key: a missing mirror

        var prev = ProcessUtils.IsProcessRunningOverride;
        ProcessUtils.IsProcessRunningOverride = _ => false; // the claimed session PID is dead
        try
        {
            WatchdogService.PollNeedsHuman(_dydoDir);
        }
        finally { ProcessUtils.IsProcessRunningOverride = prev; }

        Assert.Contains("needs-human: true", File.ReadAllText(taskPath)); // mirror repaired despite unchanged flag
    }

    [Fact]
    public void SetRole_TaskChange_WithActiveFlag_MovesMirrorFromOldTaskToNew()
    {
        // Finding 9: a flagged agent switching tasks must not strand needs-human:true in the OLD task file.
        // SetRole clears the old task's mirror and stamps the new one.
        WriteState("Adele", status: "working", task: "old-task", needsHuman: true, sessionId: "sess-1");
        var oldTaskPath = WriteTaskFileWithFlag("old-task", needsHuman: true);
        var newTaskPath = WriteTaskFile("new-task");

        Assert.True(_registry.SetRole("sess-1", "code-writer", "new-task", out var err), err);

        Assert.Contains("needs-human: false", File.ReadAllText(oldTaskPath)); // old mirror cleared
        Assert.DoesNotContain("needs-human: true", File.ReadAllText(oldTaskPath));
        Assert.Contains("needs-human: true", File.ReadAllText(newTaskPath));  // new mirror stamped
    }

    [Fact]
    public void SetRole_TraversalTaskName_Rejected_WritesNothingOutsideTasksTree()
    {
        // Finding 5: a task name reaches the tasks tree as a file path, so '--task ../..' must be rejected
        // (defence in depth in the registry) before any filesystem touch — no file fabricated outside the tree.
        WriteState("Adele", status: "working", task: "null", needsHuman: false, sessionId: "sess-1");

        var ok = _registry.SetRole("sess-1", "code-writer", "../../evil", out var err);

        Assert.False(ok);
        Assert.Contains("Invalid task name", err);
        Assert.False(File.Exists(Path.Combine(_dydoDir, "evil.md")));
        Assert.False(File.Exists(Path.Combine(_testDir, "evil.md")));
    }

    [Fact]
    public void HandRaise_LockContention_RetriesThenFailsNonZero_NoPhantomSuccess()
    {
        // Finding 6: a deliberate escalation must not vanish silently when the write is dropped on lock
        // contention. Holding the per-agent lock forces every SetNeedsHuman attempt to fail; hand must retry
        // briefly, then exit NON-ZERO (never report a phantom success), and write no flag.
        WriteState("Adele", status: "working", task: "my-task", needsHuman: false);
        Environment.CurrentDirectory = _testDir;

        var lockPath = Path.Combine(_dydoDir, "agents", "Adele", ".claim.lock");
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
        using (new FileStream(lockPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            var code = HandCommand.Create().Parse(["raise", "--agent", "Adele"]).Invoke();
            Assert.NotEqual(0, code);
        }

        Assert.False(new AgentRegistry(_testDir).GetAgentState("Adele")!.NeedsHuman);
    }

    [Fact]
    public void SetRole_ExplicitRaiseSurvivesTaskSwitch_MirrorFollowsToNewTask()
    {
        // Finding 5: an explicit `dydo hand raise` must survive a concurrent SetRole task-switch (never clobbered
        // back to false), and the mirror-move must be decided from the state re-read UNDER the lock — so the raise
        // moves to the NEW task file rather than being stranded in the old one where no sweep reaches.
        WriteState("Adele", status: "working", task: "old-task", needsHuman: false, sessionId: "sess-1");
        var oldTaskPath = WriteTaskFile("old-task");
        var newTaskPath = WriteTaskFile("new-task");

        _registry.SetNeedsHuman("Adele", true, NeedsHumanSource.Explicit); // the raise, mirrored to old-task
        Assert.True(_registry.SetRole("sess-1", "code-writer", "new-task", out var err), err);

        var state = _registry.GetAgentState("Adele")!;
        Assert.True(state.NeedsHuman);                                        // the raise survived the switch
        Assert.Equal(NeedsHumanSource.Explicit, state.NeedsHumanSource);      // and stayed explicit
        Assert.Contains("needs-human: false", File.ReadAllText(oldTaskPath)); // old mirror cleared, not stranded
        Assert.Contains("needs-human: true", File.ReadAllText(newTaskPath));  // mirror followed to the new task
    }

    [Fact]
    public void SetRole_UnderLockContention_FailsCleanly_WritesNothing()
    {
        // Finding 5: SetRole's read-modify-write now runs under the per-agent lock. With the lock held its write
        // must not proceed unlocked — it fails cleanly and creates no task file, so a concurrent raise cannot be
        // clobbered by a stale-snapshot write.
        WriteState("Adele", status: "working", task: "null", needsHuman: false, sessionId: "sess-1");

        var lockPath = Path.Combine(_dydoDir, "agents", "Adele", ".claim.lock");
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
        using (new FileStream(lockPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            var ok = _registry.SetRole("sess-1", "code-writer", "contended-task", out var err);
            Assert.False(ok);
            Assert.NotEmpty(err);
        }

        Assert.False(File.Exists(Path.Combine(_dydoDir, "project", "tasks", "contended-task.md")));
        Assert.Null(_registry.GetAgentState("Adele")!.Task); // task never advanced to the contended one
    }

    [Fact]
    public void SetRole_ContentionClearingWithinRetryWindow_Succeeds()
    {
        // Finding 5: SetRole bounded-retries the per-agent lock (3x / 50 ms) rather than a single fail-fast attempt,
        // so a watchdog per-agent sweep briefly holding the lock is ridden out and the role set succeeds — the old
        // single fail-fast attempt spuriously failed `dydo role` on such a collision.
        //
        // Made deterministic (finding 6): the prior version raced a fixed 30 ms releaser against SetRole's ~100 ms
        // retry budget, which a loaded CI box could lose (Thread.Sleep overshoot). Here the contender holds the lock
        // until SetRole is actually in flight, then releases it — so SetRole starts against a held lock (proving the
        // retry path) yet always has its full window free afterward, regardless of scheduling load.
        WriteState("Adele", status: "working", task: "null", needsHuman: false, sessionId: "sess-1");

        var lockPath = Path.Combine(_dydoDir, "agents", "Adele", ".claim.lock");
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
        var contender = new FileStream(lockPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);

        var inFlight = new ManualResetEventSlim();
        var ok = false;
        var err = "";
        var worker = new Thread(() =>
        {
            inFlight.Set();
            ok = _registry.SetRole("sess-1", "code-writer", "contended-task", out err);
        });
        worker.Start();

        inFlight.Wait();     // the worker is entering SetRole; its first lock attempt finds the lock held
        contender.Dispose(); // release now, leaving SetRole its whole retry window to acquire — no timing race
        worker.Join();

        Assert.True(ok, err);
        Assert.Equal("contended-task", _registry.GetAgentState("Adele")!.Task); // the role set rode out contention
    }

    [Fact]
    public void SetDispatchMetadata_UnderLockContention_SkipsWrite()
    {
        // Finding 6: SetDispatchMetadata's read-modify-write is under the per-agent lock too. It bounded-retries
        // (10x / 50 ms) to ride out brief contention, but with the lock held for the whole call the retries all
        // fail: it returns false and the write is skipped (never performed unlocked), so it cannot clobber a
        // concurrent raise. The false return lets the dispatcher surface a non-fatal warning.
        WriteState("Adele", status: "working", task: "my-task", needsHuman: false, sessionId: "sess-1");

        var lockPath = Path.Combine(_dydoDir, "agents", "Adele", ".claim.lock");
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
        bool landed;
        using (new FileStream(lockPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            landed = _registry.SetDispatchMetadata("Adele", "win-contended", true);

        Assert.False(landed);                                    // returns false so the dispatcher can warn
        Assert.Null(_registry.GetAgentState("Adele")!.WindowId); // the contended write did not land
    }

    [Fact]
    public void SetDispatchMetadata_ContentionClearingWithinRetryWindow_LandsWrite()
    {
        // Finding 6: brief contention — a contender (e.g. a watchdog cleanup tick) releasing the per-agent
        // lock within SetDispatchMetadata's bounded-retry window (10x / 50 ms) — is ridden out, so windowId +
        // autoClose land. Under the old single fail-fast attempt this write was silently lost, orphaning an
        // --auto-close terminal that never closed.
        WriteState("Adele", status: "working", task: "my-task", needsHuman: false, sessionId: "sess-1");

        var lockPath = Path.Combine(_dydoDir, "agents", "Adele", ".claim.lock");
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
        var contender = new FileStream(lockPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);

        // Release the lock after one retry interval so a later attempt inside the window acquires it.
        var releaser = new Thread(() => { Thread.Sleep(30); contender.Dispose(); });
        releaser.Start();

        _registry.SetDispatchMetadata("Adele", "win-late", true);
        releaser.Join();

        var state = _registry.GetAgentState("Adele")!;
        Assert.Equal("win-late", state.WindowId); // the write rode out the transient contention
        Assert.True(state.AutoClose);
    }

    [Fact]
    public void SetDispatchMetadata_PreservesConcurrentExplicitRaise()
    {
        // Finding 5: SetDispatchMetadata must not clobber an explicit needs-human flag — its locked read-modify-
        // write preserves the flag committed by a prior raise.
        WriteState("Adele", status: "working", task: "my-task", needsHuman: false, sessionId: "sess-1");
        _registry.SetNeedsHuman("Adele", true, NeedsHumanSource.Explicit);

        _registry.SetDispatchMetadata("Adele", "win-1", true);

        var state = _registry.GetAgentState("Adele")!;
        Assert.True(state.NeedsHuman);
        Assert.Equal("win-1", state.WindowId);
    }

    [Fact]
    public void SetDispatchMetadata_OverwritesStaleAutoCloseFromPriorLifecycle()
    {
        // Finding 6: the write is AUTHORITATIVE — a dispatch with auto-close FALSE must overwrite an AutoClose
        // left TRUE by a prior lifecycle (release deliberately leaves it set), or the new terminal inherits the
        // old dispatch's --auto-close and mis-closes. Landing the write is necessary and sufficient to clear the
        // staleness; it returns true so the dispatcher knows no warning is needed.
        WriteState("Adele", status: "working", task: "my-task", needsHuman: false, sessionId: "sess-1");
        Assert.True(_registry.SetDispatchMetadata("Adele", "old-win", true));   // a prior dispatch left auto-close on
        Assert.True(_registry.GetAgentState("Adele")!.AutoClose);

        Assert.True(_registry.SetDispatchMetadata("Adele", "new-win", false));  // new dispatch: no auto-close
        var state = _registry.GetAgentState("Adele")!;
        Assert.False(state.AutoClose);          // stale true overwritten, not inherited
        Assert.Equal("new-win", state.WindowId);
    }
}
