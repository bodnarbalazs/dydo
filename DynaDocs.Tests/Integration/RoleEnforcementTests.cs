namespace DynaDocs.Tests.Integration;

using DynaDocs.Services;

/// <summary>
/// Guard pipeline integration tests for role-based enforcement.
/// Tests the full flow: GuardCommand -> off-limits -> staged access -> role permissions.
/// Regression contract for decision 008 (data-driven roles).
/// </summary>
[Collection("Integration")]
public class RoleEnforcementTests : IntegrationTestBase
{
    #region Staged Access Control

    [Fact]
    public async Task Guard_NoIdentity_CanReadBootstrapFile()
    {
        await InitProjectAsync();

        var result = await GuardAsync("read", "dydo/index.md");

        result.AssertSuccess();
    }

    [Fact]
    public async Task Guard_NoIdentity_CannotReadSourceFile()
    {
        await InitProjectAsync();

        var result = await GuardAsync("read", "src/Foo.cs");

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
    }

    [Fact]
    public async Task Guard_IdentityNoRole_CanReadOwnModeFiles()
    {
        await InitProjectAsync();
        await ClaimAgentAsync("Adele");

        var result = await GuardAsync("read", "dydo/agents/Adele/modes/code-writer.md");

        result.AssertSuccess();
    }

    [Fact]
    public async Task Guard_IdentityNoRole_CannotReadSourceFile()
    {
        await InitProjectAsync();
        await ClaimAgentAsync("Adele");

        var result = await GuardAsync("read", "src/Foo.cs");

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
    }

    [Fact]
    public async Task Guard_IdentityWithRole_CanReadPerRolePaths()
    {
        await InitProjectAsync();
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");

        var result = await GuardAsync("read", "src/code.cs");

        result.AssertSuccess();
    }

    #endregion

    #region Bash Staged Access Control

    [Fact]
    public async Task Guard_Bash_NoIdentity_BlocksReadOfSourceFile()
    {
        await InitProjectAsync();

        var json = $"{{\"session_id\":\"{TestSessionId}\",\"tool_name\":\"Bash\",\"tool_input\":{{\"command\":\"cat src/Foo.cs\"}}}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
    }

    [Fact]
    public async Task Guard_Bash_NoIdentity_AllowsBootstrapFile()
    {
        await InitProjectAsync();

        // README.md is a root-level bootstrap file, not off-limits
        var json = $"{{\"session_id\":\"{TestSessionId}\",\"tool_name\":\"Bash\",\"tool_input\":{{\"command\":\"cat README.md\"}}}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertSuccess();
    }

    [Fact]
    public async Task Guard_Bash_IdentityNoRole_BlocksReadOfSourceFile()
    {
        await InitProjectAsync();
        await ClaimAgentAsync("Adele");

        var json = $"{{\"session_id\":\"{TestSessionId}\",\"tool_name\":\"Bash\",\"tool_input\":{{\"command\":\"cat src/Foo.cs\"}}}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
    }

    [Fact]
    public async Task Guard_Bash_IdentityWithRole_AllowsReadOfSourceFile()
    {
        await InitProjectAsync();
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");

        var json = $"{{\"session_id\":\"{TestSessionId}\",\"tool_name\":\"Bash\",\"tool_input\":{{\"command\":\"cat src/Foo.cs\"}}}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertSuccess();
    }

    #endregion


    #region Off-Limits Precedence

    [Theory]
    [InlineData("code-writer")]
    [InlineData("reviewer")]
    [InlineData("docs-writer")]
    public async Task Guard_OffLimits_BlocksRegardlessOfRole(string role)
    {
        await InitProjectAsync();
        await ClaimAgentAsync("Adele");
        await SetRoleAsync(role);
        await ReadMustReadsAsync();

        var result = await GuardAsync("edit", ".env");

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
        result.AssertStderrContains("off-limits");
    }

    [Fact]
    public async Task Guard_EnvExample_AllowedDespiteEnvOffLimits()
    {
        await InitProjectAsync();
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");

        var result = await GuardAsync("read", ".env.example");

        result.AssertSuccess();
    }

    #endregion

    #region Must-Read Enforcement

    [Fact]
    public async Task Guard_UnreadMustReads_BlocksWrites()
    {
        await InitProjectAsync();
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "test-task");

        var result = await GuardAsync("edit", "src/file.cs");

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
        result.AssertStderrContains("not read the required files");
    }

    [Fact]
    public async Task Guard_AllMustReadsRead_AllowsWrites()
    {
        await InitProjectAsync();
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "test-task");
        await ReadMustReadsAsync();

        var result = await GuardAsync("edit", "src/file.cs");

        result.AssertSuccess();
    }

    #endregion
}
