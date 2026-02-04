namespace DynaDocs.Tests.Integration;

/// <summary>
/// Integration tests that verify a freshly initialized DynaDocs project
/// passes validation checks without errors.
/// </summary>
[Collection("Integration")]
public class InitCheckIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task FreshInit_PassesCheck_WithNoErrors()
    {
        // Arrange - Initialize dydo in test directory
        var initResult = await InitProjectAsync("none", "testuser", 3);
        initResult.AssertSuccess();

        // Act - Run check
        var checkResult = await CheckAsync();

        // Assert - No errors (warnings are acceptable)
        // Check command returns "All checks passed." or "Found warnings (no errors)." on success
        Assert.True(
            !checkResult.Stdout.Contains("Found errors"),
            $"Expected no errors but check failed.\n\n=== STDOUT ===\n{checkResult.Stdout}\n\n=== STDERR ===\n{checkResult.Stderr}");
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
            AssertDirectoryExists($"dydo/agents/{agent}/modes");
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
}
