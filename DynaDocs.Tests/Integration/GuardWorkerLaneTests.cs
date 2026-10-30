namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;
using DynaDocs.Utils;

/// <summary>
/// Guard pipeline integration tests for the worker lane (Decision 024). Worker calls carry
/// agent_id/agent_type in the hook payload; only off-limits, dangerous-bash, and nudges apply
/// to them.
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

        var result = await GuardWithStdinAsync(WorkerJson("Read", "{\"file_path\":\"src/Foo.cs\"}"));

        result.AssertPreToolUseAllowed();
    }

    [Fact]
    public async Task Worker_NoClaim_CanWriteSourceFile()
    {
        await InitProjectAsync();

        var result = await GuardWithStdinAsync(WorkerJson("Write", "{\"file_path\":\"src/Foo.cs\"}"));

        result.AssertPreToolUseAllowed();
    }

    [Fact]
    public async Task Worker_NoClaim_CanBashReadSourceFile()
    {
        await InitProjectAsync();

        var result = await GuardWithStdinAsync(WorkerJson("Bash", "{\"command\":\"cat src/Foo.cs\"}"));

        result.AssertPreToolUseAllowed();
    }

    [Fact]
    public async Task Worker_OffLimitsPath_Blocked()
    {
        await InitProjectAsync();

        var result = await GuardWithStdinAsync(WorkerJson("Write", "{\"file_path\":\".env\"}"));

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("off-limits");
    }

    [Fact]
    public async Task Worker_BashOffLimitsPath_Blocked()
    {
        await InitProjectAsync();

        var result = await GuardWithStdinAsync(WorkerJson("Bash", "{\"command\":\"cat .env\"}"));

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("off-limits");
    }

    [Fact]
    public async Task Worker_DangerousBash_Blocked()
    {
        await InitProjectAsync();

        var result = await GuardWithStdinAsync(WorkerJson("Bash", "{\"command\":\"rm -rf /\"}"));

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("Dangerous");
    }

    [Fact]
    public async Task Worker_BlockSeverityNudge_Applies()
    {
        await InitProjectAsync();

        // H19: indirect dydo invocation is a block-severity default nudge
        var result = await GuardWithStdinAsync(WorkerJson("Bash", "{\"command\":\"npx dydo agent claim auto\"}"));

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("BLOCKED");
    }

    [Fact]
    public async Task Worker_BashWriteWithBypassAttempt_Blocked()
    {
        await InitProjectAsync();

        // Command substitution + write op: analysis is unreliable, so writes are blocked
        var result = await GuardWithStdinAsync(WorkerJson("Bash", "{\"command\":\"echo $(whoami) > out.txt\"}"));

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("bypass patterns");
    }

    [Fact]
    public async Task Worker_DydoCommand_Blocked()
    {
        await InitProjectAsync();

        // An unknown or retired subcommand (here agent claim) is outside the allowlist
        var result = await GuardWithStdinAsync(WorkerJson("Bash", "{\"command\":\"dydo agent claim auto\"}"));

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("Sub-agents may run only these dydo commands");
        result.AssertStderrContains("dydo agent");
    }

    private async Task<CommandResult> WorkerShellAsync(string tool, string command)
    {
        await InitProjectAsync();
        var input = $"{{\"command\":{System.Text.Json.JsonSerializer.Serialize(command)}}}";
        return await GuardWithStdinAsync(WorkerJson(tool, input));
    }

    // Rule 1: a command the dydo regex does not match is not a dydo call for the worker lane,
    // so directory arguments, continued lines and prose stay allowed.
    [Theory]
    [InlineData("Bash", "find dydo -name '*.md'")]
    [InlineData("Bash", "grep -rn foo dydo src")]
    [InlineData("Bash", "git diff -- dydo src")]
    [InlineData("Bash", "find \\\n  dydo -name '*.md'")]
    [InlineData("Bash", "git diff --stat \\\n  dydo src")]
    [InlineData("Bash", "rg -n \"pattern\" \\\n  dydo src tests")]
    [InlineData("PowerShell", "Get-ChildItem dydo -Recurse")]
    [InlineData("PowerShell", "Get-ChildItem `\n  dydo -Recurse")]
    [InlineData("Bash", "echo dydo sync")]
    [InlineData("Bash", "git commit -m \"fix (dydo init blocked)\"")]
    [InlineData("Bash", "rg -n \"dydo init\" Commands")]
    public async Task Worker_NotBaseDydoCommand_Allowed(string tool, string command)
    {
        var result = await WorkerShellAsync(tool, command);

        result.AssertPreToolUseAllowed();
    }

    // Rule 2 and 3: a matched command is allowed when every dydo token followed by a word
    // names a gate; a bare dydo prints help.
    [Theory]
    [InlineData("dydo check")]
    [InlineData("dydo check && dydo fix dydo/guides")]
    [InlineData("dydo check dydo/guides")]
    [InlineData("dydo --help")]
    [InlineData("dydo")]
    [InlineData("./dydo fix dydo/guides/coding-standards.md")]
    [InlineData("dydo gap-check")]
    [InlineData("dydo graph stats")]
    [InlineData("dydo validate")]
    [InlineData("dydo version")]
    [InlineData("dydo pr-ready --body pr-body.md")]
    [InlineData("dydo -h")]
    [InlineData("dydo --version")]
    [InlineData("dydo check \\\n  dydo/understand")]
    public async Task Worker_GateDydoCommand_Allowed(string command)
    {
        var result = await WorkerShellAsync("Bash", command);

        result.AssertPreToolUseAllowed();
    }

    [Theory]
    [InlineData("Bash", "dydo agent claim auto", "agent")]
    [InlineData("Bash", "dydo sync", "sync")]
    [InlineData("Bash", "dydo init claude", "init")]
    [InlineData("Bash", "dydo check && dydo sync", "sync")]
    [InlineData("Bash", "dydo check $(dydo init)", "init")]
    [InlineData("Bash", "dydo check \"$(dydo index)\"", "index")]
    [InlineData("Bash", "dydo check `dydo init`", "init")]
    [InlineData("Bash", "dydo check <(dydo init)", "init")]
    [InlineData("PowerShell", "dydo check (dydo init claude)", "init")]
    [InlineData("Bash", "dydo check && dy\\\ndo init", "init")]
    [InlineData("Bash", "dydo \\\ninit", "init")]
    [InlineData("PowerShell", "dydo `\ninit claude", "init")]
    [InlineData("PowerShell", "dydo `\r\ninit claude", "init")]
    [InlineData("Bash", "dydo check; DYDO SYNC", "SYNC")]
    [InlineData("Bash", "dydo check; ./dydo.exe init", "init")]
    [InlineData("Bash", "dydo check dydo src", "src")]
    [InlineData("PowerShell", "dydo\finit", "init")]
    [InlineData("PowerShell", "dydo\vinit", "init")]
    [InlineData("PowerShell", "dydo\u00A0init", "init")]
    [InlineData("PowerShell", "dydo\u2003init", "init")]
    [InlineData("PowerShell", "dydo\u0085init", "init")]
    [InlineData("PowerShell", "dydo check; dydo init", "init")]
    public async Task Worker_NonGateDydoCommand_Blocked(string tool, string command, string word)
    {
        var result = await WorkerShellAsync(tool, command);

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("Sub-agents may run only these dydo commands");
        result.AssertStderrContains($"Not allowed here: dydo {word}");
    }

    // Parity: outside the rule-2 relaxation the worker lane answers exactly as 3.3.0 (c31f66c7)
    // did. Expected values were recorded by running the 3.3.0 guard on a fresh `init none` project.
    [Theory]
    [InlineData("Bash", "find dydo -name '*.md'", true)]
    [InlineData("Bash", "find dydo -name '*.md' -exec grep -l foo {} \\;", true)]
    [InlineData("Bash", "find dydo -type f | xargs grep -l foo", true)]
    [InlineData("Bash", "grep -rn foo dydo src", true)]
    [InlineData("Bash", "rg -n \"foo\" dydo/ --glob '*.md'", true)]
    [InlineData("Bash", "ls dydo -la", true)]
    [InlineData("Bash", "tree dydo", true)]
    [InlineData("Bash", "cp -r dydo backup", true)]
    [InlineData("Bash", "git diff -- dydo src", true)]
    [InlineData("Bash", "git add dydo/ && git commit -m \"docs: update dydo index\"", true)]
    [InlineData("Bash", "git ls-files dydo | xargs wc -l", true)]
    [InlineData("Bash", "for f in dydo/*.md; do wc -l \"$f\"; done", true)]
    [InlineData("Bash", "(cd dydo && ls)", true)]
    [InlineData("Bash", "test -d dydo && echo yes", true)]
    [InlineData("Bash", "echo dydo sync", true)]
    [InlineData("Bash", "echo \"dydo sync is retired\"", true)]
    [InlineData("Bash", "git commit -m \"fix (dydo init blocked for workers)\"", true)]
    [InlineData("Bash", "rg -n \"dydo init\" Commands", true)]
    [InlineData("Bash", "grep -rn \"dydo check\" docs", true)]
    [InlineData("Bash", "npx prettier --check dydo", true)]
    [InlineData("Bash", "dotnet test --filter dydo", true)]
    [InlineData("Bash", "rg -n \"pattern\" \\\n  dydo src tests", true)]
    [InlineData("Bash", "git diff --stat \\\n  dydo src", true)]
    [InlineData("Bash", "find \\\n  dydo -name '*.md'", true)]
    [InlineData("Bash", "grep -rn foo \\\n  dydo", true)]
    [InlineData("Bash", "watch dydo init", true)]
    [InlineData("Bash", "echo init | xargs dydo", true)]
    [InlineData("Bash", "x=1 dydo init", true)]
    [InlineData("Bash", "npx dydo agent claim auto", false)]
    [InlineData("Bash", "dydo sync", false)]
    [InlineData("Bash", "dydo init claude", false)]
    [InlineData("Bash", "dydo check && dydo init", false)]
    [InlineData("Bash", "dydo check $(dydo init)", false)]
    [InlineData("Bash", "dydo check && dy\\\ndo init", false)]
    [InlineData("Bash", "dydo check; echo dydo sync", false)]
    [InlineData("Bash", "dydo check; DYDO SYNC", false)]
    [InlineData("PowerShell", "Get-ChildItem dydo -Recurse", true)]
    [InlineData("PowerShell", "Get-ChildItem `\n  dydo -Recurse", true)]
    [InlineData("PowerShell", "Get-ChildItem -Path dydo, src -Recurse", true)]
    [InlineData("PowerShell", "Select-String -Path dydo/*.md -Pattern foo", true)]
    [InlineData("PowerShell", "dydo check (dydo init)", false)]
    [InlineData("PowerShell", "$out = dydo init", false)]
    [InlineData("PowerShell", "@{ dydo = 1 }", true)]
    public async Task Worker_NonGateCommand_MatchesBase330(string tool, string command, bool baseAllowed)
    {
        var result = await WorkerShellAsync(tool, command);

        Assert.Equal(baseAllowed, result.IsPreToolUseAllowed);
    }

    [Fact]
    public async Task TopLevelSession_DydoSync_NotBlockedByWorkerRule()
    {
        await InitProjectAsync();

        var json = $"{{\"session_id\":\"{TestSessionId}\",\"tool_name\":\"Bash\","
            + "\"tool_input\":{\"command\":\"dydo check && dydo sync\"}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertPreToolUseAllowed();
    }

    [Theory]
    [InlineData("find dydo -name x", null)]
    [InlineData("echo dydo sync", null)]
    [InlineData("watch dydo init", null)]
    [InlineData("dydo", null)]
    [InlineData("dydo check; dydo", null)]
    [InlineData("dydo check $(dydo fix)", null)]
    [InlineData("dydo check > dydo/out.md", null)]
    [InlineData("C:/tools/dydo.exe check", null)]
    [InlineData("dydo check dydo.json", null)]
    [InlineData("dydo check dydo-notes", null)]
    [InlineData("dydo sync", "sync")]
    [InlineData("dydo check dydo src", "src")]
    [InlineData("dydo check; d\"\"ydo init", "init")]
    [InlineData("dydo check; d\\ydo init", "init")]
    [InlineData("dydo check && {dydo,init}", "init")]
    [InlineData("dydo check; C:\\tools\\dydo.cmd init", "init")]
    [InlineData("dydo check; dydo \"init\"", "init")]
    [InlineData("dydo check; dydo >/dev/null init", ">/dev/null")]
    [InlineData("dydo --verbose", "--verbose")]
    [InlineData("dydo\finit", "init")]
    [InlineData("dydo\vinit", "init")]
    [InlineData("dydo\u00A0init", "init")]
    [InlineData("dydo\u2003init", "init")]
    [InlineData("dydo\u0085init", "init")]
    [InlineData("dydo check; dydo init", "init")]
    public void FindWorkerBlockedDydoCommand_ChecksEveryTokenOfAMatchedLine(string command, string? expected)
    {
        Assert.Equal(expected, GuardCommand.FindWorkerBlockedDydoCommand(command));
    }

    [Fact]
    public async Task HookInput_NoSessionId_Blocked()
    {
        await InitProjectAsync();

        var result = await GuardWithStdinAsync("{\"tool_name\":\"Read\",\"tool_input\":{\"file_path\":\"src/Foo.cs\"}}");

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("No session_id");
    }

    [Fact]
    public async Task Worker_SystemPath_Blocked()
    {
        await InitProjectAsync();

        // dydo/_system/** and dydo.json are agent-untouchable system off-limits.
        // Use ABSOLUTE paths - that's what Claude Code's hook actually delivers.
        var systemAbs = Path.Combine(TestDir, "dydo", "_system", "roles", "x.json").Replace('\\', '/');
        var systemWrite = await GuardWithStdinAsync(WorkerJson("Write", $"{{\"file_path\":\"{systemAbs}\"}}"));
        systemWrite.AssertPreToolUseDenied();

        var configAbs = Path.Combine(TestDir, "dydo.json").Replace('\\', '/');
        var config = await GuardWithStdinAsync(WorkerJson("Write", $"{{\"file_path\":\"{configAbs}\"}}"));
        config.AssertPreToolUseDenied();
    }

    #endregion

    #region Host Memory (DR 055)

    private static string MemoryPath(params string[] tail)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).Replace('\\', '/');
        return $"{home}/.claude/projects/proj/memory/{string.Join('/', tail)}";
    }

    private string MainJson(string toolName, string inputJson) =>
        $"{{\"session_id\":\"{TestSessionId}\",\"tool_name\":\"{toolName}\",\"tool_input\":{inputJson}}}";

    [Fact]
    public async Task Memory_NoIdentity_ReadAllowed()
    {
        await InitProjectAsync();

        var result = await GuardWithStdinAsync(MainJson("Read", $"{{\"file_path\":\"{MemoryPath("debugging.md")}\"}}"));

        result.AssertPreToolUseAllowed();
    }

    [Theory]
    [InlineData("Write")]
    [InlineData("Edit")]
    public async Task Memory_NoIdentity_WriteBlocked_NamesPathAndDecision(string tool)
    {
        await InitProjectAsync();

        var result = await GuardWithStdinAsync(MainJson(tool, $"{{\"file_path\":\"{MemoryPath("MEMORY.md")}\"}}"));

        var reason = result.AssertPreToolUseDenied();
        result.AssertStderrContains(MemoryPath("MEMORY.md"));
        result.AssertStderrContains("DR 055");
        result.AssertStderrContains("skill, a Decision Record or a doc");

        // A fresh project has no DR 055 of its own: the refusal cites the upstream record at the
        // running CLI's release tag, never a repo-local decisions path.
        var upstream = $"https://github.com/bodnarbalazs/dydo/blob/v{DydoVersion.Current}" +
            "/dydo/project/decisions/055-native-agent-memory-off-and-guarded.md";
        Assert.False(File.Exists(Path.Combine(TestDir, "dydo", "project", "decisions",
            "055-native-agent-memory-off-and-guarded.md")));
        foreach (var text in new[] { result.Stderr, reason })
        {
            Assert.Contains(upstream, text);
            Assert.DoesNotContain("dydo/project/decisions/", text.Replace(upstream, ""));
        }
    }

    [Fact]
    public async Task Memory_TraversalIntoMemory_WriteBlocked()
    {
        await InitProjectAsync();

        var sneaky = MemoryPath("..", "..", "other", "..", "proj", "memory", "MEMORY.md");
        var result = await GuardWithStdinAsync(MainJson("Write", $"{{\"file_path\":\"{sneaky}\"}}"));

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("DR 055");
    }

    [Fact]
    public async Task Memory_ApplyPatchIntoCodexMemories_Blocked()
    {
        await InitProjectAsync();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).Replace('\\', '/');
        var codexHome = Environment.GetEnvironmentVariable("CODEX_HOME")?.Replace('\\', '/') ?? $"{home}/.codex";

        var patch = $"*** Begin Patch\\n*** Add File: {codexHome}/memories/note.md\\n+x\\n*** End Patch";
        var result = await GuardWithStdinAsync(MainJson("apply_patch", $"{{\"command\":\"{patch}\"}}"));

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("DR 055");
    }

    [Fact]
    public async Task Memory_ClaudeConfigDirProfile_WriteBlocked()
    {
        await InitProjectAsync();
        var profile = Path.Combine(TestDir, "profile").Replace('\\', '/');
        var original = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
        Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", profile);
        try
        {
            var result = await GuardWithStdinAsync(
                MainJson("Write", $"{{\"file_path\":\"{profile}/projects/p/memory/MEMORY.md\"}}"));

            result.AssertPreToolUseDenied();
            result.AssertStderrContains("DR 055");
        }
        finally
        {
            Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", original);
        }
    }

    [Fact]
    public async Task Worker_MemoryWrite_Blocked_ReadAllowed()
    {
        await InitProjectAsync();

        var write = await GuardWithStdinAsync(WorkerJson("Write", $"{{\"file_path\":\"{MemoryPath("MEMORY.md")}\"}}"));
        write.AssertPreToolUseDenied();
        write.AssertStderrContains("DR 055");

        var read = await GuardWithStdinAsync(WorkerJson("Read", $"{{\"file_path\":\"{MemoryPath("MEMORY.md")}\"}}"));
        read.AssertPreToolUseAllowed();
    }

    [Theory]
    [InlineData("echo note > ~/.claude/projects/proj/memory/MEMORY.md")]
    [InlineData("echo note | tee ~/.claude/projects/proj/memory/MEMORY.md")]
    [InlineData("cp notes.md ~/.claude/projects/proj/memory/notes.md")]
    [InlineData("mv notes.md ~/.claude/agent-memory/reviewer/notes.md")]
    [InlineData("rm .claude/agent-memory/reviewer/MEMORY.md")]
    [InlineData("rm -r ~/.codex/memories")]
    [InlineData("cp notes.md ~/.claude/projects/proj/memory/")]
    [InlineData("cp -t ~/.claude/projects/proj/memory notes.md")]
    [InlineData("cp ~/.claude/projects/proj/memory/a.md ~/.claude/projects/proj/memory/b.md")]
    [InlineData("mv ~/.claude/projects/proj/memory/x.md notes.md")]
    public async Task Memory_ShellWrite_Blocked(string command)
    {
        await InitProjectAsync();

        if (command == "rm -r ~/.codex/memories")
        {
            var codexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
            if (string.IsNullOrWhiteSpace(codexHome))
                codexHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
            var memoryPath = Path.GetFullPath(Path.Combine(codexHome, "memories")).Replace('\\', '/');
            command = $"rm -r '{memoryPath.Replace("'", "'\\''")}'";
        }
        var input = System.Text.Json.JsonSerializer.Serialize(new { command });

        var main = await GuardWithStdinAsync(MainJson("Bash", input));
        main.AssertPreToolUseDenied();
        main.AssertStderrContains("DR 055");

        var worker = await GuardWithStdinAsync(WorkerJson("Bash", input));
        worker.AssertPreToolUseDenied();
        worker.AssertStderrContains("DR 055");
    }

    [Theory]
    [InlineData("codex home")]
    [InlineData(null)]
    [InlineData(" \t ")]
    public async Task Memory_CodexShellWrite_EffectiveHome_Blocked(string? codexHome)
    {
        var original = Environment.GetEnvironmentVariable("CODEX_HOME");
        try
        {
            Environment.SetEnvironmentVariable("CODEX_HOME", string.IsNullOrWhiteSpace(codexHome)
                ? codexHome : Path.Combine(TestDir, codexHome));
            await Memory_ShellWrite_Blocked("rm -r ~/.codex/memories");
        }
        finally
        {
            Environment.SetEnvironmentVariable("CODEX_HOME", original);
        }
    }

    [Theory]
    [InlineData("cat ~/.claude/projects/proj/memory/MEMORY.md")]
    [InlineData("ls ~/.claude/projects/proj/memory")]
    [InlineData("grep -r lesson ~/.codex/memories")]
    [InlineData("echo note > notes/memory/MEMORY.md")]
    public async Task Memory_ShellReadOrOrdinaryWrite_Allowed(string command)
    {
        await InitProjectAsync();

        var result = await GuardWithStdinAsync(MainJson("Bash", $"{{\"command\":\"{command}\"}}"));

        result.AssertPreToolUseAllowed();
    }

    [Theory]
    [InlineData("cp ~/.claude/projects/proj/memory/MEMORY.md notes.md")]
    [InlineData("cp -r ~/.claude/projects/proj/memory backup")]
    [InlineData("cp -t backup ~/.claude/projects/proj/memory/MEMORY.md")]
    public async Task Memory_ShellCopyOut_Allowed(string command)
    {
        await InitProjectAsync();

        var main = await GuardWithStdinAsync(MainJson("Bash", $"{{\"command\":\"{command}\"}}"));
        main.AssertPreToolUseAllowed();

        var worker = await GuardWithStdinAsync(WorkerJson("Bash", $"{{\"command\":\"{command}\"}}"));
        worker.AssertPreToolUseAllowed();
    }

    #endregion

    #region Tier-1 Writes (identity-free)

    [Fact]
    public async Task Tier1_Agent_CanWriteOutsideOldRolePaths()
    {
        await InitProjectAsync();

        var result = await GuardAsync("edit", "dydo/project/decisions/099-test.md");

        result.AssertSuccess();
    }

    #endregion
}
