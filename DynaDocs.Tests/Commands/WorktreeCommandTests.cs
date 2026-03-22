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
        Directory.CreateDirectory(Path.Combine(worktreePath, "dydo", "agents"));

        var calls = new List<(string FileName, string Arguments)>();
        WorktreeCommand.RunProcessOverride = (f, a) => calls.Add((f, a));
        try
        {
            WorktreeCommand.ExecuteCleanup(worktreeId, "Adele", _registry);

            Assert.Contains(calls, c => c.FileName == "cmd" && c.Arguments.Contains("rmdir"));
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
    public void RemoveMarkers_IncludesWorktreeRoot()
    {
        var workspace = _registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(workspace, ".worktree-root"), "/some/root");

        WorktreeCommand.RemoveMarkers(workspace);

        Assert.False(File.Exists(Path.Combine(workspace, ".worktree-root")));
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

            var trimmedRoot = mainRoot.TrimEnd('/', '\\');
            var expectedAbsolute = $"Read({trimmedRoot}/**)";
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

            var trimmedRoot = mainRoot.TrimEnd('/', '\\');
            var expectedAbsolute = $"Read({trimmedRoot}/**)";
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
    public void InitSettings_PreservesBackslashes_OnWindowsPaths()
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
            var entry = allow[0]!.GetValue<string>();

            // The Read entry must preserve the path format as-is (backslashes on Windows)
            Assert.StartsWith("Read(", entry);
            Assert.EndsWith("/**)", entry);
            Assert.Contains(mainRoot.TrimEnd('/', '\\'), entry);
            // Must NOT convert backslashes to forward slashes
            if (mainRoot.Contains('\\'))
                Assert.Contains("\\", entry);
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
        Directory.CreateDirectory(Path.Combine(worktreePath, "dydo", "_system", "roles"));

        var calls = new List<(string FileName, string Arguments)>();
        WorktreeCommand.RunProcessOverride = (f, a) => calls.Add((f, a));
        try
        {
            WorktreeCommand.ExecuteCleanup(worktreeId, "Adele", _registry);

            Assert.Contains(calls, c => c.FileName == "cmd" && c.Arguments.Contains("rmdir") && c.Arguments.Contains("roles"));
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
