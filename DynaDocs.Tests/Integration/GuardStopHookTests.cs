namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;
using DynaDocs.Services;

/// <summary>
/// End-to-end coverage for the Stop-hook entry point <c>dydo guard --stop</c> (Decision 030 §1).
/// Drives the real command with a hook payload on stdin — the same path Claude Code's Stop hook
/// uses — and asserts both the agent-state flag and its task-file mirror. A Stop hook must never
/// block turn-end, so every case exits 0.
/// </summary>
[Collection("Integration")]
public class GuardStopHookTests : IntegrationTestBase
{
    private async Task<CommandResult> GuardStopAsync(string json)
    {
        StoreSessionContext();
        var command = GuardCommand.Create();
        var stdin = new StringReader(json);
        var (exitCode, stdout, stderr) = await ConsoleCapture.AllAsyncWithStdin(
            stdin, async () => await command.Parse(["--stop"]).InvokeAsync());
        return new CommandResult(exitCode, stdout, stderr);
    }

    private static string StopPayload(string? sessionId) =>
        sessionId == null
            ? "{\"hook_event_name\":\"Stop\"}"
            : $"{{\"session_id\":\"{sessionId}\",\"hook_event_name\":\"Stop\"}}";

    private string TaskFile(string task) =>
        Path.Combine(DydoDir, "project", "tasks", $"{task}.md");

    [Fact]
    public async Task GuardStop_WorkingAgentWithInFlightTask_SetsFlagAndMirrorsToTaskFile()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "stop-task");

        var result = await GuardStopAsync(StopPayload(TestSessionId));

        result.AssertSuccess();
        Assert.True(new AgentRegistry(TestDir).GetAgentState("Adele")!.NeedsHuman);
        Assert.Contains("needs-human: true", File.ReadAllText(TaskFile("stop-task")));
    }

    [Fact]
    public async Task GuardStop_ReleasedAgent_DoesNotSetFlag()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "stop-task");
        await ReleaseAgentAsync();

        var result = await GuardStopAsync(StopPayload(TestSessionId));

        result.AssertSuccess();
        Assert.False(new AgentRegistry(TestDir).GetAgentState("Adele")!.NeedsHuman);
    }

    [Fact]
    public async Task GuardStop_WorkingAgentWithNoTask_DoesNotSetFlag()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");

        var result = await GuardStopAsync(StopPayload(TestSessionId));

        result.AssertSuccess();
        Assert.False(new AgentRegistry(TestDir).GetAgentState("Adele")!.NeedsHuman);
    }

    [Fact]
    public async Task GuardStop_MalformedPayload_IsSafeNoOp_Exit0()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "stop-task");

        var result = await GuardStopAsync("{ this is not valid json ");

        result.AssertSuccess();
        Assert.False(new AgentRegistry(TestDir).GetAgentState("Adele")!.NeedsHuman);
    }
}
