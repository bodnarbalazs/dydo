namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;

/// <summary>
/// Integration tests for agent lifecycle commands:
/// whoami, claim, release, status, list, role.
/// </summary>
[Collection("Integration")]
public class AgentLifecycleTests : IntegrationTestBase
{
    #region Whoami

    [Fact]
    public async Task Whoami_NoAgent_ShowsHumanAndAvailable()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await WhoamiAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("balazs");
        // Should show available agents
        result.AssertStdoutContains("Adele");
    }

    [Fact]
    public async Task Whoami_WithAgent_ShowsIdentity()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        var result = await WhoamiAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Adele");
    }

    [Fact]
    public async Task Whoami_WithRoleSet_ShowsRole()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");

        var result = await WhoamiAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("code-writer");
    }

    #endregion

    #region Claim

    [Fact]
    public async Task Claim_Auto_ClaimsFirstFree()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await ClaimAgentAsync("auto");

        result.AssertSuccess();
        // Should claim first available (Adele)
        result.AssertStdoutContains("Adele");
    }

    [Fact]
    public async Task Claim_ByName_ClaimsSpecific()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await ClaimAgentAsync("Brian");

        result.AssertSuccess();
        result.AssertStdoutContains("Brian");
    }

    [Fact]
    public async Task Claim_WrongHuman_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Try to claim an agent assigned to a different human
        // First, add another human with their own agents
        await JoinProjectAsync("none", "alice", 2);

        // Set human to balazs and try to claim alice's agent
        SetHuman("balazs");
        var command = AgentCommand.Create();
        var result = await RunAsync(command, "claim", "Dexter"); // Dexter is alice's agent

        result.AssertExitCode(2);
        result.AssertStderrContains("assigned to human");
    }

    [Fact]
    public async Task Claim_AlreadyClaimed_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        // Try to claim another agent while one is already claimed
        var result = await ClaimAgentAsync("Brian");

        result.AssertExitCode(2);
        result.AssertStderrContains("already");
    }

    [Fact]
    public async Task Claim_NonExistent_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await ClaimAgentAsync("NotARealAgent");

        result.AssertExitCode(2);
        result.AssertStderrContains("Invalid agent name");
    }

    [Fact]
    public async Task Claim_ByLetter_IsCaseInsensitive()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Claim by first letter (lowercase should work same as uppercase)
        var result = await ClaimAgentAsync("a"); // Should match "Adele"

        result.AssertSuccess();
        result.AssertStdoutContains("Adele");
    }

    [Fact]
    public async Task Claim_ByLetter_Uppercase_Works()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await ClaimAgentAsync("B"); // Should match "Brian"

        result.AssertSuccess();
        result.AssertStdoutContains("Brian");
    }

    [Fact]
    public async Task Claim_ByLetter_NoMatch_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);

        // No agent starts with 'z'
        var result = await ClaimAgentAsync("z");

        result.AssertExitCode(2);
        result.AssertStderrContains("Invalid agent name");
    }

    #endregion

    #region Release

    [Fact]
    public async Task Release_FreesAgent()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        var result = await ReleaseAgentAsync();

        result.AssertSuccess();

        // Agent should now be free
        var listResult = await ListAgentsAsync(freeOnly: true);
        listResult.AssertStdoutContains("Adele");
    }

    [Fact]
    public async Task Release_NoAgent_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await ReleaseAgentAsync();

        result.AssertExitCode(2);
        result.AssertStderrContains("No agent");
    }

    [Fact]
    public async Task Release_ClearsRole()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "my-task");
        await ReleaseAgentAsync();

        // Claim again and verify role is cleared
        await ClaimAgentAsync("Adele");
        var status = await AgentStatusAsync();

        status.AssertSuccess();
        // Should not have the old role
        Assert.DoesNotContain("code-writer", status.Stdout);
    }

    #endregion

    #region Status

    [Fact]
    public async Task Status_ShowsCurrentState()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "implement-feature");

        var result = await AgentStatusAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Adele");
        result.AssertStdoutContains("code-writer");
        result.AssertStdoutContains("implement-feature");
    }

    [Fact]
    public async Task Status_ByName_ShowsSpecificAgent()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await AgentStatusAsync("Brian");

        result.AssertSuccess();
        result.AssertStdoutContains("Brian");
    }

    [Fact]
    public async Task Status_NoAgent_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await AgentStatusAsync();

        result.AssertExitCode(2);
        result.AssertStderrContains("No agent");
    }

    #endregion

    #region List

    [Fact]
    public async Task List_ShowsAllAgents()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await ListAgentsAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Adele");
        result.AssertStdoutContains("Brian");
        result.AssertStdoutContains("Charlie");
    }

    [Fact]
    public async Task List_Free_ShowsOnlyFree()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        var result = await ListAgentsAsync(freeOnly: true);

        result.AssertSuccess();
        // Adele is claimed, so should not be in free list
        Assert.DoesNotContain("Adele", result.Stdout);
        result.AssertStdoutContains("Brian");
        result.AssertStdoutContains("Charlie");
    }

    [Fact]
    public async Task List_ShowsHumanAssignments()
    {
        await InitProjectAsync("none", "balazs", 3);
        await JoinProjectAsync("none", "alice", 2);

        var result = await ListAgentsAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("balazs");
        result.AssertStdoutContains("alice");
    }

    #endregion

    #region Role

    [Fact]
    public async Task Role_SetsPermissions()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        var result = await SetRoleAsync("code-writer");

        result.AssertSuccess();

        // Verify role is set
        var status = await AgentStatusAsync();
        status.AssertStdoutContains("code-writer");
    }

    [Fact]
    public async Task Role_WithTask_SetsBoth()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        var result = await SetRoleAsync("code-writer", "implement-auth");

        result.AssertSuccess();

        var status = await AgentStatusAsync();
        status.AssertStdoutContains("code-writer");
        status.AssertStdoutContains("implement-auth");
    }

    [Fact]
    public async Task Role_Invalid_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        var result = await SetRoleAsync("invalid-role-name");

        result.AssertExitCode(2);
        result.AssertStderrContains("Invalid role");
    }

    [Fact]
    public async Task Role_NoAgent_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await SetRoleAsync("code-writer");

        result.AssertExitCode(2);
        result.AssertStderrContains("No agent");
    }

    [Fact]
    public async Task Role_CanSwitchRoles()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");

        var result = await SetRoleAsync("reviewer");

        result.AssertSuccess();

        var status = await AgentStatusAsync();
        status.AssertStdoutContains("reviewer");
        Assert.DoesNotContain("code-writer", status.Stdout);
    }

    #endregion
}
