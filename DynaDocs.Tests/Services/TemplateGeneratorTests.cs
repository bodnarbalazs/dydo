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
    [InlineData("mode-planner.template.md")]
    [InlineData("mode-docs-writer.template.md")]
    [InlineData("mode-test-writer.template.md")]
    [InlineData("mode-inquisitor.template.md")]
    [InlineData("mode-judge.template.md")]
    [InlineData("mode-orchestrator.template.md")]
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
    public void GetModeNames_ReturnsAllBaseModes()
    {
        var modes = TemplateGenerator.GetModeNames();

        Assert.Equal(9, modes.Count);
        Assert.Contains("code-writer", modes);
        Assert.Contains("reviewer", modes);
        Assert.Contains("co-thinker", modes);
        Assert.Contains("planner", modes);
        Assert.Contains("docs-writer", modes);
        Assert.Contains("test-writer", modes);
        Assert.Contains("orchestrator", modes);
        Assert.Contains("inquisitor", modes);
        Assert.Contains("judge", modes);
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
    public void GenerateModeFile_Planner_SkipsCodingStandards()
    {
        var content = TemplateGenerator.GenerateModeFile("Adele", "planner");

        Assert.Contains("about.md", content);
        Assert.Contains("architecture.md", content);
        // Planner doesn't need coding standards yet
        Assert.DoesNotContain("coding-standards.md", content);
    }

    [Fact]
    public void GenerateModeFile_TestWriter_SkipsCodingStandards()
    {
        var content = TemplateGenerator.GenerateModeFile("Adele", "test-writer");

        Assert.Contains("about.md", content);
        Assert.Contains("architecture.md", content);
        Assert.Contains("coding-standards.md", content);
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

        Assert.Contains("modes/planner.md", content);
        Assert.Contains("modes/code-writer.md", content);
        Assert.Contains("modes/co-thinker.md", content);
        Assert.Contains("modes/reviewer.md", content);
        Assert.Contains("modes/docs-writer.md", content);
        Assert.Contains("modes/test-writer.md", content);
    }

    [Fact]
    public void GenerateWorkflowFile_HasWorkflowFlags()
    {
        var content = TemplateGenerator.GenerateWorkflowFile("Adele");

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
    [InlineData("planner")]
    [InlineData("docs-writer")]
    [InlineData("test-writer")]
    [InlineData("inquisitor")]
    [InlineData("judge")]
    [InlineData("orchestrator")]
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
    [InlineData("planner", "Set Role")]
    [InlineData("docs-writer", "Set Role")]
    [InlineData("test-writer", "Set Role")]
    [InlineData("inquisitor", "Set Role")]
    [InlineData("judge", "Set Role")]
    [InlineData("orchestrator", "Set Role")]
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
    public void GenerateAboutDynadocsMd_ContainsInboxFlag()
    {
        var content = TemplateGenerator.GenerateAboutDynadocsMd();

        Assert.Contains("--inbox", content);
        Assert.Contains("dydo inbox show", content);
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
        // (only agent-workflow and mode-* templates are copied)

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

    #region Role Table Tests

    [Fact]
    public void GenerateRoleTable_WithNullBasePath_FallsBackToBaseDefinitions()
    {
        var table = TemplateGenerator.GenerateRoleTable(null);

        Assert.Contains("| Role | Purpose | Mode File |", table);
        Assert.Contains("|------|---------|-----------|", table);
        Assert.Contains("code-writer", table);
        Assert.Contains("reviewer", table);
        Assert.Contains("co-thinker", table);
        Assert.Contains("planner", table);
        Assert.Contains("docs-writer", table);
        Assert.Contains("test-writer", table);
        Assert.Contains("orchestrator", table);
        Assert.Contains("inquisitor", table);
        Assert.Contains("judge", table);
    }

    [Fact]
    public void GenerateRoleTable_FallbackTable_HasModeFileLinks()
    {
        var table = TemplateGenerator.GenerateRoleTable(null);

        Assert.Contains("[modes/code-writer.md](modes/code-writer.md)", table);
        Assert.Contains("[modes/reviewer.md](modes/reviewer.md)", table);
        Assert.Contains("[modes/planner.md](modes/planner.md)", table);
    }

    [Fact]
    public void GenerateRoleTable_FallbackTable_IncludesDescriptions()
    {
        var table = TemplateGenerator.GenerateRoleTable(null);

        Assert.Contains("Implements features and fixes bugs", table);
        Assert.Contains("Reviews code changes", table);
    }

    [Fact]
    public void GenerateRoleTable_FallbackTable_IsSortedAlphabetically()
    {
        var table = TemplateGenerator.GenerateRoleTable(null);
        var lines = table.Split('\n').Skip(2).ToList(); // Skip header rows

        var roleNames = lines.Select(l => l.Split('|')[1].Trim()).ToList();
        var sorted = roleNames.OrderBy(n => n, StringComparer.Ordinal).ToList();

        Assert.Equal(sorted, roleNames);
    }

    [Fact]
    public void GenerateRoleTable_WithRoleFiles_LoadsFromDisk()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dydo-test-{Guid.NewGuid():N}");
        var rolesDir = Path.Combine(tempDir, "_system", "roles");
        Directory.CreateDirectory(rolesDir);

        try
        {
            var roleJson = """
                {
                    "name": "custom-role",
                    "description": "A custom test role.",
                    "base": false,
                    "writablePaths": ["src/**"],
                    "readOnlyPaths": [],
                    "templateFile": "mode-custom-role.template.md",
                    "constraints": []
                }
                """;
            File.WriteAllText(Path.Combine(rolesDir, "custom-role.role.json"), roleJson);

            var table = TemplateGenerator.GenerateRoleTable(tempDir);

            Assert.Contains("custom-role", table);
            Assert.Contains("A custom test role.", table);
            Assert.Contains("[modes/custom-role.md](modes/custom-role.md)", table);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void GenerateRoleTable_WithEmptyRolesDir_FallsBackToBaseDefinitions()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dydo-test-{Guid.NewGuid():N}");
        var rolesDir = Path.Combine(tempDir, "_system", "roles");
        Directory.CreateDirectory(rolesDir);

        try
        {
            var table = TemplateGenerator.GenerateRoleTable(tempDir);

            // Falls back to base definitions
            Assert.Contains("code-writer", table);
            Assert.Contains("reviewer", table);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void GenerateWorkflowFile_ContainsRoleTableNotPlaceholder()
    {
        var content = TemplateGenerator.GenerateWorkflowFile("Adele");

        Assert.DoesNotContain("{{ROLE_TABLE}}", content);
        Assert.Contains("| Role | Purpose | Mode File |", content);
        Assert.Contains("code-writer", content);
    }

    #endregion

    #region Fallback and Uncovered Method Tests

    [Fact]
    public void GenerateAgentStatesMd_ReturnsValidContent()
    {
        var agents = new List<string> { "Alpha", "Beta", "Gamma" };
        var content = TemplateGenerator.GenerateAgentStatesMd(agents);

        Assert.NotEmpty(content);
        Assert.Contains("Alpha", content);
        Assert.Contains("Beta", content);
        Assert.Contains("Gamma", content);
    }

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
    public void GenerateJitiIndexMd_ReturnsValidContent()
    {
        var agents = new List<string> { "Alpha", "Beta" };
        var workflowLinks = "- [Alpha](agents/Alpha/workflow.md)\n- [Beta](agents/Beta/workflow.md)";
        var content = TemplateGenerator.GenerateJitiIndexMd(agents, workflowLinks);

        Assert.Contains("# DynaDocs", content);
        Assert.Contains("Alpha", content);
        Assert.Contains("Beta", content);
        Assert.Contains("agents/Alpha/workflow.md", content);
        Assert.Contains("area: general", content);
        Assert.Contains("type: hub", content);
        Assert.Contains("dydo agent claim", content);
        Assert.Contains("dydo check", content);
    }

    [Fact]
    public void GenerateJitiIndexMd_HasDocumentationStructure()
    {
        var agents = new List<string> { "TestAgent" };
        var links = "- [TestAgent](agents/TestAgent/workflow.md)";
        var content = TemplateGenerator.GenerateJitiIndexMd(agents, links);

        Assert.Contains("## Documentation Structure", content);
        Assert.Contains("understand/", content);
        Assert.Contains("guides/", content);
        Assert.Contains("reference/", content);
        Assert.Contains("project/", content);
    }

    [Fact]
    public void GenerateFallbackWorkflowFile_ReturnsValidContent()
    {
        var content = TemplateGenerator.GenerateFallbackWorkflowFile(
            "TestAgent", ["src/**"], ["tests/**"]);

        Assert.Contains("# Workflow — TestAgent", content);
        Assert.Contains("agent: TestAgent", content);
        Assert.Contains("dydo agent claim TestAgent", content);
        Assert.Contains("`src/**`", content);
        Assert.Contains("`tests/**`", content);
        Assert.Contains("## Must-Read Documents", content);
        Assert.Contains("## Setting Your Role", content);
    }

    [Fact]
    public void GenerateFallbackWorkflowFile_IncludesAllRoles()
    {
        var content = TemplateGenerator.GenerateFallbackWorkflowFile(
            "Alpha", ["Commands/**"], ["DynaDocs.Tests/**"]);

        Assert.Contains("code-writer", content);
        Assert.Contains("reviewer", content);
        Assert.Contains("co-thinker", content);
        Assert.Contains("docs-writer", content);
        Assert.Contains("planner", content);
        Assert.Contains("test-writer", content);
    }

    [Fact]
    public void GenerateFallbackWorkflowFile_HasQuickReference()
    {
        var content = TemplateGenerator.GenerateFallbackWorkflowFile(
            "Bravo", ["src/**"], ["tests/**"]);

        Assert.Contains("## Quick Reference", content);
        Assert.Contains("dydo whoami", content);
        Assert.Contains("dydo agent release", content);
        Assert.Contains("dydo inbox show", content);
        Assert.Contains("dydo dispatch", content);
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

    [Theory]
    [InlineData("code-writer", "implement code")]
    [InlineData("reviewer", "review code and provide feedback")]
    [InlineData("co-thinker", "think through problems collaboratively")]
    [InlineData("planner", "design implementation plans")]
    [InlineData("docs-writer", "write documentation")]
    [InlineData("test-writer", "write tests and report issues")]
    public void GenerateFallbackModeFile_KnownModes_HaveCorrectDescription(string modeName, string expectedDesc)
    {
        var content = TemplateGenerator.GenerateFallbackModeFile(
            "TestAgent", modeName, ["src/**"], ["tests/**"]);

        Assert.Contains(expectedDesc, content);
        Assert.Contains($"agent: TestAgent", content);
        Assert.Contains($"mode: {modeName}", content);
    }

    [Fact]
    public void GenerateFallbackModeFile_UnknownMode_UsesGenericDescription()
    {
        var content = TemplateGenerator.GenerateFallbackModeFile(
            "Alpha", "unknown-mode", ["src/**"], ["tests/**"]);

        Assert.Contains("complete your assigned work", content);
        Assert.Contains("(check with dydo agent status)", content);
    }

    [Fact]
    public void GenerateFallbackModeFile_HasMustReadsAndSetRole()
    {
        var content = TemplateGenerator.GenerateFallbackModeFile(
            "Alpha", "code-writer", ["Commands/**"], ["Tests/**"]);

        Assert.Contains("## Must-Reads", content);
        Assert.Contains("about.md", content);
        Assert.Contains("architecture.md", content);
        Assert.Contains("## Set Role", content);
        Assert.Contains("dydo agent role code-writer", content);
        Assert.Contains("## Verify", content);
        Assert.Contains("dydo agent status", content);
    }

    [Theory]
    [InlineData("code-writer")]
    [InlineData("reviewer")]
    [InlineData("co-thinker")]
    [InlineData("planner")]
    [InlineData("docs-writer")]
    [InlineData("test-writer")]
    public void GenerateFallbackModeFile_AllKnownModes_HaveEditPermissions(string modeName)
    {
        var content = TemplateGenerator.GenerateFallbackModeFile(
            "TestAgent", modeName, ["src/**"], ["tests/**"]);

        Assert.Contains("You can edit:", content);
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
        Assert.Contains("## Agent Commands", content);
        Assert.Contains("## Task Commands", content);
        Assert.Contains("## Workflow Commands", content);
        Assert.Contains("## Audit Commands", content);
        Assert.Contains("dydo init", content);
        Assert.Contains("dydo check", content);
        Assert.Contains("dydo agent claim", content);
        Assert.Contains("dydo task create", content);
        Assert.Contains("dydo dispatch", content);
        Assert.Contains("dydo audit", content);
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

    [Fact]
    public void GenerateFallbackAgentStatesMd_ReturnsValidContent()
    {
        var rows = "| Alpha | free | - | - | - |\n| Beta | free | - | - | - |";
        var content = TemplateGenerator.GenerateFallbackAgentStatesMd(rows);

        Assert.Contains("# Agent States", content);
        Assert.Contains("Alpha", content);
        Assert.Contains("Beta", content);
        Assert.Contains("## Pending Inbox", content);
        Assert.Contains("last-updated:", content);
    }

    #endregion
}
