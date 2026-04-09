// @test-tier: 2
namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;
using DynaDocs.Services;

/// <summary>
/// T2 tests for worktree allow JSON on Bash and Write operations.
/// The guard must emit permissionDecision:allow for all approved operations
/// in worktree contexts — not just Reads — so Claude Code skips its own
/// permission prompt for worktree-resolved paths.
/// </summary>
[Collection("Integration")]
public class GuardWorktreeAllowBashWriteTests : IntegrationTestBase
{
    private const string AllowJson =
        """{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"allow"}}""";

    private readonly Func<bool>? _originalOverride = GuardCommand.IsWorktreeContextOverride;

    private string BashJson(string command) =>
        "{\"session_id\":\"" + TestSessionId + "\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"" +
        command.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"}}";

#pragma warning disable xUnit1013
    public new void Dispose()
    {
        GuardCommand.IsWorktreeContextOverride = _originalOverride;
        base.Dispose();
    }
#pragma warning restore xUnit1013

    #region Bash: Dydo Commands

    [Fact]
    public async Task WorktreeBash_DydoCommand_Approved_OutputsAllowJson()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");
        await ReadMustReadsAsync();
        GuardCommand.IsWorktreeContextOverride = () => true;

        var result = await GuardWithStdinAsync(BashJson("dydo agent status"));

        result.AssertSuccess();
        result.AssertStdoutContains(AllowJson);
    }

    [Fact]
    public async Task NonWorktreeBash_DydoCommand_Approved_NoAllowJson()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");
        await ReadMustReadsAsync();
        GuardCommand.IsWorktreeContextOverride = () => false;

        var result = await GuardWithStdinAsync(BashJson("dydo agent status"));

        result.AssertSuccess();
        Assert.DoesNotContain("permissionDecision", result.Stdout);
    }

    #endregion

    #region Bash: Non-Dydo Commands

    [Fact]
    public async Task WorktreeBash_NonDydoCommand_Approved_OutputsAllowJson()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");
        await ReadMustReadsAsync();
        GuardCommand.IsWorktreeContextOverride = () => true;

        var result = await GuardWithStdinAsync(BashJson("git status"));

        result.AssertSuccess();
        result.AssertStdoutContains(AllowJson);
    }

    [Fact]
    public async Task NonWorktreeBash_NonDydoCommand_Approved_NoAllowJson()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");
        await ReadMustReadsAsync();
        GuardCommand.IsWorktreeContextOverride = () => false;

        var result = await GuardWithStdinAsync(BashJson("git status"));

        result.AssertSuccess();
        Assert.DoesNotContain("permissionDecision", result.Stdout);
    }

    #endregion

    #region Write/Edit: Worktree Allow

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
    public async Task NonWorktreeWrite_Approved_NoAllowJson()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");
        await ReadMustReadsAsync();
        GuardCommand.IsWorktreeContextOverride = () => false;

        var result = await GuardAsync("edit", "src/test.cs");

        result.AssertSuccess();
        Assert.Empty(result.Stdout.Trim());
    }

    [Fact]
    public async Task WorktreeWrite_GuardLifted_OutputsAllowJson()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");
        await ReadMustReadsAsync();
        GuardCommand.IsWorktreeContextOverride = () => true;

        // Lift the guard so the RBAC-skip path is exercised
        var liftService = new GuardLiftService(TestDir);
        liftService.Lift("Adele", "testuser", minutes: 5);

        var result = await GuardAsync("edit", "src/test.cs");

        result.AssertSuccess();
        result.AssertStdoutContains(AllowJson);
    }

    #endregion

    #region Security: Blocked Operations Never Emit Allow

    [Fact]
    public async Task WorktreeWrite_OffLimits_Blocked_NoAllowJson()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");
        await ReadMustReadsAsync();
        GuardCommand.IsWorktreeContextOverride = () => true;

        var result = await GuardAsync("edit", ".env");

        result.AssertExitCode(2);
        Assert.DoesNotContain(AllowJson, result.Stdout);
    }

    [Fact]
    public async Task WorktreeBash_HumanOnlyCommand_Blocked_NoAllowJson()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");
        await ReadMustReadsAsync();
        GuardCommand.IsWorktreeContextOverride = () => true;

        var result = await GuardWithStdinAsync(BashJson("dydo guard lift"));

        result.AssertExitCode(2);
        Assert.DoesNotContain(AllowJson, result.Stdout);
    }

    [Fact]
    public async Task WorktreeBash_DangerousCommand_Blocked_NoAllowJson()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");
        await ReadMustReadsAsync();
        GuardCommand.IsWorktreeContextOverride = () => true;

        var result = await GuardWithStdinAsync(BashJson("rm -rf /"));

        result.AssertExitCode(2);
        Assert.DoesNotContain(AllowJson, result.Stdout);
    }

    #endregion
}
