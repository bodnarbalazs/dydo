namespace DynaDocs.Tests.Services;

using System.Reflection;
using DynaDocs.Services;

public class TemplateGeneratorTests
{
    #region Embedded Resource Tests

    [Fact]
    public void ReadBuiltInTemplate_ReadsFromEmbeddedResources()
    {
        // This should work even without a Templates folder on disk
        var content = TemplateGenerator.ReadBuiltInTemplate("agent-workflow.template.md");

        Assert.NotEmpty(content);
        Assert.Contains("{{AGENT_NAME}}", content);
    }

    [Fact]
    public void ReadBuiltInTemplate_ThrowsForNonexistentTemplate()
    {
        var ex = Assert.Throws<FileNotFoundException>(() =>
            TemplateGenerator.ReadBuiltInTemplate("nonexistent-template.md"));

        Assert.Contains("nonexistent-template.md", ex.Message);
    }

    [Theory]
    [InlineData("agent-workflow.template.md")]
    [InlineData("mode-code-writer.template.md")]
    [InlineData("mode-reviewer.template.md")]
    [InlineData("mode-co-thinker.template.md")]
    [InlineData("mode-interviewer.template.md")]
    [InlineData("mode-planner.template.md")]
    [InlineData("mode-docs-writer.template.md")]
    [InlineData("mode-tester.template.md")]
    public void ReadBuiltInTemplate_AllListedTemplates_AreAccessible(string templateName)
    {
        var content = TemplateGenerator.ReadBuiltInTemplate(templateName);

        Assert.NotNull(content);
        Assert.NotEmpty(content);
    }

    [Fact]
    public void Assembly_ContainsEmbeddedTemplateResources()
    {
        // Get the DynaDocs assembly (not the test assembly)
        var assembly = typeof(TemplateGenerator).Assembly;
        var resourceNames = assembly.GetManifestResourceNames();

        // Verify templates are embedded
        Assert.Contains(resourceNames, r => r.Contains("Templates") && r.Contains("agent-workflow"));
        Assert.Contains(resourceNames, r => r.Contains("Templates") && r.Contains("mode-code-writer"));
    }

    [Fact]
    public void GetAllTemplateNames_AllTemplates_CanBeReadAsBuiltIn()
    {
        var templateNames = TemplateGenerator.GetAllTemplateNames();

        foreach (var templateName in templateNames)
        {
            var content = TemplateGenerator.ReadBuiltInTemplate(templateName);
            Assert.NotEmpty(content);
        }
    }

    [Fact]
    public void EmbeddedTemplates_HaveExpectedContent()
    {
        // Verify specific content to ensure templates aren't empty or corrupted
        var workflowTemplate = TemplateGenerator.ReadBuiltInTemplate("agent-workflow.template.md");
        Assert.Contains("{{AGENT_NAME}}", workflowTemplate);
        Assert.Contains("dydo agent claim", workflowTemplate);

        var codeWriterTemplate = TemplateGenerator.ReadBuiltInTemplate("mode-code-writer.template.md");
        Assert.Contains("{{AGENT_NAME}}", codeWriterTemplate);
        Assert.Contains("code-writer", codeWriterTemplate);
    }

    #endregion

    [Fact]
    public void GetModeNames_ReturnsSevenModes()
    {
        var modes = TemplateGenerator.GetModeNames();

        Assert.Equal(7, modes.Count);
        Assert.Contains("code-writer", modes);
        Assert.Contains("reviewer", modes);
        Assert.Contains("co-thinker", modes);
        Assert.Contains("interviewer", modes);
        Assert.Contains("planner", modes);
        Assert.Contains("docs-writer", modes);
        Assert.Contains("tester", modes);
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
    public void GenerateModeFile_Reviewer_HasWorkspaceOnlyGuidance()
    {
        var content = TemplateGenerator.GenerateModeFile("Adele", "reviewer");

        Assert.Contains("Adele", content);
        Assert.Contains("workspace only", content.ToLowerInvariant());
        Assert.Contains("cannot edit source code", content.ToLowerInvariant());
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
    public void GenerateModeFile_Tester_SkipsCodingStandards()
    {
        var content = TemplateGenerator.GenerateModeFile("Adele", "tester");

        Assert.Contains("about.md", content);
        Assert.Contains("architecture.md", content);
        // Tester doesn't need coding standards - focuses on behavior, not code
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

        Assert.Contains("About This Project", content);
        Assert.Contains("Describe the project in 2-3 sentences", content);
        Assert.Contains("architecture.md", content);
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
        Assert.Contains("modes/co-thinker.md", content);
        Assert.Contains("modes/reviewer.md", content);
        Assert.Contains("modes/docs-writer.md", content);
        Assert.Contains("modes/tester.md", content);
    }

    [Fact]
    public void GenerateWorkflowFile_HasWorkflowFlags()
    {
        var content = TemplateGenerator.GenerateWorkflowFile("Adele");

        Assert.Contains("--feature", content);
        Assert.Contains("--task", content);
        Assert.Contains("--quick", content);
        Assert.Contains("--think", content);
        Assert.Contains("--review", content);
        Assert.Contains("--docs", content);
        Assert.Contains("--test", content);
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

    [Fact]
    public void GenerateWritingDocsMd_HasCorrectStructure()
    {
        var content = TemplateGenerator.GenerateWritingDocsMd();

        Assert.StartsWith("---", content);
        Assert.Contains("area: reference", content);
        Assert.Contains("Writing Documentation", content);
        Assert.Contains("Frontmatter", content);
        Assert.Contains("Naming Conventions", content);
    }

    [Theory]
    [InlineData("code-writer")]
    [InlineData("reviewer")]
    [InlineData("co-thinker")]
    [InlineData("interviewer")]
    [InlineData("planner")]
    [InlineData("docs-writer")]
    [InlineData("tester")]
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
    [InlineData("co-thinker", "Set Role")]
    [InlineData("interviewer", "Set Role")]
    [InlineData("planner", "Set Role")]
    [InlineData("docs-writer", "Set Role")]
    [InlineData("tester", "Set Role")]
    public void GenerateModeFile_AllModes_HaveSetRoleSection(string mode, string expectedSection)
    {
        var content = TemplateGenerator.GenerateModeFile("TestAgent", mode);

        Assert.Contains(expectedSection, content);
        Assert.Contains($"dydo agent role {mode}", content);
    }

    #region Asset Tests

    [Fact]
    public void GetAssetNames_ReturnsDydoDiagram()
    {
        var assets = TemplateGenerator.GetAssetNames();

        Assert.Contains("dydo-diagram.svg", assets);
    }

    [Fact]
    public void GetAssetNames_ReturnsAtLeastOneAsset()
    {
        var assets = TemplateGenerator.GetAssetNames();

        Assert.NotEmpty(assets);
    }

    [Fact]
    public void ReadEmbeddedAsset_ReturnsDydoDiagram()
    {
        var content = TemplateGenerator.ReadEmbeddedAsset("dydo-diagram.svg");

        Assert.NotNull(content);
        Assert.True(content.Length > 0, "Diagram asset should have content");
    }

    [Fact]
    public void ReadEmbeddedAsset_DiagramIsSvgFormat()
    {
        var content = TemplateGenerator.ReadEmbeddedAsset("dydo-diagram.svg");
        var text = System.Text.Encoding.UTF8.GetString(content!);

        Assert.Contains("<svg", text);
    }

    [Fact]
    public void ReadEmbeddedAsset_ReturnsNullForNonexistent()
    {
        var content = TemplateGenerator.ReadEmbeddedAsset("nonexistent.svg");

        Assert.Null(content);
    }

    [Fact]
    public void AllAssetNames_CanBeReadAsEmbedded()
    {
        var assetNames = TemplateGenerator.GetAssetNames();

        foreach (var assetName in assetNames)
        {
            var content = TemplateGenerator.ReadEmbeddedAsset(assetName);
            Assert.NotNull(content);
            Assert.True(content.Length > 0, $"Asset {assetName} should have content");
        }
    }

    [Fact]
    public void Assembly_ContainsEmbeddedAssetResources()
    {
        var assembly = typeof(TemplateGenerator).Assembly;
        var resourceNames = assembly.GetManifestResourceNames();

        Assert.Contains(resourceNames, r => r.Contains("Assets") && r.Contains("dydo-diagram"));
    }

    #endregion

    #region About DynaDocs Tests

    [Fact]
    public void GenerateAboutDynadocsMd_HasCorrectFrontmatter()
    {
        var content = TemplateGenerator.GenerateAboutDynadocsMd();

        Assert.StartsWith("---", content);
        Assert.Contains("area: reference", content);
        Assert.Contains("type: reference", content);
    }

    [Fact]
    public void GenerateAboutDynadocsMd_ContainsTitle()
    {
        var content = TemplateGenerator.GenerateAboutDynadocsMd();

        Assert.Contains("# DynaDocs (dydo)", content);
    }

    [Fact]
    public void GenerateAboutDynadocsMd_ReferencesDiagram()
    {
        var content = TemplateGenerator.GenerateAboutDynadocsMd();

        Assert.Contains("dydo-diagram.svg", content);
        Assert.Contains("_assets", content);
    }

    [Fact]
    public void GenerateAboutDynadocsMd_ContainsWorkflowFlags()
    {
        var content = TemplateGenerator.GenerateAboutDynadocsMd();

        Assert.Contains("--feature", content);
        Assert.Contains("--task", content);
        Assert.Contains("--quick", content);
    }

    [Fact]
    public void GenerateAboutDynadocsMd_ContainsAgentRoles()
    {
        var content = TemplateGenerator.GenerateAboutDynadocsMd();

        Assert.Contains("code-writer", content);
        Assert.Contains("reviewer", content);
    }

    [Fact]
    public void GenerateAboutDynadocsMd_LinksToGitHub()
    {
        var content = TemplateGenerator.GenerateAboutDynadocsMd();

        Assert.Contains("github.com/bodnarbalazs/dydo", content);
    }

    #endregion
}
