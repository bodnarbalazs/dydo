namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;
using DynaDocs.Services;

/// <summary>
/// Integration tests for project-local template overrides in _system/templates/.
/// The claim-time skill-template generation tests were removed with the claim ceremony (DR-041) —
/// skills are compiled by <c>dydo sync</c>, not created at claim.
/// </summary>
[Collection("Integration")]
public class TemplateOverrideTests : IntegrationTestBase
{
    [Fact]
    public async Task Init_CopiesTemplatesToSystemFolder()
    {
        await InitProjectAsync();

        AssertDirectoryExists("dydo/_system/templates");
        AssertFileExists("dydo/_system/templates/skill-code-writer.template.md");
        AssertFileExists("dydo/_system/templates/skill-reviewer.template.md");
        AssertFileExists("dydo/_system/templates/skill-co-thinker.template.md");
        AssertFileExists("dydo/_system/templates/skill-planner.template.md");
        AssertFileExists("dydo/_system/templates/skill-docs-writer.template.md");
        AssertFileExists("dydo/_system/templates/skill-test-writer.template.md");
        AssertFileExists("dydo/_system/templates/skill-inquisitor.template.md");
        AssertFileExists("dydo/_system/templates/skill-wayfinder.template.md");
        AssertFileExists("dydo/_system/templates/skill-grilling.template.md");
        AssertFileExists("dydo/_system/templates/skill-grill-me.template.md");
        AssertFileExists("dydo/_system/templates/skill-bro.template.md");
        AssertFileExists("dydo/_system/templates/skill-writing-for-agents.template.md");
    }

    [Fact]
    public async Task Init_SystemTemplatesMatchEveryBuiltInTemplate()
    {
        await InitProjectAsync();

        foreach (var templateName in TemplateGenerator.GetAllTemplateNames())
        {
            var copiedContent = ReadFile($"dydo/_system/templates/{templateName}");
            var builtInContent = TemplateGenerator.ReadBuiltInTemplate(templateName);

            Assert.Equal(builtInContent, copiedContent);
        }

        AssertFileNotExists("dydo/_system/templates/_tasks.template.md");
        AssertFileNotExists("dydo/_system/templates/_issues.template.md");
        AssertFileNotExists("dydo/_system/templates/_backlog.template.md");
    }

    [Fact]
    public async Task Join_DoesNotOverwriteExistingTemplates()
    {
        await InitProjectAsync("none");

        // Modify a template
        var templatePath = Path.Combine(TestDir, "dydo/_system/templates/skill-code-writer.template.md");
        var customContent = "<!-- CUSTOM_CONTENT_PRESERVED -->\nCustom template";
        File.WriteAllText(templatePath, customContent);

        // Join as another user
        await JoinProjectAsync("none");

        // Verify custom template was NOT overwritten
        var contentAfterJoin = File.ReadAllText(templatePath);
        Assert.Contains("CUSTOM_CONTENT_PRESERVED", contentAfterJoin);
    }

    [Fact]
    public void GetAllTemplateNames_ReturnsExpectedTemplates()
    {
        var templateNames = TemplateGenerator.GetAllTemplateNames();

        Assert.Contains("skill-code-writer.template.md", templateNames);
        Assert.Contains("skill-reviewer.template.md", templateNames);
        Assert.Contains("skill-planner.template.md", templateNames);
        Assert.Contains("skill-chief-of-staff.template.md", templateNames);
        Assert.Contains("skill-inquisitor.template.md", templateNames);
        Assert.Contains("skill-self-improvement.template.md", templateNames);
        Assert.Contains("skill-wayfinder.template.md", templateNames);
        Assert.Contains("skill-grilling.template.md", templateNames);
        Assert.Contains("skill-grill-me.template.md", templateNames);
        Assert.Contains("skill-bro.template.md", templateNames);
        Assert.Contains("skill-writing-for-agents.template.md", templateNames);

        // The mirrored set IS the shipped set: every skill template plus every skill resource
        // template (<role>-resource-<name>.template.md). A hard-coded count would freeze the
        // inventory the DR 045 taxonomy is about to change.
        Assert.Contains("reviewer-resource-plan.template.md", templateNames);
        Assert.Equal(ShippedTemplateNames(), templateNames.OrderBy(n => n, StringComparer.Ordinal));
    }

    // DR 045 section 8: the working-tree contract is a framework document, so a fresh init
    // scaffolds it and `template update` tracks it by hash like any other framework doc.
    [Fact]
    public async Task Init_ScaffoldsAndTracksTheWorkingTreeContractGuide()
    {
        await InitProjectAsync();

        AssertFileExists("dydo/guides/working-tree-contract.md");
        Assert.Equal(
            TemplateGenerator.GenerateWorkingTreeContractMd(),
            ReadFile("dydo/guides/working-tree-contract.md"));
        Assert.Contains("guides/working-tree-contract.md", TemplateCommand.FrameworkDocFiles);

        var config = System.Text.Json.JsonSerializer.Deserialize(ReadFile("dydo.json"),
            DynaDocs.Serialization.DydoConfigJsonContext.Default.DydoConfig)!;
        Assert.True(config.FrameworkHashes.ContainsKey("guides/working-tree-contract.md"),
            "template update must track the working-tree contract by hash");
    }

    // The mirrored set is the authored set minus retired roles and anything that hangs off
    // them: a retired template mirrored into a project revives the role there.
    private static IEnumerable<string> ShippedTemplateNames()
    {
        var templates = Path.Combine(FindRepositoryRoot(), "Templates");
        var retired = SyncCommand.RetiredManagedRoles;
        return Directory.GetFiles(templates, "skill-*.template.md")
            .Concat(Directory.GetFiles(templates, "*-resource-*.template.md"))
            .Select(path => Path.GetFileName(path)!)
            .Where(name => !retired.Any(role =>
                name.Equals($"skill-{role}.template.md", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith($"{role}-resource-", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(name => name, StringComparer.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DynaDocs.csproj")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not find the DynaDocs repository root.");
    }

    [Fact]
    public void ReadBuiltInTemplate_ReturnsTemplateContent()
    {
        var content = TemplateGenerator.ReadBuiltInTemplate("skill-code-writer.template.md");

        Assert.NotEmpty(content);
        Assert.Contains("mode: code-writer", content);
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
        await InitProjectAsync("none");

        // Create a custom addition
        var additionsPath = Path.Combine(TestDir, "dydo/_system/template-additions");
        File.WriteAllText(Path.Combine(additionsPath, "custom-step.md"), "Custom content");

        // Join as another user
        await JoinProjectAsync("none");

        // Verify custom addition was NOT deleted
        Assert.True(File.Exists(Path.Combine(additionsPath, "custom-step.md")));
        var content = File.ReadAllText(Path.Combine(additionsPath, "custom-step.md"));
        Assert.Equal("Custom content", content);
    }

    #endregion
}
