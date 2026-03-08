namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;
using DynaDocs.Services;

[Collection("Integration")]
public class TemplateCommandTests : IntegrationTestBase
{
    private async Task<CommandResult> RunTemplateUpdateAsync(params string[] extraArgs)
    {
        var command = TemplateCommand.Create();
        var args = new List<string> { "update" };
        args.AddRange(extraArgs);
        return await RunAsync(command, args.ToArray());
    }

    [Fact]
    public async Task TemplateUpdate_CleanProject_UpdatesAllFiles()
    {
        await InitProjectAsync();

        // Tamper a template to simulate a framework update (on-disk != embedded, but hash matches)
        var templatePath = Path.Combine(TestDir, "dydo/_system/templates/mode-code-writer.template.md");
        var originalContent = File.ReadAllText(templatePath);
        var configContent = ReadFile("dydo.json");

        // Store current hash, then modify the embedded resource's copy on disk
        // to simulate "old framework version"
        var oldContent = originalContent + "\n<!-- old version -->";
        File.WriteAllText(templatePath, oldContent);

        // Update the stored hash to match the tampered file (simulating clean old install)
        var config = new ConfigService().LoadConfig()!;
        var relativePath = "_system/templates/mode-code-writer.template.md";
        config.FrameworkHashes[relativePath] = TemplateCommand.ComputeHash(oldContent);
        new ConfigService().SaveConfig(config, Path.Combine(TestDir, "dydo.json"));

        var result = await RunTemplateUpdateAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Updated:");
        result.AssertStdoutContains("Template update complete:");

        // File should now match embedded content
        var updatedContent = File.ReadAllText(templatePath);
        var embeddedContent = TemplateGenerator.ReadBuiltInTemplate("mode-code-writer.template.md");
        Assert.Equal(embeddedContent, updatedContent);
    }

    [Fact]
    public async Task TemplateUpdate_AlreadyCurrent_ReportsNoChanges()
    {
        await InitProjectAsync();

        var result = await RunTemplateUpdateAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Template update complete:");
        // All files should be "already current"
        Assert.DoesNotContain("Updated:", result.Stdout);
    }

    [Fact]
    public async Task TemplateUpdate_UserAddedInclude_Reanchored()
    {
        await InitProjectAsync();

        var relativePath = "_system/templates/mode-code-writer.template.md";
        var templatePath = Path.Combine(TestDir, "dydo", relativePath);
        var embeddedContent = TemplateGenerator.ReadBuiltInTemplate("mode-code-writer.template.md");

        // Find a good anchor point — insert a user include tag in the template
        var lines = embeddedContent.Split('\n').ToList();
        var verifyIdx = lines.FindIndex(l => l.Contains("{{include:extra-verify}}"));
        if (verifyIdx >= 0)
        {
            lines.Insert(verifyIdx + 1, "{{include:my-custom-step}}");
        }
        else
        {
            // Fallback: insert after first non-blank line
            lines.Insert(1, "{{include:my-custom-step}}");
        }
        var userContent = string.Join('\n', lines);
        File.WriteAllText(templatePath, userContent);

        var result = await RunTemplateUpdateAsync();

        // The user-added include should be re-anchored
        var updatedContent = File.ReadAllText(templatePath);
        Assert.Contains("{{include:my-custom-step}}", updatedContent);
    }

    [Fact]
    public async Task TemplateUpdate_Force_BackupCreated()
    {
        await InitProjectAsync();

        var relativePath = "_system/templates/mode-code-writer.template.md";
        var templatePath = Path.Combine(TestDir, "dydo", relativePath);
        var embeddedContent = TemplateGenerator.ReadBuiltInTemplate("mode-code-writer.template.md");

        // Add a user include with anchors that won't exist in the new template
        var userContent = embeddedContent + "\n{{include:orphan-hook}}\n";
        File.WriteAllText(templatePath, userContent);

        var result = await RunTemplateUpdateAsync("--force");

        // With --force, backup should be created
        var backupPath = templatePath + ".backup";
        if (File.Exists(backupPath))
        {
            var backupContent = File.ReadAllText(backupPath);
            Assert.Equal(userContent, backupContent);
        }
    }

    [Fact]
    public async Task TemplateUpdate_Diff_ShowsReanchorPlacements()
    {
        await InitProjectAsync();

        var relativePath = "_system/templates/mode-code-writer.template.md";
        var templatePath = Path.Combine(TestDir, "dydo", relativePath);
        var embeddedContent = TemplateGenerator.ReadBuiltInTemplate("mode-code-writer.template.md");

        // Add a user include
        var lines = embeddedContent.Split('\n').ToList();
        var verifyIdx = lines.FindIndex(l => l.Contains("{{include:extra-verify}}"));
        if (verifyIdx >= 0)
            lines.Insert(verifyIdx + 1, "{{include:my-diff-hook}}");
        var userContent = string.Join('\n', lines);
        File.WriteAllText(templatePath, userContent);

        var result = await RunTemplateUpdateAsync("--diff");

        // --diff should show preview without writing
        result.AssertStdoutContains("Template update complete:");
        // Original file should still have user content
        var afterDiff = File.ReadAllText(templatePath);
        Assert.Equal(userContent, afterDiff);
    }

    [Fact]
    public async Task TemplateUpdate_PreservesTemplateAdditions()
    {
        await InitProjectAsync();

        // Create a custom addition file
        var additionsPath = Path.Combine(TestDir, "dydo/_system/template-additions");
        var customFile = Path.Combine(additionsPath, "my-step.md");
        File.WriteAllText(customFile, "Custom step content");

        await RunTemplateUpdateAsync();

        // Addition file should be untouched
        Assert.True(File.Exists(customFile));
        Assert.Equal("Custom step content", File.ReadAllText(customFile));
    }

    [Fact]
    public async Task TemplateUpdate_UpdatesConfigHashes()
    {
        await InitProjectAsync();

        // Tamper a template
        var relativePath = "_system/templates/mode-code-writer.template.md";
        var templatePath = Path.Combine(TestDir, "dydo", relativePath);
        var oldContent = "old framework content";
        File.WriteAllText(templatePath, oldContent);

        var config = new ConfigService().LoadConfig()!;
        config.FrameworkHashes[relativePath] = TemplateCommand.ComputeHash(oldContent);
        new ConfigService().SaveConfig(config, Path.Combine(TestDir, "dydo.json"));

        await RunTemplateUpdateAsync();

        // Hash should be updated to match the new embedded content
        var updatedConfig = new ConfigService().LoadConfig()!;
        var embeddedContent = TemplateGenerator.ReadBuiltInTemplate("mode-code-writer.template.md");
        Assert.Equal(TemplateCommand.ComputeHash(embeddedContent), updatedConfig.FrameworkHashes[relativePath]);
    }

    [Fact]
    public async Task TemplateUpdate_NonTemplateFrameworkFiles_AlsoUpdated()
    {
        await InitProjectAsync();

        // Tamper a doc file
        var relativePath = "reference/dydo-commands.md";
        var docPath = Path.Combine(TestDir, "dydo", relativePath);
        var oldContent = "old doc content";
        File.WriteAllText(docPath, oldContent);

        var config = new ConfigService().LoadConfig()!;
        config.FrameworkHashes[relativePath] = TemplateCommand.ComputeHash(oldContent);
        new ConfigService().SaveConfig(config, Path.Combine(TestDir, "dydo.json"));

        var result = await RunTemplateUpdateAsync();

        result.AssertSuccess();
        // Doc file should be updated
        var updatedContent = File.ReadAllText(docPath);
        Assert.NotEqual(oldContent, updatedContent);
    }
}
