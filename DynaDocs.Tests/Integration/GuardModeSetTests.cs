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

    private static string Hook(string command, string? permissionMode = "default", string eventName = "PreToolUse",
        string tool = "Bash", bool codex = false)
    {
        var hook = new JsonObject
        {
            ["session_id"] = TestSessionId,
            ["hook_event_name"] = eventName,
            ["tool_name"] = tool,
            ["tool_input"] = new JsonObject { ["command"] = command }
        };
        if (permissionMode != null)
            hook["permission_mode"] = permissionMode;
        // turn_id is a Codex-only extension of the hook payload.
        if (codex)
            hook["turn_id"] = "turn-1";
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

        Assert.Contains("BLOCKED: This host cannot ask", result.AssertPreToolUseDenied());
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

    // A backslash path is a path only where backslashes are literal: bash reads C:\tools\dydo.exe
    // as C:toolsdydo.exe, so that row runs under the PowerShell tool.
    [Theory]
    [InlineData("dydo mode \\\nset overnight on", "Bash")]
    [InlineData("dydo mode `\r\nset overnight on", "Bash")]
    [InlineData(@"C:\tools\dydo.exe mode set overnight on", "PowerShell")]
    [InlineData(@"d\ydo mode set overnight on", "Bash")]
    [InlineData("echo start\ndydo mode set overnight on", "Bash")]
    [InlineData("echo $(dydo mode set overnight on)", "Bash")]
    [InlineData("git status || DYDO_X=1 dydo mode set overnight on", "Bash")]
    public async Task ObfuscatedModeSet_MeetsTheReminder(string command, string tool)
    {
        await InitProjectAsync("none");

        var result = await GuardWithStdinAsync(Hook(command, tool: tool), ClaudeHook);

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
    [InlineData("dotnet run --project DynaDocs.csproj -- mode set overnight on")]
    [InlineData("dotnet run -p ../src/DynaDocs/ -- mode set overnight on")]
    [InlineData("dotnet run --project=tools/dydo -- mode set overnight on")]
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

    [Theory]
    [InlineData("Start-Process dydo -ArgumentList @('mode', 'set', 'overnight', 'on') -Wait")]
    [InlineData("Start-Process dydo -ArgumentList ('mode', 'set', 'overnight', 'on')")]
    [InlineData("Start-Process -FilePath dydo -ArgumentList @(\"mode\", \"set\") -NoNewWindow")]
    [InlineData("Write-Output (dydo mode set overnight on)")]
    public async Task PowerShellArrayOrGroup_MeetsTheReminder(string command)
    {
        await InitProjectAsync("none");

        var result = await GuardWithStdinAsync(Hook(command, tool: "PowerShell"), ClaudeHook);

        Assert.Contains("BLOCKED: dydo mode set changes the mode", result.AssertPreToolUseDenied());
    }

    [Fact]
    public async Task PowerShellArrayRerun_NamesTheCanonicalCommand()
    {
        await InitProjectAsync("none");

        var result = await Twice(Hook("Start-Process dydo -ArgumentList @('mode', 'set', 'overnight', 'off') -Wait",
            permissionMode: "dontAsk", tool: "PowerShell"));

        Assert.EndsWith("their own terminal: dydo mode set overnight off", result.AssertPreToolUseDenied());
    }

    // PowerShell escapes a quote inside double quotes with a backtick or a doubled quote, and keeps a
    // backslash literal; bash would read the backtick as a substitution and the ; as a separator,
    // which is why the executing shell decides.
    [Theory]
    [InlineData("git commit -m \"fix `\"mode`\"; dydo mode set now asks\"")]
    [InlineData("gh pr comment 12 --body \"fix `\"mode`\"; dydo mode set now asks\"")]
    [InlineData(@"git commit -m ""path C:\"" -m ""note; dydo mode set now asks""")]
    [InlineData(@"git commit -m ""fix """"mode""""; dydo mode set now asks""")]
    [InlineData(@"git commit -m 'it''s; dydo mode set now asks'")]
    public async Task PowerShellEscapedQuotes_UnderThePowerShellTool_PassUntouched(string command)
    {
        await InitProjectAsync("none");

        (await GuardWithStdinAsync(Hook(command, tool: "PowerShell"), ClaudeHook)).AssertPreToolUseAllowed();
    }

    // A PowerShell block comment, which can span lines, is never run.
    [Theory]
    [InlineData("<#\ndydo mode set overnight on\n#>\ngit status")]
    [InlineData("git status <# dydo mode set overnight on #>; git log")]
    public async Task PowerShellBlockComment_UnderThePowerShellTool_PassesUntouched(string command)
    {
        await InitProjectAsync("none");

        (await GuardWithStdinAsync(Hook(command, tool: "PowerShell"), ClaudeHook)).AssertPreToolUseAllowed();
    }

    [Fact]
    public async Task UnterminatedPowerShellBlockComment_MeetsTheReminder()
    {
        await InitProjectAsync("none");

        var result = await GuardWithStdinAsync(Hook("<# note\ndydo mode set overnight on", tool: "PowerShell"), ClaudeHook);

        Assert.Contains("BLOCKED: dydo mode set changes the mode", result.AssertPreToolUseDenied());
    }

    // dotnet run is dydo only when its project is dydo; another project's arguments are its own.
    [Theory]
    [InlineData("dotnet run --project examples/OtherCli -- mode set dark on")]
    [InlineData("dotnet run -p OtherCli.csproj -- mode set dark on")]
    public async Task DotnetRunOfAnotherProject_PassesUntouched(string command)
    {
        await InitProjectAsync("none");

        (await GuardWithStdinAsync(Hook(command), ClaudeHook)).AssertPreToolUseAllowed();
    }

    // Bash escapes a quote or a backslash with a backslash; a PowerShell reading of the same text
    // ends the first string early and finds a dydo command after the ;.
    [Theory]
    [InlineData("git commit -m \"fix \\\"mode\\\"; dydo mode set now asks\"")]
    [InlineData("git commit -m \"dir C:\\\\\" -m \"then; dydo mode set now asks\"")]
    public async Task BashEscapedQuotes_UnderTheBashTool_PassUntouched(string command)
    {
        await InitProjectAsync("none");

        (await GuardWithStdinAsync(Hook(command), ClaudeHook)).AssertPreToolUseAllowed();
    }

    // Codex sends its shell calls as Bash with no host marker, and runs them in PowerShell on
    // Windows and in the user's POSIX shell elsewhere.
    [Fact]
    public async Task CodexShellCall_IsReadWithTheShellCodexRuns()
    {
        await InitProjectAsync("none");

        var result = await GuardWithStdinAsync(
            Hook("git commit -m \"fix `\"mode`\"; dydo mode set now asks\"", codex: true));

        if (OperatingSystem.IsWindows())
            result.AssertPreToolUseAllowed();
        else
            Assert.Contains("BLOCKED: This host cannot ask", result.AssertPreToolUseDenied());
    }

    [Theory]
    [InlineData("PowerShell", "claude", false, true, null, "PowerShell")]
    [InlineData("powershell", null, false, false, null, "PowerShell")]
    [InlineData("Bash", "claude", false, true, null, "Posix")]
    [InlineData("Bash", null, false, false, null, "Posix")]
    [InlineData("Bash", null, false, true, null, "Unknown")]
    [InlineData("Bash", null, true, true, null, "PowerShell")]
    [InlineData("Bash", null, true, false, "/bin/zsh", "Posix")]
    [InlineData("Bash", null, true, false, "/usr/bin/pwsh", "PowerShell")]
    [InlineData("shell_command", null, false, true, null, "PowerShell")]
    [InlineData("unified_exec", null, false, false, "/bin/bash", "Posix")]
    [InlineData("Read", "claude", false, true, null, "Unknown")]
    [InlineData(null, null, false, true, null, "Unknown")]
    public void ExecutingShell_FollowsTheHostAndTool(string? tool, string? host, bool fromCodex, bool windows,
        string? userShell, string expected)
    {
        Assert.Equal(expected, DynaDocs.Commands.GuardCommand.ExecutingShell(tool, host, fromCodex, windows, userShell).ToString());
    }

    private Task<CommandResult> GuardCommandAsync(string command) =>
        RunAsync(DynaDocs.Commands.GuardCommand.Create(), "--command", command);
}
