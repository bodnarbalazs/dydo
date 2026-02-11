namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;
using DynaDocs.Services;

/// <summary>
/// Integration tests for the guard command.
/// Tests security layers: off-limits, role permissions, bash analysis.
/// </summary>
[Collection("Integration")]
public class GuardIntegrationTests : IntegrationTestBase
{
    #region Off-Limits

    [Fact]
    public async Task Guard_OffLimitsPath_Blocks()
    {
        await InitProjectAsync("none", "balazs", 3);

        // .env is off-limits by default
        var result = await GuardAsync("edit", ".env");

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
        result.AssertStderrContains("off-limits");
    }

    [Fact]
    public async Task Guard_DydoSystemFile_Blocks()
    {
        await InitProjectAsync("none", "balazs", 3);

        // dydo/index.md is a system file
        var result = await GuardAsync("edit", "dydo/index.md");

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
    }

    [Fact]
    public async Task Guard_AgentStateMd_Blocks()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Agent state files are off-limits
        var result = await GuardAsync("edit", "dydo/agents/Adele/state.md");

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
    }

    [Fact]
    public async Task Guard_AllowedPath_Passes()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");
        await ReadMustReadsAsync();

        // src/file.cs is typically allowed for code-writer
        var result = await GuardAsync("edit", "src/file.cs");

        result.AssertSuccess();
    }

    #endregion

    #region Role Permissions (Writes)

    [Fact]
    public async Task Guard_NoAgent_BlocksWrite()
    {
        await InitProjectAsync("none", "balazs", 3);
        // Don't claim an agent

        var result = await GuardAsync("edit", "src/file.cs");

        // Should block writes without identity (fail-closed)
        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
        result.AssertStderrContains("No agent");
    }

    [Fact]
    public async Task Guard_NoRole_BlocksWrite()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        // Don't set a role

        var result = await GuardAsync("edit", "src/file.cs");

        // Should block writes without role (fail-closed)
        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
        result.AssertStderrContains("no role");
    }

    [Fact]
    public async Task Guard_RoleViolation_Blocks()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("reviewer"); // Reviewers can only read, not write
        await ReadMustReadsAsync();

        // Try to write outside allowed paths
        var result = await GuardAsync("edit", "src/code.cs");

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
    }

    #endregion

    #region Stage 0 - No Identity (Bootstrap Only)

    [Fact]
    public async Task Guard_NoIdentity_ReadSourceFile_Blocks()
    {
        await InitProjectAsync("none", "balazs", 3);
        // Don't claim an agent

        var result = await GuardAsync("read", "src/test.cs");

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
    }

    [Fact]
    public async Task Guard_NoIdentity_ReadRootFile_Allows()
    {
        await InitProjectAsync("none", "balazs", 3);
        // Don't claim an agent

        // Root-level files are bootstrap files
        var result = await GuardAsync("read", "CLAUDE.md");

        result.AssertSuccess();
    }

    [Fact]
    public async Task Guard_NoIdentity_ReadWorkflow_Allows()
    {
        await InitProjectAsync("none", "balazs", 3);
        // Don't claim an agent

        // workflow.md is a bootstrap file
        var result = await GuardAsync("read", "dydo/agents/Adele/workflow.md");

        result.AssertSuccess();
    }

    [Fact]
    public async Task Guard_NoIdentity_ReadIndex_Allows()
    {
        await InitProjectAsync("none", "balazs", 3);
        // Don't claim an agent

        // dydo/index.md is a bootstrap file
        var result = await GuardAsync("read", "dydo/index.md");

        result.AssertSuccess();
    }

    [Fact]
    public async Task Guard_NoIdentity_WriteRootFile_Blocks()
    {
        await InitProjectAsync("none", "balazs", 3);
        // Don't claim an agent

        // Even bootstrap files can't be written without identity
        var result = await GuardAsync("write", "CLAUDE.md");

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
    }

    #endregion

    #region Stage 1 - Identity Claimed (No Role)

    [Fact]
    public async Task Guard_IdentityNoRole_ReadModeFile_Allows()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        // Don't set a role

        // Mode files for claimed agent are readable
        var result = await GuardAsync("read", "dydo/agents/Adele/modes/code-writer.md");

        result.AssertSuccess();
    }

    [Fact]
    public async Task Guard_IdentityNoRole_ReadOtherAgentModeFile_Blocks()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        // Don't set a role

        // Can't read other agent's mode files without a role
        var result = await GuardAsync("read", "dydo/agents/Brian/modes/code-writer.md");

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
    }

    [Fact]
    public async Task Guard_IdentityNoRole_ReadSourceFile_Blocks()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        // Don't set a role

        // Source files need a role to read
        var result = await GuardAsync("read", "src/test.cs");

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
    }

    #endregion

    #region Stage 2 - Identity + Role

    [Fact]
    public async Task Guard_IdentityWithRole_ReadSourceFile_Allows()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");

        // With identity and role, all reads allowed (except off-limits)
        var result = await GuardAsync("read", "src/code.cs");

        result.AssertSuccess();
    }

    [Fact]
    public async Task Guard_IdentityWithRole_ReadOtherAgentFiles_Allows()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");

        // With role set, can read other agents' mode files too
        var result = await GuardAsync("read", "dydo/agents/Brian/modes/code-writer.md");

        result.AssertSuccess();
    }

    #endregion

    #region Stdin Hook Mode

    [Fact]
    public async Task Guard_StdinHook_ReadWithoutIdentity_Blocks()
    {
        await InitProjectAsync("none", "balazs", 3);
        // Don't claim an agent

        var json = "{\"session_id\":\"" + TestSessionId + "\",\"tool_name\":\"Read\",\"tool_input\":{\"file_path\":\"src/test.cs\"}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
    }

    [Fact]
    public async Task Guard_StdinHook_ReadBootstrapFile_Allows()
    {
        await InitProjectAsync("none", "balazs", 3);
        // Don't claim an agent

        var json = "{\"session_id\":\"" + TestSessionId + "\",\"tool_name\":\"Read\",\"tool_input\":{\"file_path\":\"CLAUDE.md\"}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertSuccess();
    }

    [Fact]
    public async Task Guard_StdinHook_WriteWithIdentityAndRole_UsesRbac()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");
        await ReadMustReadsAsync();

        var json = "{\"session_id\":\"" + TestSessionId + "\",\"tool_name\":\"Write\",\"tool_input\":{\"file_path\":\"src/test.cs\"}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertSuccess();
    }

    [Fact]
    public async Task Guard_StdinHook_WriteWithoutIdentity_Blocks()
    {
        await InitProjectAsync("none", "balazs", 3);
        // Don't claim an agent

        var json = "{\"session_id\":\"" + TestSessionId + "\",\"tool_name\":\"Write\",\"tool_input\":{\"file_path\":\"src/test.cs\"}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
    }

    #endregion

    #region Bash Commands (Hook Mode)

    // Note: Bash command analysis in guard requires hook mode (stdin JSON with toolName="bash")
    // The --command CLI flag alone doesn't trigger bash analysis because toolName isn't set
    // These tests verify the basic CLI flag parsing, not full bash analysis

    [Fact]
    public async Task Guard_CommandOption_Parses()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");

        // CLI --command option is accepted (though bash analysis requires hook mode)
        var cmd = GuardCommand.Create();
        var result = await RunAsync(cmd, "--command", "dotnet build");

        // Without toolName=bash from stdin, bash analysis is skipped
        result.AssertSuccess();
    }

    #endregion

    #region Must-Read Enforcement

    [Fact]
    public async Task Guard_BlocksWriteWhenMustReadsRemain()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        // Templates already have must-read: true on about.md, architecture.md, coding-standards.md
        await SetRoleAsync("code-writer", "test-task");

        // Attempt a write — should be blocked since must-reads haven't been read
        var result = await GuardAsync("edit", "src/file.cs");

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
        result.AssertStderrContains("not read the required files");
        // Should list specific unread files
        result.AssertStderrContains("about.md");
    }

    [Fact]
    public async Task Guard_AllowsWriteAfterAllMustReadsRead()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "test-task");

        // Read all must-read files
        var registry = new AgentRegistry(TestDir);
        var state = registry.GetCurrentAgent(TestSessionId);
        Assert.NotNull(state);
        Assert.True(state.UnreadMustReads.Count > 0, "Should have must-reads after SetRole");

        foreach (var mustRead in state.UnreadMustReads.ToList())
        {
            await GuardAsync("read", mustRead);
        }

        // Now write should succeed
        var result = await GuardAsync("edit", "src/file.cs");
        result.AssertSuccess();
    }

    [Fact]
    public async Task Guard_TracksReadOfMustReadFile()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "test-task");

        var registry = new AgentRegistry(TestDir);
        var stateBefore = registry.GetCurrentAgent(TestSessionId);
        Assert.NotNull(stateBefore);
        var countBefore = stateBefore.UnreadMustReads.Count;
        Assert.True(countBefore > 0);

        // Read a must-read file
        var firstMustRead = stateBefore.UnreadMustReads.First();
        await GuardAsync("read", firstMustRead);

        // Re-read state — should have one fewer unread
        var registryAfter = new AgentRegistry(TestDir);
        var stateAfter = registryAfter.GetCurrentAgent(TestSessionId);
        Assert.NotNull(stateAfter);
        Assert.Equal(countBefore - 1, stateAfter.UnreadMustReads.Count);
        Assert.DoesNotContain(stateAfter.UnreadMustReads,
            p => p.Equals(firstMustRead, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Guard_AllowsReadsAlways()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "test-task");

        // Verify must-reads exist (writes would be blocked)
        var registry = new AgentRegistry(TestDir);
        var state = registry.GetCurrentAgent(TestSessionId);
        Assert.NotNull(state);
        Assert.True(state.UnreadMustReads.Count > 0);

        // Read a non-must-read file — should be allowed even with unread must-reads
        var result = await GuardAsync("read", "src/some-file.cs");

        result.AssertSuccess();
    }

    [Fact]
    public async Task Guard_TracksReadOfMustReadFile_WithAbsolutePath()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "test-task");

        var registry = new AgentRegistry(TestDir);
        var stateBefore = registry.GetCurrentAgent(TestSessionId);
        Assert.NotNull(stateBefore);
        var countBefore = stateBefore.UnreadMustReads.Count;

        // Read a must-read file using its absolute path (as the hook would provide)
        var firstMustRead = stateBefore.UnreadMustReads.First();
        var absolutePath = Path.Combine(TestDir, firstMustRead.Replace('/', Path.DirectorySeparatorChar));
        await GuardAsync("read", absolutePath);

        // Should still track it despite the path format difference
        var registryAfter = new AgentRegistry(TestDir);
        var stateAfter = registryAfter.GetCurrentAgent(TestSessionId);
        Assert.NotNull(stateAfter);
        Assert.Equal(countBefore - 1, stateAfter.UnreadMustReads.Count);
    }

    #endregion
}
