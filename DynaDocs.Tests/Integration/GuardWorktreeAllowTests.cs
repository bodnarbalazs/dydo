// @test-tier: 2
namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;

/// <summary>
/// T2 tests for the worktree Read allow decision.
/// When the guard approves a Read in a worktree context, it outputs an explicit
/// allow JSON to stdout so Claude Code skips the permission prompt.
/// </summary>
[Collection("Integration")]
public class GuardWorktreeAllowTests : IntegrationTestBase
{
    private const string AllowJson =
        """{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"allow"}}""";

    private readonly Func<bool>? _originalOverride = GuardCommand.IsWorktreeContextOverride;

#pragma warning disable xUnit1013
    public new void Dispose()
    {
        GuardCommand.IsWorktreeContextOverride = _originalOverride;
        base.Dispose();
    }
#pragma warning restore xUnit1013

    #region Core Behavior

    [Fact]
    public async Task WorktreeRead_Approved_OutputsAllowJson()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");
        await ReadMustReadsAsync();
        GuardCommand.IsWorktreeContextOverride = () => true;

        var result = await GuardAsync("read", "src/test.cs");

        result.AssertSuccess();
        result.AssertStdoutContains(AllowJson);
    }

    [Fact]
    public async Task NonWorktreeRead_Approved_StdoutEmpty()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");
        await ReadMustReadsAsync();
        GuardCommand.IsWorktreeContextOverride = () => false;

        var result = await GuardAsync("read", "src/test.cs");

        result.AssertSuccess();
        Assert.Empty(result.Stdout.Trim());
    }

    [Fact]
    public async Task WorktreeRead_Blocked_NoAllowJson()
    {
        await InitProjectAsync("none", "testuser", 3);
        // No agent claimed — read of non-bootstrap file should be blocked
        GuardCommand.IsWorktreeContextOverride = () => true;

        var result = await GuardAsync("read", "src/test.cs");

        result.AssertExitCode(2);
        Assert.DoesNotContain(AllowJson, result.Stdout);
    }

    #endregion

    #region Scope: All Permitted Operations

    [Fact]
    public async Task WorktreeWrite_Approved_OutputsAllowJson()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");
        await ReadMustReadsAsync();
        GuardCommand.IsWorktreeContextOverride = () => true;

        var result = await GuardAsync("edit", "src/test.cs");

        result.AssertSuccess();
        result.AssertStdoutContains(AllowJson);
    }

    [Fact]
    public async Task WorktreeBash_Approved_OutputsAllowJson()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");
        await ReadMustReadsAsync();
        GuardCommand.IsWorktreeContextOverride = () => true;

        var json = $"{{\"session_id\":\"{TestSessionId}\",\"tool_name\":\"Bash\",\"tool_input\":{{\"command\":\"dydo whoami\"}}}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertSuccess();
        result.AssertStdoutContains(AllowJson);
    }

    [Fact]
    public async Task WorktreeGlob_Approved_OutputsAllowJson()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");
        await ReadMustReadsAsync();
        GuardCommand.IsWorktreeContextOverride = () => true;

        var json = $"{{\"session_id\":\"{TestSessionId}\",\"tool_name\":\"Glob\",\"tool_input\":{{\"pattern\":\"**/*.cs\"}}}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertSuccess();
        result.AssertStdoutContains(AllowJson);
    }

    [Fact]
    public async Task WorktreeGrep_Approved_OutputsAllowJson()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");
        await ReadMustReadsAsync();
        GuardCommand.IsWorktreeContextOverride = () => true;

        var json = $"{{\"session_id\":\"{TestSessionId}\",\"tool_name\":\"Grep\",\"tool_input\":{{\"pattern\":\"foo\",\"path\":\"src\"}}}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertSuccess();
        result.AssertStdoutContains(AllowJson);
    }

    [Fact]
    public async Task NonWorktreeGlob_Approved_StdoutEmpty()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");
        await ReadMustReadsAsync();
        GuardCommand.IsWorktreeContextOverride = () => false;

        var json = $"{{\"session_id\":\"{TestSessionId}\",\"tool_name\":\"Glob\",\"tool_input\":{{\"pattern\":\"**/*.cs\"}}}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertSuccess();
        Assert.Empty(result.Stdout.Trim());
    }

    #endregion

    #region Security: Blocked Reads Still Block

    [Fact]
    public async Task WorktreeWrite_OffLimitsPath_Blocked_NoAllowJson()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");
        await ReadMustReadsAsync();
        GuardCommand.IsWorktreeContextOverride = () => true;

        // Off-limits blocks writes to .env — verify no allow JSON leaks into stdout
        var result = await GuardAsync("edit", ".env");

        result.AssertExitCode(2);
        result.AssertStderrContains("off-limits");
        Assert.DoesNotContain(AllowJson, result.Stdout);
    }

    [Fact]
    public async Task WorktreeRead_NoIdentity_Blocked()
    {
        await InitProjectAsync("none", "testuser", 3);
        // No agent claimed
        GuardCommand.IsWorktreeContextOverride = () => true;

        var result = await GuardAsync("read", "src/code.cs");

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
        Assert.DoesNotContain(AllowJson, result.Stdout);
    }

    [Fact]
    public async Task WorktreeRead_NoRole_NonBootstrapFile_Blocked()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        // No role set
        GuardCommand.IsWorktreeContextOverride = () => true;

        var result = await GuardAsync("read", "src/code.cs");

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
        Assert.DoesNotContain(AllowJson, result.Stdout);
    }

    #endregion

    #region Staged Access: Bootstrap Reads in Worktree

    [Fact]
    public async Task WorktreeRead_BootstrapFile_NoIdentity_AllowsWithJson()
    {
        await InitProjectAsync("none", "testuser", 3);
        // No agent — but CLAUDE.md is a bootstrap file (always readable)
        GuardCommand.IsWorktreeContextOverride = () => true;

        var result = await GuardAsync("read", "CLAUDE.md");

        result.AssertSuccess();
        result.AssertStdoutContains(AllowJson);
    }

    [Fact]
    public async Task WorktreeRead_ModeFile_ClaimedNoRole_AllowsWithJson()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        // Claimed but no role — mode files are readable at Stage 1
        GuardCommand.IsWorktreeContextOverride = () => true;

        var result = await GuardAsync("read", "dydo/agents/Adele/modes/code-writer.md");

        result.AssertSuccess();
        result.AssertStdoutContains(AllowJson);
    }

    #endregion

    #region CWD Detection Logic

    [Fact]
    public void IsWorktreeContext_WithWorktreePathInCwd_ReturnsTrue()
    {
        GuardCommand.IsWorktreeContextOverride = null;
        var worktreeDir = Path.Combine(TestDir, "dydo", "_system", ".local", "worktrees", "test-task");
        Directory.CreateDirectory(worktreeDir);
        Environment.CurrentDirectory = worktreeDir;

        Assert.True(GuardCommand.IsWorktreeContext());
    }

    [Fact]
    public void IsWorktreeContext_WithNormalCwd_ReturnsFalse()
    {
        GuardCommand.IsWorktreeContextOverride = null;
        // TestDir is a normal temp path, no worktree marker
        Assert.False(GuardCommand.IsWorktreeContext());
    }

    [Fact]
    public void IsWorktreeContext_WithNestedWorktreePath_ReturnsTrue()
    {
        GuardCommand.IsWorktreeContextOverride = null;
        var nestedDir = Path.Combine(TestDir, "dydo", "_system", ".local", "worktrees", "parent", "child");
        Directory.CreateDirectory(nestedDir);
        Environment.CurrentDirectory = nestedDir;

        Assert.True(GuardCommand.IsWorktreeContext());
    }

    [Fact]
    public void IsWorktreeContext_WithBackslashPath_ReturnsTrue()
    {
        GuardCommand.IsWorktreeContextOverride = null;
        // On Windows, paths use backslashes — detection should normalize
        var worktreeDir = Path.Combine(TestDir, "dydo", "_system", ".local", "worktrees", "backslash-test");
        Directory.CreateDirectory(worktreeDir);
        Environment.CurrentDirectory = worktreeDir;

        Assert.True(GuardCommand.IsWorktreeContext());
    }

    [Fact]
    public void IsWorktreeContext_UnanchoredSubstringMatch_ReturnsFalse()
    {
        GuardCommand.IsWorktreeContextOverride = null;
        // A directory whose name starts with "worktrees" but isn't a real worktree
        // (e.g., worktrees-notes, worktrees.backup). The marker is a substring of
        // the path, but not an exact path segment — must NOT be treated as a worktree.
        var lookalikeDir = Path.Combine(TestDir, "dydo", "_system", ".local", "worktrees-notes", "scratch");
        Directory.CreateDirectory(lookalikeDir);
        Environment.CurrentDirectory = lookalikeDir;

        Assert.False(GuardCommand.IsWorktreeContext());
    }

    [Fact]
    public void IsWorktreeContext_SiblingWorktreesBackup_ReturnsFalse()
    {
        GuardCommand.IsWorktreeContextOverride = null;
        var backupDir = Path.Combine(TestDir, "dydo", "_system", ".local", "worktrees.backup", "bar");
        Directory.CreateDirectory(backupDir);
        Environment.CurrentDirectory = backupDir;

        Assert.False(GuardCommand.IsWorktreeContext());
    }

    #endregion
}
