namespace DynaDocs.Tests.Integration;

using System.Text.Json.Nodes;

/// <summary>
/// The built-in mode-set nudge's edges beyond the machine-mode scenarios: it cannot be configured
/// away, an ask is only ever a PreToolUse answer, and any other block still wins over it.
/// </summary>
[Collection("Integration")]
public sealed class GuardModeSetTests : IntegrationTestBase
{
    // The hook command dydo init writes for Claude Code.
    private static readonly string[] ClaudeHook = ["--host", "claude"];

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
        await GuardWithStdinAsync(hook, ClaudeHook);
        return await GuardWithStdinAsync(hook, ClaudeHook);
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

        var result = await GuardWithStdinAsync(Hook("dydo mode set overnight on"), ClaudeHook);

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

        (await GuardWithStdinAsync(Hook(command), ClaudeHook)).AssertPreToolUseAllowed();
    }

    [Fact]
    public async Task OnlyTheModeSetSegment_IsNamed()
    {
        await InitProjectAsync("none");

        var result = await Twice(Hook("dydo mode set overnight off; echo done", permissionMode: "dontAsk"));

        Assert.EndsWith("their own terminal: dydo mode set overnight off", result.AssertPreToolUseDenied());
    }

    [Theory]
    [InlineData("dydo mode \\\nset overnight on")]
    [InlineData("dydo mode `\r\nset overnight on")]
    [InlineData(@"C:\tools\dydo.exe mode set overnight on")]
    [InlineData(@"d\ydo mode set overnight on")]
    [InlineData("echo start\ndydo mode set overnight on")]
    [InlineData("echo $(dydo mode set overnight on)")]
    [InlineData("git status || DYDO_X=1 dydo mode set overnight on")]
    public async Task ObfuscatedModeSet_MeetsTheReminder(string command)
    {
        await InitProjectAsync("none");

        var result = await GuardWithStdinAsync(Hook(command), ClaudeHook);

        Assert.Contains("BLOCKED: dydo mode set changes the mode", result.AssertPreToolUseDenied());
    }

    [Fact]
    public async Task ObfuscatedRerun_NamesTheCanonicalCommand()
    {
        await InitProjectAsync("none");

        var result = await Twice(Hook("\"C:/tools/dydo.exe\" mode 'set' overnight \"off\"", permissionMode: "dontAsk"));

        Assert.EndsWith("their own terminal: dydo mode set overnight off", result.AssertPreToolUseDenied());
    }

    [Theory]
    [InlineData("git commit -m \"fix: the dydo mode set nudge\"")]
    [InlineData("grep -rn \"dydo mode set\" Commands")]
    [InlineData("echo dydo.exe mode set overnight on")]
    public async Task MentionOnly_PassesUntouched(string command)
    {
        await InitProjectAsync("none");

        (await GuardWithStdinAsync(Hook(command), ClaudeHook)).AssertPreToolUseAllowed();
    }

    [Theory]
    [InlineData("git commit -m \"fix guard\n\nTests pass; dydo mode set now asks\nand | dydo mode set overnight on\"")]
    [InlineData("@'\nfix guard; dydo mode set must ask\n'@ | git commit -F -")]
    [InlineData("@\"\nfix \"guard\"; dydo mode set must ask\n\"@ | git commit -F -")]
    [InlineData("git commit -m \"$(cat <<'EOF'\nfix guard\n\ndydo mode set overnight on now asks, it doesn't deny\nEOF\n)\"")]
    [InlineData("grep -rn 'dydo mode set' Commands | head")]
    [InlineData("echo 'a;b' && git log --grep=\"dydo mode set\"")]
    public async Task QuotedSeparators_DoNotMakeACommand(string command)
    {
        await InitProjectAsync("none");

        (await GuardWithStdinAsync(Hook(command), ClaudeHook)).AssertPreToolUseAllowed();
    }

    [Theory]
    [InlineData("cmd.exe /C dydo mode set overnight on")]
    [InlineData("powershell -c \"& { dydo mode set overnight on }\"")]
    [InlineData("sh -lc \"cd /tmp; dydo mode set overnight on\"")]
    [InlineData("sudo -u me nohup dydo mode set overnight on")]
    [InlineData("echo on | xargs -n 1 dydo mode set overnight")]
    [InlineData("saps -FilePath C:/tools/dydo.exe -ArgumentList 'mode set overnight on' -Wait")]
    [InlineData("Start-Process cmd -ArgumentList '/c dydo mode set overnight on'")]
    [InlineData("git commit -m \"x $(dydo mode set overnight on)\"")]
    [InlineData("dotnet exec C:/src/bin/dydo.dll mode set overnight on")]
    [InlineData("dydo mode set \"overnight on")]
    public async Task WrappedModeSet_MeetsTheReminder(string command)
    {
        await InitProjectAsync("none");

        var result = await GuardWithStdinAsync(Hook(command), ClaudeHook);

        Assert.Contains("BLOCKED: dydo mode set changes the mode", result.AssertPreToolUseDenied());
    }

    [Fact]
    public async Task StartProcessRerun_NamesTheCanonicalCommand()
    {
        await InitProjectAsync("none");

        var result = await Twice(Hook("Start-Process dydo -ArgumentList \"mode\", \"set\", \"overnight\", \"off\"",
            permissionMode: "dontAsk"));

        Assert.EndsWith("their own terminal: dydo mode set overnight off", result.AssertPreToolUseDenied());
    }

    private Task<CommandResult> GuardCommandAsync(string command) =>
        RunAsync(DynaDocs.Commands.GuardCommand.Create(), "--command", command);
}
