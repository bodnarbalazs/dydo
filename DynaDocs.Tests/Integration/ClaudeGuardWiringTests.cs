namespace DynaDocs.Tests.Integration;

using System.Text.Json.Nodes;
using DynaDocs.Commands;

/// <summary>
/// dydo validate reports a project that records the Claude integration but whose committed
/// .claude/settings.json no longer runs the guard (DYD-501); init's own wiring never trips it.
/// </summary>
[Collection("Integration")]
public sealed class ClaudeGuardWiringTests : IntegrationTestBase
{
    private const string SettingsPath = ".claude/settings.json";
    private const string Unguarded = "Claude agents run without the dydo guard";

    private Task<CommandResult> ValidateAsync() => RunAsync(ValidateCommand.Create());

    private static void AssertPasses(CommandResult result)
    {
        result.AssertSuccess();
        Assert.Contains("Validation passed. No issues found.", result.Stdout);
        Assert.DoesNotContain(SettingsPath, result.Stdout + result.Stderr);
    }

    private void RemoveGuardHooks()
    {
        var settings = JsonNode.Parse(ReadFile(SettingsPath))!.AsObject();
        Assert.True(settings.Remove("hooks"));
        WriteFile(SettingsPath, settings.ToJsonString());
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("all")]
    public async Task FreshInitWithClaude_ValidatesWithoutClaudeWarning(string integration)
    {
        (await InitProjectAsync(integration)).AssertSuccess();

        AssertPasses(await ValidateAsync());
    }

    [Theory]
    [InlineData("none")]
    [InlineData("codex")]
    public async Task FreshInitWithoutClaude_ValidatesWithoutClaudeSettings(string integration)
    {
        (await InitProjectAsync(integration)).AssertSuccess();

        Assert.False(Directory.Exists(Path.Combine(TestDir, ".claude")));
        AssertPasses(await ValidateAsync());
    }

    [Fact]
    public async Task RejoiningClaude_ClearsTheReportedMissingGuard()
    {
        (await InitProjectAsync("claude")).AssertSuccess();
        RemoveGuardHooks();

        var reported = await ValidateAsync();
        reported.AssertSuccess();
        Assert.Contains($"{SettingsPath}: {Unguarded}", reported.Stderr);
        Assert.Contains("Run 'dydo init claude --join'.", reported.Stderr);

        (await JoinProjectAsync("claude")).AssertSuccess();

        AssertPasses(await ValidateAsync());
    }

    // --join refuses a file it cannot parse, or whose hooks it cannot read, rather than repair it,
    // which is why the warning says to fix the JSON first.
    [Theory]
    [InlineData("{\"hooks\":", "the file is not valid JSON")]
    [InlineData("""{"\uD800":0}""", "the file is not valid JSON")]
    [InlineData("""{"hooks":{"PreToolUse":[{"hooks":[{"command":"\uD800"}]}]}}""", "the file is not valid JSON")]
    [InlineData("""{"hooks":[]}""", "its 'hooks' is not a JSON object")]
    [InlineData("""{"hooks":{"PreToolUse":{}}}""", "its 'hooks.PreToolUse' is not a JSON array")]
    public async Task MalformedSettings_WarnWithoutThrowing_AndJoinRefusesThem(string settings, string reason)
    {
        (await InitProjectAsync("claude")).AssertSuccess();
        WriteFile(SettingsPath, settings);

        var reported = await ValidateAsync();
        reported.AssertSuccess();
        Assert.Contains(
            $"{SettingsPath}: {Unguarded}: {reason}. Fix the JSON, then run 'dydo init claude --join'.",
            reported.Stderr);

        Assert.False((await JoinProjectAsync("claude")).IsSuccess);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{\"hooks\":")]
    [InlineData("[]")]
    [InlineData("""{"\uD800":0}""")]
    public async Task GuardDailyValidation_ShowsTheWarning_AndStillAllowsTheCall(string? settings)
    {
        (await InitProjectAsync("claude")).AssertSuccess();
        if (settings == null)
            File.Delete(Path.Combine(TestDir, SettingsPath));
        else
            WriteFile(SettingsPath, settings);
        var hook = new JsonObject
        {
            ["session_id"] = TestSessionId,
            ["hook_event_name"] = "PreToolUse",
            ["tool_name"] = "Read",
            ["tool_input"] = new JsonObject { ["file_path"] = "src/app.cs" }
        }.ToJsonString();

        var result = await GuardWithStdinAsync(hook, "--host", "claude");

        result.AssertPreToolUseAllowed();
        Assert.Contains("Daily validation found issues:", result.Stderr);
        Assert.Contains($"[warning] {SettingsPath}: {Unguarded}", result.Stderr);
    }
}
