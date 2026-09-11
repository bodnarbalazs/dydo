namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;
using DynaDocs.Models;
using DynaDocs.Services;

[Collection("Integration")]
public class TemplateCommandTests : IntegrationTestBase
{
    [Fact]
    public async Task Update_WarningsReturnNonzeroAndPreserveOriginalConfigBytes()
    {
        (await InitProjectAsync()).AssertSuccess();
        var configPath = Path.Combine(TestDir, "dydo.json");
        UserEditFrameworkDoc(configPath);
        AddLegacyModels(configPath);
        var original = File.ReadAllBytes(configPath);

        var result = await RunTemplateUpdateAsync();

        Assert.NotEqual(0, result.ExitCode);
        result.AssertStdoutContains("Template update complete:");
        Assert.Contains("user-edited", result.Stderr);
        Assert.Equal(original, File.ReadAllBytes(configPath));
        Assert.Contains("\"models\"", File.ReadAllText(configPath));
        Assert.Empty(Directory.GetFiles(TestDir, "dydo.json.*.tmp"));
    }

    [Fact]
    public async Task Update_DiffWithWarnings_ReturnsNonzeroWithoutSaving()
    {
        (await InitProjectAsync()).AssertSuccess();
        var configPath = Path.Combine(TestDir, "dydo.json");
        UserEditFrameworkDoc(configPath);
        AddLegacyModels(configPath);
        var original = File.ReadAllBytes(configPath);

        var result = await RunTemplateUpdateAsync("--diff");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("user-edited", result.Stderr);
        Assert.Equal(original, File.ReadAllBytes(configPath));
        Assert.Empty(Directory.GetFiles(TestDir, "dydo.json.*.tmp"));
    }

    [Fact]
    public async Task Update_PostWorkFailure_PreservesOriginalConfigBytes()
    {
        (await InitProjectAsync()).AssertSuccess();
        var configPath = Path.Combine(TestDir, "dydo.json");
        AddLegacyModels(configPath);
        var original = File.ReadAllBytes(configPath);

        var (exitCode, stdout, stderr) = ConsoleCapture.All(() =>
            TemplateCommand.ExecuteUpdate(false, () => throw new IOException("injected post-work failure")));

        Assert.Equal(1, exitCode);
        Assert.Contains("injected post-work failure", stderr);
        // The complete tally is reported before the commit seam.
        Assert.Contains("Template update complete:", stdout);
        Assert.Equal(original, File.ReadAllBytes(configPath));
        Assert.Contains("\"models\"", File.ReadAllText(configPath));
        Assert.Empty(Directory.GetFiles(TestDir, "dydo.json.*.tmp"));
    }

    // Stores the framework hash of the first framework doc, then edits the doc: the next update
    // sees a hash mismatch and warns "user-edited" instead of overwriting it.
    private void UserEditFrameworkDoc(string configPath)
    {
        var relativePath = TemplateCommand.FrameworkDocFiles.First();
        var docPath = Path.Combine(DydoDir, relativePath);
        var config = new ConfigService().LoadConfigStrict(TestDir)!;
        config.FrameworkHashes[relativePath] = TemplateCommand.ComputeHash(File.ReadAllText(docPath));
        new ConfigService().SaveConfig(config, configPath);
        File.AppendAllText(docPath, "\nUSER EDIT SENTINEL\n");
    }

    private static void AddLegacyModels(string configPath)
    {
        var raw = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(configPath))!.AsObject();
        raw["models"] = System.Text.Json.Nodes.JsonNode.Parse(
            """{"agents":{"reviewer":"strong"},"tiers":{"anthropic":{"strong":"legacy"}}}""");
        raw["unrelated"] = System.Text.Json.Nodes.JsonNode.Parse("""{"sentinel":[3,1,4]}""");
        File.WriteAllText(configPath, raw.ToJsonString());
    }

    public static IEnumerable<object[]> UnsupportedSourceShapes()
    {
        string[] names = ["Skill-ghost.template.md", "valid-Resource-ghost.template.md",
            "skill-ghost.TEMPLATE.MD", "skill-ghost.Template.md", "skill-ghost.template.Md",
            "valid-resource-ghost.TEMPLATE.MD", "valid-resource-ghost.Template.md",
            "valid-resource-ghost.template.Md", "skill-ghost.template.md.bak",
            "valid-resource-ghost.template.md.bak", "arbitrary.template.md", "README", ".hidden", "binary.dat"];
        foreach (var name in names)
        foreach (var directory in new[] { "", "nested/" })
        foreach (var operation in new[] { "sync", "update", "preview" })
            yield return [directory + name, operation];
    }

    [Theory]
    [MemberData(nameof(UnsupportedSourceShapes))]
    public async Task SourceCommands_IgnoreUnsupportedShapesBeforeCheckingLocation(string name, string operation)
    {
        (await InitProjectAsync()).AssertSuccess();
        var sources = Path.Combine(TestDir, "dydo/_system/templates");
        File.WriteAllText(Path.Combine(sources, "skill-valid.template.md"),
            "---\nname: valid\ndescription: Valid source.\nemit: agent\nargument-hint: context\n---\n\n# Valid body\n\n[Guide](resources/guide.md)\n");
        File.WriteAllText(Path.Combine(sources, "resource-valid-resource-guide.template.md"), "# Valid resource\n");
        (await RunAsync(SyncCommand.Create())).AssertSuccess();
        var config = new ConfigService().LoadConfigStrict(TestDir)!;
        Assert.True(config.Skills["valid"].Enabled);
        Assert.Equal("custom", config.Skills["valid"].Origin);
        Assert.True(config.Skills["valid"].EmitAgent);
        Assert.True(config.Skills["valid"].CodexMetadata);
        Assert.Equal(["guide"], config.Skills["valid"].Resources);
        foreach (var provider in new[] { ".claude", ".agents" })
        {
            Assert.Contains("# Valid body", File.ReadAllText(Path.Combine(TestDir, provider, "skills/valid/SKILL.md")));
            Assert.Equal("# Valid resource\n", File.ReadAllText(Path.Combine(TestDir, provider, "skills/valid/resources/guide.md")));
        }
        AssertFileExists(".claude/agents/valid.md");
        AssertFileExists(".codex/agents/valid.toml");
        AssertFileExists(".agents/skills/valid/agents/openai.yaml");
        var path = Path.Combine(sources, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // Invalid source content also proves that these files never enter source validation.
        File.WriteAllBytes(path, [0xff, 0x00, 0x81]);
        var before = Directory.GetFiles(TestDir, "*", SearchOption.AllDirectories)
            .ToDictionary(file => file, File.ReadAllBytes);

        var result = await RunSourceOperation(operation);

        result.AssertSuccess();
        Assert.Equal(before.Keys.Order(), Directory.GetFiles(TestDir, "*", SearchOption.AllDirectories).Order());
        Assert.All(before, entry => Assert.Equal(entry.Value, File.ReadAllBytes(entry.Key)));
    }

    [Theory]
    [InlineData("sync", false)]
    [InlineData("update", false)]
    [InlineData("preview", false)]
    [InlineData("sync", true)]
    [InlineData("update", true)]
    [InlineData("preview", true)]
    public async Task SourceCommands_RejectEmptyResourceOwnerAtomically(string operation, bool nested)
    {
        (await InitProjectAsync()).AssertSuccess();
        var sources = Path.Combine(TestDir, "dydo/_system/templates");
        File.WriteAllText(Path.Combine(sources, "skill-valid.template.md"),
            "---\nname: valid\ndescription: Valid source.\nemit: skill\n---\n\n# Valid body\n");
        (await RunAsync(SyncCommand.Create())).AssertSuccess();
        Assert.True(new ConfigService().LoadConfigStrict(TestDir)!.Skills["valid"].Enabled);
        foreach (var provider in new[] { ".claude", ".agents" })
            Assert.Contains("# Valid body", File.ReadAllText(Path.Combine(TestDir, provider, "skills/valid/SKILL.md")));
        if (operation != "sync")
            (await RunTemplateUpdateAsync(operation == "preview" ? ["--diff"] : [])).AssertSuccess();

        var name = nested ? Path.Combine("nested", "resource--resource-ghost.template.md") : "resource--resource-ghost.template.md";
        var path = Path.Combine(sources, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "# Empty-owner resource\n");
        var before = Directory.GetFiles(TestDir, "*", SearchOption.AllDirectories)
            .ToDictionary(file => file, File.ReadAllBytes);

        var result = await RunSourceOperation(operation);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(name.Replace('\\', '/'), result.Stderr.Replace('\\', '/'), StringComparison.Ordinal);
        Assert.Contains(nested ? "is nested; local templates must be top-level" : "invalid skill or resource name", result.Stderr);
        if (nested)
            Assert.DoesNotContain("invalid skill or resource name", result.Stderr);
        Assert.Equal(before.Keys.Order(), Directory.GetFiles(TestDir, "*", SearchOption.AllDirectories).Order());
        Assert.All(before, entry => Assert.Equal(entry.Value, File.ReadAllBytes(entry.Key)));
    }

    [Theory]
    [InlineData("nested/skill-ghost.template.md", "top-level")]
    [InlineData("nested/resource-valid-resource-ghost.template.md", "top-level")]
    [InlineData("skill-Bad.template.md", "invalid skill name")]
    [InlineData("skill-bad-resource-name.template.md", "invalid skill name")]
    [InlineData("skill-ghost.template.md", "frontmatter")]
    [InlineData("resource-valid-resource-ghost.template.md", "no matching skill source")]
    public async Task SourceCommands_RejectRecognizedInvalidSourcesAtomically(string name, string reason)
    {
        (await InitProjectAsync()).AssertSuccess();
        (await RunAsync(SyncCommand.Create())).AssertSuccess();
        var path = Path.Combine(TestDir, "dydo/_system/templates", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "Malformed recognized source");
        var before = Directory.GetFiles(TestDir, "*", SearchOption.AllDirectories)
            .ToDictionary(file => file, File.ReadAllBytes);

        foreach (var operation in new[] { "sync", "update", "preview" })
        {
            var result = await RunSourceOperation(operation);
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains(Path.GetFileName(path), result.Stderr);
            Assert.Contains(reason, result.Stderr);
            Assert.Equal(before.Keys.Order(), Directory.GetFiles(TestDir, "*", SearchOption.AllDirectories).Order());
            Assert.All(before, entry => Assert.Equal(entry.Value, File.ReadAllBytes(entry.Key)));
        }
    }

    private Task<CommandResult> RunSourceOperation(string operation)
    {
        if (operation == "sync")
            return RunAsync(SyncCommand.Create());
        var arguments = operation == "preview" ? new[] { "--diff" } : [];
        return RunTemplateUpdateAsync(arguments);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TemplateUpdate_InvalidSourceHashPath_CannotClaimOtherProjectFiles(bool diff)
    {
        await InitProjectAsync();
        var sentinel = Path.Combine(TestDir, "dydo/_system/project-owned.md");
        File.WriteAllText(sentinel, "PROJECT OWNED SENTINEL");
        var config = new ConfigService().LoadConfigStrict(TestDir)!;
        config.FrameworkHashes["_system/templates/../project-owned.md"] = new string('0', 64);
        new ConfigService().SaveConfig(config, Path.Combine(TestDir, "dydo.json"));
        var before = Directory.GetFiles(TestDir, "*", SearchOption.AllDirectories)
            .ToDictionary(path => path, File.ReadAllBytes);

        var result = await RunTemplateUpdateAsync(diff ? ["--diff"] : []);

        Assert.NotEqual(0, result.ExitCode);
        result.AssertStderrContains("_system/templates/../project-owned.md");
        Assert.Equal(before.Keys.Order(), Directory.GetFiles(TestDir, "*", SearchOption.AllDirectories).Order());
        Assert.All(before, entry => Assert.Equal(entry.Value, File.ReadAllBytes(entry.Key)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TemplateUpdate_UntrackedPackagedResourceCollision_IsAtomic(bool diff)
    {
        await InitProjectAsync();
        var configPath = Path.Combine(TestDir, "dydo.json");
        var config = new ConfigService().LoadConfigStrict(TestDir)!;
        config.FrameworkHashes.Remove("_system/templates/resource-reviewer-resource-code.template.md");
        config.Skills["reviewer"].Resources!.Remove("code");
        new ConfigService().SaveConfig(config, configPath);
        File.WriteAllText(Path.Combine(TestDir, "dydo/_system/templates/resource-reviewer-resource-code.template.md"),
            "CUSTOM RESOURCE SENTINEL — preserve exactly");
        var before = Directory.GetFiles(TestDir, "*", SearchOption.AllDirectories)
            .ToDictionary(path => path, File.ReadAllBytes);

        var result = await RunTemplateUpdateAsync(diff ? ["--diff"] : []);

        Assert.NotEqual(0, result.ExitCode);
        result.AssertStderrContains("resource-reviewer-resource-code.template.md");
        Assert.Contains("collides", result.Stderr);
        Assert.Equal(before.Keys.Order(), Directory.GetFiles(TestDir, "*", SearchOption.AllDirectories).Order());
        Assert.All(before, entry => Assert.Equal(entry.Value, File.ReadAllBytes(entry.Key)));
    }

    [Theory]
    [InlineData("skill-reviewer.template.md")]
    [InlineData("resource-reviewer-resource-code.template.md")]
    public async Task TemplateUpdate_MissingSourceHash_ReportsPreviewAndAppliedReconciliation(string file)
    {
        await InitProjectAsync();
        var configPath = Path.Combine(TestDir, "dydo.json");
        var sourcePath = Path.Combine(TestDir, "dydo", "_system", "templates", file);
        var sourceBefore = File.ReadAllBytes(sourcePath);
        var config = new ConfigService().LoadConfigStrict(TestDir)!;
        config.FrameworkHashes.Remove($"_system/templates/{file}");
        new ConfigService().SaveConfig(config, configPath);
        var before = File.ReadAllBytes(configPath);

        var preview = await RunTemplateUpdateAsync("--diff");

        preview.AssertSuccess();
        preview.AssertStdoutContains($"Reconciled source hash: _system/templates/{file}");
        Assert.Equal(before, File.ReadAllBytes(configPath));
        Assert.Equal(sourceBefore, File.ReadAllBytes(sourcePath));

        var update = await RunTemplateUpdateAsync();

        update.AssertSuccess();
        update.AssertStdoutContains($"Reconciled source hash: _system/templates/{file}");
        var expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(File.ReadAllText(sourcePath).Replace("\r\n", "\n")))).ToLowerInvariant();
        Assert.Equal(expectedHash,
            new ConfigService().LoadConfigStrict(TestDir)!.FrameworkHashes[$"_system/templates/{file}"]);
        Assert.Equal(sourceBefore, File.ReadAllBytes(sourcePath));
        var after = File.ReadAllBytes(configPath);
        var repeated = await RunTemplateUpdateAsync();
        repeated.AssertSuccess();
        Assert.DoesNotContain("Reconciled source hash:", repeated.Stdout);
        Assert.Equal(after, File.ReadAllBytes(configPath));
    }

    [Fact]
    public async Task TemplateUpdate_OverwritesShippedSourceButPreservesDistinctCustomSource()
    {
        await InitProjectAsync();
        var root = Path.Combine(TestDir, "dydo", "_system", "templates");
        var shipped = Path.Combine(root, "skill-reviewer.template.md");
        var custom = Path.Combine(root, "skill-our-review.template.md");
        File.WriteAllText(shipped, "broken hard edit");
        var customContent = "---\nname: our-review\ndescription: Our review.\nemit: skill\ninvocation: automatic\n---\n\n# Our Review\n";
        File.WriteAllText(custom, customContent);

        var result = await RunTemplateUpdateAsync();

        result.AssertSuccess();
        Assert.Equal(TemplateGenerator.ReadBuiltInTemplate("skill-reviewer.template.md"), File.ReadAllText(shipped));
        Assert.Equal(customContent, File.ReadAllText(custom));
        var config = new ConfigService().LoadConfigStrict(TestDir)!;
        Assert.Equal("custom", config.Skills["our-review"].Origin);
        Assert.True(config.Skills["our-review"].Enabled);
    }

    [Fact]
    public async Task TemplateUpdate_MigratesProjectWithoutLocalSourceLayer()
    {
        await InitProjectAsync();
        Directory.Delete(Path.Combine(TestDir, "dydo", "_system", "templates"), true);
        var config = new ConfigService().LoadConfigStrict(TestDir)!;
        config.Skills.Clear();
        foreach (var key in config.FrameworkHashes.Keys.Where(key => key.StartsWith("_system/templates/", StringComparison.Ordinal)).ToList())
            config.FrameworkHashes.Remove(key);
        new ConfigService().SaveConfig(config, Path.Combine(TestDir, "dydo.json"));

        var result = await RunTemplateUpdateAsync();

        result.AssertSuccess();
        AssertFileExists("dydo/_system/templates/skill-reviewer.template.md");
        var migrated = new ConfigService().LoadConfigStrict(TestDir)!;
        Assert.NotEmpty(migrated.Skills);
        Assert.Contains("_system/templates/", migrated.ScanExclude);
    }

    [Fact]
    public async Task TemplateUpdate_DiffPreflightsSourcesWithoutWriting()
    {
        await InitProjectAsync();
        var root = Path.Combine(TestDir, "dydo", "_system", "templates");
        var shipped = Path.Combine(root, "skill-reviewer.template.md");
        File.WriteAllText(shipped, "broken hard edit");
        var configBefore = File.ReadAllText(Path.Combine(TestDir, "dydo.json"));

        var result = await RunTemplateUpdateAsync("--diff");

        result.AssertSuccess();
        Assert.Equal("broken hard edit", File.ReadAllText(shipped));
        Assert.Equal(configBefore, File.ReadAllText(Path.Combine(TestDir, "dydo.json")));
        result.AssertStdoutContains("Updated source: _system/templates/skill-reviewer.template.md");
    }

    [Fact]
    public async Task TemplateUpdate_RemovesRetiredShippedSourceAndKeepsCleanupTombstone()
    {
        await InitProjectAsync();
        var source = Path.Combine(TestDir, "dydo", "_system", "templates", "skill-former.template.md");
        File.WriteAllText(source,
            "---\nname: former\ndescription: Former shipped skill.\nemit: skill\ninvocation: automatic\n---\n\n# Former\n");
        var config = new ConfigService().LoadConfigStrict(TestDir)!;
        config.Skills["former"] = new SkillSwitchConfig
        {
            Enabled = false,
            Origin = "shipped",
            EmitAgent = false,
            CodexMetadata = false,
            Resources = []
        };
        config.FrameworkHashes["_system/templates/skill-former.template.md"] = new string('0', 64);
        new ConfigService().SaveConfig(config, Path.Combine(TestDir, "dydo.json"));

        var result = await RunTemplateUpdateAsync();

        result.AssertSuccess();
        Assert.False(File.Exists(source));
        var updated = new ConfigService().LoadConfigStrict(TestDir)!;
        Assert.True(updated.Skills.TryGetValue("former", out var tombstone));
        Assert.False(tombstone.Enabled);
        Assert.Equal("shipped", tombstone.Origin);
        Assert.False(updated.FrameworkHashes.ContainsKey("_system/templates/skill-former.template.md"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TemplateUpdate_RejectsUntrackedSourceAtRetiredShippedNameWithoutDeletingIt(bool diff)
    {
        await InitProjectAsync();
        var source = Path.Combine(TestDir, "dydo", "_system", "templates", "skill-former.template.md");
        var customContent =
            "---\nname: former\ndescription: Custom replacement.\nemit: skill\ninvocation: automatic\n---\n\n# Custom replacement\n";
        File.WriteAllText(source, customContent);
        var config = new ConfigService().LoadConfigStrict(TestDir)!;
        config.Skills["former"] = new SkillSwitchConfig
        {
            Enabled = true,
            Origin = "shipped",
            EmitAgent = false,
            CodexMetadata = false,
            Resources = []
        };
        new ConfigService().SaveConfig(config, Path.Combine(TestDir, "dydo.json"));
        var configBefore = File.ReadAllText(Path.Combine(TestDir, "dydo.json"));

        var result = await RunTemplateUpdateAsync(diff ? ["--diff"] : []);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("retired", result.Stderr, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(customContent, File.ReadAllText(source));
        Assert.Equal(configBefore, File.ReadAllText(Path.Combine(TestDir, "dydo.json")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TemplateUpdate_IgnoresLockedAgentWorkspaceEvidence(bool diff)
    {
        await InitProjectAsync();
        var workspace = Path.Combine(TestDir, "dydo", "agents", "workspace");
        Directory.CreateDirectory(workspace);
        var evidence = Path.Combine(workspace, "active-agent-evidence.bin");
        await File.WriteAllTextAsync(evidence, "volatile evidence");
        await using var locked = new FileStream(evidence, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var result = await RunTemplateUpdateAsync(diff ? ["--diff"] : []);

        result.AssertSuccess();
        locked.Position = 0;
        using var reader = new StreamReader(locked, leaveOpen: true);
        Assert.Equal("volatile evidence", await reader.ReadToEndAsync());
    }

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
