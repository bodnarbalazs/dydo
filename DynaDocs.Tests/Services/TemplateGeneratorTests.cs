namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

public class TemplateGeneratorTests
{
    #region Embedded Resource Tests

    [Fact]
    public void ReadBuiltInTemplate_ReadsFromEmbeddedResources()
    {
        // This should work even without a Templates folder on disk
        var content = TemplateGenerator.ReadBuiltInTemplate("skill-code-writer.template.md");

        Assert.NotEmpty(content);
        Assert.Contains("Code Writer", content);
    }

    [Fact]
    public void ReadBuiltInTemplate_ThrowsForNonexistentTemplate()
    {
        var ex = Assert.Throws<FileNotFoundException>(() =>
            TemplateGenerator.ReadBuiltInTemplate("nonexistent-template.md"));

        Assert.Contains("nonexistent-template.md", ex.Message);
    }

    [Theory]
    [InlineData("skill-code-writer.template.md")]
    [InlineData("skill-reviewer.template.md")]
    [InlineData("skill-co-thinker.template.md")]
    [InlineData("skill-planner.template.md")]
    [InlineData("skill-docs-writer.template.md")]
    [InlineData("skill-test-writer.template.md")]
    [InlineData("skill-orchestrator.template.md")]
    [InlineData("skill-self-improvement.template.md")]
    [InlineData("skill-wayfinder.template.md")]
    [InlineData("skill-grilling.template.md")]
    [InlineData("skill-bro.template.md")]
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

        // Verify templates are embedded. The skill templates are the source `dydo sync` compiles.
        Assert.Contains(resourceNames, r => r.Contains("Templates") && r.Contains("skill-code-writer"));
        Assert.Contains(resourceNames, r => r.Contains("Templates") && r.Contains("skill-self-improvement"));
        Assert.Contains(resourceNames, r => r.Contains("Templates") && r.Contains("skill-wayfinder"));
        Assert.Contains(resourceNames, r => r.Contains("Templates") && r.Contains("skill-grilling"));
        Assert.Contains(resourceNames, r => r.Contains("Templates") && r.Contains("skill-bro"));
        Assert.Contains(resourceNames, r => r.Contains("Templates") && r.Contains("index.template"));
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
        var codeWriterTemplate = TemplateGenerator.ReadBuiltInTemplate("skill-code-writer.template.md");
        Assert.Contains("mode: code-writer", codeWriterTemplate);
        Assert.Contains("Linear Issue", codeWriterTemplate);
        Assert.Contains("independent review", codeWriterTemplate);
    }

    [Theory]
    [InlineData("_tasks.template.md")]
    [InlineData("_issues.template.md")]
    [InlineData("_backlog.template.md")]
    public void RetiredProductTemplates_AreNotBuiltIn(string templateName)
    {
        Assert.DoesNotContain(templateName, TemplateGenerator.GetAllTemplateNames());
        Assert.Throws<FileNotFoundException>(() => TemplateGenerator.ReadBuiltInTemplate(templateName));
    }

    #endregion

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
    public void GenerateEntryPointMd_ContainsDr038RoutingParagraphExactlyOnce_WithoutMutatingClaudeMd()
    {
        const string routingParagraph = "Before creating a memory, check whether it belongs in dydo — it probably does (issue, decision, guide, or other record). Keep memory only for facts about your human and for harness mechanics no dydo record can hold. Never store incident state or temporary workarounds as memories.";
        var rootClaude = Path.Combine(FindRepositoryRoot(), "CLAUDE.md");
        var before = File.ReadAllBytes(rootClaude);

        var content = TemplateGenerator.GenerateEntryPointMd("Example");

        Assert.Equal(1, CountOccurrences(content, routingParagraph));
        Assert.Equal(1, CountOccurrences(TemplateGenerator.ReadBuiltInTemplate("entry-point.template.md"), routingParagraph));
        Assert.Equal(1, CountOccurrences(File.ReadAllText(rootClaude), routingParagraph));
        Assert.Equal(before, File.ReadAllBytes(rootClaude));
    }

    [Fact]
    public void GenerateIndexMd_ReturnsIndexTemplateContent()
    {
        var content = TemplateGenerator.GenerateIndexMd();

        Assert.StartsWith("---", content);
        Assert.Contains("DynaDocs", content);
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

    #region Asset Tests

    // The pre-DR-041 architecture diagram was retired (issue 0301): nothing ships, nothing is
    // scaffolded, and no embedded Assets resource remains. The plumbing itself stays for
    // future assets, so the empty/null behaviors are pinned here.
    [Fact]
    public void GetAssetNames_IsEmpty_DiagramRetired()
    {
        var assets = TemplateGenerator.GetAssetNames();

        Assert.Empty(assets);
    }

    [Fact]
    public void ReadEmbeddedAsset_ReturnsNullForRetiredDiagram()
    {
        var content = TemplateGenerator.ReadEmbeddedAsset("dydo-diagram.svg");

        Assert.Null(content);
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
    public void Assembly_ContainsNoRetiredDiagramResource()
    {
        var assembly = typeof(TemplateGenerator).Assembly;
        var resourceNames = assembly.GetManifestResourceNames();

        Assert.DoesNotContain(resourceNames, r => r.Contains("dydo-diagram"));
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
    public void GenerateAboutDynadocsMd_DefinesLinearAndGitBoundary()
    {
        var content = TemplateGenerator.GenerateAboutDynadocsMd();

        Assert.Contains("Linear owns the live Initiative/Project/Issue graph", content);
        Assert.Contains("Decisions, architecture, guides, reviewed Project plans", content);
        Assert.Contains("dydo does not copy that graph into\nMarkdown", content.Replace("\r\n", "\n"));
    }

    [Fact]
    public void GenerateAboutDynadocsMd_ContainsReviewAndAuditContract()
    {
        var content = TemplateGenerator.GenerateAboutDynadocsMd();

        Assert.Contains("independently reviews each implementation Issue", content);
        Assert.Contains("integrated audit against its linked plan", content);
        Assert.Contains("assimilation brief", content);
    }

    [Fact]
    public void GenerateAboutDynadocsMd_DoesNotRestoreRetiredWorkModel()
    {
        var content = TemplateGenerator.GenerateAboutDynadocsMd();

        Assert.DoesNotContain("dydo task", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dydo issue", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("project/tasks", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("project/issues", content, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Meta File Template Tests

    [Fact]
    public void GenerateProjectMetaMd_UsesContentsHeader()
    {
        var content = TemplateGenerator.GenerateProjectMetaMd();

        Assert.Contains("## Contents", content);
        Assert.DoesNotContain("## Subfolders", content);
    }

    [Fact]
    public void TopLevelMetaTemplates_HaveRelatedSectionsWithCorrectPaths()
    {
        // _understand.md links to docs in other folders
        var understand = TemplateGenerator.GenerateUnderstandMetaMd();
        Assert.Contains("## Related", understand);
        Assert.Contains("../reference/about-dynadocs.md", understand);
        Assert.Contains("../guides/how-to-use-docs.md", understand);
        Assert.Contains("../reference/writing-docs.md", understand);

        // _guides.md links to how-to-use-docs in same folder, others in different folders
        var guides = TemplateGenerator.GenerateGuidesMetaMd();
        Assert.Contains("## Related", guides);
        Assert.Contains("../reference/about-dynadocs.md", guides);
        Assert.Contains("./how-to-use-docs.md", guides);  // Same folder
        Assert.Contains("../reference/writing-docs.md", guides);

        // _reference.md links to docs in same folder, others in different folders
        var reference = TemplateGenerator.GenerateReferenceMetaMd();
        Assert.Contains("## Related", reference);
        Assert.Contains("./about-dynadocs.md", reference);  // Same folder
        Assert.Contains("../guides/how-to-use-docs.md", reference);
        Assert.Contains("./writing-docs.md", reference);  // Same folder

        // _project.md links to docs in other folders
        var project = TemplateGenerator.GenerateProjectMetaMd();
        Assert.Contains("## Related", project);
        Assert.Contains("../reference/about-dynadocs.md", project);
        Assert.Contains("../guides/how-to-use-docs.md", project);
        Assert.Contains("../reference/writing-docs.md", project);
    }

    [Fact]
    public void GenerateChangelogMetaMd_HasSoftRuleNote()
    {
        var content = TemplateGenerator.GenerateChangelogMetaMd();

        Assert.Contains("This structure is a suggestion", content);
        Assert.Contains("dydo doesn't enforce changelog folder structure", content);
    }

    [Fact]
    public void ProjectSubfolderMetas_DoNotReferenceNonExistentTemplates()
    {
        // These meta files should NOT reference templates in _system/templates/
        // because changelog/decision/pitfall templates are not copied there
        // (only skill-* templates are copied)

        var changelog = TemplateGenerator.GenerateChangelogMetaMd();
        Assert.DoesNotContain("_system/templates/", changelog);

        var decisions = TemplateGenerator.GenerateDecisionsMetaMd();
        Assert.DoesNotContain("_system/templates/", decisions);

        var pitfalls = TemplateGenerator.GeneratePitfallsMetaMd();
        Assert.DoesNotContain("_system/templates/", pitfalls);
    }

    [Fact]
    public void GenerateReferenceMetaMd_ListsCorrectDefaultFiles()
    {
        var content = TemplateGenerator.GenerateReferenceMetaMd();

        // These files are created by default scaffolding
        Assert.Contains("writing-docs.md", content);
        Assert.Contains("dydo-commands.md", content);
        Assert.Contains("about-dynadocs.md", content);
    }

    [Fact]
    public void GenerateUnderstandMetaMd_HasCorrectFrontmatter()
    {
        var content = TemplateGenerator.GenerateUnderstandMetaMd();

        Assert.StartsWith("---", content);
        Assert.Contains("area: understand", content);
        Assert.Contains("type: folder-meta", content);
    }

    [Fact]
    public void GenerateGuidesMetaMd_HasCorrectFrontmatter()
    {
        var content = TemplateGenerator.GenerateGuidesMetaMd();

        Assert.StartsWith("---", content);
        Assert.Contains("area: guides", content);
        Assert.Contains("type: folder-meta", content);
    }

    [Fact]
    public void GenerateReferenceMetaMd_HasCorrectFrontmatter()
    {
        var content = TemplateGenerator.GenerateReferenceMetaMd();

        Assert.StartsWith("---", content);
        Assert.Contains("area: reference", content);
        Assert.Contains("type: folder-meta", content);
    }

    [Fact]
    public void GenerateProjectMetaMd_HasCorrectFrontmatter()
    {
        var content = TemplateGenerator.GenerateProjectMetaMd();

        Assert.StartsWith("---", content);
        Assert.Contains("area: project", content);
        Assert.Contains("type: folder-meta", content);
    }

    [Fact]
    public void TopLevelMetaTemplates_LinkToSiblingFolders()
    {
        // _understand.md should link to guides, reference, project (but not understand)
        var understandContent = TemplateGenerator.GenerateUnderstandMetaMd();
        Assert.Contains("../guides/_index.md", understandContent);
        Assert.Contains("../reference/_index.md", understandContent);
        Assert.Contains("../project/_index.md", understandContent);

        // _guides.md should link to understand, reference, project (but not guides)
        var guidesContent = TemplateGenerator.GenerateGuidesMetaMd();
        Assert.Contains("../understand/_index.md", guidesContent);
        Assert.Contains("../reference/_index.md", guidesContent);
        Assert.Contains("../project/_index.md", guidesContent);

        // _reference.md should link to understand, guides, project (but not reference)
        var referenceContent = TemplateGenerator.GenerateReferenceMetaMd();
        Assert.Contains("../understand/_index.md", referenceContent);
        Assert.Contains("../guides/_index.md", referenceContent);
        Assert.Contains("../project/_index.md", referenceContent);

        // _project.md links to the durable knowledge references used from this folder.
        var projectContent = TemplateGenerator.GenerateProjectMetaMd();
        Assert.Contains("../reference/dydo-glossary.md", projectContent);
        Assert.Contains("../reference/about-dynadocs.md", projectContent);
        Assert.Contains("../guides/how-to-use-docs.md", projectContent);
    }

    #endregion

    #region Hub and Fallback Tests

    [Fact]
    public void GenerateHubIndex_ReturnsValidContent()
    {
        var content = TemplateGenerator.GenerateHubIndex("guides", "How-to guides for development", "guides");

        Assert.Contains("# Guides", content);
        Assert.Contains("How-to guides for development", content);
        Assert.Contains("area: guides", content);
        Assert.Contains("type: hub", content);
    }

    [Fact]
    public void GenerateHubIndex_CapitalizesFirstLetter()
    {
        var content = TemplateGenerator.GenerateHubIndex("reference", "API reference", "reference");

        Assert.Contains("# Reference", content);
    }

    [Fact]
    public void GenerateProjectSubfolderHub_ReturnsValidContent()
    {
        var content = TemplateGenerator.GenerateProjectSubfolderHub("tasks", "Task tracking");

        Assert.Contains("# Tasks", content);
        Assert.Contains("Task tracking", content);
        Assert.Contains("area: project", content);
        Assert.Contains("type: hub", content);
    }

    [Fact]
    public void GenerateProjectSubfolderHub_CapitalizesFirstLetter()
    {
        var content = TemplateGenerator.GenerateProjectSubfolderHub("changelog", "Change history");

        Assert.Contains("# Changelog", content);
    }

    [Fact]
    public void GenerateFallbackArchitectureMd_ReturnsValidContent()
    {
        var content = TemplateGenerator.GenerateFallbackArchitectureMd();

        Assert.Contains("# Architecture Overview", content);
        Assert.Contains("area: understand", content);
        Assert.Contains("type: concept", content);
        Assert.Contains("## Project Structure", content);
        Assert.Contains("## Key Components", content);
    }

    [Fact]
    public void GenerateFallbackWelcomeMd_ReturnsValidContent()
    {
        var content = TemplateGenerator.GenerateFallbackWelcomeMd();

        Assert.Contains("# Welcome", content);
        Assert.Contains("area: general", content);
        Assert.Contains("type: hub", content);
        Assert.Contains("## Getting Started", content);
        Assert.Contains("about.md", content);
        Assert.Contains("architecture.md", content);
        Assert.Contains("coding-standards.md", content);
    }

    [Fact]
    public void GenerateFallbackCodingStandardsMd_ReturnsValidContent()
    {
        var content = TemplateGenerator.GenerateFallbackCodingStandardsMd();

        Assert.Contains("# Coding Standards", content);
        Assert.Contains("area: guides", content);
        Assert.Contains("type: guide", content);
        Assert.Contains("## General Principles", content);
        Assert.Contains("## Naming Conventions", content);
        Assert.Contains("PascalCase", content);
        Assert.Contains("camelCase", content);
    }

    [Fact]
    public void GenerateFallbackHowToUseDocsMd_ReturnsValidContent()
    {
        var content = TemplateGenerator.GenerateFallbackHowToUseDocsMd();

        Assert.Contains("# How to Use These Docs", content);
        Assert.Contains("area: guides", content);
        Assert.Contains("type: guide", content);
        Assert.Contains("## Documentation Structure", content);
        Assert.Contains("understand/", content);
        Assert.Contains("guides/", content);
        Assert.Contains("reference/", content);
        Assert.Contains("project/", content);
        Assert.Contains("Decisions, reviewed plans, audits, changelog, pitfalls", content);
        Assert.DoesNotContain("tasks", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("## Document Types", content);
        Assert.Contains("## Navigation", content);
        Assert.Contains("dydo graph", content);
        Assert.Contains("## Key Reference Documents", content);
    }

    [Fact]
    public void GenerateFallbackFilesOffLimitsMd_ReturnsValidContent()
    {
        var content = TemplateGenerator.GenerateFallbackFilesOffLimitsMd();

        Assert.Contains("# Files Off-Limits", content);
        Assert.Contains("type: config", content);
        Assert.Contains(".env", content);
        Assert.Contains("secrets.json", content);
        Assert.Contains("*.pem", content);
        Assert.Contains("*.key", content);
        Assert.Contains(".aws", content);
    }

    [Fact]
    public void GenerateFutureFeaturesMetaMd_ReturnsValidContent()
    {
        var content = TemplateGenerator.GenerateFutureFeaturesMetaMd();
        Assert.NotEmpty(content);
    }

    [Fact]
    public void GenerateFallbackAboutMd_ReturnsValidContent()
    {
        var content = TemplateGenerator.GenerateFallbackAboutMd();

        Assert.Contains("# About This Project", content);
        Assert.Contains("area: understand", content);
        Assert.Contains("type: context", content);
        Assert.Contains("architecture.md", content);
    }

    [Fact]
    public void GenerateFallbackDydoCommandsMd_ReturnsValidContent()
    {
        var content = TemplateGenerator.GenerateFallbackDydoCommandsMd();

        Assert.Contains("# CLI Commands Reference", content);
        Assert.Contains("area: reference", content);
        Assert.Contains("type: reference", content);
        Assert.Contains("## Setup Commands", content);
        Assert.Contains("## Documentation Commands", content);
        Assert.Contains("## Work Boundary", content);
        Assert.Contains("dydo init", content);
        Assert.Contains("dydo sync", content);
        Assert.Contains("dydo check", content);
        Assert.Contains("dydo guard", content);
        Assert.Contains("dydo model", content);
        Assert.Contains("Live work is managed in Linear", content);
        Assert.DoesNotContain("dydo task", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dydo issue", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dydo review", content, StringComparison.OrdinalIgnoreCase);
        // The fallback must track the current command surface, not the retired 1.0 table.
        Assert.DoesNotContain("dydo dispatch", content);
        Assert.DoesNotContain("dydo whoami", content);
        Assert.DoesNotContain("dydo agent", content);
        Assert.DoesNotContain("dydo inbox", content);
        Assert.DoesNotContain("dydo workspace", content);
        Assert.DoesNotContain("dydo audit", content);
    }

    [Fact]
    public void GenerateFallbackWritingDocsMd_ReturnsValidContent()
    {
        var content = TemplateGenerator.GenerateFallbackWritingDocsMd();

        Assert.Contains("# Writing Documentation", content);
        Assert.Contains("area: reference", content);
        Assert.Contains("type: reference", content);
        Assert.Contains("## Frontmatter", content);
        Assert.Contains("## Naming Conventions", content);
        Assert.Contains("kebab-case", content);
        Assert.Contains("## Validation", content);
        Assert.Contains("dydo check", content);
    }

    [Fact]
    public void GenerateFallbackGlossaryMd_ReturnsValidContent()
    {
        var content = TemplateGenerator.GenerateFallbackGlossaryMd();

        Assert.Contains("# Glossary", content);
        Assert.Contains("area: general", content);
        Assert.Contains("type: reference", content);
        Assert.Contains("## Project Terms", content);
    }

    [Fact]
    public void GenerateFallbackAboutDynadocsMd_ReturnsValidContent()
    {
        var content = TemplateGenerator.GenerateFallbackAboutDynadocsMd();

        Assert.Contains("# DynaDocs (dydo)", content);
        Assert.Contains("area: reference", content);
        Assert.Contains("type: reference", content);
        Assert.Contains("## The Problem", content);
        Assert.Contains("## The Solution", content);
        Assert.Contains("## Agent Roles", content);
        Assert.Contains("Linear owns the live", content);
        Assert.Contains("code-writer", content);
        Assert.Contains("reviewer", content);
        Assert.Contains("github.com/bodnarbalazs/dydo", content);
        Assert.DoesNotContain("PM system that lives in your repo", content);
        Assert.DoesNotContain("backlog", content, StringComparison.OrdinalIgnoreCase);

        // Pre-DR-041 leftovers must not resurface (issue 0301): no retired diagram embed,
        // no --inbox workflow flags.
        Assert.DoesNotContain("dydo-diagram.svg", content);
        Assert.DoesNotContain("Workflow Flags", content);
        Assert.DoesNotContain("--inbox", content);
    }

    private static int CountOccurrences(string content, string value) =>
        content.Split(value, StringSplitOptions.None).Length - 1;

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(Environment.CurrentDirectory); directory != null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DynaDocs.csproj")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not find the DynaDocs repository root.");
    }

    #endregion
}
