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

    #region Write Enforcement Through Guard

    [Fact]
    public async Task Guard_CodeWriter_AllowedPath_Passes()
    {
        await InitProjectAsync();
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");
        await ReadMustReadsAsync();

        var result = await GuardAsync("edit", "src/file.cs");

        result.AssertSuccess();
    }

    [Fact]
    public async Task Guard_CodeWriter_DisallowedPath_BlockedWithExitCode2()
    {
        await InitProjectAsync();
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");
        await ReadMustReadsAsync();

        var result = await GuardAsync("edit", "dydo/project/tasks/foo.md");

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
    }

    [Fact]
    public async Task Guard_Reviewer_CannotEditSource_WithRoleMessage()
    {
        await InitProjectAsync();
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("reviewer");
        await ReadMustReadsAsync();

        var result = await GuardAsync("edit", "src/code.cs");

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
        result.AssertStderrContains("Reviewer role can only edit own workspace");
    }

    [Fact]
    public async Task Guard_Reviewer_CanEditOwnWorkspace()
    {
        await InitProjectAsync();
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("reviewer");
        await ReadMustReadsAsync();

        var result = await GuardAsync("edit", "dydo/agents/Adele/review-notes.md");

        result.AssertSuccess();
    }

    [Fact]
    public async Task Guard_Planner_CanEditTasks()
    {
        await InitProjectAsync();
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("planner");
        await ReadMustReadsAsync();

        var result = await GuardAsync("edit", "dydo/project/tasks/new-task.md");

        result.AssertSuccess();
    }

    [Fact]
    public async Task Guard_Planner_CannotEditSource()
    {
        await InitProjectAsync();
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("planner");
        await ReadMustReadsAsync();

        var result = await GuardAsync("edit", "src/code.cs");

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
        result.AssertStderrContains("Planner role can only edit own workspace and tasks");
    }

    [Fact]
    public async Task Guard_CoThinker_CanEditDecisions()
    {
        await InitProjectAsync();
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("co-thinker");
        await ReadMustReadsAsync();

        var result = await GuardAsync("edit", "dydo/project/decisions/001-new.md");

        result.AssertSuccess();
    }

    [Fact]
    public async Task Guard_CoThinker_CannotEditSource()
    {
        await InitProjectAsync();
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("co-thinker");
        await ReadMustReadsAsync();

        var result = await GuardAsync("edit", "src/code.cs");

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
        result.AssertStderrContains("Co-thinker role can edit own workspace and decisions");
    }

    [Fact]
    public async Task Guard_TestWriter_CanEditTests()
    {
        await InitProjectAsync();
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("test-writer");
        await ReadMustReadsAsync();

        // Default test path from config is "tests/**" — this project uses DynaDocs.Tests/**
        // The default (no dydo.json paths) is tests/**, so we test with that
        var result = await GuardAsync("edit", "tests/MyTest.cs");

        result.AssertSuccess();
    }

    [Fact]
    public async Task Guard_TestWriter_CannotEditSource()
    {
        await InitProjectAsync();
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("test-writer");
        await ReadMustReadsAsync();

        var result = await GuardAsync("edit", "src/code.cs");

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
        result.AssertStderrContains("Test-writer role can edit own workspace, tests, and pitfalls");
    }

    [Fact]
    public async Task Guard_DocsWriter_CanEditDydoDocs()
    {
        await InitProjectAsync();
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("docs-writer");
        await ReadMustReadsAsync();

        var result = await GuardAsync("edit", "dydo/understand/new-doc.md");

        result.AssertSuccess();
    }

    [Fact]
    public async Task Guard_DocsWriter_CannotEditSource()
    {
        await InitProjectAsync();
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("docs-writer");
        await ReadMustReadsAsync();

        var result = await GuardAsync("edit", "src/code.cs");

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
        result.AssertStderrContains("Docs-writer role can only edit dydo/**");
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

    #region Glob Matching Behavior

    [Fact]
    public async Task Guard_DoubleStarPattern_MatchesAnyDepth()
    {
        await InitProjectAsync();
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");
        await ReadMustReadsAsync();

        // src/** should match deeply nested paths
        var result = await GuardAsync("edit", "src/a/b/c/d.cs");

        result.AssertSuccess();
    }

    [Fact]
    public async Task Guard_SelfPattern_MatchesOnlyOwnAgent()
    {
        await InitProjectAsync();
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");
        await ReadMustReadsAsync();

        var ownResult = await GuardAsync("edit", "dydo/agents/Adele/notes.md");
        ownResult.AssertSuccess();

        var otherResult = await GuardAsync("edit", "dydo/agents/Brian/notes.md");
        otherResult.AssertExitCode(2);
        otherResult.AssertStderrContains("BLOCKED");
    }

    [Fact]
    public async Task Guard_SourcePattern_MatchesSubdirectories()
    {
        await InitProjectAsync();
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");
        await ReadMustReadsAsync();

        var shallow = await GuardAsync("edit", "src/foo.cs");
        shallow.AssertSuccess();

        var deep = await GuardAsync("edit", "src/sub/bar.cs");
        deep.AssertSuccess();
    }

    #endregion
}
