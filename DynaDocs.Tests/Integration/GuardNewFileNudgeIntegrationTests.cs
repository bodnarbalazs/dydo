namespace DynaDocs.Tests.Integration;

using System.Text.Json;
using DynaDocs.Models;
using DynaDocs.Services;

/// <summary>
/// New-file nudges through the real hook entry point (stdin payloads), main and worker lanes,
/// including Codex's real apply_patch shape: the whole patch in tool_input.command, no file_path.
/// </summary>
[Collection("Integration")]
public class GuardNewFileNudgeIntegrationTests : IntegrationTestBase
{
    private const string ToolsPattern = "^tools/(?!one-off/)";
    private const string ToolsMessage = "New tools go under tools/one-off/.";

    private async Task InitWithNudges(params NudgeConfig[] nudges)
    {
        await InitProjectAsync("none");
        var configService = new ConfigService();
        var configPath = configService.FindConfigFile(TestDir)!;
        var config = configService.LoadConfig(TestDir)!;
        config.Nudges.AddRange(nudges);
        configService.SaveConfig(config, configPath);
    }

    private static NudgeConfig ToolsNudge(string severity) =>
        new() { Pattern = ToolsPattern, Message = ToolsMessage, Severity = severity, Match = "new-file" };

    private string Payload(string toolName, object toolInput, string? agentId = null) =>
        JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["session_id"] = TestSessionId,
            ["agent_id"] = agentId,
            ["tool_name"] = toolName,
            ["tool_input"] = toolInput,
            ["cwd"] = TestDir,
        });

    private string ApplyPatch(params string[] body) =>
        Payload("apply_patch", new { command = string.Join("\n", ["*** Begin Patch", .. body, "*** End Patch"]) });

    [Fact]
    public async Task CodexApplyPatch_AddFileUnderTools_Blocked()
    {
        await InitWithNudges(ToolsNudge("block"));

        var result = await GuardWithStdinAsync(ApplyPatch("*** Add File: tools/x.py", "+print(1)"));

        result.AssertExitCode(2);
        result.AssertStderrContains(ToolsMessage);
    }

    [Fact]
    public async Task CodexApplyPatch_AddFileUnderOneOff_Allowed()
    {
        await InitWithNudges(ToolsNudge("block"));

        var result = await GuardWithStdinAsync(ApplyPatch("*** Add File: tools/one-off/x.py", "+print(1)"));

        result.AssertSuccess();
    }

    [Fact]
    public async Task CodexApplyPatch_UpdateExistingTool_Allowed()
    {
        await InitWithNudges(ToolsNudge("block"));
        WriteFile("tools/existing.py", "print(0)");

        var result = await GuardWithStdinAsync(ApplyPatch("*** Update File: tools/existing.py", "@@", "-print(0)", "+print(1)"));

        result.AssertSuccess();
    }

    [Fact]
    public async Task CodexApplyPatch_MoveToNewToolPath_Blocked()
    {
        await InitWithNudges(ToolsNudge("block"));
        WriteFile("scripts/a.py", "print(0)");

        var result = await GuardWithStdinAsync(ApplyPatch("*** Update File: scripts/a.py", "*** Move to: tools/a.py"));

        result.AssertExitCode(2);
    }

    [Fact]
    public async Task ClaudeWrite_NewTool_WarnBlocksOnceThenPassesOnRetry()
    {
        await InitWithNudges(ToolsNudge("warn"));
        var payload = Payload("Write", new { file_path = Path.Combine(TestDir, "tools", "new.py"), content = "x" });

        var first = await GuardWithStdinAsync(payload);
        var retry = await GuardWithStdinAsync(payload);

        first.AssertExitCode(2);
        first.AssertStderrContains(ToolsMessage);
        retry.AssertSuccess();
    }

    [Fact]
    public async Task ClaudeEdit_ExistingTool_Allowed()
    {
        await InitWithNudges(ToolsNudge("block"));
        WriteFile("tools/existing.py", "print(0)");

        var result = await GuardWithStdinAsync(Payload("Edit", new { file_path = "tools/existing.py", old_string = "0", new_string = "1" }));

        result.AssertSuccess();
    }

    [Fact]
    public async Task Worker_ClaudeWrite_NewTool_Blocked()
    {
        await InitWithNudges(ToolsNudge("block"));

        var result = await GuardWithStdinAsync(Payload("Write", new { file_path = "tools/new.py", content = "x" }, agentId: "wkr-1"));

        result.AssertExitCode(2);
        result.AssertStderrContains(ToolsMessage);
    }

    [Fact]
    public async Task Worker_CodexApplyPatch_AddFileUnderTools_Blocked()
    {
        await InitWithNudges(ToolsNudge("block"));

        var payload = Payload("apply_patch", new { command = "*** Begin Patch\n*** Add File: tools/x.py\n+x\n*** End Patch" }, agentId: "wkr-1");
        var result = await GuardWithStdinAsync(payload);

        result.AssertExitCode(2);
    }

    private const string AnyPathMessage = "Nudge fired for any new file.";

    private static NudgeConfig AnyNewFileBlock() =>
        new() { Pattern = ".", Message = AnyPathMessage, Severity = "block", Match = "new-file" };

    [Theory]
    [InlineData(null, ".env", "off-limits")]
    [InlineData("wkr-1", ".env", "off-limits")]
    [InlineData(null, "dydo/index.md", "protected")]
    [InlineData("wkr-1", "dydo/index.md", "protected")]
    public async Task NewFileNudge_RunsAfterOffLimitsAndProtected_BlockTextWins(string? agentId, string path, string tier)
    {
        await InitWithNudges(AnyNewFileBlock());
        var existing = Path.Combine(TestDir, path);
        if (File.Exists(existing)) File.Delete(existing);

        var result = await GuardWithStdinAsync(Payload("Write", new { file_path = path, content = "x" }, agentId));

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED:");
        result.AssertStderrContains(tier);
        Assert.DoesNotContain(AnyPathMessage, result.Stderr);
    }

    [Fact]
    public async Task NewFileNudge_WithoutPayloadCwd_FallsBackToProcessDirectory()
    {
        await InitWithNudges(ToolsNudge("block"));
        var payload = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["session_id"] = TestSessionId,
            ["tool_name"] = "Write",
            ["tool_input"] = new { file_path = "tools/new.py", content = "x" },
        });

        var result = await GuardWithStdinAsync(payload);

        result.AssertExitCode(2);
        result.AssertStderrContains(ToolsMessage);
    }

    [Fact]
    public async Task ShellCreatedFile_IsCommandLaneOnly()
    {
        await InitWithNudges(ToolsNudge("block"));

        var result = await GuardWithStdinAsync(Payload("Bash", new { command = "touch tools/x.py" }));

        result.AssertSuccess();
    }

    [Fact]
    public async Task NewFileEntryWithH19Pattern_LeavesH19BlockingBash()
    {
        var h19 = ConfigFactory.DefaultNudges.First(n => n.Severity == "block" && n.Pattern.Contains("npx"));
        await InitProjectAsync("none");
        var configService = new ConfigService();
        var config = configService.LoadConfig(TestDir)!;
        config.Nudges = [new NudgeConfig { Pattern = h19.Pattern, Message = "file lane", Severity = "notice", Match = "new-file" }];
        configService.SaveConfig(config, configService.FindConfigFile(TestDir)!);

        var result = await GuardWithStdinAsync(Payload("Bash", new { command = "npx dydo agent claim auto" }));

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
        Assert.DoesNotContain("file lane", result.Stderr);
        Assert.Contains("\"match\": \"new-file\"", File.ReadAllText(configService.FindConfigFile(TestDir)!));
    }
}
