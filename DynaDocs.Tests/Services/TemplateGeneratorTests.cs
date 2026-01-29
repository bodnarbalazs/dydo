namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

public class TemplateGeneratorTests
{
    [Fact]
    public void GetModeNames_ReturnsFiveModes()
    {
        var modes = TemplateGenerator.GetModeNames();

        Assert.Equal(5, modes.Count);
        Assert.Contains("code-writer", modes);
        Assert.Contains("reviewer", modes);
        Assert.Contains("interviewer", modes);
        Assert.Contains("planner", modes);
        Assert.Contains("docs-writer", modes);
    }

    [Fact]
    public void GenerateModeFile_ReplacesAgentNamePlaceholder()
    {
        var content = TemplateGenerator.GenerateModeFile("Adele", "code-writer");

        Assert.Contains("Adele", content);
        Assert.DoesNotContain("{{AGENT_NAME}}", content);
    }

    [Fact]
    public void GenerateModeFile_CodeWriter_ContainsMustReads()
    {
        var content = TemplateGenerator.GenerateModeFile("Adele", "code-writer");

        Assert.Contains("about.md", content);
        Assert.Contains("architecture.md", content);
        Assert.Contains("coding-standards.md", content);
    }

    [Fact]
    public void GenerateModeFile_Reviewer_HasReadOnlyGuidance()
    {
        var content = TemplateGenerator.GenerateModeFile("Adele", "reviewer");

        Assert.Contains("Adele", content);
        Assert.Contains("read-only", content.ToLowerInvariant());
    }

    [Fact]
    public void GenerateModeFile_Interviewer_SkipsCodingStandards()
    {
        var content = TemplateGenerator.GenerateModeFile("Adele", "interviewer");

        Assert.Contains("about.md", content);
        Assert.Contains("architecture.md", content);
        // Interviewer doesn't need coding standards yet
        Assert.DoesNotContain("coding-standards.md", content);
    }

    [Fact]
    public void GenerateModeFile_Planner_SkipsCodingStandards()
    {
        var content = TemplateGenerator.GenerateModeFile("Adele", "planner");

        Assert.Contains("about.md", content);
        Assert.Contains("architecture.md", content);
        // Planner doesn't need coding standards yet
        Assert.DoesNotContain("coding-standards.md", content);
    }

    [Fact]
    public void GenerateModeFile_DocsWriter_ContainsDocsGuidance()
    {
        var content = TemplateGenerator.GenerateModeFile("Adele", "docs-writer");

        Assert.Contains("about.md", content);
        Assert.Contains("how-to-use-docs.md", content);
    }

    [Fact]
    public void GenerateModeFile_UnknownMode_ReturnsFallback()
    {
        var content = TemplateGenerator.GenerateModeFile("Adele", "unknown-mode");

        // Should still generate something
        Assert.Contains("Adele", content);
        Assert.Contains("unknown-mode", content);
    }

    [Fact]
    public void GenerateAboutMd_ContainsPlaceholders()
    {
        var content = TemplateGenerator.GenerateAboutMd();

        Assert.Contains("What We're Building", content);
        Assert.Contains("Tech Stack", content);
        Assert.Contains("Key Concepts", content);
        Assert.Contains("Current State", content);
        Assert.Contains("Constraints", content);
    }

    [Fact]
    public void GenerateAboutMd_HasCorrectFrontmatter()
    {
        var content = TemplateGenerator.GenerateAboutMd();

        Assert.StartsWith("---", content);
        Assert.Contains("area: understand", content);
        Assert.Contains("type: context", content);
    }

    [Fact]
    public void GenerateWorkflowFile_ContainsAgentName()
    {
        var content = TemplateGenerator.GenerateWorkflowFile("Adele");

        Assert.Contains("Adele", content);
        Assert.DoesNotContain("{{AGENT_NAME}}", content);
    }

    [Fact]
    public void GenerateWorkflowFile_LinksToModeFiles()
    {
        var content = TemplateGenerator.GenerateWorkflowFile("Adele");

        Assert.Contains("modes/interviewer.md", content);
        Assert.Contains("modes/planner.md", content);
        Assert.Contains("modes/code-writer.md", content);
        Assert.Contains("modes/reviewer.md", content);
        Assert.Contains("modes/docs-writer.md", content);
    }

    [Fact]
    public void GenerateWorkflowFile_HasWorkflowFlags()
    {
        var content = TemplateGenerator.GenerateWorkflowFile("Adele");

        Assert.Contains("--feature", content);
        Assert.Contains("--task", content);
        Assert.Contains("--quick", content);
        Assert.Contains("--review", content);
        Assert.Contains("--docs", content);
        Assert.Contains("--inbox", content);
    }

    [Fact]
    public void GenerateWorkflowFile_HasClaimCommand()
    {
        var content = TemplateGenerator.GenerateWorkflowFile("Adele");

        Assert.Contains("dydo agent claim Adele", content);
    }

    [Fact]
    public void GenerateIndexMd_ContainsAgentPath()
    {
        var agentNames = new List<string> { "Adele", "Brian" };
        var content = TemplateGenerator.GenerateIndexMd(agentNames);

        Assert.Contains("agents/", content);
        Assert.Contains("workflow.md", content);
    }

    [Fact]
    public void GenerateArchitectureMd_HasCorrectStructure()
    {
        var content = TemplateGenerator.GenerateArchitectureMd();

        Assert.StartsWith("---", content);
        Assert.Contains("area: understand", content);
        Assert.Contains("Architecture", content);
    }

    [Fact]
    public void GenerateCodingStandardsMd_HasCorrectStructure()
    {
        var content = TemplateGenerator.GenerateCodingStandardsMd();

        Assert.StartsWith("---", content);
        Assert.Contains("area: general", content);  // coding-standards uses general area
        Assert.Contains("Coding Standards", content);
    }

    [Fact]
    public void GenerateHowToUseDocsMd_HasCorrectStructure()
    {
        var content = TemplateGenerator.GenerateHowToUseDocsMd();

        Assert.StartsWith("---", content);
        Assert.Contains("area: guides", content);
    }

    [Fact]
    public void GenerateFilesOffLimitsMd_HasCorrectStructure()
    {
        var content = TemplateGenerator.GenerateFilesOffLimitsMd();

        Assert.StartsWith("---", content);
        Assert.Contains("Off-Limits", content);
    }

    [Theory]
    [InlineData("code-writer")]
    [InlineData("reviewer")]
    [InlineData("interviewer")]
    [InlineData("planner")]
    [InlineData("docs-writer")]
    public void GenerateModeFile_AllModes_HaveValidFrontmatter(string mode)
    {
        var content = TemplateGenerator.GenerateModeFile("TestAgent", mode);

        Assert.StartsWith("---", content);
        Assert.Contains("agent: TestAgent", content);
        Assert.Contains($"mode: {mode}", content);
    }

    [Theory]
    [InlineData("code-writer", "Set Role")]
    [InlineData("reviewer", "Set Role")]
    [InlineData("interviewer", "Set Role")]
    [InlineData("planner", "Set Role")]
    [InlineData("docs-writer", "Set Role")]
    public void GenerateModeFile_AllModes_HaveSetRoleSection(string mode, string expectedSection)
    {
        var content = TemplateGenerator.GenerateModeFile("TestAgent", mode);

        Assert.Contains(expectedSection, content);
        Assert.Contains($"dydo agent role {mode}", content);
    }
}
