namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;

/// <summary>
/// Integration tests for the init command.
/// </summary>
[Collection("Integration")]
public class InitCommandTests : IntegrationTestBase
{
    #region Init None

    [Fact]
    public async Task Init_None_CreatesBasicStructure()
    {
        var result = await InitProjectAsync("none", "balazs", 3);

        result.AssertSuccess();
        AssertFileExists("dydo.json");
        AssertFileExists("CLAUDE.md");
        AssertDirectoryExists("dydo");
        AssertDirectoryExists("dydo/understand");
        AssertDirectoryExists("dydo/guides");
        AssertDirectoryExists("dydo/reference");
        AssertDirectoryExists("dydo/project");
        AssertDirectoryExists("dydo/agents");
    }

    [Fact]
    public async Task Init_None_CreatesFoundationDocs()
    {
        var result = await InitProjectAsync("none", "balazs", 3);

        result.AssertSuccess();
        AssertFileExists("dydo/index.md");
        AssertFileExists("dydo/welcome.md");
        AssertFileExists("dydo/files-off-limits.md");
        AssertFileExists("dydo/understand/about.md");
        AssertFileExists("dydo/understand/architecture.md");
        AssertFileExists("dydo/guides/coding-standards.md");
        AssertFileExists("dydo/guides/how-to-use-docs.md");
        AssertFileExists("dydo/reference/writing-docs.md");
    }

    [Fact]
    public async Task Init_None_CreatesAgentWorkspaces()
    {
        var result = await InitProjectAsync("none", "balazs", 3);

        result.AssertSuccess();

        // Should create 3 agent workspaces
        AssertDirectoryExists("dydo/agents/Adele");
        AssertDirectoryExists("dydo/agents/Brian");
        AssertDirectoryExists("dydo/agents/Charlie");

        // Each with workflow (modes created at claim, not init)
        AssertFileExists("dydo/agents/Adele/workflow.md");
        Assert.False(Directory.Exists(Path.Combine(TestDir, "dydo/agents/Adele/modes")));
    }

    [Fact]
    public async Task Init_None_CreatesCorrectConfig()
    {
        var result = await InitProjectAsync("none", "balazs", 3);

        result.AssertSuccess();
        AssertFileContains("dydo.json", "\"version\": 1");
        AssertFileContains("dydo.json", "\"balazs\"");
        AssertFileContains("dydo.json", "Adele");
        AssertFileContains("dydo.json", "Brian");
        AssertFileContains("dydo.json", "Charlie");
    }

    [Fact]
    public async Task Init_None_UpdatesGitignore()
    {
        var result = await InitProjectAsync("none", "balazs", 3);

        result.AssertSuccess();
        AssertFileExists(".gitignore");
        AssertFileContains(".gitignore", "dydo/agents/");
    }

    [Fact]
    public async Task Init_None_CreatesProjectSubfolderDocs()
    {
        var result = await InitProjectAsync("none", "balazs", 3);

        result.AssertSuccess();

        // Tasks folder - hub and meta
        AssertFileExists("dydo/project/tasks/_index.md");
        AssertFileExists("dydo/project/tasks/_tasks.md");

        // Decisions folder - hub and meta
        AssertFileExists("dydo/project/decisions/_index.md");
        AssertFileExists("dydo/project/decisions/_decisions.md");

        // Changelog folder - hub and meta
        AssertFileExists("dydo/project/changelog/_index.md");
        AssertFileExists("dydo/project/changelog/_changelog.md");

        // Pitfalls folder - hub and meta
        AssertFileExists("dydo/project/pitfalls/_index.md");
        AssertFileExists("dydo/project/pitfalls/_pitfalls.md");
    }

    [Fact]
    public async Task Init_None_MetaFilesHaveMeaningfulContent()
    {
        var result = await InitProjectAsync("none", "balazs", 3);

        result.AssertSuccess();

        // Tasks meta should describe task lifecycle
        AssertFileContains("dydo/project/tasks/_tasks.md", "Task Lifecycle");
        AssertFileContains("dydo/project/tasks/_tasks.md", "pending");
        AssertFileContains("dydo/project/tasks/_tasks.md", "review-pending");

        // Decisions meta should describe decision record format
        AssertFileContains("dydo/project/decisions/_decisions.md", "Decision Records");
        AssertFileContains("dydo/project/decisions/_decisions.md", "proposed");
        AssertFileContains("dydo/project/decisions/_decisions.md", "accepted");

        // Changelog meta should describe folder structure
        AssertFileContains("dydo/project/changelog/_changelog.md", "Chronological record");
        AssertFileContains("dydo/project/changelog/_changelog.md", "Files Changed");

        // Pitfalls meta should describe naming conventions
        AssertFileContains("dydo/project/pitfalls/_pitfalls.md", "Known gotchas");
        AssertFileContains("dydo/project/pitfalls/_pitfalls.md", "Symptoms");
    }

    #endregion

    #region Init Claude

    [Fact]
    public async Task Init_Claude_CreatesHooksConfig()
    {
        var result = await InitProjectAsync("claude", "balazs", 3);

        result.AssertSuccess();
        AssertFileExists(".claude/settings.local.json");
        AssertFileContains(".claude/settings.local.json", "PreToolUse");
        AssertFileContains(".claude/settings.local.json", "dydo guard");
    }

    [Fact]
    public async Task Init_Claude_HooksMatchEditWriteReadBash()
    {
        var result = await InitProjectAsync("claude", "balazs", 3);

        result.AssertSuccess();
        var content = ReadFile(".claude/settings.local.json");
        Assert.Contains("Edit|Write|Read|Bash", content);
    }

    [Fact]
    public async Task Init_Claude_PreservesExistingHooks()
    {
        // Arrange: Create existing settings with a custom hook
        Directory.CreateDirectory(Path.Combine(TestDir, ".claude"));
        WriteFile(".claude/settings.local.json", """
            {
              "hooks": {
                "PreToolUse": [
                  {
                    "matcher": "CustomTool",
                    "hooks": [
                      {
                        "type": "command",
                        "command": "echo custom"
                      }
                    ]
                  }
                ],
                "PostToolUse": [
                  {
                    "matcher": "AnyTool",
                    "hooks": [
                      {
                        "type": "command",
                        "command": "echo post"
                      }
                    ]
                  }
                ]
              },
              "otherSetting": true
            }
            """);

        // Act
        var result = await InitProjectAsync("claude", "balazs", 3);

        // Assert
        result.AssertSuccess();
        var content = ReadFile(".claude/settings.local.json");

        // Should preserve existing PreToolUse entry
        Assert.Contains("CustomTool", content);
        Assert.Contains("echo custom", content);

        // Should add dydo guard entry
        Assert.Contains("dydo guard", content);
        Assert.Contains("Edit|Write|Read|Bash", content);

        // Should preserve PostToolUse
        Assert.Contains("PostToolUse", content);
        Assert.Contains("echo post", content);

        // Should preserve other settings
        Assert.Contains("otherSetting", content);
    }

    [Fact]
    public async Task Init_Claude_DoesNotDuplicateDydoHook()
    {
        // First init
        await InitProjectAsync("claude", "balazs", 3);

        // Manually re-run hook configuration (simulating re-init scenario)
        // Since init fails if already initialized, we test via join
        var result = await JoinProjectAsync("claude", "alice", 2);

        result.AssertSuccess();
        var content = ReadFile(".claude/settings.local.json");

        // Count occurrences of "dydo guard" - should only appear once
        var count = content.Split("dydo guard").Length - 1;
        Assert.Equal(1, count);
    }

    #endregion

    #region Init Join

    [Fact]
    public async Task Init_Join_AddsSecondHuman()
    {
        // First human initializes
        await InitProjectAsync("none", "balazs", 3);

        // Second human joins
        var result = await JoinProjectAsync("none", "alice", 2);

        result.AssertSuccess();
        AssertFileContains("dydo.json", "\"alice\"");

        // Should have 5 total agents now (3 + 2)
        AssertDirectoryExists("dydo/agents/Dexter");
        AssertDirectoryExists("dydo/agents/Emma");
    }

    [Fact]
    public async Task Init_Join_WithoutExistingProject_Fails()
    {
        var command = InitCommand.Create();
        var result = await RunAsync(command, "none", "--join", "--name", "alice", "--agents", "2");

        result.AssertExitCode(2);
        result.AssertStderrContains("No DynaDocs project found");
    }

    [Fact]
    public async Task Init_Join_AlreadyMember_Succeeds()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Join again with same name
        var result = await JoinProjectAsync("none", "balazs", 2);

        result.AssertSuccess();
        result.AssertStdoutContains("already a member");
    }

    #endregion

    #region Error Cases

    [Fact]
    public async Task Init_WithoutName_Fails()
    {
        ClearHuman();
        var command = InitCommand.Create();

        // Note: Without --name and without DYDO_HUMAN set, and with empty stdin,
        // PromptForInput returns empty string
        var result = await RunAsync(command, "none");

        result.AssertExitCode(2);
        result.AssertStderrContains("Name is required");
    }

    [Fact]
    public async Task Init_InvalidIntegration_Fails()
    {
        var command = InitCommand.Create();
        var result = await RunAsync(command, "invalid", "--name", "balazs", "--agents", "3");

        result.AssertExitCode(2);
        result.AssertStderrContains("Unknown integration");
    }

    [Fact]
    public async Task Init_AlreadyInitialized_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);

        var command = InitCommand.Create();
        var result = await RunAsync(command, "none", "--name", "balazs", "--agents", "3");

        result.AssertExitCode(2);
        result.AssertStderrContains("already initialized");
    }

    [Fact]
    public async Task Init_TooManyAgents_Fails()
    {
        var command = InitCommand.Create();
        var result = await RunAsync(command, "none", "--name", "balazs", "--agents", "105");

        result.AssertExitCode(2);
        result.AssertStderrContains("between 1 and 104");
    }

    [Fact]
    public async Task Init_ZeroAgents_Fails()
    {
        var command = InitCommand.Create();
        var result = await RunAsync(command, "none", "--name", "balazs", "--agents", "0");

        result.AssertExitCode(2);
        result.AssertStderrContains("between 1 and 104");
    }

    #endregion

    #region Idempotency

    [Fact]
    public async Task Init_DoesNotOverwriteExistingDocs()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Modify a file
        WriteFile("dydo/understand/architecture.md", "# Custom Architecture\n\nMy custom content");

        // Try to init again (should fail because already initialized)
        var command = InitCommand.Create();
        var result = await RunAsync(command, "none", "--name", "balazs", "--agents", "3");

        // Init should fail, but more importantly, the file should not be overwritten
        var content = ReadFile("dydo/understand/architecture.md");
        Assert.Contains("Custom Architecture", content);
    }

    #endregion
}
