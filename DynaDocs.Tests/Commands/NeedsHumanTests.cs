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
            status: pending
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
            status: pending
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
}
