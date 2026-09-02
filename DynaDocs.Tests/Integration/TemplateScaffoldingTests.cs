namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;
using DynaDocs.Services;

/// <summary>
/// Integration tests for the shipped template inventory, framework-doc hash tracking and the
/// template-additions scaffold.
/// The claim-time skill-template generation tests were removed with the claim ceremony (DR-041) —
/// skills are compiled by <c>dydo sync</c>, not created at claim.
/// </summary>
[Collection("Integration")]
public class TemplateScaffoldingTests : IntegrationTestBase
{
    [Fact]
    public void GetAllTemplateNames_ReturnsExpectedTemplates()
    {
        var templateNames = TemplateGenerator.GetAllTemplateNames();

        Assert.Contains("skill-code-writer.template.md", templateNames);
        Assert.Contains("skill-reviewer.template.md", templateNames);
        Assert.Contains("skill-project-planner.template.md", templateNames);
        Assert.Contains("skill-issue-planner.template.md", templateNames);
        Assert.Contains("skill-chief-of-staff.template.md", templateNames);
        Assert.Contains("skill-inquisitor.template.md", templateNames);
        Assert.Contains("skill-self-improvement.template.md", templateNames);
        Assert.Contains("skill-wayfinder.template.md", templateNames);
        Assert.Contains("skill-grilling.template.md", templateNames);
        Assert.Contains("skill-grill-me.template.md", templateNames);
        Assert.Contains("skill-bro.template.md", templateNames);
        Assert.Contains("skill-writing-for-agents.template.md", templateNames);

        // The inventory is every skill template plus every skill resource
        // template (<skill>-resource-<name>.template.md). A hard-coded count would freeze the
        // inventory the DR 045 taxonomy is about to change.
        Assert.Contains("reviewer-resource-project-plan.template.md", templateNames);
        Assert.Contains("reviewer-resource-issue-plan.template.md", templateNames);
        Assert.DoesNotContain("reviewer-resource-plan.template.md", templateNames);
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

    [Fact]
    public async Task Init_ScaffoldsAndTracksTheLinearWorkspaceStandard()
    {
        await InitProjectAsync();

        AssertFileExists("dydo/reference/linear-workspace-standard.md");
        Assert.Equal(
            TemplateGenerator.GenerateLinearWorkspaceStandardMd(),
            ReadFile("dydo/reference/linear-workspace-standard.md"));
        Assert.Contains("reference/linear-workspace-standard.md", TemplateCommand.FrameworkDocFiles);

        var config = System.Text.Json.JsonSerializer.Deserialize(ReadFile("dydo.json"),
            DynaDocs.Serialization.DydoConfigJsonContext.Default.DydoConfig)!;
        Assert.True(config.FrameworkHashes.ContainsKey("reference/linear-workspace-standard.md"),
            "template update must track the Linear workspace standard by hash");
    }

    // The shipped set is the authored set minus retired skills and anything that hangs off them.
    private static IEnumerable<string> ShippedTemplateNames()
    {
        var templates = Path.Combine(FindRepositoryRoot(), "Templates");
        var retired = SyncCommand.RetiredSkills;
        return Directory.GetFiles(templates, "skill-*.template.md")
            .Concat(Directory.GetFiles(templates, "*-resource-*.template.md"))
            .Select(path => Path.GetFileName(path)!)
            .Where(name => !retired.Any(skill =>
                name.Equals($"skill-{skill}.template.md", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith($"{skill}-resource-", StringComparison.OrdinalIgnoreCase)))
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

        // Must have a hash for every framework doc file
        foreach (var docFile in TemplateCommand.FrameworkDocFiles)
            Assert.True(config.FrameworkHashes.ContainsKey(docFile),
                $"Missing hash for framework doc: {docFile}");

        // Each hash must be a non-empty SHA256 hex string (64 chars)
        foreach (var (key, hash) in config.FrameworkHashes)
        {
            Assert.False(string.IsNullOrWhiteSpace(hash), $"Empty hash for {key}");
            Assert.Equal(64, hash.Length);
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
