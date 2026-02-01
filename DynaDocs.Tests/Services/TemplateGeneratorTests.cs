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
    [InlineData("process-index.template.md")]
    [InlineData("process-feature-implementation.template.md")]
    [InlineData("process-bug-fix.template.md")]
    [InlineData("process-refactoring.template.md")]
    [InlineData("process-code-review.template.md")]
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

    #region Process Document Generation Tests

    // Process Index Tests
    [Fact]
    public void GenerateProcessIndexMd_HasCorrectFrontmatter()
    {
        var content = TemplateGenerator.GenerateProcessIndexMd();

        Assert.StartsWith("---", content);
        Assert.Contains("area: project", content);
        Assert.Contains("type: hub", content);
    }

    [Fact]
    public void GenerateProcessIndexMd_LinksToAllProcesses()
    {
        var content = TemplateGenerator.GenerateProcessIndexMd();

        Assert.Contains("feature-implementation", content);
        Assert.Contains("bug-fix", content);
        Assert.Contains("refactoring", content);
        Assert.Contains("code-review", content);
    }

    [Fact]
    public void GenerateProcessIndexMd_DocumentsNoSelfReview()
    {
        var content = TemplateGenerator.GenerateProcessIndexMd().ToLowerInvariant();

        // Should mention self-review rule
        Assert.Contains("self-review", content);
    }

    // Feature Implementation Process Tests
    [Fact]
    public void GenerateFeatureImplementationProcessMd_HasCorrectFrontmatter()
    {
        var content = TemplateGenerator.GenerateFeatureImplementationProcessMd();

        Assert.StartsWith("---", content);
        Assert.Contains("area: project", content);
        Assert.Contains("type: process", content);
    }

    [Fact]
    public void GenerateFeatureImplementationProcessMd_DocumentsAllPhases()
    {
        var content = TemplateGenerator.GenerateFeatureImplementationProcessMd();

        Assert.Contains("Interviewer", content);
        Assert.Contains("Planner", content);
        Assert.Contains("Code-Writer", content);
        Assert.Contains("Reviewer", content);
    }

    [Fact]
    public void GenerateFeatureImplementationProcessMd_HasPlanningTriggers()
    {
        var content = TemplateGenerator.GenerateFeatureImplementationProcessMd().ToLowerInvariant();

        // Should mention when to use planning
        Assert.True(
            content.Contains("trigger") || content.Contains("when to use") || content.Contains("use this"),
            "Should describe when to use feature implementation process");
    }

    // Bug Fix Process Tests
    [Fact]
    public void GenerateBugFixProcessMd_HasCorrectFrontmatter()
    {
        var content = TemplateGenerator.GenerateBugFixProcessMd();

        Assert.StartsWith("---", content);
        Assert.Contains("area: project", content);
        Assert.Contains("type: process", content);
    }

    [Fact]
    public void GenerateBugFixProcessMd_DocumentsInvestigationPhase()
    {
        var content = TemplateGenerator.GenerateBugFixProcessMd().ToLowerInvariant();

        // Should mention investigation or co-thinker phase
        Assert.True(
            content.Contains("investigation") || content.Contains("co-thinker"),
            "Should document investigation/co-thinker phase");
    }

    [Fact]
    public void GenerateBugFixProcessMd_HasComplexityDecision()
    {
        var content = TemplateGenerator.GenerateBugFixProcessMd().ToLowerInvariant();

        // Should mention simple vs complex decision
        Assert.True(
            content.Contains("simple") || content.Contains("complex") || content.Contains("trivial"),
            "Should document complexity decision criteria");
    }

    // Refactoring Process Tests
    [Fact]
    public void GenerateRefactoringProcessMd_HasCorrectFrontmatter()
    {
        var content = TemplateGenerator.GenerateRefactoringProcessMd();

        Assert.StartsWith("---", content);
        Assert.Contains("area: project", content);
        Assert.Contains("type: process", content);
    }

    [Fact]
    public void GenerateRefactoringProcessMd_EmphasizesDiscussion()
    {
        var content = TemplateGenerator.GenerateRefactoringProcessMd().ToLowerInvariant();

        // Should mention discussion or co-thinker first approach
        Assert.True(
            content.Contains("discussion") || content.Contains("co-thinker") || content.Contains("agree"),
            "Should emphasize discussion-first approach");
    }

    [Fact]
    public void GenerateRefactoringProcessMd_HasInvariants()
    {
        var content = TemplateGenerator.GenerateRefactoringProcessMd().ToLowerInvariant();

        // Should mention behavior preservation or invariants
        Assert.True(
            content.Contains("invariant") || content.Contains("behavior") || content.Contains("preserve"),
            "Should document behavior preservation/invariants");
    }

    // Code Review Process Tests
    [Fact]
    public void GenerateCodeReviewProcessMd_HasCorrectFrontmatter()
    {
        var content = TemplateGenerator.GenerateCodeReviewProcessMd();

        Assert.StartsWith("---", content);
        Assert.Contains("area: project", content);
        Assert.Contains("type: process", content);
    }

    [Fact]
    public void GenerateCodeReviewProcessMd_HasReviewerMindset()
    {
        var content = TemplateGenerator.GenerateCodeReviewProcessMd().ToLowerInvariant();

        // Should mention senior engineer or reviewer mindset
        Assert.True(
            content.Contains("senior") || content.Contains("mindset") || content.Contains("disdain"),
            "Should document reviewer mindset");
    }

    [Fact]
    public void GenerateCodeReviewProcessMd_HasAiSlopDetection()
    {
        var content = TemplateGenerator.GenerateCodeReviewProcessMd().ToLowerInvariant();

        // Should mention AI slop
        Assert.Contains("slop", content);
    }

    [Fact]
    public void GenerateCodeReviewProcessMd_HasComprehensiveChecklist()
    {
        var content = TemplateGenerator.GenerateCodeReviewProcessMd();

        // Should have key checklist categories
        Assert.Contains("Correctness", content);
        Assert.Contains("Security", content);
        Assert.True(
            content.Contains("Maintainability") || content.Contains("maintainable"),
            "Should have maintainability section");
        Assert.True(
            content.Contains("Performance") || content.Contains("performance"),
            "Should have performance section");
    }

    // General Process Generation Tests
    [Theory]
    [InlineData("ProcessIndex")]
    [InlineData("FeatureImplementation")]
    [InlineData("BugFix")]
    [InlineData("Refactoring")]
    [InlineData("CodeReview")]
    public void GenerateProcessMd_AllProcesses_ReturnNonEmptyContent(string processType)
    {
        var content = processType switch
        {
            "ProcessIndex" => TemplateGenerator.GenerateProcessIndexMd(),
            "FeatureImplementation" => TemplateGenerator.GenerateFeatureImplementationProcessMd(),
            "BugFix" => TemplateGenerator.GenerateBugFixProcessMd(),
            "Refactoring" => TemplateGenerator.GenerateRefactoringProcessMd(),
            "CodeReview" => TemplateGenerator.GenerateCodeReviewProcessMd(),
            _ => throw new ArgumentException($"Unknown process type: {processType}")
        };

        Assert.False(string.IsNullOrWhiteSpace(content));
        Assert.True(content.Length > 100, "Process document should have substantial content");
    }

    #endregion
}
