namespace DynaDocs.Tests.Integration;

using System.Text.Json.Nodes;

/// <summary>
/// The built-in mode-set nudge's edges beyond the machine-mode scenarios: it cannot be configured
/// away, an ask is only ever a PreToolUse answer, and any other block still wins over it.
/// </summary>
[Collection("Integration")]
public sealed class GuardModeSetTests : IntegrationTestBase, IDisposable
{
    private readonly string? _originalClaudeCode = Environment.GetEnvironmentVariable("CLAUDECODE");

    public GuardModeSetTests() => Environment.SetEnvironmentVariable("CLAUDECODE", "1");

    void IDisposable.Dispose()
    {
        Environment.SetEnvironmentVariable("CLAUDECODE", _originalClaudeCode);
        base.Dispose();
    }

    private static string Hook(string command, string? permissionMode = "default", string eventName = "PreToolUse")
    {
        var hook = new JsonObject
        {
            ["session_id"] = TestSessionId,
            ["hook_event_name"] = eventName,
            ["tool_name"] = "Bash",
            ["tool_input"] = new JsonObject { ["command"] = command }
        };
        if (permissionMode != null)
            hook["permission_mode"] = permissionMode;
        return hook.ToJsonString();
    }

    private async Task<CommandResult> Twice(string hook)
    {
        await GuardWithStdinAsync(hook);
        return await GuardWithStdinAsync(hook);
    }

    [Fact]
    public async Task ConfigNudgeOnTheSameCommand_CannotDowngradeIt()
    {
        await InitProjectAsync("none");
        var config = JsonNode.Parse(ReadFile("dydo.json"))!.AsObject();
        config["nudges"] = new JsonArray(new JsonObject
        {
            ["pattern"] = @"dydo\s+mode\s+set",
            ["message"] = "fine",
            ["severity"] = "notice"
        });
        WriteFile("dydo.json", config.ToJsonString());

        var result = await GuardWithStdinAsync(Hook("dydo mode set overnight on"));

        Assert.Contains("BLOCKED: dydo mode set changes the mode", result.AssertPreToolUseDenied());
    }

    [Fact]
    public async Task MissingPermissionMode_IsDenied()
    {
        await InitProjectAsync("none");

        var result = await Twice(Hook("dydo mode set overnight on", permissionMode: null));

        Assert.StartsWith("BLOCKED: This host cannot ask", result.AssertPreToolUseDenied());
    }

    [Fact]
    public async Task OtherHookEvent_NeverAsks()
    {
        await InitProjectAsync("none");

        var result = await Twice(Hook("dydo mode set overnight on", eventName: "PostToolUse"));

        result.AssertExitCode(2);
        Assert.Equal("", result.Stdout);
        result.AssertStderrContains("ASK: ");
    }

    [Fact]
    public async Task CliLane_BlocksTheRerunWithExitTwo()
    {
        await InitProjectAsync("none");

        await GuardCommandAsync("dydo mode set overnight on");
        var result = await GuardCommandAsync("dydo mode set overnight on");

        result.AssertExitCode(2);
        result.AssertStderrContains("Ask the user to run it in their own terminal: dydo mode set overnight on");
    }

    [Fact]
    public async Task AnotherBlock_WinsOverTheAsk()
    {
        await InitProjectAsync("none");

        var result = await Twice(Hook("dydo mode set overnight on && cat .env"));

        Assert.StartsWith("BLOCKED: Command references off-limits path.", result.AssertPreToolUseDenied());
    }

    [Theory]
    [InlineData("dydo mode show")]
    [InlineData("echo dydo mode")]
    public async Task OtherModeCommands_PassUntouched(string command)
    {
        await InitProjectAsync("none");

        (await GuardWithStdinAsync(Hook(command))).AssertPreToolUseAllowed();
    }

    [Fact]
    public async Task OnlyTheModeSetSegment_IsNamed()
    {
        await InitProjectAsync("none");

        var result = await Twice(Hook("dydo mode set overnight off; echo done", permissionMode: "dontAsk"));

        Assert.EndsWith("their own terminal: dydo mode set overnight off", result.AssertPreToolUseDenied());
    }

    private Task<CommandResult> GuardCommandAsync(string command) =>
        RunAsync(DynaDocs.Commands.GuardCommand.Create(), "--command", command);
}
