namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

public class TemplateGeneratorTests
{
    #region Embedded Resource Tests

    [Fact]
    public void ReadBuiltInTemplate_ReadsFromEmbeddedResources()
    {
        // This should work even without a Templates folder on disk
        var content = TemplateGenerator.ReadBuiltInTemplate("mode-code-writer.template.md");

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
    [InlineData("mode-code-writer.template.md")]
    [InlineData("mode-reviewer.template.md")]
    [InlineData("mode-co-thinker.template.md")]
    [InlineData("mode-planner.template.md")]
    [InlineData("mode-docs-writer.template.md")]
    [InlineData("mode-test-writer.template.md")]
    [InlineData("mode-orchestrator.template.md")]
    [InlineData("mode-sprint-auditor.template.md")]
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

        // Verify templates are embedded. The mode templates are the source `dydo sync` compiles.
        Assert.Contains(resourceNames, r => r.Contains("Templates") && r.Contains("mode-code-writer"));
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
        var codeWriterTemplate = TemplateGenerator.ReadBuiltInTemplate("mode-code-writer.template.md");
        Assert.Contains("{{AGENT_NAME}}", codeWriterTemplate);
        Assert.Contains("code-writer", codeWriterTemplate);
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
    public void GenerateAboutDynadocsMd_ReferencesVisualPlaceholder()
    {
        var content = TemplateGenerator.GenerateAboutDynadocsMd();

        // The diagram was replaced by a deliberate placeholder pointing at the _assets folder.
        Assert.Contains("<!-- VISUAL:", content);
        Assert.Contains("dydo/_assets/", content);
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
        // (only mode-* templates are copied)

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

        // _project.md should link to understand, guides, reference (but not project)
        var projectContent = TemplateGenerator.GenerateProjectMetaMd();
        Assert.Contains("../understand/_index.md", projectContent);
        Assert.Contains("../guides/_index.md", projectContent);
        Assert.Contains("../reference/_index.md", projectContent);
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
    public void GenerateIssuesMetaMd_ReturnsValidContent()
    {
        var content = TemplateGenerator.GenerateIssuesMetaMd();
        Assert.NotEmpty(content);
    }

    [Fact]
    public void GenerateBacklogMetaMd_ReturnsValidContent()
    {
        var content = TemplateGenerator.GenerateBacklogMetaMd();
        Assert.NotEmpty(content);
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
        Assert.Contains("## Project Commands", content);
        Assert.Contains("dydo init", content);
        Assert.Contains("dydo sync", content);
        Assert.Contains("dydo check", content);
        Assert.Contains("dydo guard", content);
        Assert.Contains("dydo model", content);
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
        Assert.Contains("dydo-diagram.svg", content);
        Assert.Contains("## Workflow Flags", content);
        Assert.Contains("## Agent Roles", content);
        Assert.Contains("code-writer", content);
        Assert.Contains("reviewer", content);
        Assert.Contains("github.com/bodnarbalazs/dydo", content);
    }

    #endregion
}
