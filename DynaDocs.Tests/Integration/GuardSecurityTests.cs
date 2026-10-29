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
    [InlineData("git push --force-with-lease origin main")]
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
    [InlineData("bash -c 'git push --force-with-lease origin main'")]
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

    // DYD-421 DECIDED: single-quoted text is searched for bypasses as on master, so these
    // single-quoted GS-02 writes stay refused until DYD-422 designs a relaxation.
    [Theory]
    [InlineData("Bash", GuardRetroCommands.Gs02SedBacktickCommit)]
    [InlineData("Bash", GuardRetroCommands.Gs02SedBacktickSingleLine)]
    [InlineData("Bash", "sed -i 's/`x`/y/' f")]
    [InlineData("Bash", "printf '%s' 'first line\n$HOME is literal' > notes.md")]
    [InlineData("PowerShell", "Set-Content notes.md 'first line\n$HOME is literal'")]
    public async Task Gs02_SingleQuotedDataWrite_IsDeniedAsOnMaster(string toolName, string command)
    {
        await SetupClaimedAgent();

        var result = await GuardWithStdinAsync(ShellJson(toolName, command));

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("bypass");
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
