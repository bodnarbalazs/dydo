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
