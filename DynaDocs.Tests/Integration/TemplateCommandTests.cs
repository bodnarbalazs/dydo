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
    public async Task TemplateUpdate_StaleHash_Pruned()
    {
        await InitProjectAsync();

        var config = new ConfigService().LoadConfig()!;
        config.FrameworkHashes["reference/retired-framework-doc.md"] = "abc123";
        new ConfigService().SaveConfig(config, Path.Combine(TestDir, "dydo.json"));

        var result = await RunTemplateUpdateAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Pruned stale hash:");

        var updatedConfig = new ConfigService().LoadConfig()!;
        Assert.False(updatedConfig.FrameworkHashes.ContainsKey("reference/retired-framework-doc.md"));
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
    public async Task TemplateUpdate_RetiredBinary_NotScaffoldedAndNotRecreated()
    {
        // Issue 0301: the pre-DR-041 diagram is retired — a fresh init must not scaffold it,
        // and template update must not resurrect it.
        await InitProjectAsync();

        var svgPath = Path.Combine(TestDir, "dydo/_assets/dydo-diagram.svg");
        Assert.False(File.Exists(svgPath), "retired diagram must not be scaffolded by init");

        var result = await RunTemplateUpdateAsync();

        result.AssertSuccess();
        Assert.False(File.Exists(svgPath), "retired diagram must not be recreated by template update");
    }

    [Fact]
    public async Task TemplateUpdate_RetiredBinary_UserModifiedCopy_Kept()
    {
        // A legacy project whose diagram was hand-modified: retirement must not destroy user
        // data — the file stays (now user-owned) and only its stale hash entry is pruned.
        await InitProjectAsync();

        var relativePath = "_assets/dydo-diagram.svg";
        var svgPath = Path.Combine(TestDir, "dydo", relativePath);
        File.WriteAllText(svgPath, "<svg>custom user content</svg>");

        var config = new ConfigService().LoadConfig()!;
        config.FrameworkHashes[relativePath] = "0000000000000000000000000000000000000000000000000000000000000000";
        new ConfigService().SaveConfig(config, Path.Combine(TestDir, "dydo.json"));

        var result = await RunTemplateUpdateAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Kept: _assets/dydo-diagram.svg");
        Assert.True(File.Exists(svgPath), "user-modified retired binary must be kept");

        var updatedConfig = new ConfigService().LoadConfig()!;
        Assert.False(updatedConfig.FrameworkHashes.ContainsKey(relativePath),
            "stale hash entry must be pruned even when the file is kept");
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

        var docPath = Path.Combine(TestDir, "dydo/reference/dydo-commands.md");
        File.Delete(docPath);

        var result = await RunTemplateUpdateAsync("--diff");

        result.AssertSuccess();
        Assert.False(File.Exists(docPath));
    }

    [Fact]
    public async Task TemplateUpdate_RetiredBinary_HashCleanCopy_Deleted()
    {
        // A legacy project carrying the untouched framework diagram (stored hash matches the
        // on-disk bytes): retirement deletes the file and prunes its hash entry.
        await InitProjectAsync();

        var relativePath = "_assets/dydo-diagram.svg";
        var svgPath = Path.Combine(TestDir, "dydo", relativePath);
        var oldContent = "<svg>old framework version</svg>";
        File.WriteAllText(svgPath, oldContent);

        var config = new ConfigService().LoadConfig()!;
        config.FrameworkHashes[relativePath] = TemplateCommand.ComputeHashBytes(
            System.Text.Encoding.UTF8.GetBytes(oldContent));
        new ConfigService().SaveConfig(config, Path.Combine(TestDir, "dydo.json"));

        var result = await RunTemplateUpdateAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Removed retired: _assets/dydo-diagram.svg");
        Assert.False(File.Exists(svgPath), "hash-clean retired binary must be deleted");

        var updatedConfig = new ConfigService().LoadConfig()!;
        Assert.False(updatedConfig.FrameworkHashes.ContainsKey(relativePath));
    }

    [Fact]
    public async Task TemplateUpdate_RetiredDoc_NotScaffoldedAndNotRecreated()
    {
        // DYD-68: the navigation guide is retired — a fresh init must not scaffold it,
        // and template update must not resurrect it.
        await InitProjectAsync();

        var docPath = Path.Combine(TestDir, "dydo/guides/how-to-use-docs.md");
        Assert.False(File.Exists(docPath), "retired doc must not be scaffolded by init");

        var result = await RunTemplateUpdateAsync();

        result.AssertSuccess();
        Assert.False(File.Exists(docPath), "retired doc must not be recreated by template update");
    }

    [Fact]
    public async Task TemplateUpdate_RetiredDoc_HashCleanCopy_Deleted()
    {
        // A legacy project carrying the untouched framework guide (stored hash matches the
        // on-disk text): retirement deletes the file and prunes its hash entry.
        await InitProjectAsync();

        var relativePath = "guides/how-to-use-docs.md";
        var docPath = Path.Combine(TestDir, "dydo", relativePath);
        var oldContent = "# How to Use These Docs\n\nold framework version\n";
        File.WriteAllText(docPath, oldContent);

        var config = new ConfigService().LoadConfig()!;
        config.FrameworkHashes[relativePath] = TemplateCommand.ComputeHash(oldContent);
        new ConfigService().SaveConfig(config, Path.Combine(TestDir, "dydo.json"));

        var result = await RunTemplateUpdateAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Removed retired: guides/how-to-use-docs.md");
        Assert.False(File.Exists(docPath), "hash-clean retired doc must be deleted");

        var updatedConfig = new ConfigService().LoadConfig()!;
        Assert.False(updatedConfig.FrameworkHashes.ContainsKey(relativePath));
    }

    [Fact]
    public async Task TemplateUpdate_RetiredDoc_UserModifiedCopy_Kept()
    {
        // A hand-modified copy: retirement must not destroy user data — the file stays
        // (now user-owned) and only its stale hash entry is pruned.
        await InitProjectAsync();

        var relativePath = "guides/how-to-use-docs.md";
        var docPath = Path.Combine(TestDir, "dydo", relativePath);
        File.WriteAllText(docPath, "# My navigation notes\n");

        var config = new ConfigService().LoadConfig()!;
        config.FrameworkHashes[relativePath] = "0000000000000000000000000000000000000000000000000000000000000000";
        new ConfigService().SaveConfig(config, Path.Combine(TestDir, "dydo.json"));

        var result = await RunTemplateUpdateAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Kept: guides/how-to-use-docs.md");
        Assert.True(File.Exists(docPath), "user-modified retired doc must be kept");

        var updatedConfig = new ConfigService().LoadConfig()!;
        Assert.False(updatedConfig.FrameworkHashes.ContainsKey(relativePath));
    }

    [Fact]
    public async Task Init_StoresHashesForAllFrameworkFiles()
    {
        await InitProjectAsync();

        var config = new ConfigService().LoadConfig()!;

        foreach (var docPath in TemplateCommand.FrameworkDocFiles)
        {
            Assert.True(config.FrameworkHashes.ContainsKey(docPath),
                $"Expected hash for doc file '{docPath}' but none found");
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
