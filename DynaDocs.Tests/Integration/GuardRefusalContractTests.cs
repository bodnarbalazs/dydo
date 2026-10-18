namespace DynaDocs.Tests.Integration;

/// <summary>
/// LC-1021: Codex runs hooks through `powershell -NoProfile -Command`, which turns a native
/// exit 2 into exit 1, and Codex lets the tool run on exit 1. A PreToolUse refusal therefore
/// exits 0 with one deny-decision object on stdout, which Codex and Claude Code both honour.
/// </summary>
[Collection("Integration")]
public class GuardRefusalContractTests : IntegrationTestBase
{
    private static string Hook(string tool, string toolInput, string? eventName = "PreToolUse") =>
        "{\"session_id\":\"" + TestSessionId + "\""
        + (eventName == null ? "" : ",\"hook_event_name\":\"" + eventName + "\"")
        + ",\"tool_name\":\"" + tool + "\",\"tool_input\":" + toolInput + "}";

    [Fact]
    public async Task ChainedCd_IsDeniedThroughStdoutJson()
    {
        await InitProjectAsync("none");

        var result = await GuardWithStdinAsync(Hook("Bash", "{\"command\":\"cd tools && echo hi\"}"));

        var reason = result.AssertPreToolUseDenied();
        Assert.StartsWith("BLOCKED: Don't chain cd / Set-Location", reason);
        Assert.Contains("Otherwise just run: echo hi", reason);
    }

    [Fact]
    public async Task VariableExpansionWrite_IsDeniedWithItsWarnings()
    {
        await InitProjectAsync("none");

        var result = await GuardWithStdinAsync(Hook("Bash", "{\"command\":\"echo x > $OUT\"}"));

        var reason = result.AssertPreToolUseDenied();
        Assert.Contains("WARNING: ", reason);
        Assert.Contains("BLOCKED: Command contains bypass patterns", reason);
        Assert.Contains("Use literal paths instead.", reason);
    }

    [Fact]
    public async Task OffLimitsFileToolPath_IsDeniedWithEveryLine()
    {
        await InitProjectAsync("none");

        var result = await GuardWithStdinAsync(Hook("Edit", "{\"file_path\":\".env\"}"));

        var reason = result.AssertPreToolUseDenied();
        Assert.StartsWith("BLOCKED: Path is off-limits to all agents.", reason);
        Assert.Contains("Path: .env", reason);
        Assert.Contains("Configure exceptions in dydo/files-off-limits.md", reason);
    }

    [Fact]
    public async Task MissingSessionId_IsDenied()
    {
        await InitProjectAsync("none");

        var result = await GuardWithStdinAsync(
            "{\"hook_event_name\":\"PreToolUse\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"ls\"}}");

        Assert.Equal("BLOCKED: No session_id in hook input.", result.AssertPreToolUseDenied());
    }

    [Fact]
    public async Task PayloadWithoutEventName_IsTreatedAsPreToolUse()
    {
        await InitProjectAsync("none");

        var result = await GuardWithStdinAsync(Hook("Bash", "{\"command\":\"cd tools && echo hi\"}", eventName: null));

        result.AssertPreToolUseDenied();
    }

    [Fact]
    public async Task NonAsciiReason_IsEscapedToAsciiJson()
    {
        await InitProjectAsync("none");

        var result = await GuardWithStdinAsync(Hook("Read", "{\"file_path\":\"config/café/secrets.json\"}"));

        var reason = result.AssertPreToolUseDenied();
        Assert.Contains("config/café/secrets.json", reason);
        Assert.All(result.Stdout, c => Assert.True(c < 128, $"Non-ASCII char U+{(int)c:X4} on stdout"));
    }

    [Fact]
    public async Task Refusal_StillWritesTheHumanTextToStderr()
    {
        await InitProjectAsync("none");

        var result = await GuardWithStdinAsync(Hook("Bash", "{\"command\":\"cd tools && echo hi\"}"));

        Assert.Equal(result.AssertPreToolUseDenied(), result.Stderr.Trim());
    }

    [Fact]
    public async Task AllowedCall_ExitsZeroWithEmptyStdout()
    {
        await InitProjectAsync("none");

        var result = await GuardWithStdinAsync(Hook("Bash", "{\"command\":\"echo hi\"}"));

        result.AssertSuccess();
        Assert.Equal("", result.Stdout);
    }

    [Fact]
    public async Task OtherHookEvent_KeepsExitTwoAndEmptyStdout()
    {
        await InitProjectAsync("none");

        var result = await GuardWithStdinAsync(Hook("Bash", "{\"command\":\"cd tools && echo hi\"}", "PostToolUse"));

        result.AssertExitCode(2);
        Assert.Equal("", result.Stdout);
        result.AssertStderrContains("BLOCKED");
    }
}
