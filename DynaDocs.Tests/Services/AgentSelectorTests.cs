namespace DynaDocs.Tests.Services;

using DynaDocs.Commands;
using DynaDocs.Services;
using DynaDocs.Tests.Integration;

[Collection("Integration")]
public class AgentSelectorTests : IntegrationTestBase
{
    [Fact]
    public async Task SelectAutomatic_AllAgentsBusy_ReturnsError()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Dispatch to all agents to mark them as "dispatched" (busy)
        var dispatch = DispatchCommand.Create();
        await RunAsync(dispatch, "--role", "code-writer", "--task", "task-1", "--brief", "Brief A", "--to", "Adele", "--no-launch", "--no-wait");
        await RunAsync(dispatch, "--role", "code-writer", "--task", "task-2", "--brief", "Brief B", "--to", "Brian", "--no-launch", "--no-wait");
        await RunAsync(dispatch, "--role", "code-writer", "--task", "task-3", "--brief", "Brief C", "--to", "Charlie", "--no-launch", "--no-wait");

        var registry = new AgentRegistry(TestDir);
        var (result, error) = AgentSelector.SelectAutomatic(
            registry, "testuser", "code-writer", "new-task", "orchestrator", null);

        Assert.Null(result);
        Assert.NotNull(error);
        Assert.Contains("No free agents", error);
    }

    [Fact]
    public async Task SelectAutomatic_AllAgentsBusy_NoHuman_ReturnsGenericError()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Dispatch to all agents
        var dispatch = DispatchCommand.Create();
        await RunAsync(dispatch, "--role", "code-writer", "--task", "task-1", "--brief", "Brief A", "--to", "Adele", "--no-launch", "--no-wait");
        await RunAsync(dispatch, "--role", "code-writer", "--task", "task-2", "--brief", "Brief B", "--to", "Brian", "--no-launch", "--no-wait");
        await RunAsync(dispatch, "--role", "code-writer", "--task", "task-3", "--brief", "Brief C", "--to", "Charlie", "--no-launch", "--no-wait");

        var registry = new AgentRegistry(TestDir);
        var (result, error) = AgentSelector.SelectAutomatic(
            registry, null, "code-writer", "new-task", "orchestrator", null);

        Assert.Null(result);
        Assert.Contains("No free agents available.", error);
    }

    [Fact]
    public async Task SelectAutomatic_OriginNotValidAgent_FallsThrough()
    {
        await InitProjectAsync("none", "testuser", 3);

        var registry = new AgentRegistry(TestDir);
        var (result, error) = AgentSelector.SelectAutomatic(
            registry, "testuser", "code-writer", "my-task", "sender", "InvalidAgent");

        // Origin is invalid → falls through to pool selection → selects Adele
        Assert.NotNull(result);
        Assert.Null(error);
        Assert.Equal("Adele", result.AgentName);
    }

    [Fact]
    public async Task SelectAutomatic_OriginCantTakeRole_FallsThrough()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        // Dispatch Adele as code-writer on my-task (creates role history)
        await SetRoleAsync("code-writer", "my-task");
        await ReleaseAgentAsync();

        var registry = new AgentRegistry(TestDir);
        var (result, error) = AgentSelector.SelectAutomatic(
            registry, "testuser", "reviewer", "my-task", "sender", "Adele");

        // Adele was code-writer on my-task → can't be reviewer → falls through to pool → selects Brian
        Assert.NotNull(result);
        Assert.Null(error);
        Assert.NotEqual("Adele", result.AgentName);
    }

    [Fact]
    public async Task SelectAutomatic_OriginDifferentHuman_FallsThrough()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Join as alice with separate agents
        await JoinProjectAsync("none", "alice", 2);

        var registry = new AgentRegistry(TestDir);
        // Try to auto-return to Adele (testuser's agent) while acting as alice
        var (result, error) = AgentSelector.SelectAutomatic(
            registry, "alice", "code-writer", "my-task", "sender", "Adele");

        // Adele is testuser's agent, not alice's → falls through to pool
        Assert.NotNull(result);
        Assert.Null(error);
    }
}
