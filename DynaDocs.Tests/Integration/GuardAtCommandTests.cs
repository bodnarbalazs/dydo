namespace DynaDocs.Tests.Integration;

using System.Text.Json;
using DynaDocs.Commands;

/// <summary>
/// DYD-675: a guard argument whose text starts with '@' is text, never a System.CommandLine
/// response-file reference. Taken as one, the guard exits 1 (a hook host may read that as a
/// non-blocking error, so a PowerShell here-string would fail open) or splices a file's lines into
/// its own arguments. The argv lane is `dydo guard --command|--path|--action`; the two hook lanes
/// (`dydo guard --host claude`, Codex's `dydo guard`) carry the command in stdin JSON.
/// </summary>
[Collection("Integration")]
public class GuardAtCommandTests : IntegrationTestBase
{
    private const string HarmlessHereString = "@'\nWrite-Output hi\n'@";

    // The dangerous-command layer reads the whole text, so a here-string body cannot hide it.
    private const string DangerousHereString = "@'\ngit push --force origin main\n'@";

    private const string ForcePushBlock = "Force push";

    private static void AssertNotResponseFileRead(CommandResult result)
    {
        Assert.DoesNotContain("Response file", result.Stderr);
        Assert.DoesNotContain("Required argument missing", result.Stderr);
    }

    private async Task<CommandResult> CliCommandAsync(string command)
    {
        await InitProjectAsync("none");
        return await RunAsync(GuardCommand.Create(), "--command", command);
    }

    // ---- argv lane: dydo guard --command <text> ----

    [Fact]
    public async Task CliCommand_HarmlessHereString_IsAllowed()
    {
        var result = await CliCommandAsync(HarmlessHereString);

        AssertNotResponseFileRead(result);
        result.AssertExitCode(0);
        Assert.Equal("", result.Stderr);
    }

    [Fact]
    public async Task CliCommand_DangerousHereString_IsDenied()
    {
        var result = await CliCommandAsync(DangerousHereString);

        AssertNotResponseFileRead(result);
        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
        result.AssertStderrContains(ForcePushBlock);
    }

    [Fact]
    public async Task CliCommand_BareAtWord_IsAllowed()
    {
        var result = await CliCommandAsync("@nofile");

        AssertNotResponseFileRead(result);
        result.AssertExitCode(0);
        Assert.Equal("", result.Stderr);
    }

    [Fact]
    public async Task CliCommand_AtWordBeforeDangerousCommand_IsDenied()
    {
        var result = await CliCommandAsync("@nofile; git push --force origin main");

        AssertNotResponseFileRead(result);
        result.AssertExitCode(2);
        result.AssertStderrContains(ForcePushBlock);
    }

    [Fact]
    public async Task CliCommand_AtExistingFile_IsTextAndTheFileIsNotRead()
    {
        // Spliced into argv, this one quoted line becomes `--command "git push --force origin main"`
        // and the guard denies it. As text, "@<path>" is a harmless word, so the allow proves the
        // file was never opened.
        await InitProjectAsync("none");
        WriteFile("response.rsp", "\"git push --force origin main\"\n");

        var result = await RunAsync(GuardCommand.Create(), "--command", "@" + Path.Combine(TestDir, "response.rsp"));

        AssertNotResponseFileRead(result);
        result.AssertExitCode(0);
        Assert.Equal("", result.Stderr);
    }

    [Fact]
    public async Task CliCommand_AtExistingFileOfOptions_IsNotSplicedIntoArguments()
    {
        // Spliced, these lines would turn the call into `--action write --path .env`, which the
        // off-limits tier denies. They are never read.
        await InitProjectAsync("none");
        WriteFile("options.rsp", "--action\nwrite\n--path\n.env\n");

        var result = await RunAsync(GuardCommand.Create(), "--command", "@" + Path.Combine(TestDir, "options.rsp"));

        AssertNotResponseFileRead(result);
        result.AssertExitCode(0);
        Assert.Equal("", result.Stderr);
    }

    // ---- argv lane: --path and --action ----

    [Fact]
    public async Task CliPath_StartingWithAt_IsEvaluatedAsAPath()
    {
        await InitProjectAsync("none");

        var result = await RunAsync(GuardCommand.Create(), "--action", "read", "--path", "@nofile");

        AssertNotResponseFileRead(result);
        result.AssertExitCode(0);
    }

    [Fact]
    public async Task CliPath_AtExistingFileOfOptions_IsNotSplicedIntoArguments()
    {
        await InitProjectAsync("none");
        WriteFile("options.rsp", "--action\nwrite\n--path\n.env\n");

        var result = await RunAsync(GuardCommand.Create(), "--action", "read", "--path", "@" + Path.Combine(TestDir, "options.rsp"));

        AssertNotResponseFileRead(result);
        result.AssertExitCode(0);
    }

    [Fact]
    public async Task CliAction_StartingWithAt_IsNotAResponseFile()
    {
        await InitProjectAsync("none");

        var result = await RunAsync(GuardCommand.Create(), "--action", "@nofile", "--path", "src/file.cs");

        AssertNotResponseFileRead(result);
        Assert.Contains(result.ExitCode, new[] { 0, 2 });
    }

    // ---- hook lanes: the command is stdin JSON, never argv ----

    private static string HookPayload(string toolName, string command) => JsonSerializer.Serialize(new
    {
        session_id = TestSessionId,
        tool_name = toolName,
        tool_input = new { command }
    });

    [Theory]
    [InlineData("PowerShell", new string[0])]
    [InlineData("Bash", new[] { "--host", "claude" })]
    public async Task HookLane_HarmlessHereString_IsAllowed(string toolName, string[] arguments)
    {
        await InitProjectAsync("none");

        var result = await GuardWithStdinAsync(HookPayload(toolName, HarmlessHereString), arguments);

        result.AssertPreToolUseAllowed();
    }

    [Theory]
    [InlineData("PowerShell", new string[0])]
    [InlineData("Bash", new[] { "--host", "claude" })]
    public async Task HookLane_DangerousHereString_IsDenied(string toolName, string[] arguments)
    {
        await InitProjectAsync("none");

        var result = await GuardWithStdinAsync(HookPayload(toolName, DangerousHereString), arguments);

        Assert.Contains(ForcePushBlock, result.AssertPreToolUseDenied());
    }
}
