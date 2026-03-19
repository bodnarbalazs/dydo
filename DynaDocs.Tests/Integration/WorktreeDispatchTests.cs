namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;
using DynaDocs.Services;

/// <summary>
/// Tests for worktree inheritance during dispatch and worktree group display in agent tree.
/// </summary>
[Collection("Integration")]
public class WorktreeDispatchTests : IntegrationTestBase
{
    #region Worktree Inheritance

    [Fact]
    public async Task Dispatch_SenderHasWorktree_ChildInheritsWorktreeId()
    {
        await SetupSenderWithWorktree("Adele", "test-wt-id");

        var result = await DispatchNoLaunch("code-writer", "child-task", "Do work", to: "Brian");
        result.AssertSuccess();

        var childMarker = Path.Combine(TestDir, "dydo/agents/Brian/.worktree");
        Assert.True(File.Exists(childMarker));
        Assert.Equal("test-wt-id", File.ReadAllText(childMarker).Trim());
    }

    [Fact]
    public async Task Dispatch_SenderHasWorktree_ChildInheritsWorktreePath()
    {
        var wtPath = Path.Combine(TestDir, "dydo/_system/.local/worktrees/test-wt-id");
        await SetupSenderWithWorktree("Adele", "test-wt-id", wtPath);

        var result = await DispatchNoLaunch("code-writer", "child-task", "Do work", to: "Brian");
        result.AssertSuccess();

        var childPathMarker = Path.Combine(TestDir, "dydo/agents/Brian/.worktree-path");
        Assert.True(File.Exists(childPathMarker));
        Assert.Equal(wtPath, File.ReadAllText(childPathMarker).Trim());
    }

    [Fact]
    public async Task Dispatch_SenderHasNoWorktree_ChildGetsNoWorktreeMarker()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("orchestrator", "parent-task");

        var result = await DispatchNoLaunch("code-writer", "child-task", "Do work", to: "Brian");
        result.AssertSuccess();

        var childMarker = Path.Combine(TestDir, "dydo/agents/Brian/.worktree");
        Assert.False(File.Exists(childMarker));
    }

    [Fact]
    public async Task Dispatch_SenderHasWorktreeAndWorktreeFlag_CreatesChildWorktree()
    {
        await SetupSenderWithWorktree("Adele", "parent-wt-id");

        // Dispatch with --worktree flag creates a child worktree (nested dispatch)
        var result = await DispatchWithWorktreeFlag("code-writer", "child-task", "Do work", to: "Brian");
        result.AssertSuccess();

        var childMarker = Path.Combine(TestDir, "dydo/agents/Brian/.worktree");
        Assert.True(File.Exists(childMarker));
        Assert.Equal("parent-wt-id/child-task", File.ReadAllText(childMarker).Trim());
    }

    [Fact]
    public async Task Dispatch_ChildWorktree_WritesCorrectWorktreeBase()
    {
        await SetupSenderWithWorktree("Adele", "parent-wt-id");

        var result = await DispatchWithWorktreeFlag("code-writer", "child-task", "Do work", to: "Brian");
        result.AssertSuccess();

        var childBase = Path.Combine(TestDir, "dydo/agents/Brian/.worktree-base");
        Assert.True(File.Exists(childBase));
        // Child's base branch should be the parent's worktree branch
        Assert.Equal("worktree/parent-wt-id", File.ReadAllText(childBase).Trim());
    }

    [Fact]
    public async Task Dispatch_ChildWorktree_WritesWorktreeRoot()
    {
        await SetupSenderWithWorktree("Adele", "parent-wt-id");
        WriteWorktreeRoot("Adele", "/main/project");

        var result = await DispatchWithWorktreeFlag("code-writer", "child-task", "Do work", to: "Brian");
        result.AssertSuccess();

        var childRoot = Path.Combine(TestDir, "dydo/agents/Brian/.worktree-root");
        Assert.True(File.Exists(childRoot));
        Assert.Equal("/main/project", File.ReadAllText(childRoot).Trim());
    }

    [Fact]
    public async Task SetupWorktree_WritesWorktreePath()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("orchestrator", "parent-task");

        // Dispatch with --worktree to Brian (no sender worktree, so creates new)
        var result = await DispatchWithWorktreeFlag("code-writer", "wt-task", "Do work", to: "Brian");
        result.AssertSuccess();

        var brianPathMarker = Path.Combine(TestDir, "dydo/agents/Brian/.worktree-path");
        Assert.True(File.Exists(brianPathMarker), "SetupWorktree should write .worktree-path");

        var path = File.ReadAllText(brianPathMarker).Trim();
        Assert.Contains("dydo/_system/.local/worktrees", path.Replace("\\", "/"));
    }

    [Fact]
    public async Task Dispatch_SenderHasWorktree_ChildInheritsWorktreeBase()
    {
        await SetupSenderWithWorktree("Adele", "test-wt-id");
        WriteWorktreeBase("Adele", "feature-branch");

        var result = await DispatchNoLaunch("code-writer", "child-task", "Do work", to: "Brian");
        result.AssertSuccess();

        var childBaseMarker = Path.Combine(TestDir, "dydo/agents/Brian/.worktree-base");
        Assert.True(File.Exists(childBaseMarker));
        Assert.Equal("feature-branch", File.ReadAllText(childBaseMarker).Trim());
    }

    [Fact]
    public async Task SetupWorktree_WritesWorktreeBase()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("orchestrator", "parent-task");

        var result = await DispatchWithWorktreeFlag("code-writer", "wt-task", "Do work", to: "Brian");
        result.AssertSuccess();

        var brianBaseMarker = Path.Combine(TestDir, "dydo/agents/Brian/.worktree-base");
        Assert.True(File.Exists(brianBaseMarker), "SetupWorktree should write .worktree-base");
        Assert.False(string.IsNullOrWhiteSpace(File.ReadAllText(brianBaseMarker)));
    }

    #endregion

    #region Merge Dispatch

    [Fact]
    public async Task MergeDispatch_CopiesWorktreeBaseToTarget()
    {
        await SetupSenderWithWorktree("Adele", "merge-wt-id");
        WriteWorktreeBase("Adele", "main");
        WriteNeedsMerge("Adele", "some-task");

        var result = await DispatchNoLaunch("code-writer", "merge-task", "Merge worktree", to: "Brian");
        result.AssertSuccess();

        var targetBase = Path.Combine(TestDir, "dydo/agents/Brian/.worktree-base");
        Assert.True(File.Exists(targetBase));
        Assert.Equal("main", File.ReadAllText(targetBase).Trim());
    }

    [Fact]
    public async Task MergeDispatch_WritesMergeSourceToTarget()
    {
        await SetupSenderWithWorktree("Adele", "merge-wt-id");
        WriteWorktreeBase("Adele", "main");
        WriteNeedsMerge("Adele", "some-task");

        var result = await DispatchNoLaunch("code-writer", "merge-task", "Merge worktree", to: "Brian");
        result.AssertSuccess();

        var mergeSource = Path.Combine(TestDir, "dydo/agents/Brian/.merge-source");
        Assert.True(File.Exists(mergeSource));
        Assert.Equal("worktree/merge-wt-id", File.ReadAllText(mergeSource).Trim());
    }

    [Fact]
    public async Task MergeDispatch_DoesNotCopyWorktreeIdToTarget()
    {
        await SetupSenderWithWorktree("Adele", "merge-wt-id");
        WriteWorktreeBase("Adele", "main");
        WriteNeedsMerge("Adele", "some-task");

        var result = await DispatchNoLaunch("code-writer", "merge-task", "Merge worktree", to: "Brian");
        result.AssertSuccess();

        var targetWt = Path.Combine(TestDir, "dydo/agents/Brian/.worktree");
        Assert.False(File.Exists(targetWt));

        var targetWtPath = Path.Combine(TestDir, "dydo/agents/Brian/.worktree-path");
        Assert.False(File.Exists(targetWtPath));
    }

    [Fact]
    public async Task MergeDispatch_ClearsNeedsMergeOnSender()
    {
        await SetupSenderWithWorktree("Adele", "merge-wt-id");
        WriteWorktreeBase("Adele", "main");
        WriteNeedsMerge("Adele", "some-task");

        var result = await DispatchNoLaunch("code-writer", "merge-task", "Merge worktree", to: "Brian");
        result.AssertSuccess();

        var senderNeedsMerge = Path.Combine(TestDir, "dydo/agents/Adele/.needs-merge");
        Assert.False(File.Exists(senderNeedsMerge));
    }

    #endregion

    #region Agent Tree Worktree Groups

    [Fact]
    public async Task AgentTree_AgentsWithSameWorktree_GroupedTogether()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Claim Adele, set role
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "shared-task");

        // Write worktree markers for both Adele and Brian
        var worktreeId = "Adele-20260313120000";
        WriteWorktreeMarker("Adele", worktreeId);
        WriteWorktreeMarker("Brian", worktreeId);

        // Manually set Brian as dispatched (so it's active)
        SetAgentDispatched("Brian", "reviewer", "shared-task");

        var result = await RunAsync(AgentCommand.Create(), "tree");
        result.AssertSuccess();
        result.AssertStdoutContains($"\u250c {worktreeId}");
        result.AssertStdoutContains("Adele");
        result.AssertStdoutContains("Brian");
    }

    [Fact]
    public async Task AgentTree_AgentWithoutWorktree_NotGrouped()
    {
        await InitProjectAsync("none", "testuser", 3);

        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "solo-task");

        // No worktree marker for Adele
        var result = await RunAsync(AgentCommand.Create(), "tree");
        result.AssertSuccess();
        result.AssertStdoutContains("Adele");
        Assert.DoesNotContain("\u250c", result.Stdout);
    }

    [Fact]
    public async Task MergeDispatch_WritesWorktreeHoldToTarget()
    {
        await SetupSenderWithWorktree("Adele", "merge-wt-id");
        WriteWorktreeBase("Adele", "main");
        WriteNeedsMerge("Adele", "some-task");

        var result = await DispatchNoLaunch("code-writer", "merge-task", "Merge worktree", to: "Brian");
        result.AssertSuccess();

        var holdMarker = Path.Combine(TestDir, "dydo/agents/Brian/.worktree-hold");
        Assert.True(File.Exists(holdMarker));
        Assert.Equal("merge-wt-id", File.ReadAllText(holdMarker).Trim());
    }

    [Fact]
    public async Task MergeDispatch_WorktreeHold_PreventsCleanup()
    {
        await SetupSenderWithWorktree("Adele", "merge-wt-id");
        WriteWorktreeBase("Adele", "main");
        WriteNeedsMerge("Adele", "some-task");

        var result = await DispatchNoLaunch("code-writer", "merge-task", "Merge worktree", to: "Brian");
        result.AssertSuccess();

        // Verify Brian's .worktree-hold counts as a reference
        var registry = new AgentRegistry(TestDir);
        var refCount = WorktreeCommand.CountWorktreeReferences(registry, "merge-wt-id");
        Assert.True(refCount > 0, "Agent with .worktree-hold should count as a reference");
    }

    #endregion

    #region Merge-Back Enforcement

    [Fact]
    public async Task ReviewPass_InWorktree_CreatesNeedsMerge()
    {
        await SetupReviewerInWorktree("Brian", "wt-review-id", "review-task");

        var result = await ReviewCompleteAsync("review-task", "pass");
        result.AssertSuccess();

        var needsMerge = Path.Combine(TestDir, "dydo/agents/Brian/.needs-merge");
        Assert.True(File.Exists(needsMerge));
        Assert.Equal("review-task", File.ReadAllText(needsMerge).Trim());
    }

    [Fact]
    public async Task ReviewPass_NotInWorktree_NoNeedsMerge()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Brian");
        await SetRoleAsync("reviewer", "review-task");
        CreateTaskFile("review-task", "review-pending");

        var result = await ReviewCompleteAsync("review-task", "pass");
        result.AssertSuccess();

        var needsMerge = Path.Combine(TestDir, "dydo/agents/Brian/.needs-merge");
        Assert.False(File.Exists(needsMerge));
    }

    [Fact]
    public async Task ReviewFail_InWorktree_NoNeedsMerge()
    {
        await SetupReviewerInWorktree("Brian", "wt-review-id", "review-task");

        var result = await ReviewCompleteAsync("review-task", "fail", "issues found");
        result.AssertSuccess();

        var needsMerge = Path.Combine(TestDir, "dydo/agents/Brian/.needs-merge");
        Assert.False(File.Exists(needsMerge));
    }

    [Fact]
    public async Task Release_BlockedWhileNeedsMergeExists()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Brian");
        await SetRoleAsync("reviewer", "review-task");
        WriteNeedsMerge("Brian", "review-task");

        var result = await ReleaseAgentAsync();
        Assert.True(result.HasError);
        result.AssertStderrContains("merge not dispatched");
    }

    [Fact]
    public async Task Release_SucceedsAfterNeedsMergeCleared()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Brian");
        await SetRoleAsync("reviewer", "review-task");

        // .needs-merge does not exist — release should succeed
        var result = await ReleaseAgentAsync();
        result.AssertSuccess();
    }

    #endregion

    #region SetupWorktree Markers

    [Fact]
    public async Task SetupWorktree_WritesWorktreeRoot()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("orchestrator", "parent-task");

        var result = await DispatchWithWorktreeFlag("code-writer", "wt-task", "Do work", to: "Brian");
        result.AssertSuccess();

        var brianRootMarker = Path.Combine(TestDir, "dydo/agents/Brian/.worktree-root");
        Assert.True(File.Exists(brianRootMarker), "SetupWorktree should write .worktree-root");
        Assert.False(string.IsNullOrWhiteSpace(File.ReadAllText(brianRootMarker)));
    }

    [Fact]
    public async Task InheritWorktree_CopiesWorktreeRoot()
    {
        await SetupSenderWithWorktree("Adele", "test-wt-id");
        WriteWorktreeRoot("Adele", "/main/project/root");

        var result = await DispatchNoLaunch("code-writer", "child-task", "Do work", to: "Brian");
        result.AssertSuccess();

        var childRoot = Path.Combine(TestDir, "dydo/agents/Brian/.worktree-root");
        Assert.True(File.Exists(childRoot));
        Assert.Equal("/main/project/root", File.ReadAllText(childRoot).Trim());
    }

    #endregion

    #region Worktree Cleanup Markers

    [Fact]
    public void Cleanup_RemovesWorktreeBaseMarker()
    {
        var registry = new AgentRegistry(TestDir);
        var workspace = registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(workspace, ".worktree"), "cleanup-id");
        File.WriteAllText(Path.Combine(workspace, ".worktree-path"), "/some/path");
        File.WriteAllText(Path.Combine(workspace, ".worktree-base"), "main");

        WorktreeCommand.ExecuteCleanup("cleanup-id", "Adele", registry);

        Assert.False(File.Exists(Path.Combine(workspace, ".worktree-base")));
    }

    [Fact]
    public void Cleanup_RemovesMergeSourceMarker()
    {
        var registry = new AgentRegistry(TestDir);
        var workspace = registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(workspace, ".merge-source"), "worktree/some-id");

        WorktreeCommand.ExecuteCleanup("some-id", "Adele", registry);

        Assert.False(File.Exists(Path.Combine(workspace, ".merge-source")));
    }

    #endregion

    #region Helpers

    private async Task SetupSenderWithWorktree(string senderName, string worktreeId, string? worktreePath = null)
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync(senderName);
        await SetRoleAsync("orchestrator", "parent-task");

        WriteWorktreeMarker(senderName, worktreeId);
        if (worktreePath != null)
        {
            var workspace = Path.Combine(TestDir, "dydo/agents", senderName);
            File.WriteAllText(Path.Combine(workspace, ".worktree-path"), worktreePath);
        }
    }

    private void WriteWorktreeMarker(string agentName, string worktreeId)
    {
        var workspace = Path.Combine(TestDir, "dydo/agents", agentName);
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(workspace, ".worktree"), worktreeId);
    }

    private void WriteWorktreeRoot(string agentName, string rootPath)
    {
        var workspace = Path.Combine(TestDir, "dydo/agents", agentName);
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(workspace, ".worktree-root"), rootPath);
    }

    private void WriteWorktreeBase(string agentName, string baseBranch)
    {
        var workspace = Path.Combine(TestDir, "dydo/agents", agentName);
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(workspace, ".worktree-base"), baseBranch);
    }

    private void WriteNeedsMerge(string agentName, string task)
    {
        var workspace = Path.Combine(TestDir, "dydo/agents", agentName);
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(workspace, ".needs-merge"), task);
    }

    private void SetAgentDispatched(string agentName, string role, string task)
    {
        var statePath = Path.Combine(TestDir, "dydo/agents", agentName, "state.md");
        Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
        File.WriteAllText(statePath, $"""
            ---
            status: dispatched
            role: {role}
            task: {task}
            ---
            """);
    }

    private async Task<CommandResult> DispatchNoLaunch(string role, string task, string brief, string? to = null)
    {
        StoreSessionContext();
        BypassNoLaunchNudge(task);
        var command = DispatchCommand.Create();
        var args = new List<string>
        {
            "--role", role,
            "--task", task,
            "--brief", brief,
            "--no-launch",
            "--no-wait"
        };
        if (to != null) { args.Add("--to"); args.Add(to); }
        return await RunAsync(command, args.ToArray());
    }

    private async Task SetupReviewerInWorktree(string agentName, string worktreeId, string taskName)
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync(agentName);
        await SetRoleAsync("reviewer", taskName);
        WriteWorktreeMarker(agentName, worktreeId);
        CreateTaskFile(taskName, "review-pending");
    }

    private void CreateTaskFile(string taskName, string status)
    {
        var tasksDir = Path.Combine(TestDir, "dydo/project/tasks");
        Directory.CreateDirectory(tasksDir);
        File.WriteAllText(Path.Combine(tasksDir, $"{taskName}.md"), $"""
            ---
            area: general
            name: {taskName}
            status: {status}
            ---

            # Task: {taskName}
            """);
    }

    private async Task<CommandResult> ReviewCompleteAsync(string task, string status, string? notes = null)
    {
        StoreSessionContext();
        var command = ReviewCommand.Create();
        var args = new List<string> { "complete", task, "--status", status };
        if (notes != null) { args.Add("--notes"); args.Add(notes); }
        return await RunAsync(command, args.ToArray());
    }

    private async Task<CommandResult> DispatchWithWorktreeFlag(string role, string task, string brief, string? to = null)
    {
        StoreSessionContext();
        var command = DispatchCommand.Create();
        var args = new List<string>
        {
            "--role", role,
            "--task", task,
            "--brief", brief,
            "--worktree",
            "--no-wait"
        };
        if (to != null) { args.Add("--to"); args.Add(to); }
        return await RunAsync(command, args.ToArray());
    }

    #endregion
}
