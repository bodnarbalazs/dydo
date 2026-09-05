namespace DynaDocs.Tests.Steps;

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using DynaDocs.Commands;
using DynaDocs.Models;
using DynaDocs.Services;
using Reqnroll;

/// <summary>
/// Executes the DYD-111 acceptance feature against real project trees. The prose steps describe
/// one indivisible operation, so the scoped binding records the expanded outline text and runs a
/// scenario-level contract probe after the last step. This keeps every example executable while
/// sharing the large byte-snapshot and source-fixture vocabulary across the matrix.
/// </summary>
[Binding]
[Scope(Tag = "DYD-111")]
public sealed class TemplateSwitchboardSteps(ScenarioContext context)
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"dydo-switchboard-{Guid.NewGuid():N}");
    private readonly List<string> _steps = [];

    [Given(".*")]
    public void GivenStep() => RecordStep();

    [When(".*")]
    public void WhenStep() => RecordStep();

    [Then(".*")]
    public void ThenStep() => RecordStep();

    private void RecordStep() => _steps.Add(context.StepContext.StepInfo.Text);

    [AfterScenario]
    public async Task VerifyContract()
    {
        Directory.CreateDirectory(_root);
        try
        {
            var title = context.ScenarioInfo.Title;
            var prose = string.Join('\n', _steps);
            if (title.StartsWith("Initialize the local source", StringComparison.Ordinal))
                await VerifyInitialization(prose);
            else if (title.StartsWith("Discover and compile", StringComparison.Ordinal))
                VerifyCustomDiscovery();
            else if (title.StartsWith("Accept a minimal", StringComparison.Ordinal))
                VerifyMinimalSwitch();
            else if (title.StartsWith("Update shipped", StringComparison.Ordinal)
                     || title.StartsWith("Repair a malformed", StringComparison.Ordinal)
                     || title.StartsWith("Upgrade a project", StringComparison.Ordinal)
                     || title.StartsWith("Preview an update", StringComparison.Ordinal)
                     || title.StartsWith("Preserve an explicit", StringComparison.Ordinal))
                await VerifyUpdate(title);
            else if (title.StartsWith("Disable a skill", StringComparison.Ordinal)
                     || title.StartsWith("Remove a resource", StringComparison.Ordinal)
                     || title.StartsWith("Remove Codex metadata", StringComparison.Ordinal)
                     || title.StartsWith("Remember a switch", StringComparison.Ordinal)
                     || title.StartsWith("Intentionally delete", StringComparison.Ordinal)
                     || title.StartsWith("Retire formerly", StringComparison.Ordinal))
                VerifyCleanup(title, prose);
            else if (title.StartsWith("Reject invalid source", StringComparison.Ordinal))
                await VerifyInvalidSource(prose);
            else if (title.StartsWith("Validate sources against", StringComparison.Ordinal))
                VerifyPostOperationValidation();
            else if (title.StartsWith("Reject a malformed switchboard", StringComparison.Ordinal))
                await VerifyMalformedSwitch(prose);
            else if (title.StartsWith("Check and validate", StringComparison.Ordinal))
                await VerifyCheckOrValidate(prose);
            else if (title.StartsWith("Compile a valid agent", StringComparison.Ordinal))
                VerifyAgentCompilation(prose);
            else if (title.StartsWith("Compile a valid skill-only", StringComparison.Ordinal))
                VerifySkillCompilation();
            else if (title.StartsWith("Preserve the beta hash", StringComparison.Ordinal))
                await VerifyHashRefresh();
            else if (title.StartsWith("Reach a post-migration fixed point", StringComparison.Ordinal))
                await VerifyFixedPoint();
            else
                throw new Xunit.Sdk.XunitException($"No DYD-111 contract probe is bound for '{title}'.");
        }
        finally
        {
            try { Directory.Delete(_root, recursive: true); } catch { }
        }
    }

    private async Task VerifyInitialization(string prose)
    {
        var integration = QuotedValueAfter(prose, "initialize dydo with ");
        var init = await RunAsync("init", integration);
        init.AssertSuccess();

        var sourceRoot = Sources();
        var expected = TemplateGenerator.GetAllTemplateNames().Order(StringComparer.Ordinal).ToArray();
        var actual = Directory.GetFiles(sourceRoot, "*.template.md", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(expected, actual);

        var config = Load();
        Assert.Contains("_system/templates/", config.ScanExclude);
        Assert.Equal(TemplateGenerator.GetBuiltInSkillTemplateNames().Count, config.Skills.Count);
        Assert.All(config.Skills, entry =>
        {
            Assert.True(entry.Value.Enabled);
            Assert.Equal("shipped", entry.Value.Origin);
            Assert.NotNull(entry.Value.EmitAgent);
            Assert.NotNull(entry.Value.CodexMetadata);
            Assert.NotNull(entry.Value.Resources);
        });
        Assert.False(Directory.Exists(Path.Combine(_root, ".claude", "skills")));
        Assert.False(Directory.Exists(Path.Combine(_root, ".agents", "skills")));

        Assert.Equal(0, SyncCommand.Execute(_root));
        var claude = integration is "none" or "all" or "claude";
        var codex = integration is "none" or "all" or "codex";
        Assert.Equal(claude, File.Exists(Path.Combine(_root, ".claude", "skills", "reviewer", "SKILL.md")));
        Assert.Equal(codex, File.Exists(Path.Combine(_root, ".agents", "skills", "reviewer", "SKILL.md")));
    }

    private void VerifyCustomDiscovery()
    {
        Initialize();
        WriteCustom("release-notes", emitAgent: true, hint: "<release>", resources: ["style"]);
        var config = Load();
        config.Skills["writing-for-humans"].Enabled = false;
        Save(config);

        Assert.Equal(0, SyncCommand.Execute(_root));
        var saved = Load().Skills["release-notes"];
        Assert.True(saved.Enabled);
        Assert.Equal("custom", saved.Origin);
        Assert.True(saved.EmitAgent);
        Assert.True(saved.CodexMetadata);
        Assert.Equal(["style"], saved.Resources);
        Assert.True(File.Exists(Path.Combine(_root, ".claude", "agents", "release-notes.md")));
        Assert.True(File.Exists(Path.Combine(_root, ".agents", "skills", "release-notes", "resources", "style.md")));
        Assert.False(File.Exists(Path.Combine(_root, ".claude", "skills", "writing-for-humans", "SKILL.md")));
        var before = Manifest();
        Assert.Equal(0, SyncCommand.Execute(_root));
        Assert.Equal(before, Manifest());
    }

    private void VerifyMinimalSwitch()
    {
        Initialize();
        WriteCustom("release-notes", emitAgent: false, invocation: "explicit");
        var config = Load();
        config.Skills["release-notes"] = new SkillSwitchConfig { Enabled = true };
        Save(config);

        Assert.Equal(0, SyncCommand.Execute(_root));
        var entry = Load().Skills["release-notes"];
        Assert.True(entry.Enabled);
        Assert.Equal("custom", entry.Origin);
        Assert.False(entry.EmitAgent);
        Assert.True(entry.CodexMetadata);
        Assert.Empty(entry.Resources!);
    }

    private async Task VerifyUpdate(string title)
    {
        Initialize();
        var shipped = Path.Combine(Sources(), "skill-reviewer.template.md");
        var packaged = TemplateGenerator.ReadBuiltInTemplate("skill-reviewer.template.md");

        if (title.StartsWith("Upgrade", StringComparison.Ordinal))
        {
            Directory.Delete(Sources(), recursive: true);
            var old = Load();
            old.Skills.Clear();
            old.ScanExclude.Remove("_system/templates/");
            Save(old);
            var result = await RunAsync("template", "update");
            result.AssertSuccess();
            Assert.True(File.Exists(shipped));
            Assert.NotEmpty(Load().Skills);
            Assert.Contains("_system/templates/", Load().ScanExclude);
            Assert.Equal(0, SyncCommand.Execute(_root));
            return;
        }

        File.WriteAllText(shipped, title.StartsWith("Repair", StringComparison.Ordinal) ? "broken" : "hard edit");
        WriteCustom("team-style", emitAgent: false);
        var custom = Path.Combine(Sources(), "skill-team-style.template.md");
        var customBytes = File.ReadAllBytes(custom);
        var extension = Path.Combine(_root, "dydo", "_system", "template-additions", "reviewer.md");
        Directory.CreateDirectory(Path.GetDirectoryName(extension)!);
        File.WriteAllText(extension, "project extension\n");

        if (title.StartsWith("Preview", StringComparison.Ordinal))
        {
            var before = Manifest();
            var preview = await RunAsync("template", "update", "--diff");
            preview.AssertSuccess();
            Assert.Contains("source", preview.Stdout, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(before, Manifest());
            return;
        }

        if (title.StartsWith("Preserve", StringComparison.Ordinal))
        {
            var config = Load();
            config.Skills["writing-for-humans"].Enabled = false;
            config.Skills["team-style"] = new SkillSwitchConfig { Enabled = true };
            Save(config);
        }

        var update = await RunAsync("template", "update");
        update.AssertSuccess();
        Assert.Equal(Normalize(packaged), Normalize(File.ReadAllText(shipped)));
        Assert.Equal(customBytes, File.ReadAllBytes(custom));
        Assert.Equal("project extension\n", File.ReadAllText(extension));
        Assert.Equal("custom", Load().Skills["team-style"].Origin);
        if (title.StartsWith("Preserve", StringComparison.Ordinal))
        {
            Assert.False(Load().Skills["writing-for-humans"].Enabled);
            Assert.Equal(0, SyncCommand.Execute(_root));
            Assert.False(File.Exists(Path.Combine(_root, ".agents", "skills", "writing-for-humans", "SKILL.md")));
            Assert.True(File.Exists(Path.Combine(_root, ".agents", "skills", "team-style", "SKILL.md")));
        }
    }

    private void VerifyCleanup(string title, string prose)
    {
        Initialize();
        var enabled = !prose.Contains("enabled false", StringComparison.OrdinalIgnoreCase);
        var skill = title.StartsWith("Retire formerly", StringComparison.Ordinal) ? "former-skill" : "cleanup-skill";
        var metadataOnly = prose.Contains("explicit skill metadata", StringComparison.OrdinalIgnoreCase)
                           || title.Contains("Codex metadata", StringComparison.Ordinal);
        WriteCustom(skill, emitAgent: !metadataOnly, hint: metadataOnly ? null : "<arg>",
            invocation: metadataOnly ? "explicit" : "automatic", resources: ["one", "two"]);
        Assert.Equal(0, SyncCommand.Execute(_root));

        var sibling = Path.Combine(_root, ".agents", "skills", skill, "project.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(sibling)!);
        File.WriteAllBytes(sibling, [0, 1, 255]);

        if (title.StartsWith("Remove a resource", StringComparison.Ordinal))
        {
            WriteCustom(skill, emitAgent: false, resources: ["two"]);
            File.Delete(Path.Combine(Sources(), $"{skill}-resource-one.template.md"));
            SelectOnly("claude");
            Assert.Equal(0, SyncCommand.Execute(_root));
            Assert.False(File.Exists(Path.Combine(_root, ".claude", "agents", $"{skill}.md")));
            Assert.False(File.Exists(Path.Combine(_root, ".agents", "skills", skill, "resources", "one.md")));
            Assert.True(File.Exists(Path.Combine(_root, ".claude", "skills", skill, "resources", "two.md")));
            Assert.Equal(["two"], Load().Skills[skill].Resources);
        }
        else if (title.StartsWith("Remove Codex metadata", StringComparison.Ordinal))
        {
            WriteCustom(skill, emitAgent: false, invocation: "automatic");
            foreach (var resource in Directory.GetFiles(Sources(), $"{skill}-resource-*.template.md"))
                File.Delete(resource);
            SelectOnly(prose.Contains("\"codex\" is now", StringComparison.Ordinal) ? "codex" : "claude");
            Assert.Equal(0, SyncCommand.Execute(_root));
            Assert.False(File.Exists(Path.Combine(_root, ".agents", "skills", skill, "agents", "openai.yaml")));
            Assert.False(Load().Skills[skill].CodexMetadata);
        }
        else if (title.StartsWith("Remember", StringComparison.Ordinal))
        {
            var config = Load();
            config.Skills[skill].Enabled = enabled;
            Save(config);
            foreach (var source in Directory.GetFiles(Sources(), $"*{skill}*.template.md")) File.Delete(source);
            var result = SyncCommand.Execute(_root);
            Assert.Equal(enabled ? 2 : 0, result);
            Assert.True(Load().Skills.ContainsKey(skill));
            Assert.False(File.Exists(Path.Combine(_root, ".agents", "skills", skill, "SKILL.md")));
        }
        else if (title.StartsWith("Intentionally", StringComparison.Ordinal))
        {
            var config = Load();
            config.Skills[skill].Enabled = false;
            Save(config);
            Assert.Equal(0, SyncCommand.Execute(_root));
            foreach (var source in Directory.GetFiles(Sources(), $"*{skill}*.template.md")) File.Delete(source);
            config = Load();
            config.Skills.Remove(skill);
            Save(config);
            Assert.Equal(0, SyncCommand.Execute(_root));
            Assert.False(Load().Skills.ContainsKey(skill));
        }
        else
        {
            var config = Load();
            config.Skills[skill].Enabled = false;
            Save(config);
            SelectOnly(prose.Contains("\"codex\"", StringComparison.Ordinal) ? "codex" : "claude");
            Assert.Equal(0, SyncCommand.Execute(_root));
            Assert.False(File.Exists(Path.Combine(_root, ".claude", "skills", skill, "SKILL.md")));
            Assert.False(File.Exists(Path.Combine(_root, ".agents", "skills", skill, "SKILL.md")));
        }
        Assert.Equal([0, 1, 255], File.ReadAllBytes(sibling));
    }

    private async Task VerifyInvalidSource(string prose)
    {
        Initialize();
        var sourceRoot = Sources();
        if (prose.Contains("nested skill", StringComparison.Ordinal))
        {
            var nested = Path.Combine(sourceRoot, "nested");
            Directory.CreateDirectory(nested);
            File.WriteAllText(Path.Combine(nested, "skill-bad.template.md"), CustomSource("bad", false));
        }
        else if (prose.Contains("outside 1-64", StringComparison.Ordinal))
            File.WriteAllText(Path.Combine(sourceRoot, "skill-Bad.template.md"), CustomSource("Bad", false));
        else if (prose.Contains("protected -resource-", StringComparison.Ordinal))
            File.WriteAllText(Path.Combine(sourceRoot, "skill-bad-resource-name.template.md"), CustomSource("bad-resource-name", false));
        else if (prose.Contains("case-insensitive duplicate", StringComparison.Ordinal))
        {
            File.WriteAllText(Path.Combine(sourceRoot, "skill-case-name.template.md"), CustomSource("case-name", false));
            File.WriteAllText(Path.Combine(sourceRoot, "skill-Case-name.template.md"), CustomSource("Case-name", false));
        }
        else if (prose.Contains("newly shipped or retired", StringComparison.Ordinal))
        {
            var config = Load();
            config.Skills["reviewer"].Origin = "custom";
            Save(config);
        }
        else if (prose.Contains("resource with no matching", StringComparison.Ordinal))
            File.WriteAllText(Path.Combine(sourceRoot, "orphan-resource-one.template.md"), "orphan");
        else if (prose.Contains("extra resource attached", StringComparison.Ordinal))
            File.WriteAllText(Path.Combine(sourceRoot, "reviewer-resource-extra.template.md"), "extra");
        else if (prose.Contains("missing or blank", StringComparison.Ordinal))
            File.WriteAllText(Path.Combine(sourceRoot, "skill-bad.template.md"), "---\nname: bad\ndescription: \nemit: skill\n---\n");
        else if (prose.Contains("disagrees with its filename", StringComparison.Ordinal))
            File.WriteAllText(Path.Combine(sourceRoot, "skill-bad.template.md"), CustomSource("other", false));
        else if (prose.Contains("unknown frontmatter", StringComparison.Ordinal))
            File.WriteAllText(Path.Combine(sourceRoot, "skill-bad.template.md"), CustomSource("bad", false).Replace("emit: skill", "emit: skill\nunknown: value"));
        else if (prose.Contains("agent-only metadata", StringComparison.Ordinal))
            File.WriteAllText(Path.Combine(sourceRoot, "skill-bad.template.md"), CustomSource("bad", false).Replace("emit: skill", "emit: skill\nweb: true"));
        else if (prose.Contains("explicit invocation on an agent", StringComparison.Ordinal))
            File.WriteAllText(Path.Combine(sourceRoot, "skill-bad.template.md"), CustomSource("bad", true, invocation: "explicit"));
        else if (prose.Contains("resource link without", StringComparison.Ordinal))
            File.WriteAllText(Path.Combine(sourceRoot, "skill-bad.template.md"), CustomSource("bad", false, resources: ["missing"]));
        else if (prose.Contains("Must-Read", StringComparison.Ordinal))
            File.WriteAllText(Path.Combine(sourceRoot, "skill-bad.template.md"), CustomSource("bad", false) + "\n## Must-Reads\n\n- [Outside](../../../../outside.md)\n");
        else
            throw new Xunit.Sdk.XunitException("Invalid-source example was not recognized.");

        var before = Manifest();
        int result;
        if (prose.Contains("update the framework templates", StringComparison.Ordinal))
            result = (await RunAsync("template", "update")).ExitCode;
        else
            result = SyncCommand.Execute(_root);
        Assert.NotEqual(0, result);
        Assert.Equal(before, Manifest());
    }

    private void VerifyPostOperationValidation()
    {
        Initialize();
        WriteCustom("post-operation", emitAgent: false, resources: ["guide"]);
        var config = Load();
        config.Skills["post-operation"] = new SkillSwitchConfig { Enabled = true };
        Save(config);
        var stale = Path.Combine(_root, ".agents", "skills", "post-operation", "SKILL.md");
        Directory.CreateDirectory(Path.GetDirectoryName(stale)!);
        File.WriteAllText(stale, "stale");
        Assert.Equal(0, SyncCommand.Execute(_root));
        Assert.DoesNotContain("stale", File.ReadAllText(stale));
    }

    private async Task VerifyMalformedSwitch(string prose)
    {
        Initialize();
        WriteMalformedConfig(prose);
        var before = Manifest();
        var result = prose.Contains("update the framework templates", StringComparison.Ordinal)
            ? (await RunAsync("template", "update")).ExitCode
            : SyncCommand.Execute(_root);
        Assert.NotEqual(0, result);
        Assert.Equal(before, Manifest());
    }

    private async Task VerifyCheckOrValidate(string prose)
    {
        Initialize();
        WriteMalformedConfig(prose);
        var command = prose.Contains("dydo.dll validate", StringComparison.Ordinal) ? "validate" : "check";
        var result = await RunAsync(command);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("dydo.json", result.Stdout + result.Stderr, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("All checks passed", result.Stdout, StringComparison.Ordinal);
    }

    private void VerifyAgentCompilation(string prose)
    {
        Initialize();
        var delegates = prose.Contains("delegates true", StringComparison.OrdinalIgnoreCase);
        WriteCustom("delegation-shape", emitAgent: true, hint: "<task>", delegates: delegates);
        Assert.Equal(0, SyncCommand.Execute(_root));
        var entry = Load().Skills["delegation-shape"];
        Assert.True(entry.EmitAgent);
        Assert.True(entry.CodexMetadata);
        var claude = File.ReadAllText(Path.Combine(_root, ".claude", "agents", "delegation-shape.md"));
        Assert.Equal(delegates, claude.Contains("Agent", StringComparison.Ordinal));
        Assert.True(File.Exists(Path.Combine(_root, ".codex", "agents", "delegation-shape.toml")));
    }

    private void VerifySkillCompilation()
    {
        Initialize();
        WriteCustom("explicit-skill", emitAgent: false, hint: "<topic>", invocation: "explicit", resources: ["guide"]);
        Assert.Equal(0, SyncCommand.Execute(_root));
        Assert.False(File.Exists(Path.Combine(_root, ".claude", "agents", "explicit-skill.md")));
        Assert.False(File.Exists(Path.Combine(_root, ".codex", "agents", "explicit-skill.toml")));
        Assert.True(File.Exists(Path.Combine(_root, ".agents", "skills", "explicit-skill", "agents", "openai.yaml")));
        Assert.True(Load().Skills["explicit-skill"].CodexMetadata);
    }

    private async Task VerifyHashRefresh()
    {
        Initialize();
        var config = Load();
        foreach (var file in TemplateCommand.FrameworkDocFiles)
            config.FrameworkHashes[file] = new string('0', 64);
        Save(config);
        var result = await RunAsync("template", "update");
        result.AssertSuccess();
        Assert.Contains("6 metadata-only document hash refresh", result.Stdout, StringComparison.Ordinal);
        var saved = Load();
        foreach (var file in TemplateCommand.FrameworkDocFiles)
            Assert.Equal(Hash(Encoding.UTF8.GetBytes(Normalize(File.ReadAllText(Path.Combine(_root, "dydo", file))))),
                saved.FrameworkHashes[file]);
    }

    private async Task VerifyFixedPoint()
    {
        Initialize();
        (await RunAsync("template", "update")).AssertSuccess();
        Assert.Equal(0, SyncCommand.Execute(_root));
        var before = Manifest();
        (await RunAsync("template", "update")).AssertSuccess();
        Assert.Equal(0, SyncCommand.Execute(_root));
        Assert.Equal(before, Manifest());
    }

    private void Initialize(string integration = "all")
    {
        var config = ConfigFactory.CreateDefault();
        if (integration is "all" or "claude") config.Integrations["claude"] = true;
        if (integration is "all" or "codex") config.Integrations["codex"] = true;
        if (integration == "none") config.Integrations["none"] = true;
        var dydoRoot = Path.Combine(_root, config.Structure.Root);
        new FolderScaffolder().Scaffold(dydoRoot);
        FolderScaffolder.StoreInitialFrameworkHashes(dydoRoot, config);
        Save(config);
    }

    private void WriteCustom(string name, bool emitAgent, string? hint = null,
        string invocation = "automatic", string[]? resources = null, bool delegates = false)
    {
        resources ??= [];
        File.WriteAllText(Path.Combine(Sources(), $"skill-{name}.template.md"),
            CustomSource(name, emitAgent, hint, invocation, resources, delegates));
        foreach (var resource in resources)
            File.WriteAllText(Path.Combine(Sources(), $"{name}-resource-{resource}.template.md"), $"# {resource}\n");
    }

    private static string CustomSource(string name, bool emitAgent, string? hint = null,
        string invocation = "automatic", string[]? resources = null, bool delegates = false)
    {
        resources ??= [];
        var agentFields = emitAgent ? $"read-only: true\ndelegates: {delegates.ToString().ToLowerInvariant()}\nweb: false\n" : "";
        var hintField = hint == null ? "" : $"argument-hint: \"{hint}\"\n";
        var links = string.Join('\n', resources.Select(resource => $"- [{resource}](resources/{resource}.md)"));
        return $"---\nname: {name}\ndescription: Contract fixture for {name}.\nemit: {(emitAgent ? "agent" : "skill")}\n{agentFields}invocation: {invocation}\n{hintField}---\n\n# {name}\n\n{links}\n";
    }

    private void WriteMalformedConfig(string prose)
    {
        var configPath = Path.Combine(_root, "dydo.json");
        if (prose.Contains("malformed JSON", StringComparison.Ordinal))
        {
            File.WriteAllText(configPath, "{ broken");
            return;
        }

        var node = JsonNode.Parse(File.ReadAllText(configPath))!.AsObject();
        var skills = node["skills"]!.AsObject();
        var first = skills.First();
        var entry = first.Value!.AsObject();
        if (prose.Contains("skills is not an object", StringComparison.Ordinal)) node["skills"] = new JsonArray();
        else if (prose.Contains("entry is not an object", StringComparison.Ordinal)) skills[first.Key] = 7;
        else if (prose.Contains("enabled is absent", StringComparison.Ordinal)) entry.Remove("enabled");
        else if (prose.Contains("enabled value", StringComparison.Ordinal)) entry["enabled"] = "yes";
        else if (prose.Contains("uppercase", StringComparison.Ordinal) || prose.Contains("outside 1-64", StringComparison.Ordinal))
        { skills.Remove(first.Key); skills["Bad-Key"] = entry; }
        else if (prose.Contains("protected -resource-", StringComparison.Ordinal))
        { skills.Remove(first.Key); skills["bad-resource-name"] = entry; }
        else if (prose.Contains("collide", StringComparison.Ordinal)) skills[first.Key.ToUpperInvariant()] = entry.DeepClone();
        else if (prose.Contains("origin", StringComparison.Ordinal)) entry["origin"] = "other";
        else if (prose.Contains("emitAgent or codexMetadata", StringComparison.Ordinal)
                 || prose.Contains("wrong JSON type", StringComparison.Ordinal)) entry["emitAgent"] = "true";
        else if (prose.Contains("resources", StringComparison.Ordinal)) entry["resources"] = new JsonArray("Bad", "Bad");
        else if (prose.Contains("unknown property", StringComparison.Ordinal)) entry["permission"] = "all";
        else if (prose.Contains("no source", StringComparison.Ordinal)) skills["missing-custom"] = new JsonObject { ["enabled"] = false };
        else throw new Xunit.Sdk.XunitException("Malformed-switch example was not recognized.");
        File.WriteAllText(configPath, node.ToJsonString(new() { WriteIndented = true }));
    }

    private void SelectOnly(string provider)
    {
        var config = Load();
        config.Integrations.Clear();
        config.Integrations[provider] = true;
        Save(config);
    }

    private DydoConfig Load() => new ConfigService().LoadConfigStrict(_root)!;
    private void Save(DydoConfig config) => new ConfigService().SaveConfig(config, Path.Combine(_root, "dydo.json"));
    private string Sources() => Path.Combine(_root, "dydo", "_system", "templates");

    private Dictionary<string, string> Manifest() => Directory.GetFiles(_root, "*", SearchOption.AllDirectories)
        .ToDictionary(path => Path.GetRelativePath(_root, path).Replace('\\', '/'),
            path => Hash(File.ReadAllBytes(path)), StringComparer.Ordinal);

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static string Normalize(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string QuotedValueAfter(string prose, string marker)
    {
        var start = prose.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var quote = prose.IndexOf('"', start);
        var end = prose.IndexOf('"', quote + 1);
        return prose[(quote + 1)..end];
    }

    private async Task<CliResult> RunAsync(params string[] arguments)
    {
        var runtime = Path.Combine(AppContext.BaseDirectory, "DynaDocs.Tests");
        string[] invocation = ["exec", "--runtimeconfig", runtime + ".runtimeconfig.json",
            "--depsfile", runtime + ".deps.json", typeof(CheckCommand).Assembly.Location, .. arguments];
        var start = CliScenario.CreateStartInfo(_root, invocation,
            Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
                .ToDictionary(entry => (string)entry.Key, entry => (string?)entry.Value));
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(stdout, stderr, process.WaitForExitAsync()).WaitAsync(TimeSpan.FromSeconds(60));
        return new CliResult(process.ExitCode, await stdout, await stderr);
    }
}
