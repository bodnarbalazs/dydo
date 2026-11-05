namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;
using DynaDocs.Models;
using DynaDocs.Services;

/// <summary>
/// Tests for security findings from the guard system inquisition.
/// Each section maps to a specific finding number.
/// </summary>
[Collection("Integration")]
public class GuardSecurityTests : IntegrationTestBase
{
    [Theory]
    [InlineData("git push --force origin main")]
    [InlineData("gh auth token")]
    [InlineData("bw export --format json")]
    [InlineData("mkfs.ext4 /dev/sda1")]
    [InlineData("dd if=/dev/zero of=/dev/mapper/vg-root")]
    [InlineData("dd if=/dev/zero of=/dev/loop0")]
    [InlineData("dd if=/dev/zero of=/dev/dm-0")]
    [InlineData("dd if=x of='/dev/loop0'")]
    [InlineData("dd if=x of='/dev/mapper/vg-root'")]
    [InlineData("gpg2 --export-secret-keys ABC")]
    [InlineData("cat /Applications/Bitwarden.app/Contents/Info.plist")]
    [InlineData("cat /tmp/Bitwarden.app/Contents/Info.plist")]
    [InlineData("ls Bitwarden.app")]
    [MemberData(nameof(DangerousCommandCases.WrappedFamilies), MemberType = typeof(DangerousCommandCases))]
    [InlineData("/usr/bin/dd if=x of=/dev/sda")]
    [InlineData("LC_ALL=C dd if=x of=/dev/sda")]
    [InlineData("if true; then dd if=x of=/dev/sda; fi")]
    [InlineData("sudo -- dd if=x of=/dev/sda")]
    [InlineData("nice -n 10 dd if=x of=/dev/sda")]
    [InlineData("xargs dd of=/dev/sda")]
    [InlineData("ssh host 'dd if=x of=/dev/sda'")]
    [InlineData("GIT_SSH=x git push --force")]
    [InlineData("echo 'sudo dd if=x of=/dev/sda'")]
    [InlineData("eval 'dd if=/dev/zero of=/dev/sda'")]
    [InlineData("echo 'dd if=/dev/zero of=/dev/sda' | sh")]
    [InlineData("bash <<< 'dd if=/dev/zero of=/dev/sda'")]
    [InlineData("bash -c -- 'dd if=/dev/zero of=/dev/sda'")]
    [InlineData("powershell -Command \"dd if=/dev/zero of=/dev/sda\"")]
    public async Task OndrejDenylist_HookBlocksDangerousCommand(string command)
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(BashJson(command));

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("Dangerous");
    }

    [Theory]
    [InlineData("git push --force-with-lease origin feature/x")]
    [InlineData("gh auth status")]
    [InlineData("op --version")]
    [InlineData("dd if=/dev/zero of=/dev/null")]
    [InlineData("dd if=x of='/dev/null'")]
    [InlineData("echo 'dd if=x of=/dev/loop0'")]
    [InlineData("grep 'dd if=x of=/dev/mapper/vg-root' README.md")]
    [InlineData("cat /tmp/Safari.app/Contents/Info.plist")]
    [InlineData("ls Safari.app")]
    [InlineData("sudo -u me git status")]
    [InlineData("nohup rm file.txt")]
    [InlineData("bash -c 'git push --force-with-lease origin feature/x'")]
    [InlineData("(dd if=x of=/dev/null)")]
    public async Task OndrejDenylist_HookAllowsBenignCommand(string command)
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(BashJson(command));

        result.AssertPreToolUseAllowed();
    }

    private async Task SetupClaimedAgent()
    {
        await InitProjectAsync("none");
    }

    private string BashJson(string command) =>
        "{\"session_id\":\"" + TestSessionId + "\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"" +
        command.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"}}";

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
    // Finding 3: Command substitution - tainted writes blocked
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

        var result = await GuardWithStdinAsync(
            BashJson("echo $PAYLOAD > src/output.txt"));
        result.AssertPreToolUseDenied();
        result.AssertStderrContains("bypass");
    }

    [Fact]
    public async Task Finding3_TaintedReadOperation_Allowed()
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(BashJson("cat $FILENAME"));
        result.AssertPreToolUseAllowed();
    }

    // ================================================================
    // Finding 5: Command substitution hiding entire write operations
    // ================================================================

    [Fact]
    public void Finding5_WriteInsideCommandSubstitution_DetectedAsWriteOp()
    {
        var analyzer = new BashCommandAnalyzer();
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
        result.AssertPreToolUseDenied();
        result.AssertStderrContains("bypass");
    }

    [Fact]
    public async Task Finding5_ReadOnlyInSubstitution_Allowed()
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(
            BashJson("echo $(cat readme.txt)"));
        result.AssertPreToolUseAllowed();
    }

    // ================================================================
    // Finding 4: nudges fire for every shell command, dydo included (DR-041 Part C),
    // so prefixing a dydo call cannot bypass a block nudge.
    // ================================================================

    [Fact]
    public async Task Finding4_DydoMatchingPattern_NowFires()
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(
            BashJson("dydo task list && tail -f build.log"));

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("poll-loop");
    }

    [Fact]
    public async Task Finding4_DangerousPattern_CheckedBeforeNudges()
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(BashJson("rm -rf /"));
        result.AssertPreToolUseDenied();
        result.AssertStderrContains("Dangerous");
    }

    // ================================================================
    // Issue #155: dydo command chains must retain full bash security
    // ================================================================

    [Fact]
    public async Task Issue155_DydoChain_OffLimitsRead_Blocked()
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(
            BashJson("dydo whoami && cat config/secrets.json"));

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("off-limits");
    }

    [Fact]
    public async Task Issue155_DydoChain_OffLimitsWrite_Blocked()
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(
            BashJson("dydo whoami && tee config/secrets.json"));

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("off-limits");
    }

    [Fact]
    public async Task Issue155_DydoChain_DangerousPattern_Blocked()
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(BashJson("dydo whoami && rm -rf /"));

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("Dangerous");
    }

    [Fact]
    public async Task Issue155_DydoNotFirst_DangerousPattern_Blocked()
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(BashJson("rm -rf / && dydo whoami"));

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("Dangerous");
    }

    [Fact]
    public async Task Issue155_DydoWaitCancel_ChainedWritableWrite_Succeeds()
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(
            BashJson("dydo wait --cancel && tee scratch/notes.md"));

        result.AssertPreToolUseAllowed();
    }

    [Fact]
    public async Task Issue155_BareDydoCommand_Succeeds()
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(BashJson("dydo whoami"));

        result.AssertPreToolUseAllowed();
    }

    private string PowerShellJson(string command) =>
        "{\"session_id\":\"" + TestSessionId + "\",\"tool_name\":\"PowerShell\",\"tool_input\":{\"command\":\"" +
        command.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"}}";

    [Fact]
    public async Task BugB_PowerShell_DangerousPattern_IsBlocked()
    {
        // PowerShell must route through HandleBashCommand like Bash; unrouted, it reached
        // HandleWriteOperation with a null filePath and passed - a full guard bypass.
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(PowerShellJson("rm -rf /"));
        result.AssertPreToolUseDenied();
        result.AssertStderrContains("Dangerous");
    }

    // ================================================================
    // Issue #295: codex shell tool names must route through the shell guard
    // ================================================================
    //
    // Codex names its shell tool shell_command (also exec/local_shell/unified_exec by mode),
    // not Bash/PowerShell. Unrouted, these reach HandleWriteOperation with a null filePath and
    // pass, so every codex shell command runs unguarded.

    private string CodexShellJson(string toolName, string command) =>
        "{\"session_id\":\"" + TestSessionId + "\",\"tool_name\":\"" + toolName + "\",\"tool_input\":{\"command\":\"" +
        command.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"}}";

    [Theory]
    [InlineData("shell_command")]
    [InlineData("exec")]
    [InlineData("local_shell")]
    [InlineData("unified_exec")]
    public async Task Issue295_CodexShellTool_OffLimitsRead_IsBlocked(string toolName)
    {
        // The reported repro: a codex worker read an off-limits agent state file and got the
        // contents back, exit 0, no block.
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(
            CodexShellJson(toolName, "cat config/secrets.json"));
        result.AssertPreToolUseDenied();
        result.AssertStderrContains("off-limits");
    }

    [Fact]
    public async Task Issue295_CodexShellTool_OffLimitsWrite_IsBlocked()
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(CodexShellJson("shell_command", "tee config/secrets.json"));
        result.AssertPreToolUseDenied();
        result.AssertStderrContains("off-limits");
    }

    [Fact]
    public async Task Issue295_CodexShellTool_DangerousPattern_IsBlocked()
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(CodexShellJson("shell_command", "rm -rf /"));
        result.AssertPreToolUseDenied();
        result.AssertStderrContains("Dangerous");
    }

    [Fact]
    public async Task Issue295_CodexShellTool_BenignCommand_IsAllowed()
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(CodexShellJson("shell_command", "git status --short"));
        result.AssertPreToolUseAllowed();
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

        // Real quotes around -c argument - BashJson handles JSON escaping
        var result = await GuardWithStdinAsync(
            BashJson("bash -c \"tee config/secrets.json\""));
        result.AssertPreToolUseDenied();
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
        // $(echo tee) tokenizes to ["$(echo", "tee)", "file.cs"] - cmdName starts with $
        var result = analyzer.Analyze("$(echo tee) file.cs");
        Assert.True(result.HasBypassAttempt);
        Assert.Contains(result.Operations, op => op.IsUncertain && op.Path == "file.cs");
    }

    [Fact]
    public async Task Issue86_VariableHidingWriteCommand_Blocked()
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(BashJson("$CMD dydo/files-off-limits.md"));
        result.AssertPreToolUseDenied();
        result.AssertStderrContains("bypass");
    }

    // ================================================================
    // Issue #64: Block-severity nudges can't be removed from config
    // ================================================================

    [Fact]
    public void Issue64_MergeSystemNudges_AddsBlockDefaults()
    {
        var nudges = GuardCommand.MergeSystemNudges([]);
        Assert.True(nudges.Count > 0);
        Assert.All(nudges, n => Assert.Equal("block", n.Severity, ignoreCase: true));
    }

    [Fact]
    public void Issue64_MergeSystemNudges_NoDoubles()
    {
        var nudges = GuardCommand.MergeSystemNudges(ConfigFactory.DefaultNudges.ToList());
        var patterns = nudges.Select(n => n.Pattern).ToList();
        Assert.Equal(patterns.Count, patterns.Distinct().Count());
    }

    [Fact]
    public async Task Issue64_IndirectDydo_BlockedEvenWithEmptyNudgeConfig()
    {
        await InitProjectAsync("none");

        var configPath = Path.Combine(TestDir, "dydo", "dydo.json");
        if (File.Exists(configPath))
        {
            var content = File.ReadAllText(configPath);
            content = content.Replace("\"nudges\"", "\"_nudges_disabled\"");
            File.WriteAllText(configPath, content);
        }

        var result = await GuardWithStdinAsync(
            BashJson("npx dydo agent claim auto"));
        result.AssertPreToolUseDenied();
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
        var nudges = GuardCommand.MergeSystemNudges([]);
        Assert.All(nudges, n => Assert.Equal("block", n.Severity, ignoreCase: true));
    }

    [Fact]
    public void Issue64_MergeSystemNudges_DowngradedSeverity_ForcedBackToBlock()
    {
        var blockDefault = ConfigFactory.DefaultNudges.First(
            n => string.Equals(n.Severity, "block", StringComparison.OrdinalIgnoreCase));

        var configNudges = new List<NudgeConfig>
        {
            new() { Pattern = blockDefault.Pattern, Message = blockDefault.Message, Severity = "warn" }
        };

        var result = GuardCommand.MergeSystemNudges(configNudges);

        var matching = result.Where(n => n.Pattern == blockDefault.Pattern).ToList();
        Assert.Single(matching);
        Assert.Equal("block", matching[0].Severity, ignoreCase: true);
    }

    [Fact]
    public void MergeSystemNudges_HealsStaleWarnMessage()
    {
        // A known-stale poll-loop warn text that still recommends the deleted `dydo wait`.
        var stale = new NudgeConfig
        {
            Pattern = @"\buntil\s+\[",
            Message = "Open-ended Bash poll-loop detected. Prefer a bounded for i in {1..30}; do ...; sleep 1; done, or `gh run watch`, or `dydo wait` for dydo-native waits. Open-ended polls have caused agent crashes (issue 0177).",
            Severity = "warn"
        };

        var result = GuardCommand.MergeSystemNudges([stale]);

        var healed = Assert.Single(result, n => n.Pattern == stale.Pattern);
        Assert.DoesNotContain("dydo wait", healed.Message);
        Assert.Equal("warn", healed.Severity, ignoreCase: true);
        var currentDefault = ConfigFactory.DefaultNudges.First(d => d.Pattern == stale.Pattern);
        Assert.Equal(currentDefault.Message, healed.Message);
    }

    [Fact]
    public void MergeSystemNudges_RemovesRetiredWorktreeNudge()
    {
        // A retired worktree nudge has no current default → it is dropped entirely.
        var retired = new NudgeConfig
        {
            Pattern = @"\bgit\b[^;|&]*\bworktree\s+(add|remove)\b",
            Message = "Use dydo worktree commands instead of git worktree directly.",
            Severity = "block"
        };

        var result = GuardCommand.MergeSystemNudges([retired]);

        Assert.DoesNotContain(result, n => n.Pattern == retired.Pattern);
        Assert.DoesNotContain(result, n => n.Message == retired.Message);
    }

    [Fact]
    public void MergeSystemNudges_LeavesUserCustomizedMessageAlone()
    {
        var blockDefault = ConfigFactory.DefaultNudges.First(
            n => string.Equals(n.Severity, "block", StringComparison.OrdinalIgnoreCase));
        var custom = new NudgeConfig
        {
            Pattern = blockDefault.Pattern,
            Message = "My team's own wording for this rule.",
            Severity = "block"
        };

        var result = GuardCommand.MergeSystemNudges([custom]);

        var matching = Assert.Single(result, n => n.Pattern == custom.Pattern);
        Assert.Equal("My team's own wording for this rule.", matching.Message);
    }

    [Fact]
    public void MergeSystemNudges_ForceRestoresSeverityWithoutClobberingCustomMessage()
    {
        var blockDefault = ConfigFactory.DefaultNudges.First(
            n => string.Equals(n.Severity, "block", StringComparison.OrdinalIgnoreCase));
        var custom = new NudgeConfig
        {
            Pattern = blockDefault.Pattern,
            Message = "My team's own wording for this rule.",
            Severity = "warn"
        };

        var result = GuardCommand.MergeSystemNudges([custom]);

        var matching = Assert.Single(result, n => n.Pattern == custom.Pattern);
        Assert.Equal("block", matching.Severity, ignoreCase: true);
        Assert.Equal("My team's own wording for this rule.", matching.Message);
    }

    [Fact]
    public void Issue85_ShellWithoutCFlag_NoInnerAnalysis()
    {
        var analyzer = new BashCommandAnalyzer();
        var result = analyzer.Analyze("bash -e script.sh");
        // No file operations expected - bash is not in command dictionaries
        Assert.DoesNotContain(result.Operations, op => op.Type == FileOperationType.Read);
    }

    [Fact]
    public void Issue85_ShellCWithNoArgument_NoInnerAnalysis()
    {
        var analyzer = new BashCommandAnalyzer();
        var result = analyzer.Analyze("bash -c");
        Assert.Empty(result.Operations);
    }

    [Fact]
    public void Issue85_ShellCWithMultipleSubcommands()
    {
        var analyzer = new BashCommandAnalyzer();
        var result = analyzer.Analyze("bash -c \"cat file.txt; rm temp.txt\"");
        Assert.Contains(result.Operations, op => op.Type == FileOperationType.Read);
        Assert.Contains(result.Operations, op => op.Type == FileOperationType.Delete);
    }

    [Fact]
    public void Issue86_VariableWithNoPathArgs_NoUncertainOps()
    {
        var analyzer = new BashCommandAnalyzer();
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

    [Fact]
    public async Task BashReadOperation_AllowedForClaimedAgent()
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(BashJson("cat README.md"));
        result.AssertPreToolUseAllowed();
    }

    [Fact]
    public async Task BashWriteToAllowedPath_Succeeds()
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(
            BashJson("tee scratch/notes.md"));
        result.AssertPreToolUseAllowed();
    }

    [Fact]
    public async Task BashDeleteOperation_OffLimitsBlocked()
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(
            BashJson("rm config/secrets.json"));
        result.AssertPreToolUseDenied();
        result.AssertStderrContains("off-limits");
    }

    [Fact]
    public async Task Issue60_BashReadOffLimitsNonMode_Blocked()
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(
            BashJson("cat config/secrets.json"));
        result.AssertPreToolUseDenied();
        result.AssertStderrContains("off-limits");
    }

    // ================================================================
    // DYD-406: the 3.6.0 session retro's guard misreadings, through the hook
    // ================================================================

    private static string ShellJson(string toolName, string command) =>
        System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["session_id"] = TestSessionId,
            ["tool_name"] = toolName,
            ["tool_input"] = new Dictionary<string, string> { ["command"] = command },
        });

    // GS-02: literal data written to a literal destination is no bypass.
    [Theory]
    [InlineData("PowerShell", GuardRetroCommands.Gs02SetContentEvidence)]
    [InlineData("PowerShell", GuardRetroCommands.Gs02SetContentVerdict)]
    [InlineData("Bash", GuardRetroCommands.Gs02CatSlotScript)]
    [InlineData("Bash", GuardRetroCommands.Gs02CatAppendEvidence)]
    [InlineData("Bash", "cat > notes.md <<'EOF'\nplain words\nEOF")]
    public async Task Gs02_LiteralDataWrite_ToALiteralDestination_IsAllowed(string toolName, string command)
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(ShellJson(toolName, command));

        result.AssertPreToolUseAllowed();
    }

    // The CLI lane reads its command as an unknown shell, both ways.
    private Task<CommandResult> UnknownShellGuardAsync(string command) => GuardWithStdinAsync("", "--command", command);

    // DYD-422: one sed, printf or echo command writing single-quoted literal data to a literal
    // destination is no bypass, through Claude's Bash and PowerShell tools and in an unknown shell.
    [Theory]
    [InlineData("sed -i 's/`x`/y/' f")]
    [InlineData("printf '%s' 'first line\n$HOME is literal' > notes.md")]
    [InlineData("echo 'cost: $5 and `date`' > notes.md")]
    public async Task Gs02_SingleQuotedLiteralDataWrite_IsAllowed(string command)
    {
        await SetupClaimedAgent();

        var bash = await GuardWithStdinAsync(ShellJson("Bash", command));
        var powerShell = await GuardWithStdinAsync(ShellJson("PowerShell", command));
        var unknown = await UnknownShellGuardAsync(command);

        bash.AssertPreToolUseAllowed();
        powerShell.AssertPreToolUseAllowed();
        unknown.AssertSuccess();
    }

    // Literal data keeps its path checks: a guarded destination, or a guarded path a substitution
    // in it names, is refused for that path.
    [Theory]
    [InlineData("printf '%s' 'first line\n$HOME is literal' > dydo/index.md", "protected")]
    [InlineData("sed -i 's/`x`/y/' dydo/files-off-limits.md", "protected")]
    [InlineData("sed -i 's#`rm dydo/index.md`#y#' notes.md", "protected")]
    [InlineData("printf '%s' '$HOME' > .env", "off-limits")]
    public async Task Gs02_SingleQuotedLiteralDataWrite_ToAGuardedPath_IsStillDenied(string command, string tier)
    {
        await SetupClaimedAgent();

        var bash = await GuardWithStdinAsync(ShellJson("Bash", command));
        var unknown = await UnknownShellGuardAsync(command);

        bash.AssertPreToolUseDenied();
        bash.AssertStderrContains(tier);
        unknown.AssertExitCode(2);
        unknown.AssertStderrContains(tier);
    }

    // Single-quoted text no reading certifies as literal data is refused as on master.
    [Fact]
    public async Task Gs02_SingleQuotedSetContentWrite_IsDeniedAsOnMaster()
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(ShellJson("PowerShell", "Set-Content notes.md 'first line\n$HOME is literal'"));

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("bypass");
    }

    // DYD-422: where a program on the line may run the single-quoted text, a reading may not be the
    // shell's, or the text sits in a comment or a body, the call is refused as on master and the
    // protected canary keeps its bytes.
    [Theory]
    [InlineData("eval 'echo x' '> $OUT'")]
    [InlineData("iex 'echo x' '> $OUT'")]
    [InlineData("cmd /c echo 'x > $OUT'")]
    [InlineData("env -S 'echo x' '> $OUT'")]
    [InlineData("pwsh -Command 'echo x' '> $OUT'")]
    [InlineData("ssh host 'echo x' '> $OUT'")]
    [InlineData("watch -n 1 'echo x > $OUT'")]
    [InlineData("echo don\\'t $(rm dydo/index.md) won\\'t")]
    [MemberData(nameof(GuardRetroCommands.Gs02SingleQuotedLiteralLines), MemberType = typeof(GuardRetroCommands))]
    [MemberData(nameof(GuardRetroCommands.Gs02StateChangingLines), MemberType = typeof(GuardRetroCommands))]
    public async Task Gs02_SingleQuotedTextAProgramMayRun_IsDeniedAndTheCanaryUnchanged(string command)
    {
        await SetupClaimedAgent();
        var canary = Path.Combine(DydoDir, "index.md");
        var before = File.ReadAllBytes(canary);

        var bash = await GuardWithStdinAsync(ShellJson("Bash", command));
        var unknown = await UnknownShellGuardAsync(command);

        bash.AssertPreToolUseDenied();
        bash.AssertStderrContains("bypass");
        unknown.AssertExitCode(2);
        unknown.AssertStderrContains("bypass");
        Assert.Equal(before, File.ReadAllBytes(canary));
    }

    // DYD-422 review: every route the reviews found (a second command, a file the line writes and
    // then runs, an option off sed's list, a word the shell may expand, text only PowerShell runs)
    // is refused through Claude's Bash and PowerShell tools and an unknown shell, as on master.
    [Theory]
    [MemberData(nameof(GuardRetroCommands.Gs02WriteThenRunLines), MemberType = typeof(GuardRetroCommands))]
    [MemberData(nameof(GuardRetroCommands.Gs02OptionLines), MemberType = typeof(GuardRetroCommands))]
    [MemberData(nameof(GuardRetroCommands.Gs02OptionValueLines), MemberType = typeof(GuardRetroCommands))]
    [MemberData(nameof(GuardRetroCommands.Gs02NarrowedLines), MemberType = typeof(GuardRetroCommands))]
    [MemberData(nameof(GuardRetroCommands.Gs02ExpandingWordLines), MemberType = typeof(GuardRetroCommands))]
    [MemberData(nameof(GuardRetroCommands.Gs02PowerShellReadingLines), MemberType = typeof(GuardRetroCommands))]
    [MemberData(nameof(GuardRetroCommands.Gs02BreakCharacterLines), MemberType = typeof(GuardRetroCommands))]
    public async Task Gs02_WriteThenRun_IsDeniedInEveryShell(string command)
    {
        await SetupClaimedAgent();

        var bash = await GuardWithStdinAsync(ShellJson("Bash", command));
        var powerShell = await GuardWithStdinAsync(ShellJson("PowerShell", command));
        var unknown = await UnknownShellGuardAsync(command);

        bash.AssertPreToolUseDenied();
        bash.AssertStderrContains("bypass");
        powerShell.AssertPreToolUseDenied();
        powerShell.AssertStderrContains("bypass");
        unknown.AssertExitCode(2);
        unknown.AssertStderrContains("bypass");
    }

    // Split seat D: a Codex pwsh argv envelope runs its script in PowerShell, from a caller read as
    // bash; relief needs PowerShell's reading too, so the text it runs is refused as on master.
    [Theory]
    [MemberData(nameof(GuardRetroCommands.Gs02PowerShellReadingLines), MemberType = typeof(GuardRetroCommands))]
    [MemberData(nameof(GuardRetroCommands.Gs02BreakCharacterLines), MemberType = typeof(GuardRetroCommands))]
    public async Task Gs02_CodexPwshEnvelope_IsDenied(string script)
    {
        await SetupClaimedAgent();
        var payload = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["session_id"] = TestSessionId,
            ["turn_id"] = "t1",
            ["tool_name"] = "shell_command",
            ["cwd"] = TestDir,
            ["tool_input"] = new Dictionary<string, object> { ["command"] = new[] { "pwsh", "-Command", script }, ["workdir"] = TestDir },
        });

        var result = await GuardWithStdinAsync(payload);

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("bypass");
    }

    // DYD-641: pwsh 7.6.9 ends a statement and a comment at a bare carriage return, and a here-string
    // at a bare carriage return or a typographic quote before @ (measured in /tmp/dyd641-evidence);
    // the text after each runs, so Claude's PowerShell tool, a Codex pwsh argv envelope and an
    // unknown shell refuse it, and the protected canary keeps its bytes.
    [Theory]
    [InlineData("echo @'\nx\n’@; iex '$(echo x > $OUT)'\n'@ # '")]
    [InlineData("echo @'\nx\r'@; iex '$(echo x > $OUT)'\n'@ # '")]
    [InlineData("echo x\riex @'\n$(echo x > $OUT)\n'@")]
    [InlineData("echo hello\rrm dydo/index.md")]
    [InlineData("echo hello # note\rRemove-Item dydo/index.md")]
    [InlineData("# x\riex (\n@'\n$(echo x > dydo/index.md)\n'@\n)")]
    [InlineData("# x\riex `\n@'\n$(echo x > dydo/index.md)\n'@")]
    [InlineData("echo a > notes.md # x\"\rRemove-Item dydo/index.md\r# y\"")]
    public async Task Dyd641_StatementPwshRuns_IsDeniedWhereverPwshMayRunIt(string command)
    {
        await SetupClaimedAgent();
        var canary = Path.Combine(DydoDir, "index.md");
        var before = File.ReadAllBytes(canary);

        await AssertPwshToolsDenyAsync(command);
        var unknown = await UnknownShellGuardAsync(command);

        unknown.AssertExitCode(2);
        unknown.AssertStderrContains("bypass");
        Assert.Equal(before, File.ReadAllBytes(canary));
    }

    // A line starting with @ reaches the CLI lane's argv parser as a response file, so the unknown
    // shell's reading of this one is proved in the analyzer's tests.
    [Fact]
    public async Task Dyd641_PipeAfterABareCarriageReturn_IsDeniedWherePwshRunsIt()
    {
        await SetupClaimedAgent();
        var canary = Path.Combine(DydoDir, "index.md");
        var before = File.ReadAllBytes(canary);

        await AssertPwshToolsDenyAsync("@'\n$(echo x > dydo/index.md)\n'@\n# c\r| iex");

        Assert.Equal(before, File.ReadAllBytes(canary));
    }

    // bash 5.3.9 keeps a bare carriage return in a word, so the words after it are still the command's
    // arguments: it deletes, truncates, edits or chmods the canary (measured in /tmp/dyd641-evidence/fix2).
    // Claude's Bash tool and an unknown shell refuse it, as before DYD-641.
    [Theory]
    [InlineData("rm \r dydo/index.md")]
    [InlineData("rm -- \r dydo/index.md")]
    [InlineData("tee \r dydo/index.md")]
    [InlineData("sed -i\r s/a/b/ dydo/index.md")]
    [InlineData("truncate -s 0 \r dydo/index.md")]
    [InlineData("chmod 000 \r dydo/index.md")]
    [InlineData("echo $(rm \r dydo/index.md)")]
    public async Task Dyd641_ArgumentsAfterABareCarriageReturn_AreDeniedWhereverBashMayRunThem(string command)
    {
        await SetupClaimedAgent();
        var canary = Path.Combine(DydoDir, "index.md");
        var before = File.ReadAllBytes(canary);

        var bash = await GuardWithStdinAsync(ShellJson("Bash", command));
        var unknown = await UnknownShellGuardAsync(command);

        bash.AssertPreToolUseDenied();
        unknown.AssertExitCode(2);
        Assert.Equal(before, File.ReadAllBytes(canary));
    }

    // A bash -c string is bash's whichever tool hands it on, so every lane refuses it.
    [Fact]
    public async Task Dyd641_BashCommandStringWithABareCarriageReturn_IsDeniedInEveryLane()
    {
        await SetupClaimedAgent();
        const string command = "bash -c 'rm \r dydo/index.md'";

        foreach (var json in new[] { ShellJson("Bash", command), ShellJson("PowerShell", command), CodexPwshJson(command) })
            (await GuardWithStdinAsync(json)).AssertPreToolUseDenied();
        (await UnknownShellGuardAsync(command)).AssertExitCode(2);
    }

    // Each carriage return here is half of a CRLF, so the line holds none of the three characters:
    // the Windows-authored here-string write is allowed as before DYD-641 (pwsh 7.6.9 runs it harmlessly).
    // A line starting with @ reaches the CLI lane's argv parser as a response file (DYD-675).
    [Theory]
    [InlineData("@'\r\nhello\r\n'@ | Set-Content notes.md\r\necho done")]
    [InlineData("@'\r\na\r\n'@ | Set-Content notes.md\r\n@'\r\nb\r\n'@ | Add-Content notes.md")]
    [InlineData("@'\nhello\n'@ | Set-Content notes.md\r\n")]
    [InlineData("cat @'\r\nhello\r\n'@ > notes.md\r\n")]
    public async Task Dyd641_CrlfAfterAHereStringWrite_IsAllowed(string command)
    {
        await SetupClaimedAgent();

        foreach (var json in new[] { ShellJson("Bash", command), ShellJson("PowerShell", command), CodexPwshJson(command) })
            (await GuardWithStdinAsync(json)).AssertPreToolUseAllowed();
    }

    [Fact]
    public async Task Dyd641_CrlfAfterAHereStringWrite_IsAllowedInAnUnknownShell()
    {
        await SetupClaimedAgent();

        (await UnknownShellGuardAsync("cat @'\r\nhello\r\n'@ > notes.md\r\n")).AssertExitCode(0);
    }

    // Claude's PowerShell tool and a Codex pwsh argv envelope both refuse the command as a bypass.
    private async Task AssertPwshToolsDenyAsync(string command)
    {
        var powerShell = await GuardWithStdinAsync(ShellJson("PowerShell", command));
        var envelope = await GuardWithStdinAsync(CodexPwshJson(command));

        powerShell.AssertPreToolUseDenied();
        powerShell.AssertStderrContains("bypass");
        envelope.AssertPreToolUseDenied();
        envelope.AssertStderrContains("bypass");
    }

    // A Codex shell call that runs the command through pwsh -Command.
    private string CodexPwshJson(string command) =>
        System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["session_id"] = TestSessionId,
            ["turn_id"] = "t1",
            ["tool_name"] = "shell_command",
            ["cwd"] = TestDir,
            ["tool_input"] = new Dictionary<string, object> { ["command"] = new[] { "pwsh", "-Command", command }, ["workdir"] = TestDir },
        });

    // Split seat B's expanding-word probes that write no path: master warns, and so does every lane.
    [Theory]
    [MemberData(nameof(GuardRetroCommands.Gs02ExpandingWordWarningLines), MemberType = typeof(GuardRetroCommands))]
    public async Task Gs02_ExpandingWordWithoutAWrite_KeepsTheBypassWarning(string command)
    {
        await SetupClaimedAgent();

        var bash = await GuardWithStdinAsync(ShellJson("Bash", command));
        var powerShell = await GuardWithStdinAsync(ShellJson("PowerShell", command));
        var unknown = await UnknownShellGuardAsync(command);

        foreach (var result in new[] { bash, powerShell, unknown })
            result.AssertStderrContains("WARNING: Command contains command substitution - paths may be dynamic");
    }

    // The round-5 apostrophes pair only in PowerShell; bash's reading is required for every caller,
    // so Claude's Bash and PowerShell tools and an unknown shell all refuse them, as on master.
    [Theory]
    [InlineData("echo don\\'t $(rm dydo/index.md) won\\'t")]
    [InlineData("echo don\\'t $(echo x > $OUT) won\\'t")]
    [InlineData("echo don\\'t $(rm dydo/index.md) won\\'t # it's")]
    [InlineData("# don't\necho don\\'t $(rm dydo/index.md) won\\'t")]
    [InlineData("cat > notes.md <<'EOF'\nplain words\nEOF\necho don\\'t $(rm dydo/index.md) won\\'t")]
    [InlineData("cat <<EOF\necho don\\'t $(rm dydo/index.md) won\\'t\nEOF")]
    public async Task Gs02_RoundFiveApostrophes_AreDeniedInEveryShellAndTheCanaryUnchanged(string command)
    {
        await SetupClaimedAgent();
        var canary = Path.Combine(DydoDir, "index.md");
        var before = File.ReadAllBytes(canary);

        var bash = await GuardWithStdinAsync(ShellJson("Bash", command));
        var powerShell = await GuardWithStdinAsync(ShellJson("PowerShell", command));
        var unknown = await UnknownShellGuardAsync(command);

        bash.AssertPreToolUseDenied();
        bash.AssertStderrContains("bypass");
        powerShell.AssertPreToolUseDenied();
        powerShell.AssertStderrContains("bypass");
        unknown.AssertExitCode(2);
        unknown.AssertStderrContains("bypass");
        Assert.Equal(before, File.ReadAllBytes(canary));
    }

    // Where PowerShell may run the line: a typographic quote closes its string, a caret escapes
    // nothing, --% stops parsing, and a group word, an alias, an assignment or a call operator may
    // run the text, so it is code.
    [Theory]
    [InlineData("echo 'a’ $(echo x > $OUT) ’b'")]
    [InlineData("echo “a '$(echo x > $OUT)' b”")]
    [InlineData("echo ^\"'$(echo x > $OUT)'^\"")]
    [InlineData("echo '$HOME' (iex 'rm dydo/index.md') > notes.md")]
    [InlineData("echo --% '$(echo x > $OUT)' > notes.md")]
    [InlineData("echo '$(echo x > $OUT)' -OutVariable a > notes.md")]
    [InlineData("Set-Alias echo Invoke-Expression; echo 'echo x > $OUT'")]
    [InlineData("New-Alias printf Invoke-Expression; printf 'echo x > $OUT'")]
    [InlineData("sal echo iex; echo 'echo x > $OUT'")]
    [InlineData("Set-Item alias:echo Invoke-Expression; echo 'echo x > $OUT'")]
    [InlineData("$x = 1; echo '$(echo x > $OUT)' > notes.md")]
    [InlineData("& echo '$(echo x > $OUT)' > notes.md")]
    public async Task Gs02_TextPowerShellMayRun_IsDenied(string command)
    {
        await SetupClaimedAgent();

        var powerShell = await GuardWithStdinAsync(ShellJson("PowerShell", command));
        var unknown = await UnknownShellGuardAsync(command);

        powerShell.AssertPreToolUseDenied();
        powerShell.AssertStderrContains("bypass");
        unknown.AssertExitCode(2);
        unknown.AssertStderrContains("bypass");
    }

    // DYD-421 DECIDED: apostrophes around a substitution or a variable, on a command line or in an
    // expanding heredoc body, hide nothing; the protected canary keeps its bytes.
    [Theory]
    [InlineData("echo don\\'t $(rm dydo/index.md) won\\'t")]
    [InlineData("cat > notes.md <<EOF\nDon't $(rm dydo/index.md) it's\nEOF")]
    [InlineData("cat > notes.md <<EOF\nDon't $(echo y > dydo/index.md) it's\nEOF")]
    [InlineData("cat > notes.md <<EOF\nthe user's file: $(cat dydo/index.md > /dev/null; rm dydo/index.md) isn't kept\nEOF")]
    [InlineData("cat > notes.md <<EOF\nDon't run $(date) or it's late\nEOF")]
    [InlineData("cat > notes.md <<EOF\nDon't touch $HOME, it's mine\nEOF")]
    public async Task Gs02_ExpansionBetweenApostrophes_IsDeniedAndTheCanaryUnchanged(string command)
    {
        await SetupClaimedAgent();
        var canary = Path.Combine(DydoDir, "index.md");
        var before = File.ReadAllBytes(canary);

        var result = await GuardWithStdinAsync(ShellJson("Bash", command));

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("Command contains bypass patterns (command substitution or variable expansion)");
        Assert.Equal(before, File.ReadAllBytes(canary));
    }

    [Theory]
    [InlineData("Bash", "cat > $OUT <<'EOF'\nplain words\nEOF")]
    [InlineData("Bash", "cat > notes.md <<EOF\n$(rm -rf build)\nEOF")]
    [InlineData("Bash", "cat > notes.md <<EOF\nprice: $PRICE\nEOF")]
    [InlineData("Bash", "cat > notes.md <<'EOF'\nplain words\nEOF\nrm $TARGET")]
    [InlineData("Bash", "bash <<'EOF'\necho x > $OUT\nEOF")]
    [InlineData("Bash", "bash -c 'echo x > $OUT'")]
    [InlineData("Bash", "bash -c 'cd build\necho x > $OUT'")]
    [InlineData("Bash", "echo hi # don't\necho x > $OUT\necho 'ok'")]
    [InlineData("PowerShell", "Set-Content -LiteralPath $p -Value @'\nplain words\n'@")]
    [InlineData("PowerShell", "@\"\n$(Remove-Item build -Recurse)\n\"@ | Set-Content notes.md")]
    public async Task Gs02_DynamicWrite_NextToLiteralData_IsStillDenied(string toolName, string command)
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(ShellJson(toolName, command));

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("bypass");
    }

    [Theory]
    [InlineData("Bash", "cat > dydo/files-off-limits.md <<'EOF'\nplain words\nEOF")]
    [InlineData("Bash", "cat > notes.md <<'EOF'\nplain words\nEOF\nrm dydo/index.md")]
    [InlineData("PowerShell", "Set-Content -LiteralPath 'dydo/index.md' -Value @'\nplain words\n'@")]
    [InlineData("Bash", "cat > .env <<'EOF'\nplain words\nEOF")]
    public async Task Gs02_LiteralDataWrite_ToAGuardedPath_IsStillDenied(string toolName, string command)
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(ShellJson(toolName, command));

        result.AssertPreToolUseDenied();
    }

    // GS-03 (review round 6): a launch a runner makes, or the command string ssh or watch runs, keeps
    // master's poll-loop nudge. Unquoted and uninventoried quoted arguments still count;
    // only the finite data operands and proven inert bodies receive relief.
    [Theory]
    [InlineData("Bash", "stdbuf -oL tail -f app.log")]
    [InlineData("Bash", "stdbuf -o L tail -f app.log")]
    [InlineData("Bash", "stdbuf --output=L tail -f app.log")]
    [InlineData("Bash", "ionice -c 3 tail -f app.log")]
    [InlineData("Bash", "setsid tail -f app.log")]
    [InlineData("Bash", "unbuffer tail -f app.log")]
    [InlineData("Bash", "caffeinate -t 60 tail -f app.log")]
    [InlineData("Bash", "wsl -d Ubuntu -- tail -f app.log")]
    [InlineData("PowerShell", "wsl bash -c 'tail -f app.log'")]
    [InlineData("Bash", "ssh host 'tail -f app.log'")]
    [InlineData("Bash", "watch -n 1 'tail -f app.log'")]
    public async Task Gs03_RunnerAndRemoteLaunch_KeepsThePollLoopNudge(string toolName, string command)
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(ShellJson(toolName, command));

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("Open-ended Bash poll-loop detected");
    }

    [Theory]
    [InlineData("echo stdbuf -oL tail -f app.log")]
    [InlineData("grep 'ssh host tail -f' notes.md")]
    [InlineData("docker exec web tail -f app.log")]
    [InlineData("kubectl exec pod -- tail -f app.log")]
    [InlineData("flock /tmp/lock tail -f app.log")]
    [InlineData("script -q -c 'tail -f app.log' /dev/null")]
    [InlineData("su -c 'tail -f app.log'")]
    [InlineData("tmux new -d 'tail -f app.log'")]
    [InlineData("unknown-wrapper 'tail -f app.log'")]
    [InlineData("rg tail --follow docs")]
    public async Task Gs03_UninventoriedArguments_KeepTheWarning(string command)
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(ShellJson("Bash", command));

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("Open-ended Bash poll-loop detected");
    }

    [Theory]
    [InlineData("Bash", "cat <<'EOF' | bash\ntail -f app.log\nEOF")]
    [InlineData("Bash", "cat <<'EOF' | unknown-wrapper\ntail -f app.log\nEOF")]
    [InlineData("PowerShell", "Write-Output @'\ntail -f app.log\n'@ | Invoke-Expression")]
    [InlineData("PowerShell", "Write-Output @'\ntail -f app.log\n'@ | pwsh -Command -")]
    [InlineData("Bash", "echo @'\nx'; tail -f app.log; echo '\n'@")]
    [InlineData("Bash", "bash <<'OUT' | bash\ncat <<'IN'\ntail -f app.log\nIN\nOUT")]
    [InlineData("Bash", "bash <<'OUT' | unknown-wrapper\ncat <<'IN'\ntail -f app.log\nIN\nOUT")]
    [InlineData("Bash", "bash -c \"cat <<'IN'\ntail -f app.log\nIN\" | bash")]
    [InlineData("Bash", "bash -c \"cat <<'IN'\ntail -f app.log\nIN\" | unknown-wrapper")]
    public async Task Gs03_ContextBody_HookDenialAndRetry(string toolName, string command)
    {
        await SetupClaimedAgent();
        // The baseline has no newline advisory for the quoted -c unknown-consumer row;
        // its otherwise identical downstream bash row retains the advisory.
        var warning = command.StartsWith("bash -c", StringComparison.Ordinal)
            && command.EndsWith("| unknown-wrapper", StringComparison.Ordinal)
            ? "" : "WARNING: Command contains embedded newlines" + Environment.NewLine;
        const string nudge = "BLOCKED: Open-ended Bash poll-loop detected. Prefer a bounded for i in {1..30}; do ...; sleep 1; done, or `gh run watch`. Open-ended polls have caused agent crashes (issue 0177).";
        var expected = warning + nudge + Environment.NewLine + "  (Run the same command again to proceed anyway.)";
        var first = await GuardWithStdinAsync(ShellJson(toolName, command));
        Assert.Equal(expected + Environment.NewLine, first.Stderr);
        Assert.Equal(expected, first.AssertPreToolUseDenied());
        var retry = await GuardWithStdinAsync(ShellJson(toolName, command));
        retry.AssertPreToolUseAllowed();
        Assert.Equal(warning, retry.Stderr);
    }

    [Theory]
    [InlineData("Bash", "cat <<'EOF'\ntail -f app.log\nEOF")]
    [InlineData("PowerShell", "Write-Output @'\ntail -f app.log\n'@")]
    [InlineData("Bash", "echo @'\n$(tail -f app.log)\n'@")]
    [InlineData("Bash", "bash <<'OUT'\ncat <<'IN'\ntail -f app.log\nIN\nOUT")]
    [InlineData("Bash", "bash -c \"cat <<'IN'\ntail -f app.log\nIN\"")]
    public async Task Gs03_ContextBody_HookLiteralControl(string toolName, string command)
    {
        await SetupClaimedAgent();
        var result = await GuardWithStdinAsync(ShellJson(toolName, command));
        result.AssertPreToolUseAllowed();
        Assert.DoesNotContain("Open-ended Bash poll-loop", result.Stderr);
    }

    [Fact]
    public async Task Gs02_HereStringBoundary_RawWarningDenialAndRetryMatchMaster()
    {
        await SetupClaimedAgent();
        const string command = "Write-Output @'\ntext\n'@tail -f app.log";
        var warning = "WARNING: Command contains embedded newlines" + Environment.NewLine;
        const string nudge = "BLOCKED: Open-ended Bash poll-loop detected. Prefer a bounded for i in {1..30}; do ...; sleep 1; done, or `gh run watch`. Open-ended polls have caused agent crashes (issue 0177).";
        var expected = warning + nudge + Environment.NewLine + "  (Run the same command again to proceed anyway.)";

        var first = await GuardWithStdinAsync(ShellJson("PowerShell", command));

        Assert.Equal(expected + Environment.NewLine, first.Stderr);
        Assert.Equal(expected, first.AssertPreToolUseDenied());

        var retry = await GuardWithStdinAsync(ShellJson("PowerShell", command));

        retry.AssertPreToolUseAllowed();
        Assert.Equal(warning, retry.Stderr);
    }

    [Theory]
    [InlineData("H01-posix", "Bash", "echo @\"\n\" $(cat <<'IN'\ntail -f app.log\nIN\n) | bash; echo \"\n\"@", true)]
    [InlineData("H03-data", "Bash", "echo @\"\n$(cat <<'IN'\ntail -f app.log\nIN\n)\n\"@", false)]
    [InlineData("H04-child-quotes", "Bash", "echo @\"\n$(cat <<'IN'\nIt's \"ready\"; tail -f app.log\nIN\n)\n\"@", false)]
    [InlineData("H05-code", "Bash", "echo @\"\n$(cat <<'IN'\ntail -f app.log\nIN\n)\n\"@ | bash", true)]
    [InlineData("H06-unknown-consumer", "Bash", "echo @\"\n$(cat <<'IN'\ntail -f app.log\nIN\n)\n\"@ | unknown-wrapper", true)]
    [InlineData("H07-malformed", "PowerShell", "Write-Output @\"\n$(Write-Output @'\ntail -f app.log\n'@\n)\n\"@suffix", true)]
    [InlineData("H08-data", "PowerShell", "Write-Output @\"\n$(Write-Output @'\ntail -f app.log\n'@\n)\n\"@", false)]
    [InlineData("H09-child-quotes", "PowerShell", "Write-Output @\"\n$(Write-Output @'\nIt's \"ready\"; tail -f app.log\n'@\n)\n\"@", false)]
    [InlineData("H10-code", "PowerShell", "Write-Output @\"\n$(Write-Output @'\ntail -f app.log\n'@\n)\n\"@ | Invoke-Expression", true)]
    [InlineData("H11-unknown-consumer", "PowerShell", "Write-Output @\"\n$(Write-Output @'\ntail -f app.log\n'@\n)\n\"@ | unknown-wrapper", true)]
    [InlineData("H12-dash-suffix", "PowerShell", "Write-Output @\"\n$(Write-Output @'\ntail -f app.log\n'@\n)\n\"@-suffix", true)]
    [InlineData("H13-space", "PowerShell", "Write-Output @\"\n$(Write-Output @'\ntail -f app.log\n'@\n)\n\"@ ", false)]
    [InlineData("H13-tab", "PowerShell", "Write-Output @\"\n$(Write-Output @'\ntail -f app.log\n'@\n)\n\"@\t", false)]
    [InlineData("H14-later-launch", "PowerShell", "Write-Output @\"\n$(Write-Output @'\ntail -f app.log\n'@\n)\n\"@; tail -f real.log", true)]
    [InlineData("H15-backtick-data", "Bash", "echo @\"\n`cat <<'IN'\ntail -f app.log\nIN\n`\n\"@", false)]
    [InlineData("H15-backtick-code", "Bash", "echo @\"\n`cat <<'IN'\ntail -f app.log\nIN\n`\n\"@ | bash", true)]
    [InlineData("H16-identical-children", "Bash", "echo @\"\n$(cat <<'IN'\ntail -f app.log\nIN\n)\n$(cat <<'IN'\ntail -f app.log\nIN\n)\n\"@", false)]
    [InlineData("H17-sibling-quotes-unknown", "Bash", "echo @\"\n$(cat <<'IN'\nIt's \"ready\"; tail -f app.log\nIN\n)\n$(cat <<'IN'\ntail -f app.log\nIN\n)\n\"@ | unknown-wrapper", true)]
    [InlineData("H18-stdin-data", "Bash", "bash <<'OUT'\necho @\"\n$(cat <<'IN'\ntail -f app.log\nIN\n)\n\"@\nOUT", false)]
    [InlineData("H18-stdin-code", "Bash", "bash <<'OUT' | bash\necho @\"\n$(cat <<'IN'\ntail -f app.log\nIN\n)\n\"@\nOUT", true)]
    [InlineData("H18-stdin-unknown", "Bash", "bash <<'OUT' | unknown-wrapper\necho @\"\n$(cat <<'IN'\ntail -f app.log\nIN\n)\n\"@\nOUT", true)]
    [InlineData("H19-script-data", "Bash", "bash -c \"echo @\\\"\n\\$(cat <<'IN'\ntail -f app.log\nIN\n)\n\\\"@\"", false)]
    [InlineData("H19-script-code", "Bash", "bash -c \"echo @\\\"\n\\$(cat <<'IN'\ntail -f app.log\nIN\n)\n\\\"@\" | bash", true)]
    [InlineData("H20-script-ps-data", "Bash", "pwsh -Command \"Write-Output @\\\"\n\\$(Write-Output @'\ntail -f app.log\n'@\n)\n\\\"@\"", false)]
    [InlineData("H20-script-ps-unknown", "Bash", "pwsh -Command \"Write-Output @\\\"\n\\$(Write-Output @'\ntail -f app.log\n'@\n)\n\\\"@\" | unknown-wrapper", true)]
    [InlineData("H21-data-then-unsafe", "Bash", "echo @\"\n$(cat <<'IN'\ntail -f app.log\nIN\n)\n\"@\necho @\"\n\" $(cat <<'IN'\ntail -f app.log\nIN\n) | bash; echo \"\n\"@", true)]
    [InlineData("H21-unsafe-then-data", "Bash", "echo @\"\n\" $(cat <<'IN'\ntail -f app.log\nIN\n) | bash; echo \"\n\"@\necho @\"\n$(cat <<'IN'\ntail -f app.log\nIN\n)\n\"@", true)]
    [InlineData("H22-child-launch", "Bash", "echo @\"\n$(cat <<'IN'\ntail -f app.log\nIN\ntail -f real.log\n)\n\"@", true)]
    [InlineData("H23-child-launch-code", "Bash", "echo @\"\n$(cat <<'IN'\ntail -f app.log\nIN\ntail -f real.log\n)\n\"@ | bash", true)]
    [InlineData("H24-crlf-posix", "Bash", "echo @\"\r\n$(cat <<'IN'\r\ntail -f app.log\r\nIN\r\n)\r\n\"@", false)]
    [InlineData("H24-crlf-powershell", "PowerShell", "Write-Output @\"\r\n$(Write-Output @'\r\ntail -f app.log\r\n'@\r\n)\r\n\"@", false)]
    [InlineData("H25-capture-stdin-data", "Bash", "echo @\"\n$(bash <<'MID'\ncat <<'IN'\ntail -f app.log\nIN\nMID\n)\n\"@", false)]
    [InlineData("H25-capture-stdin-code", "Bash", "echo @\"\n$(bash <<'MID'\ncat <<'IN'\ntail -f app.log\nIN\nMID\n)\n\"@ | bash", true)]
    [InlineData("H26-hereword-data", "Bash", "cat <<< \"$(cat <<'IN'\nIt's \"ready\"; tail -f app.log\nIN\n)\"", false)]
    [InlineData("H26-hereword-code", "Bash", "cat <<< \"$(cat <<'IN'\nIt's \"ready\"; tail -f app.log\nIN\n)\" | bash", true)]
    [InlineData("H27-two-captures-data", "Bash", "cat <<OUTER\n$(cat <<MIDDLE\n$(cat <<'INNER'\ntail -f app.log\nINNER\n)\nMIDDLE\n)\nOUTER", false)]
    [InlineData("H27-two-captures-code", "Bash", "cat <<OUTER | bash\n$(cat <<MIDDLE\n$(cat <<'INNER'\ntail -f app.log\nINNER\n)\nMIDDLE\n)\nOUTER", true)]
    public async Task Gs03_HostEdge_HookFirstAndRetry(string id, string toolName, string command, bool deny)
    {
        await SetupClaimedAgent();
        var canary = Path.Combine(DydoDir, "index.md");
        var before = File.ReadAllBytes(canary);
        var payload = ShellJson(toolName, command);
        var first = await GuardWithStdinAsync(payload, "--host", "claude");
        var retry = await GuardWithStdinAsync(payload, "--host", "claude");
        Assert.Equal(before, File.ReadAllBytes(canary));
        retry.AssertPreToolUseAllowed();
        if (!deny)
        {
            first.AssertPreToolUseAllowed();
            Assert.Equal(first.Stderr, retry.Stderr);
            Assert.DoesNotContain("Open-ended Bash poll-loop", first.Stderr);
            return;
        }
        const string nudge = "BLOCKED: Open-ended Bash poll-loop detected. Prefer a bounded for i in {1..30}; do ...; sleep 1; done, or `gh run watch`. Open-ended polls have caused agent crashes (issue 0177).";
        var expected = retry.Stderr + nudge + Environment.NewLine
            + "  (Run the same command again to proceed anyway.)";
        Assert.Equal(id + ":" + expected, id + ":" + first.AssertPreToolUseDenied());
        Assert.Equal(expected + Environment.NewLine, first.Stderr);
    }

    // GS-01: a PowerShell variable assigned from a literal Get-Content is a read.
    [Theory]
    [InlineData(GuardRetroCommands.Gs01SlicedRead)]
    [InlineData(GuardRetroCommands.Gs01SelectStringRead)]
    [InlineData("$p = Get-Content dydo/index.md; $p[0..5]")]
    public async Task Gs01_LiteralReadAssignment_IsAllowed(string command)
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(ShellJson("PowerShell", command));

        result.AssertPreToolUseAllowed();
    }

    [Theory]
    [InlineData("$p = Get-Content notes.md; Set-Content notes.md $p")]
    [InlineData("$x = Remove-Item notes.md")]
    [InlineData("$p=Get-Content notes.md | Set-Content dydo/index.md")]
    [InlineData("$CMD dydo/files-off-limits.md")]
    [InlineData("$p = Get-Content .env")]
    public async Task Gs01_AssignmentNextToAMutationOrAGuardedRead_IsStillDenied(string command)
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(ShellJson("PowerShell", command));

        result.AssertPreToolUseDenied();
    }

    [Fact]
    public async Task CaseArm_MatchingExecutableArm_IsDeniedByHook()
    {
        await SetupClaimedAgent();
        const string command = "cat <<EOF\n$(case x in x) tail -f app.log;; esac)\nEOF";

        var result = await GuardWithStdinAsync(ShellJson("Bash", command));

        // The restored conservative file analysis refuses before command nudges run.
        Assert.Contains("Command contains bypass patterns", result.AssertPreToolUseDenied());
    }

    [Theory]
    [InlineData("x", "rm dydo/files-off-limits.md")]
    [InlineData("x", "echo changed > dydo/index.md")]
    [InlineData("x|y", "rm dydo/files-off-limits.md")]
    [InlineData("x|y", "echo changed > dydo/index.md")]
    public async Task CaseArm_ProtectedMutation_IsDeniedOnFirstAndRetry(string pattern, string mutation)
    {
        await SetupClaimedAgent();
        var paths = new[] { Path.Combine(DydoDir, "files-off-limits.md"), Path.Combine(DydoDir, "index.md") };
        var before = paths.Select(File.ReadAllBytes).ToArray();
        var command = "cat <<EOF\n$(case x in " + pattern + ") " + mutation + ";; esac)\nEOF";

        var first = await GuardWithStdinAsync(ShellJson("Bash", command));
        var retry = await GuardWithStdinAsync(ShellJson("Bash", command));

        first.AssertPreToolUseDenied();
        retry.AssertPreToolUseDenied();
        for (var i = 0; i < paths.Length; i++)
            Assert.Equal(before[i], File.ReadAllBytes(paths[i]));
    }

    // GS-05: a Python program on stdin is refused before it runs, so the protected canary keeps its bytes.
    [Theory]
    [InlineData("Bash", "python - <<'EOF'\nopen('dydo/files-off-limits.md','w').write('changed')\nEOF")]
    [InlineData("Bash", "printf \"open('dydo/files-off-limits.md','w').write('changed')\" | python3")]
    [InlineData("PowerShell", "@'\nopen('dydo/files-off-limits.md','w').write('changed')\n'@ | py -")]
    [InlineData("Bash", GuardRetroCommands.Gs05ProtectedRewrite)]
    [InlineData("Bash", "python3 < tools/rewrite.py")]
    public async Task Gs05_PythonOnStdin_WritingTheProtectedCanary_IsDeniedAndTheCanaryUnchanged(string toolName, string command)
    {
        await SetupClaimedAgent();
        var canary = Path.Combine(DydoDir, "files-off-limits.md");
        var before = File.ReadAllBytes(canary);

        var result = await GuardWithStdinAsync(ShellJson(toolName, command));

        var reason = result.AssertPreToolUseDenied();
        Assert.Contains("Python program on stdin", reason);
        Assert.Equal(before, File.ReadAllBytes(canary));
    }

    // A script file runs and its own file operations are not followed: the documented limit.
    [Fact]
    public async Task Gs05_PythonScriptFile_IsAllowed()
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(ShellJson("Bash", "python tools/rewrite.py"));

        result.AssertPreToolUseAllowed();
    }

    // Nothing feeds stdin, so no program is read from it: a lookup, xargs operands, a prompt.
    [Theory]
    [InlineData("command -v python3")]
    [InlineData("command -V python3")]
    [InlineData("command -v python3 || command -v python")]
    [InlineData("ls *.py | xargs python3")]
    [InlineData("python")]
    public async Task Gs05_PythonWithNothingFedToIt_IsAllowed(string command)
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(ShellJson("Bash", command));

        result.AssertPreToolUseAllowed();
    }

    // GS-02: every word eval, ssh or watch joins into the code it runs is code, and so is a line a
    // shell reads from a pipe, so a dynamic write in any of them is still denied.
    [Theory]
    [InlineData("eval 'echo x' '> $OUT'")]
    [InlineData("eval echo 'x > $OUT'")]
    [InlineData("ssh host 'echo x' '> $OUT'")]
    [InlineData("watch -n 1 'echo x > $OUT'")]
    [InlineData("printf 'echo x > $OUT' | bash")]
    public async Task Gs02_DynamicWrite_InAnyWordACommandRuns_IsDenied(string command)
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(ShellJson("Bash", command));

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("bypass");
    }

    // GS-05: a < or | in a comment feeds nothing.
    [Theory]
    [InlineData("python # <stdin>")]
    [InlineData("python # input < program.py")]
    [InlineData("python # input | python")]
    public async Task Gs05_PythonWithAFeedOnlyInAComment_IsAllowed(string command)
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(ShellJson("Bash", command));

        result.AssertPreToolUseAllowed();
    }

    // A real feed is still refused after a comment, into a group, and inside a substitution.
    [Theory]
    [InlineData("printf \"open('dydo/files-off-limits.md','w').write('changed')\" | python3 # feeds it")]
    [InlineData("echo \"$(printf \"open('dydo/files-off-limits.md','w').write('changed')\" | python3)\"")]
    [InlineData("printf \"open('dydo/files-off-limits.md','w').write('changed')\" | (python3)")]
    [InlineData("printf \"open('dydo/files-off-limits.md','w').write('changed')\" | { python3; }")]
    public async Task Gs05_PythonFedThroughACommentGroupOrSubstitution_IsDeniedAndTheCanaryUnchanged(string command)
    {
        await SetupClaimedAgent();
        var canary = Path.Combine(DydoDir, "files-off-limits.md");
        var before = File.ReadAllBytes(canary);

        var result = await GuardWithStdinAsync(ShellJson("Bash", command));

        var reason = result.AssertPreToolUseDenied();
        Assert.Contains("Python program on stdin", reason);
        Assert.Equal(before, File.ReadAllBytes(canary));
    }

    // GS-02: a runner named only in a comment or an argument runs no body, and the blank or
    // comment-only lines after a heredoc separate no commands.
    [Theory]
    [InlineData("Bash", "cat > helper.sh <<'EOF' # bash script\necho hello\nEOF")]
    [InlineData("Bash", "grep -c bash <<'EOF'\nline one\nline two\nEOF")]
    [InlineData("Bash", "cat > /tmp/python <<'EOF'\n$HOME\nEOF")]
    [InlineData("PowerShell", "Set-Content -LiteralPath notes.md -Value @'\n$HOME\n'@ # bash example")]
    [InlineData("Bash", "cat > notes.md <<'EOF'\n$HOME\nEOF\n\n")]
    [InlineData("Bash", "cat > notes.md <<'EOF'\nplain\nEOF\n\n# written\n")]
    public async Task Gs02_LiteralBody_WithARunnerInACommentOrTrailingBlankLines_IsAllowed(string toolName, string command)
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(ShellJson(toolName, command));

        result.AssertPreToolUseAllowed();
    }

    [Theory]
    [InlineData("Bash", "bash <<'EOF' # bash script\necho x > $OUT\nEOF")]
    [InlineData("Bash", "cat <<'EOF' | bash # bash script\necho x > $OUT\nEOF")]
    [InlineData("PowerShell", "@'\necho x > $OUT\n'@ | pwsh -Command - # runs it")]
    [InlineData("Bash", "cat > notes.md <<'EOF'\nplain\nEOF\n\nrm secret.txt")]
    public async Task Gs02_BodyARunnerIsFed_OrACommandAfterABlankLine_IsStillDenied(string toolName, string command)
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(ShellJson(toolName, command));

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("bypass");
    }

    // Round 4: a body is data only when data commands alone read it; a quoted or pathed runner, a
    // stdin source, a group piped on, a later line of code and a comment's apostrophe hide nothing.
    [Theory]
    [InlineData("Bash", "cat > notes.md <<'EOF' # don't expand\nplain\nEOF\nrm dydo/index.md")]
    [InlineData("Bash", "\"C:/Program Files/Git/bin/bash.exe\" <<'EOF'\necho y > dydo/index.md\nEOF")]
    [InlineData("Bash", "\"bash\" <<'EOF'\necho y > dydo/index.md\nEOF")]
    [InlineData("Bash", ". /dev/stdin <<'EOF'\necho y > dydo/index.md\nEOF")]
    [InlineData("Bash", "fish <<'EOF'\necho y > dydo/index.md\nEOF")]
    [InlineData("Bash", "cat <<'EOF' | \"C:/Program Files/Git/bin/bash.exe\"\necho y > dydo/index.md\nEOF")]
    [InlineData("Bash", "cat <<'EOF' |\necho y > dydo/index.md\nEOF\nbash")]
    [InlineData("Bash", "{\ncat <<'EOF'\necho y > dydo/index.md\nEOF\n} | bash")]
    [InlineData("Bash", "cat <<'EOF' > >(bash)\necho y > dydo/index.md\nEOF")]
    [InlineData("Bash", "echo hi # don't\nrm 'dydo/index.md'; echo x > notes.md")]
    [InlineData("PowerShell", "@'\necho y > dydo/index.md\n'@ | & \"C:\\Program Files\\Git\\bin\\bash.exe\"")]
    [InlineData("PowerShell", "@'\necho y > dydo/index.md\n'@ | wsl bash")]
    [InlineData("PowerShell", "@'\necho y > dydo/index.md\n'@\n| iex")]
    [InlineData("PowerShell", "@'\nplain\n'@ | Set-Content notes.md # don't\nRemove-Item 'dydo/index.md'")]
    public async Task Gs02_ProtectedChangeABodyOrALaterLineHolds_IsDenied(string toolName, string command)
    {
        await SetupClaimedAgent();
        var canary = Path.Combine(DydoDir, "index.md");
        var before = File.ReadAllBytes(canary);

        var result = await GuardWithStdinAsync(ShellJson(toolName, command));

        result.AssertPreToolUseDenied();
        Assert.Equal(before, File.ReadAllBytes(canary));
    }

    [Theory]
    [InlineData("Bash", "cat > notes.md <<'END-OF-FILE'\nplain\nEND-OF-FILE")]
    [InlineData("Bash", "cat > notes.md <<\"END_OF_FILE\"\nplain\nEND_OF_FILE")]
    [InlineData("Bash", "cat > notes.md <<-'END-OF-FILE'\n\tplain\n\tEND-OF-FILE")]
    [InlineData("Bash", "cat > notes.md <<'EOF' # don't expand\nplain $HOME\nEOF")]
    [InlineData("PowerShell", "@'\nIt's ready.\n'@ | Set-Content notes.md")]
    public async Task Gs02_LiteralBodyOnlyDataCommandsRead_IsAllowed(string toolName, string command)
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(ShellJson(toolName, command));

        result.AssertPreToolUseAllowed();
    }

    [Theory]
    [InlineData("echo \"$(python3 tools/rewrite.py)\"")]
    [InlineData("printf x | (python3 tools/rewrite.py)")]
    public async Task Gs05_PythonScriptFileInAGroupOrSubstitution_IsAllowed(string command)
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(ShellJson("Bash", command));

        result.AssertPreToolUseAllowed();
    }
}
