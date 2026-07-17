namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;
using DynaDocs.Services;

/// <summary>
/// Integration tests for project-local template overrides in _system/templates/.
/// The claim-time mode-file generation tests were removed with the claim ceremony (DR-041) —
/// modes are compiled by <c>dydo sync</c>, not created at claim.
/// </summary>
[Collection("Integration")]
public class TemplateOverrideTests : IntegrationTestBase
{
    [Fact]
    public async Task Init_CopiesTemplatesToSystemFolder()
    {
        await InitProjectAsync();

        AssertDirectoryExists("dydo/_system/templates");
        AssertFileExists("dydo/_system/templates/mode-code-writer.template.md");
        AssertFileExists("dydo/_system/templates/mode-reviewer.template.md");
        AssertFileExists("dydo/_system/templates/mode-co-thinker.template.md");
        AssertFileExists("dydo/_system/templates/mode-planner.template.md");
        AssertFileExists("dydo/_system/templates/mode-docs-writer.template.md");
        AssertFileExists("dydo/_system/templates/mode-test-writer.template.md");
        AssertFileExists("dydo/_system/templates/mode-orchestrator.template.md");
    }

    [Fact]
    public async Task Init_SystemTemplatesMatchBuiltIn()
    {
        await InitProjectAsync();

        var copiedContent = ReadFile("dydo/_system/templates/mode-code-writer.template.md");
        var builtInContent = TemplateGenerator.ReadBuiltInTemplate("mode-code-writer.template.md");

        Assert.Equal(builtInContent, copiedContent);
    }

    [Fact]
    public async Task Join_DoesNotOverwriteExistingTemplates()
    {
        await InitProjectAsync("none", "testuser");

        // Modify a template
        var templatePath = Path.Combine(TestDir, "dydo/_system/templates/mode-code-writer.template.md");
        var customContent = "<!-- CUSTOM_CONTENT_PRESERVED -->\nCustom template";
        File.WriteAllText(templatePath, customContent);

        // Join as another user
        await JoinProjectAsync("none", "alice");

        // Verify custom template was NOT overwritten
        var contentAfterJoin = File.ReadAllText(templatePath);
        Assert.Contains("CUSTOM_CONTENT_PRESERVED", contentAfterJoin);
    }

    [Fact]
    public void GetAllTemplateNames_ReturnsExpectedTemplates()
    {
        var templateNames = TemplateGenerator.GetAllTemplateNames();

        Assert.Contains("mode-code-writer.template.md", templateNames);
        Assert.Contains("mode-reviewer.template.md", templateNames);
        Assert.Contains("mode-planner.template.md", templateNames);
        Assert.Contains("mode-chief-of-staff.template.md", templateNames);

        // 8 mode templates (the compiler's role sources) + the reviewer's 5 skill
        // resource templates (<role>-resource-<name>.template.md).
        Assert.Contains("reviewer-resource-plan.template.md", templateNames);
        Assert.Equal(13, templateNames.Count);
    }

    [Fact]
    public void ReadBuiltInTemplate_ReturnsTemplateContent()
    {
        var content = TemplateGenerator.ReadBuiltInTemplate("mode-code-writer.template.md");

        Assert.NotEmpty(content);
        Assert.Contains("Code Writer", content);
    }

    [Fact]
    public void ReadBuiltInTemplate_ThrowsForMissingTemplate()
    {
        Assert.Throws<FileNotFoundException>(() =>
            TemplateGenerator.ReadBuiltInTemplate("nonexistent.template.md"));
    }

    #region Template Additions

    [Fact]
    public async Task Init_CreatesTemplateAdditionsFolder()
    {
        await InitProjectAsync();

        AssertDirectoryExists("dydo/_system/template-additions");
    }

    [Fact]
    public async Task Init_CreatesReadmeInAdditions()
    {
        await InitProjectAsync();

        AssertFileExists("dydo/_system/template-additions/_README.md");
        var content = ReadFile("dydo/_system/template-additions/_README.md");
        Assert.Contains("Template Additions", content);
    }

    [Fact]
    public async Task Init_CreatesExampleFile()
    {
        await InitProjectAsync();

        AssertFileExists("dydo/_system/template-additions/extra-verify.md.example");
    }

    [Fact]
    public async Task Init_StoresFrameworkHashes()
    {
        await InitProjectAsync();

        var json = ReadFile("dydo.json");
        var config = System.Text.Json.JsonSerializer.Deserialize(json,
            DynaDocs.Serialization.DydoConfigJsonContext.Default.DydoConfig)!;

        // Must have a hash for every framework template file
        foreach (var templateFile in TemplateCommand.FrameworkTemplateFiles)
            Assert.True(config.FrameworkHashes.ContainsKey(templateFile),
                $"Missing hash for framework template: {templateFile}");

        // Each hash must be a non-empty SHA256 hex string (64 chars)
        foreach (var (key, hash) in config.FrameworkHashes)
        {
            Assert.False(string.IsNullOrWhiteSpace(hash), $"Empty hash for {key}");
            Assert.Equal(64, hash.Length);
        }
    }

    [Fact]
    public async Task Init_FrameworkHashes_MatchEmbeddedTemplateContent()
    {
        // Regression for Slice 3: when embedded templates change and dydo.json
        // hashes are bumped to the new content, init must produce the same hash
        // — guaranteeing no false-positive override detection downstream.
        await InitProjectAsync();

        var json = ReadFile("dydo.json");
        var config = System.Text.Json.JsonSerializer.Deserialize(json,
            DynaDocs.Serialization.DydoConfigJsonContext.Default.DydoConfig)!;

        foreach (var name in TemplateGenerator.GetAllTemplateNames())
        {
            var relativePath = $"_system/templates/{name}";
            var embedded = TemplateGenerator.ReadBuiltInTemplate(name);
            var expectedHash = TemplateCommand.ComputeHash(embedded);
            Assert.Equal(expectedHash, config.FrameworkHashes[relativePath]);
        }
    }

    [Fact]
    public async Task Join_DoesNotOverwriteExistingAdditions()
    {
        await InitProjectAsync("none", "testuser");

        // Create a custom addition
        var additionsPath = Path.Combine(TestDir, "dydo/_system/template-additions");
        File.WriteAllText(Path.Combine(additionsPath, "custom-step.md"), "Custom content");

        // Join as another user
        await JoinProjectAsync("none", "alice");

        // Verify custom addition was NOT deleted
        Assert.True(File.Exists(Path.Combine(additionsPath, "custom-step.md")));
        var content = File.ReadAllText(Path.Combine(additionsPath, "custom-step.md"));
        Assert.Equal("Custom content", content);
    }

    #endregion
}
