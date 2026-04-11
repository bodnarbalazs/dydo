namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;
using DynaDocs.Services;

/// <summary>
/// Tests for security findings from the guard system inquisition.
/// Each section maps to a specific finding number.
/// </summary>
[Collection("Integration")]
public class GuardSecurityTests : IntegrationTestBase
{
    private async Task SetupClaimedAgent(string agentName = "Adele", string role = "code-writer", string task = "test-task")
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync(agentName);
        await SetRoleAsync(role, task);
        await ReadMustReadsAsync();
    }

    private string BashJson(string command) =>
        "{\"session_id\":\"" + TestSessionId + "\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"" +
        command.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"}}";

    // ================================================================
    // Finding 1: Guard lift self-escalation — .guard-lift.json off-limits
    // ================================================================

    [Fact]
    public async Task Finding1_AgentCannotWriteGuardLiftMarker_DirectWrite()
    {
        await SetupClaimedAgent();

        var result = await GuardAsync("write", "dydo/agents/Adele/.guard-lift.json");
        result.AssertExitCode(2);
        result.AssertStderrContains("off-limits");
    }

    [Fact]
    public async Task Finding1_AgentCannotWriteGuardLiftMarker_BashRedirect()
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(
            BashJson("echo test > dydo/agents/Adele/.guard-lift.json"));
        result.AssertExitCode(2);
        result.AssertStderrContains("off-limits");
    }

    [Fact]
    public async Task Finding1_LiftedViaService_StillWorks()
    {
        await SetupClaimedAgent();

        var service = new GuardLiftService(TestDir);
        service.Lift("Adele", "testuser", null);
        Assert.True(service.IsLifted("Adele"));

        var result = await GuardAsync("write", "dydo/some-file.md");
        result.AssertSuccess();
    }

    // ================================================================
    // Finding 2: Interpreter inline execution blocked as dangerous
    // ================================================================

    [Theory]
    [InlineData("python -c \"open('secrets.json').read()\"")]
    [InlineData("python3 -c \"import os; os.remove('file')\"")]
    [InlineData("node -e \"require('fs').writeFileSync('x','y')\"")]
    [InlineData("ruby -e \"File.read('secrets.json')\"")]
    [InlineData("perl -e \"open(F,'<secrets.json')\"")]
    [InlineData("php -r \"file_get_contents('secrets.json')\"")]
    public void Finding2_InlineInterpreterExecution_IsDangerous(string command)
    {
        var analyzer = new BashCommandAnalyzer();
        var (isDangerous, reason) = analyzer.CheckDangerousPatterns(command);
        Assert.True(isDangerous, $"Expected dangerous: {command}");
        Assert.Contains("interpreter", reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("python script.py")]
    [InlineData("python3 my_script.py")]
    [InlineData("node app.js")]
    [InlineData("ruby script.rb")]
    [InlineData("perl script.pl")]
    [InlineData("php script.php")]
    [InlineData("bash script.sh")]
    [InlineData("sh script.sh")]
    [InlineData("zsh script.sh")]
    public void Finding2_InterpreterWithScriptFile_NotDangerous(string command)
    {
        var analyzer = new BashCommandAnalyzer();
        var (isDangerous, _) = analyzer.CheckDangerousPatterns(command);
        Assert.False(isDangerous, $"Should not be dangerous: {command}");
    }

    [Theory]
    [InlineData("python --version")]
    [InlineData("node --version")]
    [InlineData("ruby --version")]
    public void Finding2_InterpreterVersionCheck_NotDangerous(string command)
    {
        var analyzer = new BashCommandAnalyzer();
        var (isDangerous, _) = analyzer.CheckDangerousPatterns(command);
        Assert.False(isDangerous, $"Should not be dangerous: {command}");
    }

    // ================================================================
    // Finding 3: Command substitution — tainted writes blocked
    // ================================================================

    [Fact]
    public void Finding3_HasBypassAttempt_SetOnCommandSubstitution()
    {
        var analyzer = new BashCommandAnalyzer();
        var result = analyzer.Analyze("cat $(echo secret.txt)");
        Assert.True(result.HasBypassAttempt);
    }

    [Fact]
    public void Finding3_HasBypassAttempt_SetOnVariableExpansion()
    {
        var analyzer = new BashCommandAnalyzer();
        var result = analyzer.Analyze("cat $FILENAME");
        Assert.True(result.HasBypassAttempt);
    }

    [Fact]
    public void Finding3_HasBypassAttempt_NotSetOnSimpleCommand()
    {
        var analyzer = new BashCommandAnalyzer();
        var result = analyzer.Analyze("cat file.txt");
        Assert.False(result.HasBypassAttempt);
    }

    [Fact]
    public async Task Finding3_TaintedWriteOperation_Blocked()
    {
        await SetupClaimedAgent();

        // Variable expansion + write redirection = tainted write, should be blocked
        var result = await GuardWithStdinAsync(
            BashJson("echo $PAYLOAD > src/output.txt"));
        result.AssertExitCode(2);
        result.AssertStderrContains("bypass");
    }

    [Fact]
    public async Task Finding3_TaintedReadOperation_Allowed()
    {
        await SetupClaimedAgent();

        // Variable expansion + read only = allowed (with warning)
        var result = await GuardWithStdinAsync(BashJson("cat $FILENAME"));
        result.AssertSuccess();
    }

    // ================================================================
    // Finding 5: Command substitution hiding entire write operations
    // ================================================================

    [Fact]
    public void Finding5_WriteInsideCommandSubstitution_DetectedAsWriteOp()
    {
        var analyzer = new BashCommandAnalyzer();
        // cp is entirely inside $() — analyzer must still detect the write
        var result = analyzer.Analyze("echo $(cp secret.txt /tmp/output.txt)");
        Assert.True(result.HasBypassAttempt);
        Assert.Contains(result.Operations, op =>
            op.Type is FileOperationType.Copy or FileOperationType.Write
            or FileOperationType.Delete or FileOperationType.Move);
    }

    [Fact]
    public void Finding5_WriteInsideBackticks_DetectedAsWriteOp()
    {
        var analyzer = new BashCommandAnalyzer();
        var result = analyzer.Analyze("echo `rm important.txt`");
        Assert.True(result.HasBypassAttempt);
        Assert.Contains(result.Operations, op =>
            op.Type is FileOperationType.Delete);
    }

    [Fact]
    public void Finding5_ReadInsideCommandSubstitution_NoWriteOp()
    {
        var analyzer = new BashCommandAnalyzer();
        // cat inside $() is a read, not a write — should not produce write ops
        var result = analyzer.Analyze("echo $(cat readme.txt)");
        Assert.True(result.HasBypassAttempt);
        Assert.DoesNotContain(result.Operations, op =>
            op.Type is FileOperationType.Write or FileOperationType.Delete
            or FileOperationType.Move or FileOperationType.Copy);
    }

    [Fact]
    public async Task Finding5_WriteHiddenInSubstitution_Blocked()
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(
            BashJson("echo $(cp secret.txt /tmp/leak.txt)"));
        result.AssertExitCode(2);
        result.AssertStderrContains("bypass");
    }

    [Fact]
    public async Task Finding5_ReadOnlyInSubstitution_Allowed()
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(
            BashJson("echo $(cat readme.txt)"));
        result.AssertSuccess();
    }

    // ================================================================
    // Finding 4: Nudge false positives — dydo commands skip nudges
    // ================================================================

    [Fact]
    public async Task Finding4_DydoCommand_SkipsNudges()
    {
        await SetupClaimedAgent();

        // Brief text contains "git worktree add" which matches the worktree nudge pattern,
        // but since this is a dydo command, nudges should be skipped
        var result = await GuardWithStdinAsync(
            BashJson("dydo dispatch --no-wait --auto-close --role reviewer --task test --brief 'text about git worktree add'"));

        // Should not be blocked by the nudge
        if (result.HasError)
        {
            Assert.DoesNotContain("worktree commands instead", result.Stderr);
        }
    }

    [Fact]
    public async Task Finding4_DangerousPattern_CheckedBeforeNudges()
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(BashJson("rm -rf /"));
        result.AssertExitCode(2);
        result.AssertStderrContains("Dangerous");
    }

    // ================================================================
    // Issue #58: DangerousPatterns gaps
    // ================================================================

    [Theory]
    [InlineData("rm -rf ./")]
    [InlineData("rm -Rf ./")]
    public void Issue58_RmRfCurrentDir_IsDangerous(string command)
    {
        var analyzer = new BashCommandAnalyzer();
        var (isDangerous, _) = analyzer.CheckDangerousPatterns(command);
        Assert.True(isDangerous, $"Expected dangerous: {command}");
    }

    [Theory]
    [InlineData("Remove-Item -Recurse -Force /")]
    [InlineData("Remove-Item -Recurse -Force C:\\")]
    [InlineData("Remove-Item -Force -Recurse ~")]
    public void Issue58_PowerShellRecursiveDelete_IsDangerous(string command)
    {
        var analyzer = new BashCommandAnalyzer();
        var (isDangerous, _) = analyzer.CheckDangerousPatterns(command);
        Assert.True(isDangerous, $"Expected dangerous: {command}");
    }

    [Theory]
    [InlineData("Remove-Item -Recurse ./node_modules")]
    [InlineData("Remove-Item -Force ./temp")]
    public void Issue58_PowerShellNonRoot_NotDangerous(string command)
    {
        var analyzer = new BashCommandAnalyzer();
        var (isDangerous, _) = analyzer.CheckDangerousPatterns(command);
        Assert.False(isDangerous, $"Should not be dangerous: {command}");
    }

    [Theory]
    [InlineData("> /dev/nvme0n1")]
    [InlineData("> /dev/vda")]
    [InlineData("> /dev/mmcblk0")]
    [InlineData("dd of=/dev/nvme0n1")]
    [InlineData("dd of=/dev/vda")]
    [InlineData("dd of=/dev/mmcblk0")]
    public void Issue58_NonSdaDiskDevices_IsDangerous(string command)
    {
        var analyzer = new BashCommandAnalyzer();
        var (isDangerous, _) = analyzer.CheckDangerousPatterns(command);
        Assert.True(isDangerous, $"Expected dangerous: {command}");
    }

    // ================================================================
    // Issue #85: bash -c / sh -c inner command analysis
    // ================================================================

    [Fact]
    public void Issue85_BashC_InnerReadCommandAnalyzed()
    {
        var analyzer = new BashCommandAnalyzer();
        var result = analyzer.Analyze("bash -c \"cat state.md\"");
        Assert.Contains(result.Operations, op => op.Type == FileOperationType.Read);
    }

    [Fact]
    public void Issue85_ShC_InnerWriteDetected()
    {
        var analyzer = new BashCommandAnalyzer();
        var result = analyzer.Analyze("sh -c \"tee output.txt\"");
        Assert.Contains(result.Operations, op => op.Type == FileOperationType.Write);
    }

    [Fact]
    public void Issue85_BashC_InnerRedirectionDetected()
    {
        var analyzer = new BashCommandAnalyzer();
        var result = analyzer.Analyze("zsh -c \"echo pwned > file.txt\"");
        Assert.Contains(result.Operations, op => op.Type == FileOperationType.Write && op.Path == "file.txt");
    }

    [Fact]
    public void Issue85_BashC_CombinedFlags()
    {
        var analyzer = new BashCommandAnalyzer();
        var result = analyzer.Analyze("bash -xc \"cat secret.txt\"");
        Assert.Contains(result.Operations, op => op.Type == FileOperationType.Read);
    }

    [Theory]
    [InlineData("bash -c \"echo hello\"")]
    [InlineData("sh -c \"npm install\"")]
    [InlineData("bash -c \"git status\"")]
    public void Issue85_BashC_NotBlockedAsDangerous(string command)
    {
        var analyzer = new BashCommandAnalyzer();
        var (isDangerous, _) = analyzer.CheckDangerousPatterns(command);
        Assert.False(isDangerous, $"Should not be dangerous: {command}");
    }

    [Fact]
    public async Task Issue85_BashC_InnerOffLimitsWrite_Blocked()
    {
        await SetupClaimedAgent();

        // Real quotes around -c argument — BashJson handles JSON escaping
        var result = await GuardWithStdinAsync(
            BashJson("bash -c \"tee dydo/agents/Adele/.guard-lift.json\""));
        result.AssertExitCode(2);
        result.AssertStderrContains("off-limits");
    }

    // ================================================================
    // Issue #86: Variable hiding command name
    // ================================================================

    [Fact]
    public void Issue86_VariableAsCommand_PathArgsFlaggedAsUncertainWrite()
    {
        var analyzer = new BashCommandAnalyzer();
        var result = analyzer.Analyze("$CMD dydo/files-off-limits.md");
        Assert.True(result.HasBypassAttempt);
        Assert.Contains(result.Operations, op =>
            op.IsUncertain && op.Type == FileOperationType.Write);
    }

    [Fact]
    public void Issue86_SubstitutionAsCommand_PathArgsFlagged()
    {
        var analyzer = new BashCommandAnalyzer();
        // $(echo tee) tokenizes to ["$(echo", "tee)", "file.cs"] — cmdName starts with $
        var result = analyzer.Analyze("$(echo tee) file.cs");
        Assert.True(result.HasBypassAttempt);
        Assert.Contains(result.Operations, op => op.IsUncertain && op.Path == "file.cs");
    }

    [Fact]
    public async Task Issue86_VariableHidingWriteCommand_Blocked()
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(BashJson("$CMD dydo/files-off-limits.md"));
        result.AssertExitCode(2);
        result.AssertStderrContains("bypass");
    }

    // ================================================================
    // Issue #60: Off-limits bypass consistency for bash reads
    // ================================================================

    [Fact]
    public async Task Issue60_BashReadModeFile_AllowedLikeDirectRead()
    {
        await SetupClaimedAgent();

        // Mode files are off-limits but should be readable via bootstrap bypass,
        // both for direct reads AND bash reads (consistency)
        var result = await GuardWithStdinAsync(
            BashJson("cat dydo/agents/Adele/modes/code-writer.md"));
        result.AssertSuccess();
    }

    // ================================================================
    // Issue #64: Block-severity nudges can't be removed from config
    // ================================================================

    [Fact]
    public void Issue64_MergeSystemNudges_AddsBlockDefaults()
    {
        // Empty config — should merge in all block-severity defaults
        var nudges = GuardCommand.MergeSystemNudges([]);
        Assert.True(nudges.Count > 0);
        Assert.All(nudges, n => Assert.Equal("block", n.Severity, ignoreCase: true));
    }

    [Fact]
    public void Issue64_MergeSystemNudges_NoDoubles()
    {
        // Full config — no duplicates added
        var nudges = GuardCommand.MergeSystemNudges(ConfigFactory.DefaultNudges.ToList());
        var patterns = nudges.Select(n => n.Pattern).ToList();
        Assert.Equal(patterns.Count, patterns.Distinct().Count());
    }

    [Fact]
    public async Task Issue64_IndirectDydo_BlockedEvenWithEmptyNudgeConfig()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Clear all nudges from config
        var configPath = Path.Combine(TestDir, "dydo", "dydo.json");
        if (File.Exists(configPath))
        {
            var content = File.ReadAllText(configPath);
            content = content.Replace("\"nudges\"", "\"_nudges_disabled\"");
            File.WriteAllText(configPath, content);
        }

        var result = await GuardWithStdinAsync(
            BashJson("npx dydo agent claim auto"));
        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
    }

    [Fact]
    public void Issue64_MergeSystemNudges_NullInput_ReturnsDefaults()
    {
        var nudges = GuardCommand.MergeSystemNudges(null);
        Assert.True(nudges.Count > 0);
    }

    [Fact]
    public void Issue64_MergeSystemNudges_DoesNotAddWarnNudges()
    {
        // Only block-severity defaults should be merged
        var nudges = GuardCommand.MergeSystemNudges([]);
        Assert.All(nudges, n => Assert.Equal("block", n.Severity, ignoreCase: true));
    }

    // ================================================================
    // Additional edge-case tests for BashCommandAnalyzer
    // ================================================================

    [Fact]
    public void Issue85_ShellWithoutCFlag_NoInnerAnalysis()
    {
        var analyzer = new BashCommandAnalyzer();
        // bash with -e flag (exit on error) should not trigger inner analysis
        var result = analyzer.Analyze("bash -e script.sh");
        // No file operations expected — bash is not in command dictionaries
        Assert.DoesNotContain(result.Operations, op => op.Type == FileOperationType.Read);
    }

    [Fact]
    public void Issue85_ShellCWithNoArgument_NoInnerAnalysis()
    {
        var analyzer = new BashCommandAnalyzer();
        // bash -c with no following argument — edge case
        var result = analyzer.Analyze("bash -c");
        Assert.Empty(result.Operations);
    }

    [Fact]
    public void Issue85_ShellCWithMultipleSubcommands()
    {
        var analyzer = new BashCommandAnalyzer();
        // Inner command has multiple subcommands separated by ;
        var result = analyzer.Analyze("bash -c \"cat file.txt; rm temp.txt\"");
        Assert.Contains(result.Operations, op => op.Type == FileOperationType.Read);
        Assert.Contains(result.Operations, op => op.Type == FileOperationType.Delete);
    }

    [Fact]
    public void Issue86_VariableWithNoPathArgs_NoUncertainOps()
    {
        var analyzer = new BashCommandAnalyzer();
        // $CMD with no arguments — no uncertain operations
        var result = analyzer.Analyze("$CMD");
        Assert.True(result.HasBypassAttempt);
        Assert.Empty(result.Operations);
    }

    [Fact]
    public void Issue86_BacktickAsCommand_PathArgsFlagged()
    {
        var analyzer = new BashCommandAnalyzer();
        var result = analyzer.Analyze("`get_cmd` file.txt");
        Assert.True(result.HasBypassAttempt);
        Assert.Contains(result.Operations, op => op.IsUncertain && op.Path == "file.txt");
    }

    [Theory]
    [InlineData("rm -rf /some/path")]
    [InlineData("rm -rf specific-dir")]
    public void Issue58_RmRfNonRoot_NotDangerous(string command)
    {
        var analyzer = new BashCommandAnalyzer();
        var (isDangerous, _) = analyzer.CheckDangerousPatterns(command);
        Assert.False(isDangerous, $"Should not be dangerous: {command}");
    }

    [Theory]
    [InlineData("> /dev/sda")]
    [InlineData("dd of=/dev/sdb")]
    public void Issue58_OriginalSdaDevices_StillDangerous(string command)
    {
        var analyzer = new BashCommandAnalyzer();
        var (isDangerous, _) = analyzer.CheckDangerousPatterns(command);
        Assert.True(isDangerous, $"Expected dangerous: {command}");
    }

    // ================================================================
    // CheckBashFileOperation coverage — bash write operations vs RBAC
    // ================================================================

    [Fact]
    public async Task BashWriteOperation_BlockedByRbac()
    {
        await SetupClaimedAgent();

        // Write to a path outside code-writer permissions via bash
        var result = await GuardWithStdinAsync(
            BashJson("tee dydo/some-protected-file.md"));
        result.AssertExitCode(2);
    }

    [Fact]
    public async Task BashReadOperation_AllowedForClaimedAgent()
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(BashJson("cat README.md"));
        result.AssertSuccess();
    }

    [Fact]
    public async Task BashWriteToAllowedPath_Succeeds()
    {
        await SetupClaimedAgent();

        // Code-writer can write to own workspace
        var result = await GuardWithStdinAsync(
            BashJson("tee dydo/agents/Adele/notes.md"));
        result.AssertSuccess();
    }

    [Fact]
    public async Task BashDeleteOperation_OffLimitsBlocked()
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(
            BashJson("rm dydo/agents/Adele/.guard-lift.json"));
        result.AssertExitCode(2);
        result.AssertStderrContains("off-limits");
    }

    [Fact]
    public async Task Issue60_BashReadBootstrapFile_Allowed()
    {
        await SetupClaimedAgent();

        // Bootstrap files (workflow.md) should be readable even via bash
        var result = await GuardWithStdinAsync(
            BashJson("cat dydo/agents/Adele/workflow.md"));
        result.AssertSuccess();
    }

    [Fact]
    public async Task Issue60_BashReadOffLimitsNonMode_Blocked()
    {
        await SetupClaimedAgent();

        // Off-limits file that's NOT a mode or bootstrap file should still be blocked
        var result = await GuardWithStdinAsync(
            BashJson("cat dydo/agents/Adele/state.md"));
        result.AssertExitCode(2);
        result.AssertStderrContains("off-limits");
    }

    [Fact]
    public async Task BashGuardLiftedWrite_Succeeds()
    {
        await SetupClaimedAgent();

        // Lift guard
        var service = new GuardLiftService(TestDir);
        service.Lift("Adele", "testuser", null);

        // Write to normally-blocked path should succeed when guard is lifted
        var result = await GuardWithStdinAsync(
            BashJson("tee dydo/some-protected.md"));
        result.AssertSuccess();
    }
}
