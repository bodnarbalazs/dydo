// @test-tier: 2
namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;

/// <summary>
/// T2 tests for worktree allow JSON on Bash and Write operations.
/// The guard currently only emits permissionDecision:allow for Read operations
/// in worktree contexts (GuardCommand.cs line 365-366). These tests verify that
/// the same allow JSON is emitted for Bash (dydo commands) and Write/Edit
/// operations when in a worktree context and the operation is permitted.
///
/// These tests are expected to FAIL against current code (red phase TDD).
/// The fix will extend WorktreeReadAllowJson output to HandleDydoBashCommand
/// and HandleWriteOperation.
/// </summary>
[Collection("Integration")]
public class GuardWorktreeAllowBashWriteTests : IntegrationTestBase
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

    #region Bash: Worktree Allow

    [Fact]
    public async Task WorktreeBash_DydoCommand_Approved_OutputsAllowJson()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");
        await ReadMustReadsAsync();
        GuardCommand.IsWorktreeContextOverride = () => true;

        var json = $"{{\"session_id\":\"{TestSessionId}\",\"tool_name\":\"Bash\",\"tool_input\":{{\"command\":\"dydo agent status\"}}}}";
        var result = await GuardWithStdinAsync(json);

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

        var json = $"{{\"session_id\":\"{TestSessionId}\",\"tool_name\":\"Bash\",\"tool_input\":{{\"command\":\"dydo agent status\"}}}}";
        var result = await GuardWithStdinAsync(json);

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

        // dydo guard lift is a human-only command — agents cannot run it
        var json = $"{{\"session_id\":\"{TestSessionId}\",\"tool_name\":\"Bash\",\"tool_input\":{{\"command\":\"dydo guard lift\"}}}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertExitCode(2);
        Assert.DoesNotContain(AllowJson, result.Stdout);
    }

    #endregion
}
