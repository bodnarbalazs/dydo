namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;

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

        // src/file.cs is typically allowed for code-writer
        var result = await GuardAsync("edit", "src/file.cs");

        result.AssertSuccess();
    }

    #endregion

    #region Role Permissions

    [Fact]
    public async Task Guard_NoAgent_Warns()
    {
        await InitProjectAsync("none", "balazs", 3);
        // Don't claim an agent

        var result = await GuardAsync("edit", "src/file.cs");

        // Should warn but allow (non-strict mode)
        result.AssertSuccess();
        result.AssertStderrContains("WARNING");
        result.AssertStderrContains("No agent");
    }

    [Fact]
    public async Task Guard_NoRole_Warns()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        // Don't set a role

        var result = await GuardAsync("edit", "src/file.cs");

        // Should warn but allow (non-strict mode)
        result.AssertSuccess();
        result.AssertStderrContains("WARNING");
        result.AssertStderrContains("no role");
    }

    [Fact]
    public async Task Guard_RoleViolation_Blocks()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("reviewer"); // Reviewers can only read, not write

        // Try to write outside allowed paths
        var result = await GuardAsync("edit", "src/code.cs");

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
    }

    [Fact]
    public async Task Guard_ReadOperation_Allowed()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("reviewer");

        // Read operations are always allowed (except off-limits)
        var result = await GuardAsync("read", "src/code.cs");

        result.AssertSuccess();
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
}
