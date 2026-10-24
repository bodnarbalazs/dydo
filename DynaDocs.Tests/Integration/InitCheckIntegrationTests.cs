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
        var initResult = await InitProjectAsync("none");
        initResult.AssertSuccess();

        var checkResult = await CheckAsync();

        Assert.DoesNotContain("Found errors", checkResult.Stdout);
        Assert.Contains("Found warnings", checkResult.Stdout);
        Assert.Contains("About.md is not customized", checkResult.Stdout);
        Assert.Contains("Architecture.md is not customized", checkResult.Stdout);
    }

    [Fact]
    public async Task FreshInit_CreatesExpectedStructure()
    {
        var initResult = await InitProjectAsync("none");
        initResult.AssertSuccess();

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

        // Folder hubs are optional author-owned pages; init does not create them.
        AssertFileNotExists("dydo/understand/_index.md");
        AssertFileNotExists("dydo/guides/_index.md");
        AssertFileNotExists("dydo/reference/_index.md");
        AssertFileNotExists("dydo/project/_index.md");

        AssertFileExists("dydo/understand/about.md");
        AssertFileExists("dydo/understand/architecture.md");
        AssertFileExists("dydo/guides/coding-standards.md");
        AssertFileExists("dydo/reference/writing-docs.md");
        AssertFileExists("dydo/reference/about-dynadocs.md");

        // _assets scaffolds empty; the diagram is retired (issue 0301).
        Assert.False(File.Exists(Path.Combine(TestDir, "dydo/_assets/dydo-diagram.svg")));
    }

    [Fact]
    public async Task FreshInit_IndexRoutesRelevantWorkToBothGlossaries()
    {
        var initResult = await InitProjectAsync("none");
        initResult.AssertSuccess();

        var index = ReadFile("dydo/index.md");
        Assert.Contains("reference/dydo-glossary.md", index);
        Assert.Contains("[glossary.md](glossary.md)", index);
    }

    [Fact]
    public async Task FreshInit_CreatesMainFolderMetaFiles()
    {
        var initResult = await InitProjectAsync("none");
        initResult.AssertSuccess();

        AssertFileExists("dydo/understand/_understand.md");
        AssertFileExists("dydo/guides/_guides.md");
        AssertFileExists("dydo/reference/_reference.md");
        AssertFileExists("dydo/project/_project.md");

        AssertFileExists("dydo/project/decisions/_decisions.md");
        AssertFileExists("dydo/project/changelog/_changelog.md");
        AssertFileExists("dydo/project/pitfalls/_pitfalls.md");
        AssertFileExists("dydo/project/future-features/_future-features.md");
        foreach (var retired in new[] { "tasks", "issues", "campaigns", "sprints", "slices", "backlog" })
            Assert.False(Directory.Exists(Path.Combine(TestDir, "dydo", "project", retired)));
    }

    [Fact]
    public async Task FreshInit_CreatesEmptyAgentsWorkspaceRoot()
    {
        var initResult = await InitProjectAsync("none");
        initResult.AssertSuccess();

        AssertDirectoryExists("dydo/agents");
        Assert.False(Directory.Exists(Path.Combine(TestDir, "dydo/agents/sample")));
    }

    [Fact]
    public async Task FreshInit_WelcomeMdLinksToGlossary()
    {
        var initResult = await InitProjectAsync("none");
        initResult.AssertSuccess();

        AssertFileExists("dydo/welcome.md");
        AssertFileExists("dydo/glossary.md");

        var welcomeContent = ReadFile("dydo/welcome.md");
        Assert.Contains("glossary", welcomeContent, StringComparison.OrdinalIgnoreCase);

        var checkResult = await CheckAsync();
        Assert.DoesNotContain("Found errors", checkResult.Stdout);
    }

    [Fact]
    public async Task FreshInit_OffLimitsFileDoesNotCreateFalsePatterns()
    {
        var initResult = await InitProjectAsync("none");
        initResult.AssertSuccess();

        // Syntax documentation in the off-limits file must not parse as patterns.
        AssertFileExists("dydo/files-off-limits.md");

        var checkResult = await CheckAsync();
        Assert.DoesNotContain("Found errors", checkResult.Stdout);
    }

    [Fact]
    public async Task Check_AllowsDirectChildWithoutMetaFile()
    {
        var initResult = await InitProjectAsync("none");
        initResult.AssertSuccess();

        WriteFile("dydo/guides/testing/unit-tests.md",
            "---\narea: guides\ntype: guide\n---\n\n# Unit Tests\n\nHow to write tests.");

        var checkResult = await CheckAsync(DydoDir);

        // Folder-meta pages are optional authored navigation.
        var output = checkResult.Stdout + checkResult.Stderr;
        Assert.DoesNotContain("_testing.md", output);
    }

    [Fact]
    public async Task Check_AcceptsFolderWithOptionalMetaFile()
    {
        var initResult = await InitProjectAsync("none");
        initResult.AssertSuccess();

        WriteFile("dydo/guides/testing/_testing.md",
            "---\narea: guides\ntype: folder-meta\n---\n\n# Testing\n\nTesting guides.");
        WriteFile("dydo/guides/testing/unit-tests.md",
            "---\narea: guides\ntype: guide\n---\n\n# Unit Tests\n\nHow to write tests.");

        var checkResult = await CheckAsync(DydoDir);

        var output = checkResult.Stdout + checkResult.Stderr;
        Assert.DoesNotContain("_testing.md", output);
    }

    [Fact]
    public async Task Check_ExcludesAgentWorkspaceFiles()
    {
        var initResult = await InitProjectAsync("none");
        initResult.AssertSuccess();

        WriteFile("dydo/agents/sample/bad-doc.md", "no frontmatter at all, this is invalid");

        var checkResult = await CheckAsync();

        var output = checkResult.Stdout + checkResult.Stderr;
        Assert.DoesNotContain("bad-doc.md", output);
        Assert.DoesNotContain("Found errors", output);
    }

    [Fact]
    public async Task FreshInit_DoesNotCreateModeFiles()
    {
        var initResult = await InitProjectAsync("none");
        initResult.AssertSuccess();

        var expectedAgents = new[] { "sample", "peer" };
        foreach (var agent in expectedAgents)
        {
            var modesPath = Path.Combine(TestDir, "dydo", "agents", agent, "modes");
            Assert.False(Directory.Exists(modesPath), $"Modes folder for {agent} should NOT exist after init");
        }
    }

}
