namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;
using DynaDocs.Services;
using DynaDocs.Utils;

[Collection("Integration")]
public class FixCommandIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task Fix_ValidationErrorsPreserveOriginalConfigBytes()
    {
        (await InitProjectAsync()).AssertSuccess();
        var configPath = Path.Combine(TestDir, "dydo.json");
        RemoveScanExcludeInvariant(configPath);
        AddLegacyModels(configPath);
        var original = File.ReadAllBytes(configPath);
        WriteFile("dydo/guides/Bad Name.md", "---\narea: guides\ntype: guide\n---\n\n# Bad Name\n");
        WriteFile("dydo/guides/bad-name.md", "---\narea: guides\ntype: guide\n---\n\n# Existing\n");

        var (exitCode, stdout, _) = ConsoleCapture.All(() => FixCommand.Execute(DydoDir));

        Assert.Equal(ExitCodes.ValidationErrors, exitCode);
        Assert.Contains("issues require manual attention", stdout);
        Assert.Equal(original, File.ReadAllBytes(configPath));
        Assert.Contains("\"models\"", File.ReadAllText(configPath));
        Assert.Empty(Directory.GetFiles(TestDir, "dydo.json.*.tmp"));
    }

    [Fact]
    public async Task Fix_PostWorkFailure_PreservesOriginalConfigBytes()
    {
        (await InitProjectAsync()).AssertSuccess();
        var configPath = Path.Combine(TestDir, "dydo.json");
        RemoveScanExcludeInvariant(configPath);
        AddLegacyModels(configPath);
        var original = File.ReadAllBytes(configPath);

        var (exitCode, stdout, stderr) = ConsoleCapture.All(() =>
            FixCommand.Execute(DydoDir, () => throw new IOException("injected post-work failure")));

        Assert.Equal(ExitCodes.ToolError, exitCode);
        Assert.Contains("injected post-work failure", stderr);
        // The in-memory restoration and the final report both precede the commit seam.
        Assert.Contains("Restored 1 scanExclude invariant(s)", stdout);
        Assert.Contains("Fixed 1 issues automatically.", stdout);
        Assert.Equal(original, File.ReadAllBytes(configPath));
        Assert.Contains("\"models\"", File.ReadAllText(configPath));
        Assert.Empty(Directory.GetFiles(TestDir, "dydo.json.*.tmp"));
    }

    [Fact]
    public async Task Fix_NoConfigChange_LeavesConfigBytesUntouched()
    {
        (await InitProjectAsync()).AssertSuccess();
        var configPath = Path.Combine(TestDir, "dydo.json");
        AddLegacyModels(configPath);
        var original = File.ReadAllBytes(configPath);

        var (exitCode, stdout, _) = ConsoleCapture.All(() => FixCommand.Execute(DydoDir));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("Fixed 0 issues", stdout);
        Assert.Equal(original, File.ReadAllBytes(configPath));
        Assert.Contains("\"models\"", File.ReadAllText(configPath));
        Assert.Empty(Directory.GetFiles(TestDir, "dydo.json.*.tmp"));
    }

    private void RemoveScanExcludeInvariant(string configPath)
    {
        var config = new ConfigService().LoadConfigStrict(TestDir)!;
        config.ScanExclude.Remove("_system/templates/");
        new ConfigService().SaveConfig(config, configPath);
    }

    private static void AddLegacyModels(string configPath)
    {
        var raw = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(configPath))!.AsObject();
        raw["models"] = System.Text.Json.Nodes.JsonNode.Parse(
            """{"agents":{"reviewer":"strong"},"tiers":{"anthropic":{"strong":"legacy"}}}""");
        raw["unrelated"] = System.Text.Json.Nodes.JsonNode.Parse("""{"sentinel":[3,1,4]}""");
        File.WriteAllText(configPath, raw.ToJsonString());
    }

    #region Explicit File Scope

    [Fact]
    public async Task Fix_ExplicitStandaloneFile_Succeeds()
    {
        var filePath = Path.Combine(TestDir, "standalone.md");
        File.WriteAllText(filePath, "---\narea: test\ntype: context\n---\n\n# Standalone\n\nA standalone document.");

        var result = await RunAsync(FixCommand.Create(), filePath);

        result.AssertSuccess();
        Assert.Contains($"Fixing {filePath}...", result.Stdout);
        Assert.Contains("Fixed 0 issues automatically.", result.Stdout);
        Assert.DoesNotContain("Error:", result.Stderr);
    }

    [Fact]
    public async Task Fix_ExplicitFile_OnlyFixesSelectedFileAfterRename()
    {
        (await InitProjectAsync()).AssertSuccess();
        var selectedPath = Path.Combine(DydoDir, "guides", "Selected File.md");
        var unrelatedPath = Path.Combine(DydoDir, "guides", "Unrelated File.md");
        var targetPath = Path.Combine(DydoDir, "reference", "resolution-target.md");
        var content = "---\narea: guides\ntype: guide\n---\n\n# Selected\n\nSee [[resolution-target]].";
        WriteFile("dydo/guides/Selected File.md", content);
        WriteFile("dydo/guides/Unrelated File.md", content);
        WriteFile("dydo/reference/resolution-target.md", "---\narea: reference\ntype: context\n---\n\n# Resolution Target\n\nTarget document.");
        var unrelatedBefore = File.ReadAllText(unrelatedPath);
        var targetBefore = File.ReadAllText(targetPath);

        var result = await RunAsync(FixCommand.Create(), selectedPath);

        result.AssertSuccess();
        AssertFileExists("dydo/guides/selected-file.md");
        AssertFileNotExists("dydo/guides/Selected File.md");
        var selected = ReadFile("dydo/guides/selected-file.md");
        Assert.Contains("[resolution-target](../reference/resolution-target.md)", selected);
        Assert.DoesNotContain("[[resolution-target]]", selected);
        Assert.Equal(unrelatedBefore, File.ReadAllText(unrelatedPath));
        Assert.Equal(targetBefore, File.ReadAllText(targetPath));
        Assert.True(File.Exists(unrelatedPath));
        Assert.False(File.Exists(Path.Combine(DydoDir, "guides", "unrelated-file.md")));
        Assert.Contains("Renamed Selected File.md -> selected-file.md", result.Stdout);
        Assert.DoesNotContain("Unrelated File.md", result.Stdout);
        Assert.Equal(1, result.Stdout.Split("Converted 1 wikilinks to relative paths").Length - 1);
    }

    [Fact]
    public async Task Fix_ExplicitFileBesideProjectConfig_UsesParentCorpus()
    {
        (await InitProjectAsync()).AssertSuccess();
        var notePath = Path.Combine(TestDir, "Project Note.md");
        File.WriteAllText(notePath, "---\narea: test\ntype: context\n---\n\n# Project Note\n\nA project-level note.");

        var result = await RunAsync(FixCommand.Create(), notePath);

        result.AssertSuccess();
        AssertFileExists("project-note.md");
        AssertFileNotExists("Project Note.md");
        Assert.Contains("Renamed Project Note.md -> project-note.md", result.Stdout);
        Assert.DoesNotContain("Fixed 0 issues automatically.", result.Stdout);
    }

    [Fact]
    public async Task Fix_ExplicitFileInLegacyDocs_UsesContainingRoot()
    {
        WriteFile("docs/index.md", "---\narea: docs\ntype: hub\n---\n\n# Docs\n\nDocumentation root.");
        var selectedPath = Path.Combine(TestDir, "docs", "guides", "Legacy Selected.md");
        var targetPath = Path.Combine(TestDir, "docs", "reference", "legacy-target.md");
        WriteFile("docs/guides/Legacy Selected.md", "---\narea: guides\ntype: guide\n---\n\n# Legacy Selected\n\nSee [[legacy-target]].");
        WriteFile("docs/reference/legacy-target.md", "---\narea: reference\ntype: context\n---\n\n# Legacy Target\n\nTarget document.");
        var targetBefore = File.ReadAllText(targetPath);

        var result = await RunAsync(FixCommand.Create(), selectedPath);

        result.AssertSuccess();
        AssertFileExists("docs/guides/legacy-selected.md");
        AssertFileNotExists("docs/guides/Legacy Selected.md");
        var selected = ReadFile("docs/guides/legacy-selected.md");
        Assert.Contains("[legacy-target](../reference/legacy-target.md)", selected);
        Assert.DoesNotContain("[[legacy-target]]", selected);
        Assert.Equal(1, result.Stdout.Split("Converted 1 wikilinks to relative paths").Length - 1);
        Assert.Equal(targetBefore, File.ReadAllText(targetPath));
    }

    #endregion

    #region Obsidian Compatibility

    [Fact]
    public async Task Fix_IgnoresObsidianFolder()
    {
        // Arrange
        var initResult = await InitProjectAsync();
        initResult.AssertSuccess();

        // Create .obsidian folder with typical config files
        WriteFile("dydo/.obsidian/app.json", "{\"alwaysUpdateLinks\": true}");
        WriteFile("dydo/.obsidian/appearance.json", "{\"theme\": \"obsidian\"}");
        WriteFile("dydo/.obsidian/workspace.json", "{\"main\": {}}");

        // Act
        var fixCommand = FixCommand.Create();
        var fixResult = await RunAsync(fixCommand, DydoDir);

        // Assert - Fix should succeed without creating hub for .obsidian
        fixResult.AssertSuccess();
        Assert.DoesNotContain(".obsidian", fixResult.Stdout);
        AssertFileNotExists("dydo/.obsidian/_index.md");
    }

    [Fact]
    public async Task Check_IgnoresObsidianFolder()
    {
        // Arrange
        var initResult = await InitProjectAsync();
        initResult.AssertSuccess();

        // Create .obsidian folder with typical config files
        WriteFile("dydo/.obsidian/app.json", "{\"alwaysUpdateLinks\": true}");
        WriteFile("dydo/.obsidian/appearance.json", "{\"theme\": \"obsidian\"}");
        WriteFile("dydo/.obsidian/workspace.json", "{\"main\": {}}");

        // Run fix to generate hubs
        var fixCommand = FixCommand.Create();
        await RunAsync(fixCommand, DydoDir);

        // Act
        var checkResult = await CheckAsync(DydoDir);

        // Assert - Check should not complain about .obsidian folder
        checkResult.AssertSuccess();
        Assert.DoesNotContain(".obsidian", checkResult.Stdout);
    }

    #endregion

    [Fact]
    public async Task Fix_PreservesHandwrittenNavigationFiles()
    {
        (await InitProjectAsync()).AssertSuccess();
        const string hub = "# Custom hub\n";
        const string meta = "---\narea: guides\ntype: folder-meta\n---\n\n# Custom\n";
        WriteFile("dydo/guides/custom/_index.md", hub);
        WriteFile("dydo/guides/custom/_custom.md", meta);
        WriteFile("dydo/guides/custom/note.md", "---\narea: guides\ntype: guide\n---\n\n# Note\n");

        (await RunAsync(FixCommand.Create(), DydoDir)).AssertSuccess();

        Assert.Equal(hub, ReadFile("dydo/guides/custom/_index.md"));
        Assert.Equal(meta, ReadFile("dydo/guides/custom/_custom.md"));
        AssertFileExists("dydo/guides/custom/note.md");
    }

}
