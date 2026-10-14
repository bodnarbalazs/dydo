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

        // An unknown or retired subcommand (here the 2.0-era agent claim) is outside the allowlist
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

    // A blocked dydo call nested in an allowed one's arguments, or split by a line
    // continuation, is still a dydo call: every occurrence faces the allowlist.
    [Theory]
    [InlineData("Bash", "dydo check $(dydo init)", "init")]
    [InlineData("Bash", "dydo check \"$(dydo index)\"", "index")]
    [InlineData("Bash", "dydo check `dydo init`", "init")]
    [InlineData("Bash", "dydo check <(dydo init)", "init")]
    [InlineData("Bash", "dydo \\\ninit claude", "init")]
    [InlineData("Bash", "dydo check \\\ndydo init", "init")]
    [InlineData("PowerShell", "dydo check (dydo init claude)", "init")]
    [InlineData("PowerShell", "dydo check $(dydo init claude)", "init")]
    [InlineData("PowerShell", "dydo `\ninit claude", "init")]
    [InlineData("PowerShell", "dydo `\r\ninit claude", "init")]
    public async Task Worker_NestedOrContinuedDydoCommand_Blocked(string tool, string command, string subcommand)
    {
        await InitProjectAsync();

        var input = $"{{\"command\":{System.Text.Json.JsonSerializer.Serialize(command)}}}";
        var result = await GuardWithStdinAsync(WorkerJson(tool, input));

        result.AssertExitCode(2);
        result.AssertStderrContains("Sub-agents may run only these dydo commands");
        result.AssertStderrContains($"Not allowed here: dydo {subcommand}");
    }

    // dydo is only an invocation in command position; as a directory or argument of another
    // command it is a path, and the worker may search, list and copy it.
    [Theory]
    [InlineData("Bash", "find dydo -name '*.md'")]
    [InlineData("Bash", "find ./dydo -name '*.md'")]
    [InlineData("Bash", "find dydo -type f")]
    [InlineData("Bash", "grep -rn foo dydo src")]
    [InlineData("Bash", "rg foo dydo --type md")]
    [InlineData("Bash", "tree dydo -L 2")]
    [InlineData("Bash", "ls dydo -la")]
    [InlineData("Bash", "cp -r dydo backup")]
    [InlineData("Bash", "git diff -- dydo src")]
    [InlineData("PowerShell", "Get-ChildItem dydo -Recurse")]
    [InlineData("PowerShell", "Get-ChildItem -Path dydo -Recurse -Filter *.md")]
    public async Task Worker_DydoAsPathArgument_Allowed(string tool, string command)
    {
        await InitProjectAsync();

        var input = $"{{\"command\":{System.Text.Json.JsonSerializer.Serialize(command)}}}";
        var result = await GuardWithStdinAsync(WorkerJson(tool, input));

        result.AssertSuccess();
    }

    // A wrapper that runs its arguments as a command puts dydo back in command position.
    [Theory]
    [InlineData("Bash", "env dydo init", "init")]
    [InlineData("Bash", "env FOO=1 -u BAR dydo init", "init")]
    [InlineData("Bash", "command dydo init", "init")]
    [InlineData("Bash", "exec dydo init", "init")]
    [InlineData("Bash", "nohup dydo init", "init")]
    [InlineData("Bash", "time dydo init", "init")]
    [InlineData("Bash", "timeout 5 dydo init", "init")]
    [InlineData("Bash", "nice -n 5 dydo init", "init")]
    [InlineData("Bash", "sudo -u root dydo init", "init")]
    [InlineData("Bash", "echo x | xargs -n 1 dydo index", "index")]
    [InlineData("Bash", "find . -exec dydo init \\;", "init")]
    [InlineData("Bash", "find dydo -execdir dydo index {} +", "index")]
    [InlineData("Bash", "cmd /c dydo init", "init")]
    [InlineData("Bash", "cmd.exe /C dydo init", "init")]
    [InlineData("Bash", "cmd /c \"dydo init\"", "init")]
    [InlineData("Bash", "bash -c \"dydo sync\"", "sync")]
    [InlineData("Bash", "bash -lc 'dydo check && dydo init'", "init")]
    [InlineData("Bash", "sh -c 'dydo init'", "init")]
    [InlineData("Bash", "pwsh -c \"dydo init\"", "init")]
    [InlineData("Bash", "powershell -NoProfile -Command dydo init claude", "init")]
    [InlineData("Bash", "python -c \"import os; os.system('dydo init')\"", "init")]
    [InlineData("Bash", "node -e \"require('child_process').execSync('dydo init')\"", "init")]
    [InlineData("Bash", "if true; then dydo init; fi", "init")]
    [InlineData("Bash", "if false; then :; else dydo index; fi", "index")]
    [InlineData("Bash", "for x in 1; do dydo init; done", "init")]
    [InlineData("Bash", "true && { dydo init; }", "init")]
    [InlineData("Bash", "echo `dydo init`", "init")]
    [InlineData("Bash", "x=$(dydo init)", "init")]
    [InlineData("Bash", "FOO=1 BAR=2 dydo map", "map")]
    [InlineData("Bash", "\"dydo\" init claude", "init")]
    [InlineData("Bash", "d\"\"ydo init", "init")]
    [InlineData("Bash", "C:/Program\\ Files/dydo/dydo.exe init", "init")]
    [InlineData("Bash", "./dydo.cmd init", "init")]
    [InlineData("Bash", "dydo.bat init", "init")]
    [InlineData("PowerShell", "& dydo init claude", "init")]
    [InlineData("PowerShell", "& 'C:/tools/dydo.ps1' init", "init")]
    [InlineData("PowerShell", "iex 'dydo init'", "init")]
    [InlineData("PowerShell", "Invoke-Expression \"dydo index\"", "index")]
    [InlineData("PowerShell", "Start-Process dydo -ArgumentList init", "init")]
    [InlineData("PowerShell", "Start-Process -FilePath dydo.exe -ArgumentList init", "init")]
    [InlineData("PowerShell", "$out = dydo init", "init")]
    [InlineData("PowerShell", "1 | ForEach-Object { dydo init }", "init")]
    public async Task Worker_WrappedDydoCommand_Blocked(string tool, string command, string subcommand)
    {
        await InitProjectAsync();

        var input = $"{{\"command\":{System.Text.Json.JsonSerializer.Serialize(command)}}}";
        var result = await GuardWithStdinAsync(WorkerJson(tool, input));

        result.AssertExitCode(2);
        result.AssertStderrContains("Sub-agents may run only these dydo commands");
        result.AssertStderrContains($"Not allowed here: dydo {subcommand}");
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
    [InlineData("cat dydo/index.md", null)]
    [InlineData("dydo check > dydo/out.md", null)]
    [InlineData("dydo check $(dydo fix)", null)]
    [InlineData("echo dydo sync", null)]
    [InlineData("find dydo -name x", null)]
    [InlineData("dydo \\\ncheck dydo/guides", null)]
    [InlineData("git commit -m \"dydo check passes\"", null)]
    [InlineData("grep -rn \"dydo init\" docs", "init")]
    [InlineData("dydo check $(dydo init)", "init")]
    [InlineData("dydo check \"$(dydo index)\"", "index")]
    [InlineData("dydo check `dydo init`", "init")]
    [InlineData("dydo check <(dydo init)", "init")]
    [InlineData("git status $(dydo init)", "init")]
    [InlineData("x=$(dydo init)", "init")]
    [InlineData("dydo check (dydo init claude)", "init")]
    [InlineData("dydo \\\ninit claude", "init")]
    [InlineData("dydo `\ninit claude", "init")]
    [InlineData("\"C:/Program Files/dydo/dydo.exe\" init", "init")]
    [InlineData("for x in 1; do dydo init; done", "init")]
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
