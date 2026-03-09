namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;

/// <summary>
/// Integration tests for workspace and clean commands.
/// </summary>
[Collection("Integration")]
public class WorkspaceAndCleanTests : IntegrationTestBase
{
    #region Workspace Init

    [Fact]
    public async Task Workspace_Init_CreatesAgentFolders()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Init creates agent workspaces with workflow.md (modes created at claim)
        AssertDirectoryExists("dydo/agents/Adele");
        AssertFileExists("dydo/agents/Adele/workflow.md");
        Assert.False(Directory.Exists(Path.Combine(TestDir, "dydo/agents/Adele/modes")));
    }

    [Fact]
    public async Task Workspace_Check_PassesCleanState()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        var result = await WorkspaceCheckAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("passed");
    }

    [Fact]
    public async Task Workspace_Check_NoAgent_Skips()
    {
        await InitProjectAsync("none", "balazs", 3);
        // Don't claim an agent

        var result = await WorkspaceCheckAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("No agent");
    }

    #endregion

    #region Clean

    [Fact]
    public async Task Clean_Agent_ClearsWorkspace()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "my-task");

        // Create some files in workspace
        WriteFile("dydo/agents/Adele/notes.md", "Some notes");

        // Release first to avoid "working" status
        await ReleaseAgentAsync();

        var result = await CleanAsync("Adele");

        result.AssertSuccess();
        result.AssertStdoutContains("Cleaned");

        // Notes should be deleted
        Assert.False(File.Exists(Path.Combine(TestDir, "dydo/agents/Adele/notes.md")));
    }

    [Fact]
    public async Task Clean_WorkingAgent_RequiresForce()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");

        // Don't release - agent is "working"
        var result = await CleanAsync("Adele");

        result.AssertExitCode(2);
        result.AssertStderrContains("--force");
    }

    [Fact]
    public async Task Clean_WorkingAgent_WithForce_Cleans()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");

        var result = await CleanAsync("Adele", force: true);

        result.AssertSuccess();
        result.AssertStdoutContains("Cleaned");
    }

    [Fact]
    public async Task Clean_All_ClearsAllWorkspaces()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Create notes in multiple workspaces
        WriteFile("dydo/agents/Adele/notes.md", "Notes A");
        WriteFile("dydo/agents/Brian/notes.md", "Notes B");

        var result = await CleanAllAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Cleaned");

        Assert.False(File.Exists(Path.Combine(TestDir, "dydo/agents/Adele/notes.md")));
        Assert.False(File.Exists(Path.Combine(TestDir, "dydo/agents/Brian/notes.md")));
    }

    [Fact]
    public async Task Clean_ByTask_CleansMatchingWorkspaces()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Simulate an agent that was working on a task
        WriteFile("dydo/agents/Adele/state.md", """
            ---
            agent: Adele
            role: code-writer
            task: feature-auth
            status: free
            started: null
            writable-paths: []
            readonly-paths: []
            ---
            """);
        WriteFile("dydo/agents/Adele/notes.md", "Auth notes");

        var result = await CleanByTaskAsync("feature-auth");

        result.AssertSuccess();
        result.AssertStdoutContains("feature-auth");
    }

    [Fact]
    public async Task Clean_Agent_RemovesModesDirectory()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");

        // Modes directory should exist after claim
        var modesPath = Path.Combine(TestDir, "dydo/agents/Adele/modes");
        Assert.True(Directory.Exists(modesPath), "Modes should exist after claim");

        // Force-clean without releasing (modes still present)
        var result = await CleanAsync("Adele", force: true);

        result.AssertSuccess();
        Assert.False(Directory.Exists(modesPath), "Modes directory should be removed by clean");
    }

    [Fact]
    public async Task Clean_UnknownAgent_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await CleanAsync("NotAnAgent");

        result.AssertExitCode(2);
        result.AssertStderrContains("Unknown agent");
    }

    [Fact]
    public async Task Clean_NoArguments_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);

        var command = CleanCommand.Create();
        var result = await RunAsync(command);

        result.AssertExitCode(2);
        result.AssertStderrContains("Specify");
    }

    #endregion

    #region Clean — Marker Removal

    [Fact]
    public async Task Clean_Agent_RemovesWaitingDirectory()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Create .waiting/ with marker files
        WriteFile("dydo/agents/Adele/.waiting/my-task.json", """{"target":"Brian","task":"my-task","since":"2026-03-09T00:00:00Z"}""");
        WriteFile("dydo/agents/Adele/.waiting/other-task.json", """{"target":"Charlie","task":"other-task","since":"2026-03-09T00:00:00Z"}""");

        var result = await CleanAsync("Adele");

        result.AssertSuccess();
        Assert.False(Directory.Exists(Path.Combine(TestDir, "dydo/agents/Adele/.waiting")));
    }

    [Fact]
    public async Task Clean_Agent_RemovesReplyPendingDirectory()
    {
        await InitProjectAsync("none", "balazs", 3);

        WriteFile("dydo/agents/Adele/.reply-pending/my-task.json", """{"to":"Brian","task":"my-task","since":"2026-03-09T00:00:00Z"}""");

        var result = await CleanAsync("Adele");

        result.AssertSuccess();
        Assert.False(Directory.Exists(Path.Combine(TestDir, "dydo/agents/Adele/.reply-pending")));
    }

    [Fact]
    public async Task Clean_Agent_RemovesAutoCloseMarker()
    {
        await InitProjectAsync("none", "balazs", 3);

        WriteFile("dydo/agents/Adele/.auto-close", "Charlie");

        var result = await CleanAsync("Adele");

        result.AssertSuccess();
        Assert.False(File.Exists(Path.Combine(TestDir, "dydo/agents/Adele/.auto-close")));
    }

    [Fact]
    public async Task Clean_Agent_RemovesNestedWaitingFiles()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Multiple marker files in .waiting/
        WriteFile("dydo/agents/Adele/.waiting/task-a.json", """{"target":"Brian","task":"task-a","since":"2026-03-09T00:00:00Z"}""");
        WriteFile("dydo/agents/Adele/.waiting/task-b.json", """{"target":"Charlie","task":"task-b","since":"2026-03-09T00:00:00Z"}""");
        WriteFile("dydo/agents/Adele/.waiting/task-c.json", """{"target":"Dexter","task":"task-c","since":"2026-03-09T00:00:00Z"}""");

        var result = await CleanAsync("Adele");

        result.AssertSuccess();
        Assert.False(Directory.Exists(Path.Combine(TestDir, "dydo/agents/Adele/.waiting")));
    }

    [Fact]
    public async Task Clean_Agent_NoMarkersPresent_NoError()
    {
        await InitProjectAsync("none", "balazs", 3);

        // No markers exist — clean should succeed without errors
        var result = await CleanAsync("Adele");

        result.AssertSuccess();
        result.AssertStdoutContains("Cleaned");
    }

    [Fact]
    public async Task Clean_Agent_PreservesWorkflowMd()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Create markers alongside workflow.md
        WriteFile("dydo/agents/Adele/.waiting/task.json", """{"target":"Brian","task":"task","since":"2026-03-09T00:00:00Z"}""");
        WriteFile("dydo/agents/Adele/.auto-close", "Charlie");

        var result = await CleanAsync("Adele");

        result.AssertSuccess();
        AssertFileExists("dydo/agents/Adele/workflow.md");
    }

    [Fact]
    public async Task Clean_Agent_PreservesArchiveDirectory()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Create archive with content
        WriteFile("dydo/agents/Adele/archive/inbox/old-item.md", "archived item");
        WriteFile("dydo/agents/Adele/.waiting/task.json", """{"target":"Brian","task":"task","since":"2026-03-09T00:00:00Z"}""");

        var result = await CleanAsync("Adele");

        result.AssertSuccess();
        AssertFileExists("dydo/agents/Adele/archive/inbox/old-item.md");
    }

    #endregion

    #region Clean All — Marker Removal

    [Fact]
    public async Task Clean_All_RemovesMarkersFromAllAgents()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Create markers in multiple agent workspaces
        WriteFile("dydo/agents/Adele/.waiting/task.json", """{"target":"Brian","task":"task","since":"2026-03-09T00:00:00Z"}""");
        WriteFile("dydo/agents/Adele/.auto-close", "Charlie");
        WriteFile("dydo/agents/Brian/.reply-pending/task.json", """{"to":"Adele","task":"task","since":"2026-03-09T00:00:00Z"}""");
        WriteFile("dydo/agents/Charlie/.waiting/other.json", """{"target":"Adele","task":"other","since":"2026-03-09T00:00:00Z"}""");

        var result = await CleanAllAsync();

        result.AssertSuccess();
        Assert.False(Directory.Exists(Path.Combine(TestDir, "dydo/agents/Adele/.waiting")));
        Assert.False(File.Exists(Path.Combine(TestDir, "dydo/agents/Adele/.auto-close")));
        Assert.False(Directory.Exists(Path.Combine(TestDir, "dydo/agents/Brian/.reply-pending")));
        Assert.False(Directory.Exists(Path.Combine(TestDir, "dydo/agents/Charlie/.waiting")));
    }

    [Fact]
    public async Task Clean_All_WorkingAgents_RequiresForce()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");

        // Create markers on working agent
        WriteFile("dydo/agents/Adele/.waiting/task.json", """{"target":"Brian","task":"task","since":"2026-03-09T00:00:00Z"}""");

        // --all without --force should fail when agents are working
        var result = await CleanAllAsync(force: false);

        result.AssertExitCode(2);
        // Markers should still exist
        Assert.True(File.Exists(Path.Combine(TestDir, "dydo/agents/Adele/.waiting/task.json")));
    }

    #endregion

    #region Clean By Task — Marker Removal

    [Fact]
    public async Task Clean_ByTask_RemovesMarkersForMatchingAgents()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Agent on matching task with markers
        WriteFile("dydo/agents/Adele/state.md", """
            ---
            agent: Adele
            role: code-writer
            task: feature-auth
            status: free
            started: null
            writable-paths: []
            readonly-paths: []
            ---
            """);
        WriteFile("dydo/agents/Adele/.waiting/feature-auth.json", """{"target":"Brian","task":"feature-auth","since":"2026-03-09T00:00:00Z"}""");
        WriteFile("dydo/agents/Adele/.reply-pending/feature-auth.json", """{"to":"Brian","task":"feature-auth","since":"2026-03-09T00:00:00Z"}""");
        WriteFile("dydo/agents/Adele/.auto-close", "Brian");

        var result = await CleanByTaskAsync("feature-auth");

        result.AssertSuccess();
        Assert.False(Directory.Exists(Path.Combine(TestDir, "dydo/agents/Adele/.waiting")));
        Assert.False(Directory.Exists(Path.Combine(TestDir, "dydo/agents/Adele/.reply-pending")));
        Assert.False(File.Exists(Path.Combine(TestDir, "dydo/agents/Adele/.auto-close")));
    }

    [Fact]
    public async Task Clean_ByTask_DoesNotCleanUnrelatedAgents()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Agent on a different task
        WriteFile("dydo/agents/Brian/state.md", """
            ---
            agent: Brian
            role: code-writer
            task: feature-payments
            status: free
            started: null
            writable-paths: []
            readonly-paths: []
            ---
            """);
        WriteFile("dydo/agents/Brian/.waiting/feature-payments.json", """{"target":"Adele","task":"feature-payments","since":"2026-03-09T00:00:00Z"}""");

        var result = await CleanByTaskAsync("feature-auth");

        result.AssertSuccess();
        // Brian's markers should be untouched
        Assert.True(File.Exists(Path.Combine(TestDir, "dydo/agents/Brian/.waiting/feature-payments.json")));
    }

    #endregion

    #region Wait Marker Audit

    [Fact]
    public async Task Clean_AuditReportsStaleMarkers()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Create wait markers
        WriteFile("dydo/agents/Adele/.waiting/task.json", """{"target":"Brian","task":"task","since":"2026-03-09T00:00:00Z"}""");
        WriteFile("dydo/agents/Adele/.waiting/task2.json", """{"target":"Charlie","task":"task2","since":"2026-03-09T00:00:00Z"}""");

        var result = await CleanAsync("Adele");

        result.AssertSuccess();
        result.AssertStdoutContains("Audit:");
        result.AssertStdoutContains("cleaned 2");
    }

    [Fact]
    public async Task Clean_AuditFixesStalePid()
    {
        // Without PID field in WaitMarker, force clean removes all markers
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");

        WriteFile("dydo/agents/Adele/.waiting/task.json", """{"target":"Brian","task":"task","since":"2026-03-09T00:00:00Z"}""");

        var result = await CleanAsync("Adele", force: true);

        result.AssertSuccess();
        Assert.False(Directory.Exists(Path.Combine(TestDir, "dydo/agents/Adele/.waiting")));
    }

    [Fact]
    public async Task Clean_AuditNoMarkers_NoOutput()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await CleanAsync("Adele");

        result.AssertSuccess();
        Assert.DoesNotContain("Audit:", result.Stdout);
    }

    #endregion

    #region Helper Methods

    private async Task<CommandResult> WorkspaceCheckAsync()
    {
        var command = WorkspaceCommand.Create();
        return await RunAsync(command, "check");
    }

    private async Task<CommandResult> CleanAsync(string agent, bool force = false)
    {
        var command = CleanCommand.Create();
        var args = force
            ? new[] { agent, "--force" }
            : new[] { agent };
        return await RunAsync(command, args);
    }

    private async Task<CommandResult> CleanAllAsync(bool force = false)
    {
        var command = CleanCommand.Create();
        var args = force
            ? new[] { "--all", "--force" }
            : new[] { "--all" };
        return await RunAsync(command, args);
    }

    private async Task<CommandResult> CleanByTaskAsync(string task, bool force = false)
    {
        var command = CleanCommand.Create();
        var args = force
            ? new[] { "--task", task, "--force" }
            : new[] { "--task", task };
        return await RunAsync(command, args);
    }

    #endregion
}
