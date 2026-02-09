namespace DynaDocs.Tests.Integration;

/// <summary>
/// Integration tests that verify a freshly initialized DynaDocs project
/// passes validation checks without errors.
/// </summary>
[Collection("Integration")]
public class InitCheckIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task FreshInit_PassesCheck_WithOneWarning()
    {
        // Arrange - Initialize dydo in test directory
        var initResult = await InitProjectAsync("none", "testuser", 3);
        initResult.AssertSuccess();

        // Act - Run check
        var checkResult = await CheckAsync();

        // Assert - No errors, warnings about uncustomized about.md and architecture.md
        Assert.DoesNotContain("Found errors", checkResult.Stdout);
        Assert.Contains("Found warnings", checkResult.Stdout);
        Assert.Contains("About.md is not customized", checkResult.Stdout);
        Assert.Contains("Architecture.md is not customized", checkResult.Stdout);
    }

    [Fact]
    public async Task FreshInit_CreatesExpectedStructure()
    {
        // Arrange & Act
        var initResult = await InitProjectAsync("none", "testuser", 3);
        initResult.AssertSuccess();

        // Assert - Core documentation structure exists
        AssertFileExists("dydo/index.md");
        AssertFileExists("dydo/welcome.md");
        AssertFileExists("dydo/glossary.md");
        AssertFileExists("dydo/files-off-limits.md");
        AssertDirectoryExists("dydo/understand");
        AssertDirectoryExists("dydo/guides");
        AssertDirectoryExists("dydo/reference");
        AssertDirectoryExists("dydo/project");
        AssertDirectoryExists("dydo/agents");
        AssertDirectoryExists("dydo/_assets");

        // Assert - Hub index files exist
        AssertFileExists("dydo/understand/_index.md");
        AssertFileExists("dydo/guides/_index.md");
        AssertFileExists("dydo/reference/_index.md");
        AssertFileExists("dydo/project/_index.md");

        // Assert - Foundation docs exist
        AssertFileExists("dydo/understand/about.md");
        AssertFileExists("dydo/understand/architecture.md");
        AssertFileExists("dydo/guides/coding-standards.md");
        AssertFileExists("dydo/guides/how-to-use-docs.md");
        AssertFileExists("dydo/reference/writing-docs.md");
        AssertFileExists("dydo/reference/about-dynadocs.md");

        // Assert - Assets exist
        AssertFileExists("dydo/_assets/dydo-diagram.svg");
    }

    [Fact]
    public async Task FreshInit_CreatesMainFolderMetaFiles()
    {
        // Arrange & Act
        var initResult = await InitProjectAsync("none", "testuser", 3);
        initResult.AssertSuccess();

        // Assert - Main folder meta files exist
        AssertFileExists("dydo/understand/_understand.md");
        AssertFileExists("dydo/guides/_guides.md");
        AssertFileExists("dydo/reference/_reference.md");
        AssertFileExists("dydo/project/_project.md");

        // Assert - Project subfolder meta files exist
        AssertFileExists("dydo/project/tasks/_tasks.md");
        AssertFileExists("dydo/project/decisions/_decisions.md");
        AssertFileExists("dydo/project/changelog/_changelog.md");
        AssertFileExists("dydo/project/pitfalls/_pitfalls.md");
    }

    [Fact]
    public async Task FreshInit_CreatesAgentWorkspaces()
    {
        // Arrange & Act
        var initResult = await InitProjectAsync("none", "testuser", 3);
        initResult.AssertSuccess();

        // Assert - Default agent workspaces exist (Adele, Brian, Charlie from PresetAgentNames)
        var expectedAgents = new[] { "Adele", "Brian", "Charlie" };
        foreach (var agent in expectedAgents)
        {
            AssertDirectoryExists($"dydo/agents/{agent}");
            AssertDirectoryExists($"dydo/agents/{agent}/inbox");
            AssertFileExists($"dydo/agents/{agent}/workflow.md");
        }
    }

    [Fact]
    public async Task FreshInit_TemplatesAreExcludedFromCheck()
    {
        // Arrange - Initialize
        var initResult = await InitProjectAsync("none", "testuser", 3);
        initResult.AssertSuccess();

        // Assert - Templates folder exists
        AssertDirectoryExists("dydo/_system/templates");

        // Act - Run check
        var checkResult = await CheckAsync();

        // Assert - Check passes despite templates having different naming/frontmatter rules
        Assert.DoesNotContain("Found errors", checkResult.Stdout);
    }

    [Fact]
    public async Task FreshInit_WelcomeMdLinksToGlossary()
    {
        // Arrange & Act
        var initResult = await InitProjectAsync("none", "testuser", 3);
        initResult.AssertSuccess();

        // Assert - welcome.md links to glossary.md and both exist
        AssertFileExists("dydo/welcome.md");
        AssertFileExists("dydo/glossary.md");

        var welcomeContent = ReadFile("dydo/welcome.md");
        Assert.Contains("glossary", welcomeContent, StringComparison.OrdinalIgnoreCase);

        // Run check to verify no broken links
        var checkResult = await CheckAsync();
        Assert.DoesNotContain("Found errors", checkResult.Stdout);
    }

    [Fact]
    public async Task FreshInit_OffLimitsFileDoesNotCreateFalsePatterns()
    {
        // Arrange & Act
        var initResult = await InitProjectAsync("none", "testuser", 3);
        initResult.AssertSuccess();

        // Assert - off-limits file exists and check passes
        // This validates that syntax documentation in off-limits file
        // is not incorrectly parsed as patterns
        AssertFileExists("dydo/files-off-limits.md");

        var checkResult = await CheckAsync();
        Assert.DoesNotContain("Found errors", checkResult.Stdout);
    }

    [Fact]
    public async Task Check_ReportsMissingMetaFile()
    {
        // Arrange
        var initResult = await InitProjectAsync("none", "testuser", 3);
        initResult.AssertSuccess();

        // Create subfolder without meta file
        WriteFile("dydo/guides/testing/unit-tests.md",
            "---\narea: guides\ntype: guide\n---\n\n# Unit Tests\n\nHow to write tests.");

        // Act
        var checkResult = await CheckAsync(DydoDir);

        // Assert - Should report missing meta file
        var output = checkResult.Stdout + checkResult.Stderr;
        Assert.Contains("_testing.md", output);
    }

    [Fact]
    public async Task Check_AcceptsFolderWithMetaFile()
    {
        // Arrange
        var initResult = await InitProjectAsync("none", "testuser", 3);
        initResult.AssertSuccess();

        // Create subfolder with meta file
        WriteFile("dydo/guides/testing/_testing.md",
            "---\narea: guides\ntype: folder-meta\n---\n\n# Testing\n\nTesting guides.");
        WriteFile("dydo/guides/testing/unit-tests.md",
            "---\narea: guides\ntype: guide\n---\n\n# Unit Tests\n\nHow to write tests.");

        // Act
        var checkResult = await CheckAsync(DydoDir);

        // Assert - Should not complain about missing meta file
        var output = checkResult.Stdout + checkResult.Stderr;
        Assert.DoesNotContain("_testing.md", output);
    }

    [Fact]
    public async Task Check_ExcludesAgentWorkspaceFiles()
    {
        // Arrange
        var initResult = await InitProjectAsync("none", "testuser", 3);
        initResult.AssertSuccess();

        // Write a malformed .md into an agent workspace
        WriteFile("dydo/agents/Adele/bad-doc.md", "no frontmatter at all, this is invalid");

        // Act
        var checkResult = await CheckAsync();

        // Assert - The malformed agent file should NOT be reported
        var output = checkResult.Stdout + checkResult.Stderr;
        Assert.DoesNotContain("bad-doc.md", output);
        Assert.DoesNotContain("Found errors", output);
    }

    [Fact]
    public async Task FreshInit_DoesNotCreateModeFiles()
    {
        // Arrange & Act
        var initResult = await InitProjectAsync("none", "testuser", 3);
        initResult.AssertSuccess();

        // Assert - No modes/ directory for any agent
        var expectedAgents = new[] { "Adele", "Brian", "Charlie" };
        foreach (var agent in expectedAgents)
        {
            var modesPath = Path.Combine(TestDir, "dydo", "agents", agent, "modes");
            Assert.False(Directory.Exists(modesPath), $"Modes folder for {agent} should NOT exist after init");
        }
    }

    [Fact]
    public async Task ClaimAgent_CreatesModeFiles()
    {
        // Arrange
        var initResult = await InitProjectAsync("none", "testuser", 3);
        initResult.AssertSuccess();

        // Verify no modes before claim
        var modesPath = Path.Combine(TestDir, "dydo", "agents", "Adele", "modes");
        Assert.False(Directory.Exists(modesPath));

        // Act - Claim agent
        var claimResult = await ClaimAgentAsync("Adele");
        claimResult.AssertSuccess();

        // Assert - Modes folder and all 7 mode files exist after claim
        Assert.True(Directory.Exists(modesPath), "Modes folder should exist after claim");

        var expectedModes = new[] { "code-writer", "reviewer", "co-thinker", "interviewer", "planner", "docs-writer", "tester" };
        foreach (var mode in expectedModes)
        {
            AssertFileExists($"dydo/agents/Adele/modes/{mode}.md");
        }
    }
}
