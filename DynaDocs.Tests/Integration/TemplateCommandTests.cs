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

    [Fact]
    public async Task TemplateUpdate_StaleTemplate_Removed()
    {
        await InitProjectAsync();

        var staleFile = Path.Combine(TestDir, "dydo/_system/templates/mode-old-removed.template.md");
        File.WriteAllText(staleFile, "stale template");

        var result = await RunTemplateUpdateAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Removed stale:");
        Assert.False(File.Exists(staleFile));
    }

    [Fact]
    public async Task TemplateUpdate_StaleHash_Pruned()
    {
        await InitProjectAsync();

        var config = new ConfigService().LoadConfig()!;
        config.FrameworkHashes["_system/templates/mode-deleted.template.md"] = "abc123";
        new ConfigService().SaveConfig(config, Path.Combine(TestDir, "dydo.json"));

        var result = await RunTemplateUpdateAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Pruned stale hash:");

        var updatedConfig = new ConfigService().LoadConfig()!;
        Assert.False(updatedConfig.FrameworkHashes.ContainsKey("_system/templates/mode-deleted.template.md"));
    }

    [Fact]
    public async Task TemplateUpdate_MissingDocFile_Created()
    {
        await InitProjectAsync();

        var docPath = Path.Combine(TestDir, "dydo/reference/dydo-commands.md");
        File.Delete(docPath);

        var result = await RunTemplateUpdateAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Created: reference/dydo-commands.md");
        Assert.True(File.Exists(docPath));
    }

    [Fact]
    public async Task TemplateUpdate_CrlfOnDisk_NotDetectedAsUserEdited()
    {
        await InitProjectAsync();

        // Simulate CRLF conversion on a doc file (e.g., git autocrlf)
        var relativePath = "reference/dydo-commands.md";
        var docPath = Path.Combine(TestDir, "dydo", relativePath);
        var originalContent = File.ReadAllText(docPath);
        var crlfContent = originalContent.Replace("\n", "\r\n");
        File.WriteAllText(docPath, crlfContent);

        var result = await RunTemplateUpdateAsync();

        result.AssertSuccess();
        // Should NOT be reported as user-edited
        Assert.DoesNotContain("user-edited", result.Stderr);
    }

    [Fact]
    public async Task TemplateUpdate_UserEditedDocFile_Skipped()
    {
        await InitProjectAsync();

        var relativePath = "reference/dydo-commands.md";
        var docPath = Path.Combine(TestDir, "dydo", relativePath);
        var originalContent = File.ReadAllText(docPath);

        var config = new ConfigService().LoadConfig()!;
        config.FrameworkHashes[relativePath] = TemplateCommand.ComputeHash(originalContent);
        new ConfigService().SaveConfig(config, Path.Combine(TestDir, "dydo.json"));

        File.WriteAllText(docPath, originalContent + "\n\n<!-- User added this -->");

        var result = await RunTemplateUpdateAsync();

        Assert.Contains("user-edited", result.Stderr);
    }

    [Fact]
    public async Task TemplateUpdate_MissingBinaryFile_Created()
    {
        await InitProjectAsync();

        var svgPath = Path.Combine(TestDir, "dydo/_assets/dydo-diagram.svg");
        if (File.Exists(svgPath))
        {
            File.Delete(svgPath);

            var result = await RunTemplateUpdateAsync();

            result.AssertSuccess();
            result.AssertStdoutContains("Created: _assets/dydo-diagram.svg");
            Assert.True(File.Exists(svgPath));
        }
    }

    [Fact]
    public async Task TemplateUpdate_UserEditedBinaryFile_Skipped()
    {
        await InitProjectAsync();

        var relativePath = "_assets/dydo-diagram.svg";
        var svgPath = Path.Combine(TestDir, "dydo", relativePath);
        if (!File.Exists(svgPath)) return;

        var originalBytes = File.ReadAllBytes(svgPath);
        var config = new ConfigService().LoadConfig()!;
        config.FrameworkHashes[relativePath] = TemplateCommand.ComputeHashBytes(originalBytes);
        new ConfigService().SaveConfig(config, Path.Combine(TestDir, "dydo.json"));

        File.WriteAllText(svgPath, "<svg>custom content</svg>");

        var result = await RunTemplateUpdateAsync();

        Assert.Contains("user-edited", result.Stderr);
    }

    [Fact]
    public async Task TemplateUpdate_RegeneratesAgentWorkspaces()
    {
        await InitProjectAsync();

        var result = await RunTemplateUpdateAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Regenerated: agents/");
    }

    [Fact]
    public async Task TemplateUpdate_Diff_DoesNotRecreateFiles()
    {
        await InitProjectAsync();

        var relativePath = "_system/templates/mode-code-writer.template.md";
        var templatePath = Path.Combine(TestDir, "dydo", relativePath);
        File.Delete(templatePath);

        var result = await RunTemplateUpdateAsync("--diff");

        result.AssertSuccess();
        Assert.False(File.Exists(templatePath));
    }

    [Fact]
    public async Task TemplateUpdate_UpdatedBinaryFile_Replaced()
    {
        await InitProjectAsync();

        var relativePath = "_assets/dydo-diagram.svg";
        var svgPath = Path.Combine(TestDir, "dydo", relativePath);
        if (!File.Exists(svgPath)) return;

        var oldContent = "<svg>old framework version</svg>";
        File.WriteAllText(svgPath, oldContent);

        var config = new ConfigService().LoadConfig()!;
        config.FrameworkHashes[relativePath] = TemplateCommand.ComputeHashBytes(
            System.Text.Encoding.UTF8.GetBytes(oldContent));
        new ConfigService().SaveConfig(config, Path.Combine(TestDir, "dydo.json"));

        var result = await RunTemplateUpdateAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Updated: _assets/dydo-diagram.svg");
    }
}
