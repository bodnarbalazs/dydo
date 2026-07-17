namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;
using DynaDocs.Models;
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
    public async Task TemplateUpdate_StaleTrackedTemplate_Removed()
    {
        await InitProjectAsync();

        // A hash-tracked template that is no longer shipped = a stale framework copy.
        var staleFile = Path.Combine(TestDir, "dydo/_system/templates/mode-old-removed.template.md");
        File.WriteAllText(staleFile, "stale template");
        var config = new ConfigService().LoadConfig()!;
        config.FrameworkHashes["_system/templates/mode-old-removed.template.md"] =
            TemplateCommand.ComputeHash("stale template");
        new ConfigService().SaveConfig(config, Path.Combine(TestDir, "dydo.json"));

        var result = await RunTemplateUpdateAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Removed stale:");
        Assert.False(File.Exists(staleFile));
    }

    [Fact]
    public async Task TemplateUpdate_UntrackedCustomModeTemplate_Kept()
    {
        await InitProjectAsync();

        // An untracked mode template is a user's custom role (compiled by dydo sync) —
        // template update must never delete it.
        var customFile = Path.Combine(TestDir, "dydo/_system/templates/mode-my-custom.template.md");
        File.WriteAllText(customFile, "---\nmode: my-custom\n---\n# My Custom\n");

        var result = await RunTemplateUpdateAsync();

        result.AssertSuccess();
        Assert.True(File.Exists(customFile), "custom mode template must survive template update");
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
        var crlfContent = originalContent.Replace("\r\n", "\n").Replace("\n", "\r\n");
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
    public async Task TemplateUpdate_WarnedFilesCountedInSummary()
    {
        await InitProjectAsync();

        // Make a doc file user-edited so it triggers a warning
        var relativePath = TemplateCommand.FrameworkDocFiles.First();
        var docPath = Path.Combine(TestDir, "dydo", relativePath);
        var originalContent = File.ReadAllText(docPath);

        var config = new ConfigService().LoadConfig()!;
        config.FrameworkHashes[relativePath] = TemplateCommand.ComputeHash(originalContent);
        new ConfigService().SaveConfig(config, Path.Combine(TestDir, "dydo.json"));

        File.WriteAllText(docPath, originalContent + "\n\n<!-- User edit -->");

        var result = await RunTemplateUpdateAsync();

        // The summary should include warned files
        result.AssertStdoutContains("warned");
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

    [Fact]
    public async Task TemplateUpdate_NoStoredHash_UserEditedTemplate_OverwritesCleanly()
    {
        // Exercises GetOldStockContent when storedHash is null (legacy install scenario).
        // With no stored hash, the file is treated as user-edited and GetOldStockContent
        // returns onDisk as the old stock, so ExtractUserIncludes(onDisk, onDisk) finds
        // no user includes and the file is overwritten with the new embedded content.
        await InitProjectAsync();

        var relativePath = TemplateCommand.FrameworkTemplateFiles.First();
        var templatePath = Path.Combine(TestDir, "dydo", relativePath);

        // Remove the stored hash to simulate legacy install
        var config = new ConfigService().LoadConfig()!;
        config.FrameworkHashes.Remove(relativePath);
        new ConfigService().SaveConfig(config, Path.Combine(TestDir, "dydo.json"));

        // Modify the template (non-include edit)
        var embeddedContent = TemplateGenerator.ReadBuiltInTemplate(Path.GetFileName(templatePath));
        File.WriteAllText(templatePath, embeddedContent + "\n<!-- user tweak -->");

        var result = await RunTemplateUpdateAsync();

        result.AssertSuccess();
        // File should be overwritten with embedded content (no includes to preserve)
        var afterUpdate = File.ReadAllText(templatePath);
        Assert.DoesNotContain("<!-- user tweak -->", afterUpdate);
    }

    [Fact]
    public async Task Init_StoresHashesForAllFrameworkFiles()
    {
        await InitProjectAsync();

        var config = new ConfigService().LoadConfig()!;

        // Template files should have hashes
        foreach (var templatePath in TemplateCommand.FrameworkTemplateFiles)
        {
            Assert.True(config.FrameworkHashes.ContainsKey(templatePath),
                $"Expected hash for template file '{templatePath}' but none found");
        }

        // Doc files should also have hashes
        foreach (var docPath in TemplateCommand.FrameworkDocFiles)
        {
            Assert.True(config.FrameworkHashes.ContainsKey(docPath),
                $"Expected hash for doc file '{docPath}' but none found");
        }

        // Binary files should also have hashes
        foreach (var binaryPath in TemplateCommand.FrameworkBinaryFiles)
        {
            Assert.True(config.FrameworkHashes.ContainsKey(binaryPath),
                $"Expected hash for binary file '{binaryPath}' but none found");
        }
    }

    [Fact]
    public async Task TemplateUpdate_RestoresMissingScanExcludeInvariant()
    {
        await InitProjectAsync();

        // User scrubbed a dydo-internal scanExclude entry — template update must restore it.
        var configService = new ConfigService();
        var configPath = Path.Combine(TestDir, "dydo.json");
        var config = configService.LoadConfig()!;
        config.ScanExclude.Remove("_system/.local/");
        config.ScanExclude.Add("vendor/");
        configService.SaveConfig(config, configPath);

        var result = await RunTemplateUpdateAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("default scan-exclude entry");

        var updated = configService.LoadConfig()!;
        Assert.Contains("_system/.local/", updated.ScanExclude);
        Assert.Contains("vendor/", updated.ScanExclude);
    }

    [Fact]
    public async Task TemplateUpdate_MigratesLegacyOpenAiModelDefaults()
    {
        await InitProjectAsync();

        var configService = new ConfigService();
        var configPath = Path.Combine(TestDir, "dydo.json");
        var config = configService.LoadConfig()!;
        config.Models!.Tiers["openai"] = new Dictionary<string, string>
        {
            ["strong"] = "gpt-5.5",
            ["standard"] = "gpt-5.5",
            ["light"] = "gpt-5.5"
        };
        configService.SaveConfig(config, configPath);

        var result = await RunTemplateUpdateAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("legacy OpenAI model defaults");

        var updated = configService.LoadConfig()!;
        var openAi = updated.Models!.Tiers["openai"];
        Assert.Equal("gpt-5.6-sol", openAi["strong"]);
        Assert.Equal("gpt-5.6-terra", openAi["standard"]);
        Assert.Equal("gpt-5.6-luna", openAi["light"]);
    }

    [Fact]
    public async Task TemplateUpdate_AlreadyHasScanExcludeInvariants_NoChange()
    {
        await InitProjectAsync();

        var configService = new ConfigService();
        var configPath = Path.Combine(TestDir, "dydo.json");
        var config = configService.LoadConfig()!;
        var originalCount = config.ScanExclude.Count;

        var result = await RunTemplateUpdateAsync();

        result.AssertSuccess();
        Assert.DoesNotContain("default scan-exclude entry", result.Stdout);

        var updated = configService.LoadConfig()!;
        Assert.Equal(originalCount, updated.ScanExclude.Count);
    }

    [Fact]
    public async Task TemplateUpdate_Diff_DoesNotMutateScanExclude()
    {
        await InitProjectAsync();

        var configService = new ConfigService();
        var configPath = Path.Combine(TestDir, "dydo.json");
        var config = configService.LoadConfig()!;
        config.ScanExclude.Remove("_system/.local/");
        configService.SaveConfig(config, configPath);

        var result = await RunTemplateUpdateAsync("--diff");

        result.AssertSuccess();

        var afterDiff = configService.LoadConfig()!;
        Assert.DoesNotContain("_system/.local/", afterDiff.ScanExclude);
    }

    [Fact]
    public async Task TemplateUpdate_UserEditedDocFile_PreservedWhenHashStored()
    {
        await InitProjectAsync();

        var relativePath = TemplateCommand.FrameworkDocFiles.First();
        var docPath = Path.Combine(TestDir, "dydo", relativePath);
        var originalContent = File.ReadAllText(docPath);

        // Pre-condition: hash IS stored for doc files after init
        var config = new ConfigService().LoadConfig()!;
        Assert.True(config.FrameworkHashes.ContainsKey(relativePath),
            "Pre-condition failed: doc file should have a stored hash after init");

        // User edits the doc file
        File.WriteAllText(docPath, originalContent + "\n\n<!-- User customization -->");

        await RunTemplateUpdateAsync();

        // User edit should be preserved (skipped due to hash mismatch)
        var afterUpdate = File.ReadAllText(docPath);
        Assert.Contains("<!-- User customization -->", afterUpdate);
    }

    [Fact]
    public async Task TemplateUpdate_MissingTypesJson_Created()
    {
        await InitProjectAsync();

        var typesPath = Path.Combine(TestDir, "dydo/_system/types.json");
        if (File.Exists(typesPath)) File.Delete(typesPath);

        var result = await RunTemplateUpdateAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Created: _system/types.json");
        Assert.True(File.Exists(typesPath));

        var content = File.ReadAllText(typesPath);
        Assert.Contains("\"hub\"", content);
        Assert.Contains("\"inquisition\"", content);
    }

    [Fact]
    public async Task TemplateUpdate_TypesJsonWithUserEntries_Preserved()
    {
        await InitProjectAsync();

        var typesPath = Path.Combine(TestDir, "dydo/_system/types.json");
        File.WriteAllText(typesPath, "[\"hub\", \"my-custom\"]");

        var result = await RunTemplateUpdateAsync();

        result.AssertSuccess();

        var content = File.ReadAllText(typesPath);
        Assert.Contains("\"my-custom\"", content);
        Assert.Contains("\"inquisition\"", content);
        Assert.Contains("\"hub\"", content);
    }

    [Fact]
    public async Task TemplateUpdate_TypesJsonAlreadyCurrent_NoMutation()
    {
        await InitProjectAsync();

        var typesPath = Path.Combine(TestDir, "dydo/_system/types.json");
        var before = File.ReadAllText(typesPath);

        var result = await RunTemplateUpdateAsync();

        result.AssertSuccess();
        Assert.Equal(before, File.ReadAllText(typesPath));
    }

    [Fact]
    public async Task TemplateUpdate_MalformedTypesJson_NotOverwritten()
    {
        await InitProjectAsync();

        var typesPath = Path.Combine(TestDir, "dydo/_system/types.json");
        var malformed = "not json {";
        File.WriteAllText(typesPath, malformed);

        var result = await RunTemplateUpdateAsync();

        Assert.Equal(malformed, File.ReadAllText(typesPath));
        Assert.Contains("malformed", result.Stderr);
    }
}
