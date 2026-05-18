namespace DynaDocs.Tests.Services;

using System.Text.Json;
using DynaDocs.Commands;
using DynaDocs.Models;
using DynaDocs.Serialization;
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

    [Fact]
    public async Task SelectAutomatic_DoesNotPickSenderFromPool()
    {
        // Regression for issue #0108: when an orchestrator fires sequential dispatches
        // and ends up stale-working with a dead session pid (e.g. after watchdog
        // auto-resume), the auto-selector must not route the next dispatch back to
        // the sender — that hijacks the sender's identity in the new terminal.
        await InitProjectAsync("none", "testuser", 3);

        // Busy out the other agents so Brian is the only free-looking candidate.
        var dispatch = DispatchCommand.Create();
        await RunAsync(dispatch, "--role", "code-writer", "--task", "task-a", "--brief", "A", "--to", "Adele", "--no-launch", "--no-wait");
        await RunAsync(dispatch, "--role", "code-writer", "--task", "task-c", "--brief", "C", "--to", "Charlie", "--no-launch", "--no-wait");

        // Brian: stale-working with a session pid that probes as dead.
        WriteWorkingState("Brian", DateTime.UtcNow.AddMinutes(-10));
        WriteSession("Brian", pid: 999999);
        AgentRegistry.IsSessionPidAliveOverride = _ => false;

        try
        {
            var registry = new AgentRegistry(TestDir);
            var (result, error) = AgentSelector.SelectAutomatic(
                registry, "testuser", "reviewer", "new-task", senderName: "Brian", origin: "Brian");

            Assert.Null(result);
            Assert.NotNull(error);
            Assert.Contains("No free agents", error);
        }
        finally
        {
            AgentRegistry.IsSessionPidAliveOverride = null;
        }
    }

    [Fact]
    public async Task SelectExplicit_RejectsSelfDispatch()
    {
        // Regression for issue #0108: `dispatch --to <self>` would orphan the sender's
        // session in the same way the auto-selector path does. SelectExplicit must
        // refuse the dispatch before reserving the agent.
        await InitProjectAsync("none", "testuser", 3);

        var registry = new AgentRegistry(TestDir);
        var (result, error) = AgentSelector.SelectExplicit(
            registry, to: "Brian", currentHuman: "testuser", role: "code-writer",
            task: "self-task", senderName: "Brian");

        Assert.Null(result);
        Assert.NotNull(error);
        Assert.Contains("yourself", error, StringComparison.OrdinalIgnoreCase);

        // And Brian must still be free — a rejected explicit dispatch must not
        // leave him reserved.
        var brian = registry.GetAgentState("Brian");
        Assert.NotNull(brian);
        Assert.Equal(AgentStatus.Free, brian.Status);
    }

    private void WriteWorkingState(string agentName, DateTime since)
    {
        var workspace = Path.Combine(TestDir, "dydo", "agents", agentName);
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(workspace, "state.md"), $$"""
            ---
            agent: {{agentName}}
            role: null
            task: null
            status: working
            assigned: testuser
            dispatched-by: null
            dispatched-by-role: null
            window-id: null
            auto-close: false
            started: {{since.ToUniversalTime():o}}
            writable-paths: []
            readonly-paths: []
            unread-must-reads: []
            unread-messages: []
            task-role-history: {}
            ---
            """);
    }

    private void WriteSession(string agentName, int? pid)
    {
        var workspace = Path.Combine(TestDir, "dydo", "agents", agentName);
        Directory.CreateDirectory(workspace);
        var session = new AgentSession
        {
            Agent = agentName,
            SessionId = "test-session-" + agentName,
            Claimed = DateTime.UtcNow.AddMinutes(-15),
            ClaimedPid = pid
        };
        File.WriteAllText(
            Path.Combine(workspace, ".session"),
            JsonSerializer.Serialize(session, DydoDefaultJsonContext.Default.AgentSession));
    }
}
