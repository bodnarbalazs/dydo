namespace DynaDocs.Tests.Integration;

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
    [InlineData("bash -c \"rm -rf /tmp/secrets\"")]
    [InlineData("sh -c \"cat /etc/passwd\"")]
    [InlineData("zsh -c \"echo pwned > file.txt\"")]
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
}
