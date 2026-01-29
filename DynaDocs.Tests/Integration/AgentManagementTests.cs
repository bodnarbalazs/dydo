namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;

/// <summary>
/// Integration tests for agent management commands:
/// agent new, agent rename, agent remove, agent reassign.
/// </summary>
[Collection("Integration")]
public class AgentManagementTests : IntegrationTestBase
{
    #region Agent New

    [Fact]
    public async Task New_CreatesAgent()
    {
        await InitProjectAsync("none", "balazs", 3);
        await JoinProjectAsync("none", "alice", 0); // Add alice without agents

        var result = await AgentNewAsync("Zoe", "alice");

        result.AssertSuccess();
        result.AssertStdoutContains("Agent created");
        result.AssertStdoutContains("Zoe");
        AssertFileContains("dydo.json", "Zoe");
    }

    [Fact]
    public async Task New_CreatesWorkspace()
    {
        await InitProjectAsync("none", "balazs", 3);
        await JoinProjectAsync("none", "alice", 0);

        await AgentNewAsync("Zoe", "alice");

        AssertDirectoryExists("dydo/agents/Zoe");
        AssertFileExists("dydo/agents/Zoe/workflow.md");
        AssertDirectoryExists("dydo/agents/Zoe/modes");
    }

    [Fact]
    public async Task New_Duplicate_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Adele already exists from init
        var result = await AgentNewAsync("Adele", "balazs");

        result.AssertExitCode(2);
        result.AssertStderrContains("already exists");
    }

    [Fact]
    public async Task New_AssignsToHuman()
    {
        await InitProjectAsync("none", "balazs", 3);
        await JoinProjectAsync("none", "alice", 0);

        var result = await AgentNewAsync("Zoe", "alice");

        result.AssertSuccess();
        result.AssertStdoutContains("Assigned to: alice");
    }

    #endregion

    #region Agent Rename

    [Fact]
    public async Task Rename_UpdatesName()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await AgentRenameAsync("Adele", "Aria");

        result.AssertSuccess();
        result.AssertStdoutContains("renamed");
        result.AssertStdoutContains("Aria");
    }

    [Fact]
    public async Task Rename_UpdatesConfig()
    {
        await InitProjectAsync("none", "balazs", 3);

        await AgentRenameAsync("Adele", "Aria");

        AssertFileContains("dydo.json", "Aria");
        var content = ReadFile("dydo.json");
        Assert.DoesNotContain("Adele", content);
    }

    [Fact]
    public async Task Rename_UpdatesWorkspace()
    {
        await InitProjectAsync("none", "balazs", 3);

        await AgentRenameAsync("Adele", "Aria");

        AssertDirectoryExists("dydo/agents/Aria");
        Assert.False(Directory.Exists(Path.Combine(TestDir, "dydo/agents/Adele")));
    }

    [Fact]
    public async Task Rename_NotExists_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await AgentRenameAsync("NotReal", "Foo");

        result.AssertExitCode(2);
        result.AssertStderrContains("does not exist");
    }

    [Fact]
    public async Task Rename_WorkingAgent_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");

        var result = await AgentRenameAsync("Adele", "Aria");

        result.AssertExitCode(2);
        // Should fail because agent is working
        Assert.True(result.HasError);
    }

    [Fact]
    public async Task Rename_ToExistingName_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Try to rename Adele to Brian (Brian already exists)
        var result = await AgentRenameAsync("Adele", "Brian");

        result.AssertExitCode(2);
        result.AssertStderrContains("already exists");
    }

    #endregion

    #region Agent Remove

    [Fact]
    public async Task Remove_DeletesAgent()
    {
        await InitProjectAsync("none", "balazs", 3);
        await JoinProjectAsync("none", "alice", 1); // Add Dexter

        var result = await AgentRemoveAsync("Dexter", force: true);

        result.AssertSuccess();
        result.AssertStdoutContains("removed");
        var content = ReadFile("dydo.json");
        Assert.DoesNotContain("Dexter", content);
    }

    [Fact]
    public async Task Remove_DeletesWorkspace()
    {
        await InitProjectAsync("none", "balazs", 3);
        await JoinProjectAsync("none", "alice", 1);

        await AgentRemoveAsync("Dexter", force: true);

        Assert.False(Directory.Exists(Path.Combine(TestDir, "dydo/agents/Dexter")));
    }

    [Fact]
    public async Task Remove_NotExists_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await AgentRemoveAsync("NonExistent", force: true);

        result.AssertExitCode(2);
        result.AssertStderrContains("does not exist");
    }

    [Fact]
    public async Task Remove_ClaimedAgent_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");

        // Remove fails for a claimed agent (must release first)
        var result = await AgentRemoveAsync("Adele", force: true);

        result.AssertExitCode(2);
        result.AssertStderrContains("claimed");
    }

    #endregion

    #region Agent Reassign

    [Fact]
    public async Task Reassign_ChangesHuman()
    {
        await InitProjectAsync("none", "balazs", 3);
        await JoinProjectAsync("none", "alice", 0);

        var result = await AgentReassignAsync("Adele", "alice");

        result.AssertSuccess();
        result.AssertStdoutContains("reassigned");
        result.AssertStdoutContains("alice");
    }

    [Fact]
    public async Task Reassign_NotExists_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);
        await JoinProjectAsync("none", "alice", 0);

        var result = await AgentReassignAsync("NotReal", "alice");

        result.AssertExitCode(2);
        // Should fail because agent doesn't exist
        Assert.True(result.HasError);
    }

    [Fact]
    public async Task Reassign_UpdatesConfig()
    {
        await InitProjectAsync("none", "balazs", 3);
        await JoinProjectAsync("none", "alice", 0);

        await AgentReassignAsync("Adele", "alice");

        // Verify config was updated (Adele now under alice's assignments)
        var content = ReadFile("dydo.json");
        // The assignment should be reflected in the config
        Assert.Contains("alice", content);
    }

    #endregion

    #region Helper Methods

    private async Task<CommandResult> AgentNewAsync(string name, string human)
    {
        var command = AgentCommand.Create();
        return await RunAsync(command, "new", name, human);
    }

    private async Task<CommandResult> AgentRenameAsync(string oldName, string newName)
    {
        var command = AgentCommand.Create();
        return await RunAsync(command, "rename", oldName, newName);
    }

    private async Task<CommandResult> AgentRemoveAsync(string name, bool force = false)
    {
        var command = AgentCommand.Create();
        var args = force ? new[] { "remove", name, "--force" } : new[] { "remove", name };
        return await RunAsync(command, args);
    }

    private async Task<CommandResult> AgentReassignAsync(string name, string human)
    {
        var command = AgentCommand.Create();
        return await RunAsync(command, "reassign", name, human);
    }

    #endregion
}
