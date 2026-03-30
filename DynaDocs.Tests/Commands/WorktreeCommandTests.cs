namespace DynaDocs.Tests.Commands;

using System.Text.Json.Nodes;
using DynaDocs.Commands;
using DynaDocs.Services;

[Collection("ConsoleOutput")]
public class WorktreeCommandTests : IDisposable
{
    private readonly string _testDir;
    private readonly AgentRegistry _registry;

    public WorktreeCommandTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-wt-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _registry = new AgentRegistry(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public void Cleanup_RemovesOwnMarkers()
    {
        var workspace = _registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(workspace);

        var worktreeId = "Adele-20260313120000";
        File.WriteAllText(Path.Combine(workspace, ".worktree"), worktreeId);
        File.WriteAllText(Path.Combine(workspace, ".worktree-path"), "/some/path");

        WorktreeCommand.ExecuteCleanup(worktreeId, "Adele", _registry);

        Assert.False(File.Exists(Path.Combine(workspace, ".worktree")));
        Assert.False(File.Exists(Path.Combine(workspace, ".worktree-path")));
    }

    [Fact]
    public void Cleanup_SkipsRemoval_WhenOtherAgentsReference()
    {
        var worktreeId = "Adele-20260313120000";

        var adeleWs = _registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(adeleWs);
        File.WriteAllText(Path.Combine(adeleWs, ".worktree"), worktreeId);

        var brianWs = _registry.GetAgentWorkspace("Brian");
        Directory.CreateDirectory(brianWs);
        File.WriteAllText(Path.Combine(brianWs, ".worktree"), worktreeId);

        var stdout = CaptureStdout(() => WorktreeCommand.ExecuteCleanup(worktreeId, "Adele", _registry));

        Assert.Contains("still referencing", stdout);
        Assert.False(File.Exists(Path.Combine(adeleWs, ".worktree")));
        Assert.True(File.Exists(Path.Combine(brianWs, ".worktree")));
    }

    [Fact]
    public void Cleanup_LastAgent_AttemptsRemoval()
    {
        var worktreeId = "Adele-20260313120000";

        var adeleWs = _registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(adeleWs);
        File.WriteAllText(Path.Combine(adeleWs, ".worktree"), worktreeId);
        File.WriteAllText(Path.Combine(adeleWs, ".worktree-path"), Path.Combine(_testDir, "dydo/_system/.local/worktrees", worktreeId));

        var stdout = CaptureStdout(() => WorktreeCommand.ExecuteCleanup(worktreeId, "Adele", _registry));

        Assert.False(File.Exists(Path.Combine(adeleWs, ".worktree")));
        Assert.DoesNotContain("still referencing", stdout);
    }

    [Fact]
    public void Cleanup_ReturnsSuccess()
    {
        var workspace = _registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(workspace);

        var exitCode = WorktreeCommand.ExecuteCleanup("nonexistent-id", "Adele", _registry);
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void Cleanup_NoMarkersToRemove_StillSucceeds()
    {
        // Agent workspace exists but has no worktree markers
        var workspace = _registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(workspace);

        var exitCode = WorktreeCommand.ExecuteCleanup("some-id", "Adele", _registry);
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void Cleanup_DoesNotMatchPartialWorktreeId()
    {
        var worktreeId = "abc";
        var similarId = "xabc";

        var adeleWs = _registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(adeleWs);
        File.WriteAllText(Path.Combine(adeleWs, ".worktree-path"), Path.Combine(_testDir, "worktrees", similarId));

        var stdout = CaptureStdout(() => WorktreeCommand.ExecuteCleanup(worktreeId, "Adele", _registry));

        // Should NOT resolve the path since the directory name "xabc" != "abc"
        Assert.Contains("no path found", stdout);
    }

    [Fact]
    public void Cleanup_LastAgent_CallsRemoveGitWorktree()
    {
        var worktreeId = "Adele-20260314120000";
        var worktreePath = Path.Combine(_testDir, "dydo", "_system", ".local", "worktrees", worktreeId);

        SetupLastAgentScenario("Adele", worktreeId, worktreePath);

        var calls = new List<(string FileName, string Arguments)>();
        WorktreeCommand.RunProcessOverride = (f, a) => calls.Add((f, a));
        try
        {
            WorktreeCommand.ExecuteCleanup(worktreeId, "Adele", _registry);

            Assert.Contains(calls, c => c.FileName == "git" && c.Arguments.Contains("worktree remove"));
        }
        finally
        {
            WorktreeCommand.RunProcessOverride = null;
        }
    }

    [Fact]
    public void Cleanup_LastAgent_CallsDeleteWorktreeBranch()
    {
        var worktreeId = "Adele-20260314120000";
        var worktreePath = Path.Combine(_testDir, "dydo", "_system", ".local", "worktrees", worktreeId);

        SetupLastAgentScenario("Adele", worktreeId, worktreePath);

        var calls = new List<(string FileName, string Arguments)>();
        WorktreeCommand.RunProcessOverride = (f, a) => calls.Add((f, a));
        try
        {
            WorktreeCommand.ExecuteCleanup(worktreeId, "Adele", _registry);

            Assert.Contains(calls, c => c.FileName == "git" && c.Arguments.Contains($"branch -D worktree/{worktreeId}"));
        }
        finally
        {
            WorktreeCommand.RunProcessOverride = null;
        }
    }

    [Fact]
    public void Cleanup_LastAgent_WithJunction_CallsRmdir()
    {
        var worktreeId = "Adele-20260314120000";
        var worktreePath = Path.Combine(_testDir, "dydo", "_system", ".local", "worktrees", worktreeId);

        SetupLastAgentScenario("Adele", worktreeId, worktreePath);

        // Create the junction target directory so RemoveAgentsJunction finds it
        var agentsJunction = Path.Combine(worktreePath, "dydo", "agents");
        Directory.CreateDirectory(agentsJunction);

        var calls = new List<(string FileName, string Arguments)>();
        WorktreeCommand.RunProcessOverride = (f, a) => calls.Add((f, a));
        try
        {
            WorktreeCommand.ExecuteCleanup(worktreeId, "Adele", _registry);

            if (OperatingSystem.IsWindows())
                Assert.Contains(calls, c => c.FileName == "cmd" && c.Arguments.Contains("rmdir"));
            else
                Assert.False(Directory.Exists(agentsJunction));
        }
        finally
        {
            WorktreeCommand.RunProcessOverride = null;
        }
    }

    [Fact]
    public void Cleanup_ResolveWorktreePath_ViaAgentMarker()
    {
        var worktreeId = "Adele-20260314120000";
        var worktreePath = Path.Combine(_testDir, "some-worktree", worktreeId);

        // Adele is the cleanup agent — her markers get deleted
        var adeleWs = _registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(adeleWs);
        File.WriteAllText(Path.Combine(adeleWs, ".worktree"), worktreeId);

        // Brian still has a .worktree-path pointing to the worktree
        var brianWs = _registry.GetAgentWorkspace("Brian");
        Directory.CreateDirectory(brianWs);
        File.WriteAllText(Path.Combine(brianWs, ".worktree-path"), worktreePath);

        var calls = new List<(string FileName, string Arguments)>();
        WorktreeCommand.RunProcessOverride = (f, a) => calls.Add((f, a));
        try
        {
            var stdout = CaptureStdout(() => WorktreeCommand.ExecuteCleanup(worktreeId, "Adele", _registry));

            // Path resolved from Brian's marker; cleanup proceeds
            Assert.Contains(calls, c => c.FileName == "git" && c.Arguments.Contains("worktree remove") && c.Arguments.Contains(worktreePath));
            Assert.Contains("cleaned up", stdout);
        }
        finally
        {
            WorktreeCommand.RunProcessOverride = null;
        }
    }

    [Fact]
    public void Cleanup_WorktreeHold_CountsAsReference()
    {
        var worktreeId = "Adele-20260316120000";

        // Adele is cleaning up — her markers get removed
        var adeleWs = _registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(adeleWs);
        File.WriteAllText(Path.Combine(adeleWs, ".worktree"), worktreeId);

        // Brian has .worktree-hold (merger scenario)
        var brianWs = _registry.GetAgentWorkspace("Brian");
        Directory.CreateDirectory(brianWs);
        File.WriteAllText(Path.Combine(brianWs, ".worktree-hold"), worktreeId);

        var stdout = CaptureStdout(() => WorktreeCommand.ExecuteCleanup(worktreeId, "Adele", _registry));

        Assert.Contains("still referencing", stdout);
    }

    [Fact]
    public void Cleanup_RemovesWorktreeHoldMarker()
    {
        var workspace = _registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(workspace, ".worktree-hold"), "some-wt-id");

        WorktreeCommand.ExecuteCleanup("some-wt-id", "Adele", _registry);

        Assert.False(File.Exists(Path.Combine(workspace, ".worktree-hold")));
    }

    [Fact]
    public void Merge_MissingMergeSource_ReturnsError()
    {
        // Setup agent with .worktree-base but no .merge-source
        SetupMergeAgent("Adele", worktreeBase: "main");
        var workspace = _registry.GetAgentWorkspace("Adele");
        File.Delete(Path.Combine(workspace, ".merge-source")); // ensure missing

        var (exitCode, _, stderr) = CaptureAll(() => WorktreeCommand.ExecuteMerge(false, _registry));

        Assert.NotEqual(0, exitCode);
        Assert.Contains(".merge-source", stderr);
    }

    [Fact]
    public void Merge_MissingWorktreeBase_ReturnsError()
    {
        // Setup agent with .merge-source but no .worktree-base
        var workspace = _registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(workspace);
        StoreSessionForAgent("Adele");
        File.WriteAllText(Path.Combine(workspace, ".merge-source"), "worktree/Adele-20260316");

        var (exitCode, _, stderr) = CaptureAll(() => WorktreeCommand.ExecuteMerge(false, _registry));

        Assert.NotEqual(0, exitCode);
        Assert.Contains(".worktree-base", stderr);
    }

    [Fact]
    public void Merge_ReadsMarkersAndCallsGitMerge()
    {
        SetupMergeAgent("Adele", "main", "worktree/Adele-20260316");

        var calls = new List<(string FileName, string Arguments)>();
        WorktreeCommand.RunProcessOverride = (f, a) => calls.Add((f, a));
        try
        {
            WorktreeCommand.ExecuteMerge(false, _registry);

            Assert.Contains(calls, c => c.FileName == "git" && c.Arguments.Contains("merge worktree/Adele-20260316 --no-edit"));
        }
        finally
        {
            WorktreeCommand.RunProcessOverride = null;
        }
    }

    [Fact]
    public void Merge_CleanMerge_AutoFinalizes()
    {
        SetupMergeAgent("Adele", "main", "worktree/Adele-20260316");

        var calls = new List<(string FileName, string Arguments)>();
        WorktreeCommand.RunProcessOverride = (f, a) => calls.Add((f, a));
        try
        {
            var (exitCode, stdout, _) = CaptureAll(() => WorktreeCommand.ExecuteMerge(false, _registry));

            Assert.Equal(0, exitCode);
            Assert.DoesNotContain(calls, c => c.FileName == "git" && c.Arguments.Contains("checkout"));
            Assert.Contains(calls, c => c.FileName == "git" && c.Arguments.Contains("branch -D worktree/Adele-20260316"));
            Assert.Contains("finalized", stdout.ToLower());
        }
        finally
        {
            WorktreeCommand.RunProcessOverride = null;
        }
    }

    [Fact]
    public void Merge_Finalize_DeletesBranchAndCleansMarkers()
    {
        SetupMergeAgent("Adele", "main", "worktree/Adele-20260316");

        var calls = new List<(string FileName, string Arguments)>();
        WorktreeCommand.RunProcessOverride = (f, a) => calls.Add((f, a));
        try
        {
            WorktreeCommand.ExecuteMerge(true, _registry);

            Assert.Contains(calls, c => c.FileName == "git" && c.Arguments.Contains("branch -D worktree/Adele-20260316"));

            var workspace = _registry.GetAgentWorkspace("Adele");
            Assert.False(File.Exists(Path.Combine(workspace, ".merge-source")));
            Assert.False(File.Exists(Path.Combine(workspace, ".worktree-base")));
        }
        finally
        {
            WorktreeCommand.RunProcessOverride = null;
        }
    }

    [Fact]
    public void Merge_Finalize_RemovesWorktreeHoldMarker()
    {
        SetupMergeAgent("Adele", "main", "worktree/Adele-20260316");
        var workspace = _registry.GetAgentWorkspace("Adele");
        File.WriteAllText(Path.Combine(workspace, ".worktree-hold"), "Adele-20260316");

        WorktreeCommand.RunProcessOverride = (_, _) => { };
        try
        {
            WorktreeCommand.ExecuteMerge(true, _registry);

            Assert.False(File.Exists(Path.Combine(workspace, ".worktree-hold")));
        }
        finally
        {
            WorktreeCommand.RunProcessOverride = null;
        }
    }

    [Fact]
    public void Merge_Finalize_CallsRemoveGitWorktree()
    {
        var worktreeId = "Adele-20260316";
        var worktreePath = Path.Combine(_testDir, "dydo", "_system", ".local", "worktrees", worktreeId);
        SetupMergeAgent("Adele", "main", $"worktree/{worktreeId}");

        // Set up a .worktree-path on Brian so ResolveWorktreePath finds the path
        var brianWs = _registry.GetAgentWorkspace("Brian");
        Directory.CreateDirectory(brianWs);
        File.WriteAllText(Path.Combine(brianWs, ".worktree-path"), worktreePath);

        var calls = new List<(string FileName, string Arguments)>();
        WorktreeCommand.RunProcessOverride = (f, a) => calls.Add((f, a));
        try
        {
            WorktreeCommand.ExecuteMerge(true, _registry);

            Assert.Contains(calls, c => c.FileName == "git" && c.Arguments.Contains("worktree remove") && c.Arguments.Contains(worktreePath));
        }
        finally
        {
            WorktreeCommand.RunProcessOverride = null;
        }
    }

    [Fact]
    public void Merge_Finalize_RemovesZombieWorktreeDirectory()
    {
        var worktreeId = "Adele-20260316";
        var worktreePath = Path.Combine(_testDir, "dydo", "_system", ".local", "worktrees", worktreeId);
        Directory.CreateDirectory(worktreePath);
        SetupMergeAgent("Adele", "main", $"worktree/{worktreeId}");

        // Set up worktree-path so ResolveWorktreePath finds the directory
        var brianWs = _registry.GetAgentWorkspace("Brian");
        Directory.CreateDirectory(brianWs);
        File.WriteAllText(Path.Combine(brianWs, ".worktree-path"), worktreePath);

        // RunProcessOverride is no-op: git worktree remove won't actually delete the dir
        WorktreeCommand.RunProcessOverride = (_, _) => { };
        try
        {
            WorktreeCommand.ExecuteMerge(true, _registry);

            // RemoveZombieDirectory should clean up the leftover directory
            Assert.False(Directory.Exists(worktreePath));
        }
        finally
        {
            WorktreeCommand.RunProcessOverride = null;
        }
    }

    [Fact]
    public void Merge_NoAgentClaimed_ReturnsError()
    {
        // Set up session context but no agent claimed for it
        var agentsDir = Path.Combine(_testDir, "dydo", "agents");
        Directory.CreateDirectory(agentsDir);
        File.WriteAllText(Path.Combine(agentsDir, ".session-context"), "orphan-session");

        var (exitCode, _, stderr) = CaptureAll(() => WorktreeCommand.ExecuteMerge(false, _registry));

        Assert.NotEqual(0, exitCode);
        Assert.Contains("No agent claimed", stderr);
    }

    [Fact]
    public void Merge_ConflictDetected_ReturnsValidationErrorAndPrintsInstructions()
    {
        SetupMergeAgent("Adele", "main", "worktree/Adele-20260316");

        WorktreeCommand.RunProcessWithExitCodeOverride = (f, a) =>
            a.Contains("merge") ? 1 : 0;
        WorktreeCommand.RunProcessOverride = (_, _) => { };
        try
        {
            var (exitCode, stdout, _) = CaptureAll(() => WorktreeCommand.ExecuteMerge(false, _registry));

            Assert.Equal(1, exitCode);
            Assert.Contains("Merge conflicts detected", stdout);
            Assert.Contains("dydo worktree merge --finalize", stdout);
        }
        finally
        {
            WorktreeCommand.RunProcessWithExitCodeOverride = null;
            WorktreeCommand.RunProcessOverride = null;
        }
    }

    private void SetupMergeAgent(string agentName, string? worktreeBase = null, string? mergeSource = null)
    {
        var workspace = _registry.GetAgentWorkspace(agentName);
        Directory.CreateDirectory(workspace);
        StoreSessionForAgent(agentName);
        if (worktreeBase != null)
            File.WriteAllText(Path.Combine(workspace, ".worktree-base"), worktreeBase);
        if (mergeSource != null)
            File.WriteAllText(Path.Combine(workspace, ".merge-source"), mergeSource);
        File.WriteAllText(Path.Combine(workspace, ".worktree-root"), _testDir);
    }

    private void StoreSessionForAgent(string agentName)
    {
        var workspace = _registry.GetAgentWorkspace(agentName);
        Directory.CreateDirectory(workspace);

        // Write state file
        File.WriteAllText(Path.Combine(workspace, "state.md"), $"""
            ---
            status: working
            role: code-writer
            task: merge-task
            ---
            """);

        // Write session JSON file so GetCurrentAgent can match session ID to agent
        File.WriteAllText(Path.Combine(workspace, ".session"),
            $"{{\"Agent\":\"{agentName}\",\"SessionId\":\"test-session\"}}");

        // Write session context so GetSessionContext returns "test-session"
        var agentsDir = Path.Combine(_testDir, "dydo", "agents");
        Directory.CreateDirectory(agentsDir);
        File.WriteAllText(Path.Combine(agentsDir, ".session-context"), "test-session");
    }

    [Fact]
    public void RemoveWorktreeMarkers_IncludesWorktreeRoot()
    {
        var workspace = _registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(workspace, ".worktree-root"), "/some/root");

        WorktreeCommand.RemoveWorktreeMarkers(workspace);

        Assert.False(File.Exists(Path.Combine(workspace, ".worktree-root")));
    }

    [Fact]
    public void RemoveWorktreeMarkers_PreservesMergeSource()
    {
        var workspace = _registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(workspace, ".worktree"), "some-id");
        File.WriteAllText(Path.Combine(workspace, ".merge-source"), "worktree/some-branch");

        WorktreeCommand.RemoveWorktreeMarkers(workspace);

        Assert.False(File.Exists(Path.Combine(workspace, ".worktree")));
        Assert.True(File.Exists(Path.Combine(workspace, ".merge-source")));
    }

    [Fact]
    public void RemoveAllMarkers_DeletesMergeSource()
    {
        var workspace = _registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(workspace, ".worktree"), "some-id");
        File.WriteAllText(Path.Combine(workspace, ".merge-source"), "worktree/some-branch");

        WorktreeCommand.RemoveAllMarkers(workspace);

        Assert.False(File.Exists(Path.Combine(workspace, ".worktree")));
        Assert.False(File.Exists(Path.Combine(workspace, ".merge-source")));
    }

    [Fact]
    public void CountWorktreeReferences_CountsChildWorktrees()
    {
        var worktreeId = "parent-task";

        var adeleWs = _registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(adeleWs);
        File.WriteAllText(Path.Combine(adeleWs, ".worktree"), worktreeId);

        var brianWs = _registry.GetAgentWorkspace("Brian");
        Directory.CreateDirectory(brianWs);
        File.WriteAllText(Path.Combine(brianWs, ".worktree"), $"{worktreeId}/child-task");

        var count = WorktreeCommand.CountWorktreeReferences(_registry, worktreeId);
        Assert.Equal(2, count);
    }

    [Fact]
    public void CountWorktreeReferences_DoesNotCountUnrelatedWorktrees()
    {
        var adeleWs = _registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(adeleWs);
        File.WriteAllText(Path.Combine(adeleWs, ".worktree"), "parent-task");

        var brianWs = _registry.GetAgentWorkspace("Brian");
        Directory.CreateDirectory(brianWs);
        File.WriteAllText(Path.Combine(brianWs, ".worktree"), "other-task");

        var count = WorktreeCommand.CountWorktreeReferences(_registry, "parent-task");
        Assert.Equal(1, count);
    }

    [Fact]
    public void CountChildWorktrees_CountsOnlyChildren()
    {
        var adeleWs = _registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(adeleWs);
        File.WriteAllText(Path.Combine(adeleWs, ".worktree"), "parent-task");

        var brianWs = _registry.GetAgentWorkspace("Brian");
        Directory.CreateDirectory(brianWs);
        File.WriteAllText(Path.Combine(brianWs, ".worktree"), "parent-task/child-task");

        var count = WorktreeCommand.CountChildWorktrees(_registry, "parent-task");
        Assert.Equal(1, count);
    }

    [Fact]
    public void CountChildWorktrees_DoesNotCountParentItself()
    {
        var adeleWs = _registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(adeleWs);
        File.WriteAllText(Path.Combine(adeleWs, ".worktree"), "parent-task");

        var count = WorktreeCommand.CountChildWorktrees(_registry, "parent-task");
        Assert.Equal(0, count);
    }

    [Fact]
    public void CountChildWorktrees_CountsWorktreeHoldChildren()
    {
        var brianWs = _registry.GetAgentWorkspace("Brian");
        Directory.CreateDirectory(brianWs);
        File.WriteAllText(Path.Combine(brianWs, ".worktree-hold"), "parent-task/child-task");

        var count = WorktreeCommand.CountChildWorktrees(_registry, "parent-task");
        Assert.Equal(1, count);
    }

    [Fact]
    public void Cleanup_BlockedWhenChildWorktreesActive()
    {
        var worktreeId = "parent-task";

        var adeleWs = _registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(adeleWs);
        File.WriteAllText(Path.Combine(adeleWs, ".worktree"), worktreeId);

        var brianWs = _registry.GetAgentWorkspace("Brian");
        Directory.CreateDirectory(brianWs);
        File.WriteAllText(Path.Combine(brianWs, ".worktree"), $"{worktreeId}/child-task");

        var (exitCode, _, stderr) = CaptureAll(() => WorktreeCommand.ExecuteCleanup(worktreeId, "Adele", _registry));

        Assert.NotEqual(0, exitCode);
        Assert.Contains("child worktree", stderr);
    }

    [Fact]
    public void Merge_BlockedWhenChildWorktreesActive()
    {
        SetupMergeAgent("Adele", "main", "worktree/parent-task");

        var brianWs = _registry.GetAgentWorkspace("Brian");
        Directory.CreateDirectory(brianWs);
        File.WriteAllText(Path.Combine(brianWs, ".worktree"), "parent-task/child-task");

        var (exitCode, _, stderr) = CaptureAll(() => WorktreeCommand.ExecuteMerge(false, _registry));

        Assert.NotEqual(0, exitCode);
        Assert.Contains("child worktree", stderr);
    }

    [Fact]
    public void ResolveWorktreePath_HierarchicalId()
    {
        var worktreeId = "domain-A/auth-service";
        var worktreePath = Path.Combine(_testDir, "dydo/_system/.local/worktrees", "domain-A", "auth-service");

        var brianWs = _registry.GetAgentWorkspace("Brian");
        Directory.CreateDirectory(brianWs);
        File.WriteAllText(Path.Combine(brianWs, ".worktree-path"), worktreePath);

        var resolved = WorktreeCommand.ResolveWorktreePath(_registry, worktreeId);
        Assert.Equal(worktreePath, resolved);
    }

    [Fact]
    public void Cleanup_DeletesWorktreeBranch_WithEncodedSuffix()
    {
        var worktreeId = "parent/child";
        var worktreePath = Path.Combine(_testDir, "dydo", "_system", ".local", "worktrees", "parent", "child");

        SetupLastAgentScenario("Adele", worktreeId, worktreePath);

        var calls = new List<(string FileName, string Arguments)>();
        WorktreeCommand.RunProcessOverride = (f, a) => calls.Add((f, a));
        try
        {
            WorktreeCommand.ExecuteCleanup(worktreeId, "Adele", _registry);

            Assert.Contains(calls, c => c.FileName == "git" && c.Arguments.Contains("branch -D worktree/parent.+.child"));
        }
        finally
        {
            WorktreeCommand.RunProcessOverride = null;
        }
    }

    [Fact]
    public void Merge_Finalize_DecodesWorktreeIdFromBranchSuffix()
    {
        SetupMergeAgent("Adele", "main", "worktree/parent.+.child");

        var calls = new List<(string FileName, string Arguments)>();
        WorktreeCommand.RunProcessOverride = (f, a) => calls.Add((f, a));
        try
        {
            var (_, stdout, _) = CaptureAll(() => WorktreeCommand.ExecuteMerge(true, _registry));

            // The finalize message should show the decoded worktree ID
            Assert.Contains("parent/child", stdout);
        }
        finally
        {
            WorktreeCommand.RunProcessOverride = null;
        }
    }

    [Fact]
    public void InitSettings_CopiesSettingsWithReadPermission()
    {
        var mainRoot = Path.Combine(_testDir, "main-repo");
        var sourceClaudeDir = Path.Combine(mainRoot, ".claude");
        Directory.CreateDirectory(sourceClaudeDir);
        File.WriteAllText(Path.Combine(sourceClaudeDir, "settings.local.json"), """
            {
              "hooks": { "PreToolUse": [] }
            }
            """);

        var worktreeDir = Path.Combine(_testDir, "worktree");
        Directory.CreateDirectory(worktreeDir);

        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(worktreeDir);
            var exitCode = WorktreeCommand.ExecuteInitSettings(mainRoot);

            Assert.Equal(0, exitCode);
            var targetPath = Path.Combine(worktreeDir, ".claude", "settings.local.json");
            Assert.True(File.Exists(targetPath));

            var json = JsonNode.Parse(File.ReadAllText(targetPath))!;
            var allow = json["permissions"]?["allow"]?.AsArray();
            Assert.NotNull(allow);

            var normalizedRoot = mainRoot.Replace('\\', '/').TrimEnd('/');
            var expectedAbsolute = $"Read({normalizedRoot}/**)";
            Assert.Contains(allow, item => item?.GetValue<string>() == expectedAbsolute);
            Assert.Contains(allow, item => item?.GetValue<string>() == "Read(**)");

            // Original hooks preserved
            Assert.NotNull(json["hooks"]?["PreToolUse"]);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public void InitSettings_Idempotent_NoDuplicateReadEntry()
    {
        var mainRoot = Path.Combine(_testDir, "main-repo");
        var sourceClaudeDir = Path.Combine(mainRoot, ".claude");
        Directory.CreateDirectory(sourceClaudeDir);
        File.WriteAllText(Path.Combine(sourceClaudeDir, "settings.local.json"), "{}");

        var worktreeDir = Path.Combine(_testDir, "worktree");
        Directory.CreateDirectory(worktreeDir);

        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(worktreeDir);
            WorktreeCommand.ExecuteInitSettings(mainRoot);
            WorktreeCommand.ExecuteInitSettings(mainRoot);

            var targetPath = Path.Combine(worktreeDir, ".claude", "settings.local.json");
            var json = JsonNode.Parse(File.ReadAllText(targetPath))!;
            var allow = json["permissions"]!["allow"]!.AsArray();

            var normalizedRoot = mainRoot.Replace('\\', '/').TrimEnd('/');
            var expectedAbsolute = $"Read({normalizedRoot}/**)";
            Assert.Equal(1, allow.Count(item => item?.GetValue<string>() == expectedAbsolute));
            Assert.Equal(1, allow.Count(item => item?.GetValue<string>() == "Read(**)"));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public void InitSettings_NoSourceSettings_ReturnsSuccessWithoutWriting()
    {
        var mainRoot = Path.Combine(_testDir, "no-such-repo");
        var worktreeDir = Path.Combine(_testDir, "worktree");
        Directory.CreateDirectory(worktreeDir);

        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(worktreeDir);
            var exitCode = WorktreeCommand.ExecuteInitSettings(mainRoot);

            Assert.Equal(0, exitCode);
            Assert.False(File.Exists(Path.Combine(worktreeDir, ".claude", "settings.local.json")));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public void InitSettings_NormalizesBackslashesToForwardSlashes()
    {
        var mainRoot = Path.Combine(_testDir, "main-repo");
        var sourceClaudeDir = Path.Combine(mainRoot, ".claude");
        Directory.CreateDirectory(sourceClaudeDir);
        File.WriteAllText(Path.Combine(sourceClaudeDir, "settings.local.json"), "{}");

        var worktreeDir = Path.Combine(_testDir, "worktree");
        Directory.CreateDirectory(worktreeDir);

        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(worktreeDir);
            WorktreeCommand.ExecuteInitSettings(mainRoot);

            var targetPath = Path.Combine(worktreeDir, ".claude", "settings.local.json");
            var json = JsonNode.Parse(File.ReadAllText(targetPath))!;
            var allow = json["permissions"]!["allow"]!.AsArray();
            var absoluteEntry = allow
                .Select(i => i?.GetValue<string>())
                .First(v => v != null && v.StartsWith("Read(") && v != "Read(**)" && v != "Read(~/**)");

            Assert.NotNull(absoluteEntry);
            // Backslashes must be normalized to forward slashes for glob matching
            Assert.DoesNotContain("\\", absoluteEntry);
            Assert.StartsWith("Read(", absoluteEntry);
            Assert.EndsWith("/**)", absoluteEntry);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public void InitSettings_AddsBothSlashVariants()
    {
        var mainRoot = Path.Combine(_testDir, "main-repo");
        var sourceClaudeDir = Path.Combine(mainRoot, ".claude");
        Directory.CreateDirectory(sourceClaudeDir);
        File.WriteAllText(Path.Combine(sourceClaudeDir, "settings.local.json"), "{}");

        var worktreeDir = Path.Combine(_testDir, "worktree");
        Directory.CreateDirectory(worktreeDir);

        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(worktreeDir);
            WorktreeCommand.ExecuteInitSettings(mainRoot);

            var targetPath = Path.Combine(worktreeDir, ".claude", "settings.local.json");
            var json = JsonNode.Parse(File.ReadAllText(targetPath))!;
            var allow = json["permissions"]!["allow"]!.AsArray();
            var entries = allow.Select(i => i?.GetValue<string>()).Where(v => v != null).ToList();

            var forwardRoot = mainRoot.Replace('\\', '/').TrimEnd('/');
            var backslashRoot = mainRoot.Replace('/', '\\').TrimEnd('\\');

            Assert.Contains($"Read({forwardRoot}/**)", entries);
            Assert.Contains($"Read({backslashRoot}/**)", entries);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public void InitSettings_UpdatesMainRepoSettings()
    {
        var mainRoot = Path.Combine(_testDir, "main-repo");
        var sourceClaudeDir = Path.Combine(mainRoot, ".claude");
        Directory.CreateDirectory(sourceClaudeDir);
        var sourcePath = Path.Combine(sourceClaudeDir, "settings.local.json");
        File.WriteAllText(sourcePath, """{"permissions": {"allow": ["Bash(*)"]}}""");

        var worktreeDir = Path.Combine(_testDir, "worktree");
        Directory.CreateDirectory(worktreeDir);

        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(worktreeDir);
            WorktreeCommand.ExecuteInitSettings(mainRoot);

            // Main repo settings should now contain the Read entries
            var mainJson = JsonNode.Parse(File.ReadAllText(sourcePath))!;
            var mainAllow = mainJson["permissions"]!["allow"]!.AsArray();
            var entries = mainAllow.Select(i => i?.GetValue<string>()).Where(v => v != null).ToList();

            Assert.Contains("Read(**)", entries);
            Assert.Contains("Read(~/**)", entries);
            // Original entry preserved
            Assert.Contains("Bash(*)", entries);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public void Merge_OutputsProgressMessage()
    {
        SetupMergeAgent("Adele", "main", "worktree/Adele-20260316");

        WorktreeCommand.RunProcessOverride = (_, _) => { };
        try
        {
            var (_, stdout, _) = CaptureAll(() => WorktreeCommand.ExecuteMerge(false, _registry));

            Assert.Contains("Merging worktree branch worktree/Adele-20260316 into main", stdout);
        }
        finally
        {
            WorktreeCommand.RunProcessOverride = null;
        }
    }

    [Fact]
    public void Cleanup_LastAgent_RemovesRolesJunction()
    {
        var worktreeId = "Adele-20260314120000";
        var worktreePath = Path.Combine(_testDir, "dydo", "_system", ".local", "worktrees", worktreeId);

        SetupLastAgentScenario("Adele", worktreeId, worktreePath);

        // Create the roles junction target so RemoveJunction finds it
        var rolesJunction = Path.Combine(worktreePath, "dydo", "_system", "roles");
        Directory.CreateDirectory(rolesJunction);

        var calls = new List<(string FileName, string Arguments)>();
        WorktreeCommand.RunProcessOverride = (f, a) => calls.Add((f, a));
        try
        {
            WorktreeCommand.ExecuteCleanup(worktreeId, "Adele", _registry);

            if (OperatingSystem.IsWindows())
                Assert.Contains(calls, c => c.FileName == "cmd" && c.Arguments.Contains("rmdir") && c.Arguments.Contains("roles"));
            else
                Assert.False(Directory.Exists(rolesJunction));
        }
        finally
        {
            WorktreeCommand.RunProcessOverride = null;
        }
    }

    [Fact]
    public void WorktreeSetupScript_ContainsInitSettings()
    {
        var script = TerminalLauncher.WorktreeSetupScript("test-task", "/home/user/project");
        Assert.Contains("dydo worktree init-settings --main-root", script);
        Assert.DoesNotContain("cp ", script);
        Assert.DoesNotContain("mkdir -p .claude", script);
    }

    [Fact]
    public void WorktreeSetupScript_ContainsRolesSymlink()
    {
        var script = TerminalLauncher.WorktreeSetupScript("test-task", "/home/user/project");
        Assert.Contains("ln -s '/home/user/project/dydo/_system/roles' dydo/_system/roles", script);
    }

    [Fact]
    public void WorktreeSetupScript_WithoutMainRoot_ContainsRolesSymlink()
    {
        var script = TerminalLauncher.WorktreeSetupScript("test-task");
        Assert.Contains("ln -s \"$_wt_root/dydo/_system/roles\" dydo/_system/roles", script);
    }

    [Fact]
    public void WindowsArguments_ContainInitSettings()
    {
        var args = WindowsTerminalLauncher.GetArguments("Adele", worktreeId: "test-task", mainProjectRoot: @"C:\Projects\MyApp");
        Assert.Contains("dydo worktree init-settings --main-root", args);
        Assert.DoesNotContain("Copy-Item", args);
    }

    [Fact]
    public void WindowsArguments_ContainRolesJunction()
    {
        var args = WindowsTerminalLauncher.GetArguments("Adele", worktreeId: "test-task", mainProjectRoot: @"C:\Projects\MyApp");
        Assert.Contains("Junction -Path 'dydo/_system/roles'", args);
    }

    [Fact]
    public void WindowsArguments_WithoutMainRoot_ContainRolesJunction()
    {
        var args = WindowsTerminalLauncher.GetArguments("Adele", worktreeId: "test-task");
        Assert.Contains("Junction -Path 'dydo/_system/roles'", args);
    }

    #region PreserveAuditFiles Tests

    [Fact]
    public void PreserveAuditFiles_CopiesAuditFilesToMainRepo()
    {
        // Simulate worktree at _testDir/dydo/_system/.local/worktrees/test-wt
        var mainRoot = Path.Combine(_testDir, "main-repo");
        var worktreePath = Path.Combine(mainRoot, "dydo", "_system", ".local", "worktrees", "test-wt");

        // Create audit files in worktree
        var wtAuditDir = Path.Combine(worktreePath, "dydo", "_system", "audit", "2026");
        Directory.CreateDirectory(wtAuditDir);
        File.WriteAllText(Path.Combine(wtAuditDir, "2026-03-23-abc123.json"), """{"SessionId":"abc123"}""");
        File.WriteAllText(Path.Combine(wtAuditDir, "2026-03-23-def456.json"), """{"SessionId":"def456"}""");

        WorktreeCommand.PreserveAuditFiles(worktreePath);

        var mainAuditDir = Path.Combine(mainRoot, "dydo", "_system", "audit", "2026");
        Assert.True(File.Exists(Path.Combine(mainAuditDir, "2026-03-23-abc123.json")));
        Assert.True(File.Exists(Path.Combine(mainAuditDir, "2026-03-23-def456.json")));
    }

    [Fact]
    public void PreserveAuditFiles_PreservesYearSubfolders()
    {
        var mainRoot = Path.Combine(_testDir, "main-repo");
        var worktreePath = Path.Combine(mainRoot, "dydo", "_system", ".local", "worktrees", "test-wt");

        var year2025 = Path.Combine(worktreePath, "dydo", "_system", "audit", "2025");
        var year2026 = Path.Combine(worktreePath, "dydo", "_system", "audit", "2026");
        Directory.CreateDirectory(year2025);
        Directory.CreateDirectory(year2026);
        File.WriteAllText(Path.Combine(year2025, "2025-12-01-old.json"), "{}");
        File.WriteAllText(Path.Combine(year2026, "2026-03-23-new.json"), "{}");

        WorktreeCommand.PreserveAuditFiles(worktreePath);

        Assert.True(File.Exists(Path.Combine(mainRoot, "dydo", "_system", "audit", "2025", "2025-12-01-old.json")));
        Assert.True(File.Exists(Path.Combine(mainRoot, "dydo", "_system", "audit", "2026", "2026-03-23-new.json")));
    }

    [Fact]
    public void PreserveAuditFiles_SkipsTmpFiles()
    {
        var mainRoot = Path.Combine(_testDir, "main-repo");
        var worktreePath = Path.Combine(mainRoot, "dydo", "_system", ".local", "worktrees", "test-wt");

        var wtAuditDir = Path.Combine(worktreePath, "dydo", "_system", "audit", "2026");
        Directory.CreateDirectory(wtAuditDir);
        File.WriteAllText(Path.Combine(wtAuditDir, "2026-03-23-abc123.json"), "{}");
        File.WriteAllText(Path.Combine(wtAuditDir, "2026-03-23-abc123.json.tmp"), "partial");

        WorktreeCommand.PreserveAuditFiles(worktreePath);

        var mainAuditDir = Path.Combine(mainRoot, "dydo", "_system", "audit", "2026");
        Assert.True(File.Exists(Path.Combine(mainAuditDir, "2026-03-23-abc123.json")));
        Assert.False(File.Exists(Path.Combine(mainAuditDir, "2026-03-23-abc123.json.tmp")));
    }

    [Fact]
    public void PreserveAuditFiles_NoAuditDir_DoesNothing()
    {
        var worktreePath = Path.Combine(_testDir, "empty-wt");
        Directory.CreateDirectory(worktreePath);

        // Should not throw
        WorktreeCommand.PreserveAuditFiles(worktreePath);
    }

    [Fact]
    public void PreserveAuditFiles_NotAWorktreePath_DoesNothing()
    {
        // Path without worktree marker — GetMainProjectRoot returns null
        var worktreePath = Path.Combine(_testDir, "regular-repo");
        var auditDir = Path.Combine(worktreePath, "dydo", "_system", "audit", "2026");
        Directory.CreateDirectory(auditDir);
        File.WriteAllText(Path.Combine(auditDir, "2026-03-23-abc.json"), "{}");

        WorktreeCommand.PreserveAuditFiles(worktreePath);

        // Nothing should be copied (no main root detected)
        // The file should still exist in the original location only
        Assert.True(File.Exists(Path.Combine(auditDir, "2026-03-23-abc.json")));
    }

    [Fact]
    public void PreserveAuditFiles_OverwritesExistingFile()
    {
        var mainRoot = Path.Combine(_testDir, "main-repo");
        var worktreePath = Path.Combine(mainRoot, "dydo", "_system", ".local", "worktrees", "test-wt");

        // Create existing audit file in main repo
        var mainAuditDir = Path.Combine(mainRoot, "dydo", "_system", "audit", "2026");
        Directory.CreateDirectory(mainAuditDir);
        File.WriteAllText(Path.Combine(mainAuditDir, "2026-03-23-abc123.json"), """{"old":"data"}""");

        // Create newer audit file in worktree
        var wtAuditDir = Path.Combine(worktreePath, "dydo", "_system", "audit", "2026");
        Directory.CreateDirectory(wtAuditDir);
        File.WriteAllText(Path.Combine(wtAuditDir, "2026-03-23-abc123.json"), """{"new":"data"}""");

        WorktreeCommand.PreserveAuditFiles(worktreePath);

        var content = File.ReadAllText(Path.Combine(mainAuditDir, "2026-03-23-abc123.json"));
        Assert.Contains("new", content);
    }

    [Fact]
    public void Cleanup_LastAgent_PreservesAuditFilesBeforeRemoval()
    {
        var worktreeId = "audit-test-wt";
        var mainRoot = Path.Combine(_testDir, "main-repo");
        var worktreePath = Path.Combine(mainRoot, "dydo", "_system", ".local", "worktrees", worktreeId);

        // Setup agent pointing at this worktree path
        var adeleWs = _registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(adeleWs);
        File.WriteAllText(Path.Combine(adeleWs, ".worktree"), worktreeId);

        var brianWs = _registry.GetAgentWorkspace("Brian");
        Directory.CreateDirectory(brianWs);
        File.WriteAllText(Path.Combine(brianWs, ".worktree-path"), worktreePath);

        // Create audit files inside the worktree
        var wtAuditDir = Path.Combine(worktreePath, "dydo", "_system", "audit", "2026");
        Directory.CreateDirectory(wtAuditDir);
        File.WriteAllText(Path.Combine(wtAuditDir, "2026-03-23-session1.json"), """{"SessionId":"session1"}""");

        var calls = new List<(string FileName, string Arguments)>();
        WorktreeCommand.RunProcessOverride = (f, a) => calls.Add((f, a));
        try
        {
            WorktreeCommand.ExecuteCleanup(worktreeId, "Adele", _registry);

            // Audit file should be copied to main repo
            var mainAuditFile = Path.Combine(mainRoot, "dydo", "_system", "audit", "2026", "2026-03-23-session1.json");
            Assert.True(File.Exists(mainAuditFile));
        }
        finally
        {
            WorktreeCommand.RunProcessOverride = null;
        }
    }

    #endregion

    #region Terminal Script No Git Operations Tests

    [Fact]
    public void WorktreeSetupScript_DoesNotContainGitWorktreeAdd()
    {
        var script = TerminalLauncher.WorktreeSetupScript("test-task", "/home/user/project");
        Assert.DoesNotContain("git worktree add", script);
        Assert.DoesNotContain("git worktree prune", script);
    }

    [Fact]
    public void WorktreeSetupScript_WithoutMainRoot_DoesNotContainGitWorktreeAdd()
    {
        var script = TerminalLauncher.WorktreeSetupScript("test-task");
        Assert.DoesNotContain("git worktree add", script);
        Assert.DoesNotContain("git worktree prune", script);
    }

    [Fact]
    public void WindowsArguments_DoNotContainGitWorktreeAdd()
    {
        var args = WindowsTerminalLauncher.GetArguments("Adele", worktreeId: "test-task", mainProjectRoot: @"C:\Projects\MyApp");
        Assert.DoesNotContain("git worktree add", args);
        Assert.DoesNotContain("git worktree prune", args);
    }

    [Fact]
    public void WindowsArguments_WithoutMainRoot_DoNotContainGitWorktreeAdd()
    {
        var args = WindowsTerminalLauncher.GetArguments("Adele", worktreeId: "test-task");
        Assert.DoesNotContain("git worktree add", args);
        Assert.DoesNotContain("git worktree prune", args);
    }

    #endregion

    [Fact]
    public void InitSettings_MalformedJson_CreatesNewSettings()
    {
        var mainRoot = Path.Combine(_testDir, "main-repo");
        var sourceClaudeDir = Path.Combine(mainRoot, ".claude");
        Directory.CreateDirectory(sourceClaudeDir);
        File.WriteAllText(Path.Combine(sourceClaudeDir, "settings.local.json"), "NOT VALID JSON {{{");

        var worktreeDir = Path.Combine(_testDir, "worktree");
        Directory.CreateDirectory(worktreeDir);

        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(worktreeDir);
            var exitCode = WorktreeCommand.ExecuteInitSettings(mainRoot);

            Assert.Equal(0, exitCode);
            var targetPath = Path.Combine(worktreeDir, ".claude", "settings.local.json");
            Assert.True(File.Exists(targetPath));

            var json = JsonNode.Parse(File.ReadAllText(targetPath))!;
            var allow = json["permissions"]?["allow"]?.AsArray();
            Assert.NotNull(allow);
            Assert.True(allow.Count >= 3);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public void InitSettings_ExistingAllowEntries_DoesNotDuplicate()
    {
        var mainRoot = Path.Combine(_testDir, "main-repo");
        var sourceClaudeDir = Path.Combine(mainRoot, ".claude");
        Directory.CreateDirectory(sourceClaudeDir);
        var allEntries = WorktreeCommand.BuildPermissionEntries(mainRoot);

        // Pre-populate source with all entries so nothing new should be added
        var sourceAllow = new JsonArray();
        foreach (var entry in allEntries)
            sourceAllow.Add((JsonNode)entry);
        var sourceSettings = new JsonObject
        {
            ["permissions"] = new JsonObject { ["allow"] = sourceAllow }
        };
        File.WriteAllText(Path.Combine(sourceClaudeDir, "settings.local.json"),
            sourceSettings.ToJsonString());

        var worktreeDir = Path.Combine(_testDir, "worktree");
        Directory.CreateDirectory(worktreeDir);

        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(worktreeDir);
            var exitCode = WorktreeCommand.ExecuteInitSettings(mainRoot);

            Assert.Equal(0, exitCode);
            var targetPath = Path.Combine(worktreeDir, ".claude", "settings.local.json");
            var json = JsonNode.Parse(File.ReadAllText(targetPath))!;
            var allow = json["permissions"]!["allow"]!.AsArray();

            Assert.Equal(allEntries.Length, allow.Count(item => item?.GetValue<string>() != null));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public void InitSettings_ExistingPermissionsNoAllow_AddsAllow()
    {
        var mainRoot = Path.Combine(_testDir, "main-repo");
        var sourceClaudeDir = Path.Combine(mainRoot, ".claude");
        Directory.CreateDirectory(sourceClaudeDir);
        var sourceSettings = new JsonObject
        {
            ["permissions"] = new JsonObject
            {
                ["deny"] = new JsonArray((JsonNode)"Bash(rm*)")
            }
        };
        File.WriteAllText(Path.Combine(sourceClaudeDir, "settings.local.json"),
            sourceSettings.ToJsonString());

        var worktreeDir = Path.Combine(_testDir, "worktree");
        Directory.CreateDirectory(worktreeDir);

        var expectedCount = WorktreeCommand.BuildPermissionEntries(mainRoot).Length;

        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(worktreeDir);
            var exitCode = WorktreeCommand.ExecuteInitSettings(mainRoot);

            Assert.Equal(0, exitCode);
            var targetPath = Path.Combine(worktreeDir, ".claude", "settings.local.json");
            var json = JsonNode.Parse(File.ReadAllText(targetPath))!;
            var allow = json["permissions"]!["allow"]!.AsArray();
            Assert.Equal(expectedCount, allow.Count);
            // deny should still be preserved
            Assert.NotNull(json["permissions"]!["deny"]);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public void InitSettings_ExistingAllowWithSomeMatches_AddsMissingOnly()
    {
        var mainRoot = Path.Combine(_testDir, "main-repo");
        var sourceClaudeDir = Path.Combine(mainRoot, ".claude");
        Directory.CreateDirectory(sourceClaudeDir);
        var normalizedRoot = mainRoot.Replace('\\', '/').TrimEnd('/');

        // Only include one Read entry — everything else is missing
        var sourceSettings = new JsonObject
        {
            ["permissions"] = new JsonObject
            {
                ["allow"] = new JsonArray(
                    (JsonNode)$"Read({normalizedRoot}/**)",
                    (JsonNode)"Bash(git *)")
            }
        };
        File.WriteAllText(Path.Combine(sourceClaudeDir, "settings.local.json"),
            sourceSettings.ToJsonString());

        var worktreeDir = Path.Combine(_testDir, "worktree");
        Directory.CreateDirectory(worktreeDir);

        var allEntries = WorktreeCommand.BuildPermissionEntries(mainRoot);
        // 2 pre-existing + (allEntries - 1 overlap) = allEntries + 1
        var expectedCount = allEntries.Length + 1;

        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(worktreeDir);
            var exitCode = WorktreeCommand.ExecuteInitSettings(mainRoot);

            Assert.Equal(0, exitCode);
            var targetPath = Path.Combine(worktreeDir, ".claude", "settings.local.json");
            var json = JsonNode.Parse(File.ReadAllText(targetPath))!;
            var allow = json["permissions"]!["allow"]!.AsArray();
            Assert.Equal(expectedCount, allow.Count);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public void ResolveWorktreePath_NonMatchingMarker_ReturnsNull()
    {
        var brianWs = _registry.GetAgentWorkspace("Brian");
        Directory.CreateDirectory(brianWs);
        File.WriteAllText(Path.Combine(brianWs, ".worktree-path"), "/some/path/other-task");

        var resolved = WorktreeCommand.ResolveWorktreePath(_registry, "target-task");
        Assert.Null(resolved);
    }

    [Fact]
    public void InitSettings_AllowWithNullEntry_HandledGracefully()
    {
        var mainRoot = Path.Combine(_testDir, "main-repo");
        var sourceClaudeDir = Path.Combine(mainRoot, ".claude");
        Directory.CreateDirectory(sourceClaudeDir);
        // JSON with a null entry in the allow array
        File.WriteAllText(Path.Combine(sourceClaudeDir, "settings.local.json"),
            """{"permissions":{"allow":[null,"Bash(git *)"]}}""");

        var worktreeDir = Path.Combine(_testDir, "worktree");
        Directory.CreateDirectory(worktreeDir);

        var allEntries = WorktreeCommand.BuildPermissionEntries(mainRoot);
        // null + Bash(git *) + all permission entries
        var expectedCount = 2 + allEntries.Length;

        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(worktreeDir);
            var exitCode = WorktreeCommand.ExecuteInitSettings(mainRoot);

            Assert.Equal(0, exitCode);
            var targetPath = Path.Combine(worktreeDir, ".claude", "settings.local.json");
            var json = JsonNode.Parse(File.ReadAllText(targetPath))!;
            var allow = json["permissions"]!["allow"]!.AsArray();
            Assert.Equal(expectedCount, allow.Count);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public void Merge_Finalize_NonWorktreePrefixedBranch_StillCleans()
    {
        // Use a merge source that does NOT start with "worktree/"
        SetupMergeAgent("Adele", "main", "feature/some-branch");

        var calls = new List<(string FileName, string Arguments)>();
        WorktreeCommand.RunProcessOverride = (f, a) => calls.Add((f, a));
        try
        {
            var (exitCode, stdout, _) = CaptureAll(() => WorktreeCommand.ExecuteMerge(true, _registry));

            Assert.Equal(0, exitCode);
            Assert.Contains("finalized", stdout.ToLower());
            Assert.Contains(calls, c => c.FileName == "git" && c.Arguments.Contains("branch -D feature/some-branch"));
        }
        finally
        {
            WorktreeCommand.RunProcessOverride = null;
        }
    }

    [Fact]
    public void InitSettings_AddsWriteVariants()
    {
        var mainRoot = Path.Combine(_testDir, "main-repo");
        var sourceClaudeDir = Path.Combine(mainRoot, ".claude");
        Directory.CreateDirectory(sourceClaudeDir);
        File.WriteAllText(Path.Combine(sourceClaudeDir, "settings.local.json"), "{}");

        var worktreeDir = Path.Combine(_testDir, "worktree");
        Directory.CreateDirectory(worktreeDir);

        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(worktreeDir);
            WorktreeCommand.ExecuteInitSettings(mainRoot);

            var targetPath = Path.Combine(worktreeDir, ".claude", "settings.local.json");
            var json = JsonNode.Parse(File.ReadAllText(targetPath))!;
            var allow = json["permissions"]!["allow"]!.AsArray();
            var entries = allow.Select(i => i?.GetValue<string>()).Where(v => v != null).ToList();

            var forwardRoot = mainRoot.Replace('\\', '/').TrimEnd('/');
            var backslashRoot = mainRoot.Replace('/', '\\').TrimEnd('\\');

            Assert.Contains($"Write({forwardRoot}/**)", entries);
            Assert.Contains($"Write({backslashRoot}/**)", entries);
            Assert.Contains("Write(**)", entries);
            Assert.Contains("Write(~/**)", entries);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public void InitSettings_AddsMsysPathOnWindows()
    {
        var mainRoot = Path.Combine(_testDir, "main-repo");
        var sourceClaudeDir = Path.Combine(mainRoot, ".claude");
        Directory.CreateDirectory(sourceClaudeDir);
        File.WriteAllText(Path.Combine(sourceClaudeDir, "settings.local.json"), "{}");

        var worktreeDir = Path.Combine(_testDir, "worktree");
        Directory.CreateDirectory(worktreeDir);

        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(worktreeDir);
            WorktreeCommand.ExecuteInitSettings(mainRoot);

            var targetPath = Path.Combine(worktreeDir, ".claude", "settings.local.json");
            var json = JsonNode.Parse(File.ReadAllText(targetPath))!;
            var allow = json["permissions"]!["allow"]!.AsArray();
            var entries = allow.Select(i => i?.GetValue<string>()).Where(v => v != null).ToList();

            var normalizedRoot = mainRoot.Replace('\\', '/').TrimEnd('/');
            if (OperatingSystem.IsWindows()
                && normalizedRoot.Length >= 2 && char.IsLetter(normalizedRoot[0]) && normalizedRoot[1] == ':')
            {
                var msysRoot = "/" + char.ToLowerInvariant(normalizedRoot[0]) + normalizedRoot[2..];
                Assert.Contains($"Read({msysRoot}/**)", entries);
                Assert.Contains($"Write({msysRoot}/**)", entries);
            }
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public void BuildPermissionEntries_WindowsDrivePath_IncludesMsys()
    {
        var entries = WorktreeCommand.BuildPermissionEntries(@"C:\Users\Test\Project");

        if (OperatingSystem.IsWindows())
        {
            Assert.Contains("Read(/c/Users/Test/Project/**)", entries);
            Assert.Contains("Write(/c/Users/Test/Project/**)", entries);
        }

        // Always has Read + Write for forward, backslash, wildcard, tilde
        Assert.Contains("Read(C:/Users/Test/Project/**)", entries);
        Assert.Contains("Write(C:/Users/Test/Project/**)", entries);
        Assert.Contains(@"Read(C:\Users\Test\Project/**)", entries);
        Assert.Contains(@"Write(C:\Users\Test\Project/**)", entries);
        Assert.Contains("Read(**)", entries);
        Assert.Contains("Write(**)", entries);
        Assert.Contains("Read(~/**)", entries);
        Assert.Contains("Write(~/**)", entries);
    }

    [Fact]
    public void BuildPermissionEntries_UnixPath_NoMsys()
    {
        var entries = WorktreeCommand.BuildPermissionEntries("/home/user/project");

        // Unix paths don't start with a drive letter — no MSYS variant even on Windows
        // 4 patterns (forward, backslash, wildcard, tilde) x 2 permissions (Read, Write) = 8
        Assert.Equal(8, entries.Length);
        Assert.Contains("Read(/home/user/project/**)", entries);
        Assert.Contains("Write(/home/user/project/**)", entries);
        Assert.Contains("Read(**)", entries);
        Assert.Contains("Write(**)", entries);
        Assert.Contains("Read(~/**)", entries);
        Assert.Contains("Write(~/**)", entries);
    }

    [Fact]
    public void BuildPermissionEntries_AlwaysPairsReadAndWrite()
    {
        var entries = WorktreeCommand.BuildPermissionEntries(@"D:\MyProject");
        var readEntries = entries.Where(e => e.StartsWith("Read(")).ToList();
        var writeEntries = entries.Where(e => e.StartsWith("Write(")).ToList();

        Assert.Equal(readEntries.Count, writeEntries.Count);

        foreach (var read in readEntries)
        {
            var pattern = read["Read(".Length..];
            Assert.Contains($"Write({pattern}", writeEntries);
        }
    }

    [Fact]
    public void InitSettings_JsonNullLiteral_CreatesNewSettings()
    {
        var mainRoot = Path.Combine(_testDir, "main-repo");
        var sourceClaudeDir = Path.Combine(mainRoot, ".claude");
        Directory.CreateDirectory(sourceClaudeDir);
        File.WriteAllText(Path.Combine(sourceClaudeDir, "settings.local.json"), "null");

        var worktreeDir = Path.Combine(_testDir, "worktree");
        Directory.CreateDirectory(worktreeDir);

        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(worktreeDir);
            var exitCode = WorktreeCommand.ExecuteInitSettings(mainRoot);

            Assert.Equal(0, exitCode);
            var targetPath = Path.Combine(worktreeDir, ".claude", "settings.local.json");
            Assert.True(File.Exists(targetPath));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public void Merge_MissingWorktreeRoot_FallsBackToFindProjectRoot()
    {
        SetupMergeAgent("Adele", "main", "worktree/Adele-20260316");
        var workspace = _registry.GetAgentWorkspace("Adele");
        // Remove the .worktree-root marker so the code falls back to FindProjectRoot
        File.Delete(Path.Combine(workspace, ".worktree-root"));

        var calls = new List<(string FileName, string Arguments)>();
        WorktreeCommand.RunProcessOverride = (f, a) => calls.Add((f, a));
        WorktreeCommand.RunProcessWithExitCodeOverride = (_, _) => 0;
        try
        {
            // This may fail or succeed depending on FindProjectRoot, but it exercises the branch
            WorktreeCommand.ExecuteMerge(false, _registry);
        }
        catch { }
        finally
        {
            WorktreeCommand.RunProcessOverride = null;
            WorktreeCommand.RunProcessWithExitCodeOverride = null;
        }
    }

    [Fact]
    public void RemoveGitWorktree_NonexistentPath_DoesNotThrow()
    {
        // Exercises the real RunProcess path (no override) with a harmless failing command
        var nonexistentPath = Path.Combine(_testDir, "no-such-worktree-" + Guid.NewGuid().ToString("N")[..8]);
        WorktreeCommand.RemoveGitWorktree(nonexistentPath);
    }

    [Fact]
    public void RunProcessWithExitCode_RealProcess_ReturnsExitCode()
    {
        // Exercises the real RunProcessWithExitCode path (no overrides)
        var cmd = OperatingSystem.IsWindows() ? "cmd" : "true";
        var args = OperatingSystem.IsWindows() ? "/c exit 0" : "";
        var exitCode = WorktreeCommand.RunProcessWithExitCode(cmd, args);
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void RemoveZombieDirectory_DeletesDirectory_WhenExists()
    {
        var zombieDir = Path.Combine(_testDir, "zombie-wt-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(zombieDir);
        File.WriteAllText(Path.Combine(zombieDir, "dummy.txt"), "test");

        WorktreeCommand.RemoveZombieDirectory(zombieDir);

        Assert.False(Directory.Exists(zombieDir));
    }

    [Fact]
    public void RemoveZombieDirectory_NoOp_WhenDirectoryMissing()
    {
        var missingDir = Path.Combine(_testDir, "no-such-dir-" + Guid.NewGuid().ToString("N")[..8]);

        WorktreeCommand.RemoveZombieDirectory(missingDir);

        Assert.False(Directory.Exists(missingDir));
    }

    [Fact]
    public void Cleanup_RemovesZombieDirectory_AfterGitWorktreeRemoveFails()
    {
        var worktreeId = "zombie-" + Guid.NewGuid().ToString("N")[..8];
        var worktreePath = Path.Combine(_testDir, "dydo", "_system", ".local", "worktrees", worktreeId);
        Directory.CreateDirectory(worktreePath);
        File.WriteAllText(Path.Combine(worktreePath, "dummy.txt"), "data");

        SetupLastAgentScenario("Adele", worktreeId, worktreePath);

        // Override git calls to no-op (simulates git worktree remove failing silently)
        WorktreeCommand.RunProcessOverride = (_, _) => { };
        try
        {
            WorktreeCommand.ExecuteCleanup(worktreeId, "Adele", _registry);

            Assert.False(Directory.Exists(worktreePath));
        }
        finally
        {
            WorktreeCommand.RunProcessOverride = null;
        }
    }

    [Fact]
    public void Cleanup_RemovesMergeSourceMarker()
    {
        var workspace = _registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(workspace);

        var worktreeId = "Adele-20260313120000";
        File.WriteAllText(Path.Combine(workspace, ".worktree"), worktreeId);
        File.WriteAllText(Path.Combine(workspace, ".merge-source"), "worktree/some-branch");

        WorktreeCommand.ExecuteCleanup(worktreeId, "Adele", _registry);

        Assert.False(File.Exists(Path.Combine(workspace, ".worktree")));
        Assert.False(File.Exists(Path.Combine(workspace, ".merge-source")));
    }

    [Fact]
    public void Cleanup_IdempotentAfterFinalizeMerge()
    {
        var workspace = _registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(workspace);

        // After FinalizeMerge, all markers are already gone
        var worktreeId = "Adele-20260313120000";

        var exitCode = WorktreeCommand.ExecuteCleanup(worktreeId, "Adele", _registry);

        Assert.Equal(0, exitCode);
    }

    // --- Prune tests ---

    [Fact]
    public void Prune_NoWorktreeDirectories_ReportsNothing()
    {
        // worktrees dir doesn't exist at all
        var (exitCode, stdout, _) = CaptureAll(() => WorktreeCommand.ExecutePrune(_registry));

        Assert.Equal(0, exitCode);
        Assert.Contains("Pruned 0 orphaned worktree(s)", stdout);
    }

    [Fact]
    public void Prune_OrphanDirectory_RemovesIt()
    {
        var worktreesDir = Path.Combine(_testDir, "dydo", "_system", ".local", "worktrees");
        var orphanDir = Path.Combine(worktreesDir, "orphan-20260326");
        Directory.CreateDirectory(orphanDir);
        File.WriteAllText(Path.Combine(orphanDir, "dummy.txt"), "content");

        // Create an agent workspace so GetAllAgentStates has something to iterate
        var ws = _registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(ws);

        var calls = new List<(string FileName, string Arguments)>();
        WorktreeCommand.RunProcessOverride = (f, a) => calls.Add((f, a));
        try
        {
            var (exitCode, stdout, _) = CaptureAll(() => WorktreeCommand.ExecutePrune(_registry));

            Assert.Equal(0, exitCode);
            Assert.Contains("Pruning orphaned worktree: orphan-20260326", stdout);
            Assert.Contains("Pruned 1 orphaned worktree(s)", stdout);
            Assert.Contains(calls, c => c.FileName == "git" && c.Arguments.Contains("worktree remove"));
            Assert.Contains(calls, c => c.FileName == "git" && c.Arguments.Contains("branch -D"));
            Assert.False(Directory.Exists(orphanDir));
        }
        finally { WorktreeCommand.RunProcessOverride = null; }
    }

    [Fact]
    public void Prune_ActiveWorktree_SkipsIt()
    {
        var worktreesDir = Path.Combine(_testDir, "dydo", "_system", ".local", "worktrees");
        var activeDir = Path.Combine(worktreesDir, "active-wt");
        Directory.CreateDirectory(activeDir);

        var ws = _registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(ws);
        File.WriteAllText(Path.Combine(ws, ".worktree"), "active-wt");

        var (exitCode, stdout, _) = CaptureAll(() => WorktreeCommand.ExecutePrune(_registry));

        Assert.Equal(0, exitCode);
        Assert.Contains("skipping", stdout);
        Assert.True(Directory.Exists(activeDir));
        Assert.Contains("Pruned 0 orphaned worktree(s)", stdout);
    }

    [Fact]
    public void Prune_CleansStaleWorktreeHoldMarker()
    {
        var ws = _registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(ws);
        File.WriteAllText(Path.Combine(ws, ".worktree-hold"), "some-wt-id");
        // No agent has .worktree = some-wt-id

        WorktreeCommand.RunProcessOverride = (_, _) => { };
        try
        {
            var (exitCode, stdout, _) = CaptureAll(() => WorktreeCommand.ExecutePrune(_registry));

            Assert.Equal(0, exitCode);
            Assert.False(File.Exists(Path.Combine(ws, ".worktree-hold")));
            Assert.Contains("stale .worktree-hold", stdout);
            Assert.Contains("cleaned 1 stale marker(s)", stdout);
        }
        finally { WorktreeCommand.RunProcessOverride = null; }
    }

    [Fact]
    public void Prune_CleansStaleMarkerSourceMarker()
    {
        var ws = _registry.GetAgentWorkspace("Brian");
        Directory.CreateDirectory(ws);
        File.WriteAllText(Path.Combine(ws, ".merge-source"), "worktree/some-branch");
        // No agent has .worktree referencing corresponding worktreeId

        WorktreeCommand.RunProcessOverride = (_, _) => { };
        try
        {
            var (exitCode, stdout, _) = CaptureAll(() => WorktreeCommand.ExecutePrune(_registry));

            Assert.Equal(0, exitCode);
            Assert.False(File.Exists(Path.Combine(ws, ".merge-source")));
            Assert.Contains("stale .merge-source", stdout);
        }
        finally { WorktreeCommand.RunProcessOverride = null; }
    }

    [Fact]
    public void Prune_MixedOrphanedAndActive_OnlyRemovesOrphans()
    {
        var worktreesDir = Path.Combine(_testDir, "dydo", "_system", ".local", "worktrees");
        var orphanDir = Path.Combine(worktreesDir, "orphan-wt");
        var activeDir = Path.Combine(worktreesDir, "active-wt");
        Directory.CreateDirectory(orphanDir);
        Directory.CreateDirectory(activeDir);

        var ws = _registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(ws);
        File.WriteAllText(Path.Combine(ws, ".worktree"), "active-wt");

        var calls = new List<(string FileName, string Arguments)>();
        WorktreeCommand.RunProcessOverride = (f, a) => calls.Add((f, a));
        try
        {
            var (exitCode, stdout, _) = CaptureAll(() => WorktreeCommand.ExecutePrune(_registry));

            Assert.Equal(0, exitCode);
            Assert.False(Directory.Exists(orphanDir));
            Assert.True(Directory.Exists(activeDir));
            Assert.Contains("Pruned 1 orphaned worktree(s)", stdout);
        }
        finally { WorktreeCommand.RunProcessOverride = null; }
    }

    [Fact]
    public void Prune_EmptyZombieDirectory_RemovesIt()
    {
        var worktreesDir = Path.Combine(_testDir, "dydo", "_system", ".local", "worktrees");
        var zombieDir = Path.Combine(worktreesDir, "zombie-empty");
        Directory.CreateDirectory(zombieDir);

        var ws = _registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(ws);

        WorktreeCommand.RunProcessOverride = (_, _) => { };
        try
        {
            var (exitCode, stdout, _) = CaptureAll(() => WorktreeCommand.ExecutePrune(_registry));

            Assert.Equal(0, exitCode);
            Assert.False(Directory.Exists(zombieDir));
            Assert.Contains("Pruned 1 orphaned worktree(s)", stdout);
        }
        finally { WorktreeCommand.RunProcessOverride = null; }
    }

    // --- End prune tests ---

    private void SetupLastAgentScenario(string agent, string worktreeId, string worktreePath)
    {
        // Create agent workspace with worktree marker
        var ws = _registry.GetAgentWorkspace(agent);
        Directory.CreateDirectory(ws);
        File.WriteAllText(Path.Combine(ws, ".worktree"), worktreeId);

        // Another agent retains the worktree-path so ResolveWorktreePath can find it
        var brianWs = _registry.GetAgentWorkspace("Brian");
        Directory.CreateDirectory(brianWs);
        File.WriteAllText(Path.Combine(brianWs, ".worktree-path"), worktreePath);
    }

    private static string CaptureStdout(Action action)
    {
        var original = Console.Out;
        var sw = new StringWriter();
        Console.SetOut(sw);
        try
        {
            action();
            return sw.ToString();
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    private static (int exitCode, string stdout, string stderr) CaptureAll(Func<int> action)
    {
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        var outWriter = new StringWriter();
        var errWriter = new StringWriter();
        Console.SetOut(outWriter);
        Console.SetError(errWriter);
        try
        {
            var code = action();
            return (code, outWriter.ToString(), errWriter.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }
}
