namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;

/// <summary>
/// Guard pipeline integration tests for the Tier-2 worker lane and the universal
/// layers that replaced per-role RBAC (Decision 024). Worker calls carry
/// agent_id/agent_type in the hook payload; only off-limits, dangerous-bash,
/// and nudges apply to them — no claim, no role, no staged onboarding.
/// </summary>
[Collection("Integration")]
public class GuardWorkerLaneTests : IntegrationTestBase
{
    private string WorkerJson(string toolName, string inputJson) =>
        $"{{\"session_id\":\"{TestSessionId}\",\"agent_id\":\"wkr-test-1\",\"agent_type\":\"reviewer\","
        + $"\"tool_name\":\"{toolName}\",\"tool_input\":{inputJson}}}";

    #region Worker Lane — Universal Layers Only

    [Fact]
    public async Task Worker_NoClaim_CanReadSourceFile()
    {
        await InitProjectAsync();

        // Tier-1 stage-0 would block this read; the worker lane has no staged onboarding
        var result = await GuardWithStdinAsync(WorkerJson("Read", "{\"file_path\":\"src/Foo.cs\"}"));

        result.AssertSuccess();
    }

    [Fact]
    public async Task Worker_NoClaim_CanWriteSourceFile()
    {
        await InitProjectAsync();

        // Tier-1 would block this without identity + role + must-reads
        var result = await GuardWithStdinAsync(WorkerJson("Write", "{\"file_path\":\"src/Foo.cs\"}"));

        result.AssertSuccess();
    }

    [Fact]
    public async Task Worker_NoClaim_CanBashReadSourceFile()
    {
        await InitProjectAsync();

        var result = await GuardWithStdinAsync(WorkerJson("Bash", "{\"command\":\"cat src/Foo.cs\"}"));

        result.AssertSuccess();
    }

    [Fact]
    public async Task Worker_OffLimitsPath_Blocked()
    {
        await InitProjectAsync();

        var result = await GuardWithStdinAsync(WorkerJson("Write", "{\"file_path\":\".env\"}"));

        result.AssertExitCode(2);
        result.AssertStderrContains("off-limits");
    }

    [Fact]
    public async Task Worker_BashOffLimitsPath_Blocked()
    {
        await InitProjectAsync();

        var result = await GuardWithStdinAsync(WorkerJson("Bash", "{\"command\":\"cat .env\"}"));

        result.AssertExitCode(2);
        result.AssertStderrContains("off-limits");
    }

    [Fact]
    public async Task Worker_DangerousBash_Blocked()
    {
        await InitProjectAsync();

        var result = await GuardWithStdinAsync(WorkerJson("Bash", "{\"command\":\"rm -rf /\"}"));

        result.AssertExitCode(2);
        result.AssertStderrContains("Dangerous");
    }

    [Fact]
    public async Task Worker_BlockSeverityNudge_Applies()
    {
        await InitProjectAsync();

        // H19: indirect dydo invocation is a block-severity default nudge
        var result = await GuardWithStdinAsync(WorkerJson("Bash", "{\"command\":\"npx dydo agent claim auto\"}"));

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
    }

    [Fact]
    public async Task Worker_BashWriteWithBypassAttempt_Blocked()
    {
        await InitProjectAsync();

        // Command substitution + write op: analysis is unreliable, so writes are blocked
        var result = await GuardWithStdinAsync(WorkerJson("Bash", "{\"command\":\"echo $(whoami) > out.txt\"}"));

        result.AssertExitCode(2);
        result.AssertStderrContains("bypass patterns");
    }

    [Fact]
    public async Task Worker_BashWriteToMemoryPath_Allowed()
    {
        await InitProjectAsync();

        var result = await GuardWithStdinAsync(WorkerJson("Bash",
            "{\"command\":\"tee C:/Users/test/.claude/projects/proj/memory/notes.md\"}"));

        result.AssertSuccess();
    }

    [Fact]
    public async Task HookInput_NoSessionId_Blocked()
    {
        await InitProjectAsync();

        var result = await GuardWithStdinAsync("{\"tool_name\":\"Read\",\"tool_input\":{\"file_path\":\"src/Foo.cs\"}}");

        result.AssertExitCode(2);
        result.AssertStderrContains("No session_id");
    }

    [Fact]
    public async Task Worker_AuditEvents_CarryWorkerIdentity()
    {
        await InitProjectAsync();

        var result = await GuardWithStdinAsync(WorkerJson("Bash", "{\"command\":\"echo hello\"}"));
        result.AssertSuccess();

        var auditRoot = Path.Combine(TestDir, "dydo", "_system", "audit");
        var auditContent = Directory.GetFiles(auditRoot, "*", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .FirstOrDefault(c => c.Contains("wkr-test-1"));

        Assert.NotNull(auditContent);
        Assert.Contains("\"agent_type\":\"reviewer\"", auditContent.Replace(" ", ""));
    }

    #endregion

    #region Native Memory Whitelist

    [Fact]
    public async Task Memory_NoIdentity_ReadAllowed()
    {
        await InitProjectAsync();

        var json = $"{{\"session_id\":\"{TestSessionId}\",\"tool_name\":\"Read\","
            + "\"tool_input\":{\"file_path\":\"C:/Users/test/.claude/projects/proj/memory/debugging.md\"}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertSuccess();
    }

    [Fact]
    public async Task Memory_NoIdentity_WriteAllowed()
    {
        await InitProjectAsync();

        var json = $"{{\"session_id\":\"{TestSessionId}\",\"tool_name\":\"Write\","
            + "\"tool_input\":{\"file_path\":\"C:/Users/test/.claude/projects/proj/memory/MEMORY.md\"}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertSuccess();
    }

    [Theory]
    [InlineData("C:/Users/u/.claude/projects/my-proj/memory/MEMORY.md", true)]
    [InlineData("C:\\Users\\u\\.claude\\projects\\my-proj\\memory\\topics\\api.md", true)]
    [InlineData("C:/Users/u/.claude/projects/my-proj/other/file.md", false)]
    [InlineData("dydo/agents/Adele/memory/notes.md", false)]
    [InlineData("src/Foo.cs", false)]
    public void IsNativeMemoryPath_MatchesOnlyMemoryDir(string path, bool expected)
    {
        Assert.Equal(expected, GuardCommand.IsNativeMemoryPath(path));
    }

    #endregion

    #region Tier-1 Writes After RBAC Removal

    [Fact]
    public async Task Tier1_OnboardedAgent_CanWriteOutsideOldRolePaths()
    {
        await InitProjectAsync();
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");
        await ReadMustReadsAsync();

        // Pre-024 RBAC would block a code-writer from decisions/**; now only
        // off-limits and nudges constrain an onboarded Tier-1 agent.
        var result = await GuardAsync("edit", "dydo/project/decisions/099-test.md");

        result.AssertSuccess();
    }

    [Fact]
    public async Task Tier1_UnclaimedAgent_StillBlockedFromWrites()
    {
        await InitProjectAsync();

        // Staged onboarding is unchanged for Tier-1: no identity, no writes
        var result = await GuardAsync("edit", "src/Foo.cs");

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
    }

    #endregion
}
