namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;

/// <summary>
/// Guard pipeline integration tests for the Tier-2 worker lane and the universal
/// layers that replaced per-role RBAC (Decision 024). Worker calls carry
/// agent_id/agent_type in the hook payload; only off-limits, dangerous-bash,
/// and nudges apply to them - no claim, no role, no staged onboarding.
/// </summary>
[Collection("Integration")]
public class GuardWorkerLaneTests : IntegrationTestBase
{
    private string WorkerJson(string toolName, string inputJson) =>
        $"{{\"session_id\":\"{TestSessionId}\",\"agent_id\":\"wkr-test-1\",\"agent_type\":\"reviewer\","
        + $"\"tool_name\":\"{toolName}\",\"tool_input\":{inputJson}}}";

    #region Worker Lane - Universal Layers Only

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
    public async Task Worker_DydoCommand_Blocked()
    {
        await InitProjectAsync();

        // A worker's dydo command would otherwise mutate the parent's session state
        var result = await GuardWithStdinAsync(WorkerJson("Bash", "{\"command\":\"dydo agent claim auto\"}"));

        result.AssertExitCode(2);
        result.AssertStderrContains("Sub-agents may run only these dydo commands");
        result.AssertStderrContains("dydo agent");
    }

    // Issue Captains and their crew are native sub-agents in dydo 3; the documentation and
    // coverage gates are theirs to run, so the worker lane admits read-only and gate commands.
    [Theory]
    [InlineData("dydo check")]
    [InlineData("dydo check dydo/guides")]
    [InlineData("dydo fix dydo/guides/coding-standards.md")]
    [InlineData("./dydo check")]
    [InlineData("dydo gap-check")]
    [InlineData("dydo graph stats")]
    [InlineData("dydo validate")]
    [InlineData("dydo help")]
    [InlineData("dydo version")]
    [InlineData("dydo --version")]
    [InlineData("dydo --help")]
    [InlineData("dydo check && dydo fix")]
    [InlineData("git status && dydo check")]
    public async Task Worker_GateDydoCommand_Allowed(string command)
    {
        await InitProjectAsync();

        var result = await GuardWithStdinAsync(WorkerJson("Bash", $"{{\"command\":\"{command}\"}}"));

        result.AssertSuccess();
    }

    [Theory]
    [InlineData("dydo sync", "sync")]
    [InlineData("dydo init claude", "init")]
    [InlineData("dydo index", "index")]
    [InlineData("dydo guard", "guard")]
    [InlineData("dydo check && dydo sync", "sync")]
    [InlineData("dydo check; ./dydo init claude", "init")]
    [InlineData("dydo check || dydo index", "index")]
    [InlineData("dydo check | dydo sync", "sync")]
    [InlineData("dydo --help sync", "sync")]
    public async Task Worker_OrchestrationDydoCommand_Blocked(string command, string subcommand)
    {
        await InitProjectAsync();

        var result = await GuardWithStdinAsync(WorkerJson("Bash", $"{{\"command\":\"{command}\"}}"));

        result.AssertExitCode(2);
        result.AssertStderrContains("Sub-agents may run only these dydo commands");
        result.AssertStderrContains($"dydo {subcommand}");
    }

    [Fact]
    public async Task TopLevelSession_DydoSync_NotBlockedByWorkerRule()
    {
        await InitProjectAsync();

        var json = $"{{\"session_id\":\"{TestSessionId}\",\"tool_name\":\"Bash\","
            + "\"tool_input\":{\"command\":\"dydo check && dydo sync\"}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertSuccess();
    }

    [Theory]
    [InlineData("dydo check", null)]
    [InlineData("./dydo fix docs", null)]
    [InlineData("C:/tools/dydo.exe check", null)]
    [InlineData("dydo", null)]
    [InlineData("dydo -h", null)]
    [InlineData("echo dydo sync", null)]
    [InlineData("cat dydo/index.md", null)]
    [InlineData("dydo sync", "sync")]
    [InlineData("dydo check\ndydo init", "init")]
    [InlineData("dydo check && (dydo sync)", "sync")]
    [InlineData("DYDO_X=1 dydo map", "map")]
    [InlineData("dydo --verbose", "--verbose")]
    public void FindWorkerBlockedDydoCommand_ParsesEverySegment(string command, string? expected)
    {
        Assert.Equal(expected, GuardCommand.FindWorkerBlockedDydoCommand(command));
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
    public async Task Worker_SystemPath_Blocked()
    {
        await InitProjectAsync();

        // dydo/_system/** and dydo.json are agent-untouchable system off-limits.
        // Use ABSOLUTE paths - that's what Claude Code's hook actually delivers.
        var systemAbs = Path.Combine(TestDir, "dydo", "_system", "audit", "2026", "x.json").Replace('\\', '/');
        var audit = await GuardWithStdinAsync(WorkerJson("Write", $"{{\"file_path\":\"{systemAbs}\"}}"));
        audit.AssertExitCode(2);

        var configAbs = Path.Combine(TestDir, "dydo.json").Replace('\\', '/');
        var config = await GuardWithStdinAsync(WorkerJson("Write", $"{{\"file_path\":\"{configAbs}\"}}"));
        config.AssertExitCode(2);
    }

    #endregion

    #region Native Memory Whitelist

    private static string MemoryPath(params string[] tail)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).Replace('\\', '/');
        return $"{home}/.claude/projects/proj/memory/{string.Join('/', tail)}";
    }

    [Fact]
    public async Task Memory_NoIdentity_ReadAllowed()
    {
        await InitProjectAsync();

        var json = $"{{\"session_id\":\"{TestSessionId}\",\"tool_name\":\"Read\","
            + $"\"tool_input\":{{\"file_path\":\"{MemoryPath("debugging.md")}\"}}}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertSuccess();
    }

    [Fact]
    public async Task Memory_NoIdentity_WriteAllowed()
    {
        await InitProjectAsync();

        var json = $"{{\"session_id\":\"{TestSessionId}\",\"tool_name\":\"Write\","
            + $"\"tool_input\":{{\"file_path\":\"{MemoryPath("MEMORY.md")}\"}}}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertSuccess();
    }

    [Fact]
    public void IsNativeMemoryPath_RealMemoryDir_Matches()
    {
        Assert.True(GuardCommand.IsNativeMemoryPath(MemoryPath("MEMORY.md")));
        Assert.True(GuardCommand.IsNativeMemoryPath(MemoryPath("topics", "api.md")));
    }

    [Fact]
    public void IsNativeMemoryPath_Traversal_DoesNotMatch()
    {
        // '../' escape out of the memory dir must not be treated as native memory
        Assert.False(GuardCommand.IsNativeMemoryPath(MemoryPath("..", "..", "..", "secret.md")));
    }

    [Theory]
    [InlineData("C:/Users/u/.claude/projects/my-proj/memory/MEMORY.md")]  // wrong home root
    [InlineData("dydo/.claude/projects/x/memory/y.md")]                   // repo-internal lookalike
    [InlineData("dydo/scratch/memory/notes.md")]
    [InlineData("src/Foo.cs")]
    public void IsNativeMemoryPath_NonMemoryPaths_DoNotMatch(string path)
    {
        Assert.False(GuardCommand.IsNativeMemoryPath(path));
    }

    #endregion

    #region Tier-1 Writes (identity-free)

    [Fact]
    public async Task Tier1_Agent_CanWriteOutsideOldRolePaths()
    {
        await InitProjectAsync();

        // Only off-limits and nudges constrain writes now - no RBAC, no identity gate.
        var result = await GuardAsync("edit", "dydo/project/decisions/099-test.md");

        result.AssertSuccess();
    }

    #endregion
}
