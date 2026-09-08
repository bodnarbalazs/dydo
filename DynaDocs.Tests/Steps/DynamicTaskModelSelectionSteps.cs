namespace DynaDocs.Tests.Steps;

using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DynaDocs.Commands;
using DynaDocs.Services;
using DynaDocs.Tests.Commands;
using DynaDocs.Tests.Integration;
using DynaDocs.Tests.Services;
using Reqnroll;

[Binding]
[Scope(Tag = "DYD-134")]
public sealed class DynamicTaskModelSelectionSteps(ScenarioContext context)
{
    [Given(@"^.*$")]
    public void GivenContractStep() { }

    [When(@"^.*$")]
    public void WhenContractStep() { }

    [Then(@"^.*$")]
    public void ThenContractStep() { }

    [AfterScenario]
    public async Task VerifyContract()
    {
        var title = context.ScenarioInfo.Title;
        if (title.StartsWith("Fresh initialization", StringComparison.Ordinal))
            VerifyFreshInitialization(Argument("integration"));
        else if (title.StartsWith("Retire legacy", StringComparison.Ordinal))
            VerifyLegacySyncMigration();
        else if (title.StartsWith("A framework update", StringComparison.Ordinal))
            VerifyLegacyUpdateMigration();
        else if (title.StartsWith("A failed existing-config rewrite", StringComparison.Ordinal))
            await VerifyLateFailure(Argument("operation"), Argument("late outcome"));
        else if (title.StartsWith("A failed final atomic", StringComparison.Ordinal))
            VerifyAtomicFailure(Argument("failure"));
        else if (title.StartsWith("A temporary-name collision", StringComparison.Ordinal))
            VerifyCollision();
        else if (title.StartsWith("Preserve disabled-provider", StringComparison.Ordinal))
            VerifyDisabledProvider();
        else if (title.StartsWith("Apply dynamic selection", StringComparison.Ordinal))
            VerifyCustomAgent();
        else if (title.StartsWith("Initialization, update", StringComparison.Ordinal))
            VerifyFixedPoint(Argument("sequence"));
        else if (title.StartsWith("Delegators choose capability", StringComparison.Ordinal))
            VerifyDelegatorDoctrine();
        else if (title.StartsWith("Claude agents preserve", StringComparison.Ordinal))
            VerifyClaudeDocumentation();
        else if (title.StartsWith("Codex agents leave", StringComparison.Ordinal))
            VerifyCodexDocumentation();
        else if (title.StartsWith("A fresh native role catalog", StringComparison.Ordinal))
            Assert.Contains(context.ScenarioInfo.Tags, tag => tag == "native" || tag == "paid");
        else
            throw new Xunit.Sdk.XunitException($"Unmapped DYD-134 scenario: {title}");
    }

    private string Argument(string name) => context.ScenarioInfo.Arguments[name]?.ToString()
        ?? throw new Xunit.Sdk.XunitException($"Missing example argument '{name}'.");

    private static void VerifyFreshInitialization(string integration)
    {
        using var project = new ProjectFixture();
        Assert.Equal(0, project.Run(InitCommand.Create(), integration));
        var (exitCode, stdout, stderr) = ConsoleCapture.All(() => SyncCommand.Execute(project.Root));
        Assert.True(exitCode == 0, $"Sync failed.\nstdout: {stdout}\nstderr: {stderr}");
        Assert.False(project.ConfigJson().ContainsKey("models"));
        project.AssertSelectableAgents();
    }

    private static void VerifyLegacySyncMigration()
    {
        using var project = ProjectFixture.Initialized("all");
        project.AddLegacyModels();
        var before = project.ConfigJson();
        var sentinelNudge = before["nudges"]![0]!.DeepClone();
        var sentinelIntegrations = before["integrations"]!.DeepClone();

        var (exitCode, stdout, stderr) = ConsoleCapture.All(() => SyncCommand.Execute(project.Root));
        Assert.True(exitCode == 0, $"Sync failed.\nstdout: {stdout}\nstderr: {stderr}");

        var after = project.ConfigJson();
        Assert.False(after.ContainsKey("models"));
        Assert.True(JsonNode.DeepEquals(sentinelNudge, after["nudges"]![0]));
        Assert.True(JsonNode.DeepEquals(sentinelIntegrations, after["integrations"]));
        project.AssertSelectableAgents();
    }

    private static void VerifyLegacyUpdateMigration()
    {
        using var project = ProjectFixture.Initialized("all");
        project.AddLegacyModels();

        Assert.Equal(0, project.Run(TemplateCommand.Create(), "update"));

        Assert.False(project.ConfigJson().ContainsKey("models"));
    }

    private static async Task VerifyLateFailure(string operation, string outcome)
    {
        if (operation == "sync")
        {
            using var tests = new SyncCommandTests();
            tests.Execute_PostWorkFailure_PreservesOriginalConfigBytes();
        }
        else if (operation == "template update" && outcome.Contains("warning", StringComparison.Ordinal))
        {
            using var tests = new TemplateCommandTests();
            await tests.Update_WarningsReturnNonzeroAndPreserveOriginalConfigBytes();
        }
        else if (operation == "template update")
        {
            using var tests = new TemplateCommandTests();
            await tests.Update_PostWorkFailure_PreservesOriginalConfigBytes();
        }
        else if (operation == "fix" && outcome.Contains("validation", StringComparison.Ordinal))
        {
            using var tests = new FixCommandIntegrationTests();
            await tests.Fix_ValidationErrorsPreserveOriginalConfigBytes();
        }
        else if (operation == "fix")
        {
            using var tests = new FixCommandIntegrationTests();
            await tests.Fix_PostWorkFailure_PreservesOriginalConfigBytes();
        }
        else if (operation == "init codex --join")
        {
            using var tests = new InitCommandTests();
            await tests.Join_PostWorkFailure_PreservesOriginalConfigBytes();
        }
        else
            throw new Xunit.Sdk.XunitException($"Unmapped late failure: {operation} / {outcome}");
    }

    private static void VerifyAtomicFailure(string failure)
    {
        using var tests = new ConfigServiceTests();
        if (failure.StartsWith("after a strict prefix", StringComparison.Ordinal))
            tests.SaveConfig_PartialTemporaryWriteFailure_PreservesOriginalAndCleansTemporarySibling();
        else if (failure.StartsWith("during durable flush", StringComparison.Ordinal))
            tests.SaveConfig_FlushFailure_PreservesOriginalAndCleansTemporarySibling();
        else if (failure.StartsWith("during close", StringComparison.Ordinal))
            tests.SaveConfig_CloseFailure_PreservesOriginalAndCleansTemporarySibling();
        else if (failure.StartsWith("during replacement", StringComparison.Ordinal))
            tests.SaveConfig_ReplacementFailure_PreservesOriginalAndCleansTemporarySibling();
        else
            throw new Xunit.Sdk.XunitException($"Unmapped atomic failure: {failure}");
    }

    private static void VerifyCollision()
    {
        using var tests = new ConfigServiceTests();
        tests.SaveConfig_TemporaryNameCollision_PreservesBothFiles();
    }

    private static void VerifyDisabledProvider()
    {
        using var project = ProjectFixture.Initialized("all");
        Assert.Equal(0, SyncCommand.Execute(project.Root));
        project.Write(".claude/agents/project-owned.md", "CLAUDE SENTINEL");
        project.Write(".codex/agents/project-owned.toml", "CODEX SENTINEL");
        var config = new ConfigService().LoadConfigStrict(project.Root)!;
        config.Integrations["claude"] = false;
        new ConfigService().SaveConfig(config, project.Path("dydo.json"));
        var disabled = project.Snapshot(".claude");

        Assert.Equal(0, SyncCommand.Execute(project.Root));
        project.AssertSnapshot(".claude", disabled);
        project.AssertCodexSelectable();

        config = new ConfigService().LoadConfigStrict(project.Root)!;
        config.Integrations["claude"] = true;
        new ConfigService().SaveConfig(config, project.Path("dydo.json"));
        Assert.Equal(0, SyncCommand.Execute(project.Root));
        project.AssertSelectableAgents();
        Assert.Equal("CLAUDE SENTINEL", project.Read(".claude/agents/project-owned.md"));
        Assert.Equal("CODEX SENTINEL", project.Read(".codex/agents/project-owned.toml"));
    }

    private static void VerifyCustomAgent()
    {
        using var project = ProjectFixture.Initialized("all");
        project.Write("dydo/_system/templates/skill-release-notes.template.md", """
            ---
            name: release-notes
            description: Prepare release notes.
            emit: agent
            read-only: true
            delegates: true
            web: false
            invocation: automatic
            argument-hint: release context
            ---

            # Release Notes

            Read [policy](resources/policy.md).
            """);
        project.Write("dydo/_system/templates/resource-release-notes-resource-policy.template.md", "# Policy\n");

        var (exitCode, stdout, stderr) = ConsoleCapture.All(() => SyncCommand.Execute(project.Root));
        Assert.True(exitCode == 0, $"Sync failed.\nstdout: {stdout}\nstderr: {stderr}");

        project.AssertClaudeSelectable("release-notes");
        project.AssertCodexSelectable("release-notes");
        Assert.Contains("max_depth = 3", project.Read(".codex/agents/release-notes.toml"));
        Assert.Contains("default_prompt: \"release context\"", project.Read(".agents/skills/release-notes/agents/openai.yaml"));
        Assert.Equal("# Policy\n", project.Read(".agents/skills/release-notes/resources/policy.md"));
    }

    private static void VerifyFixedPoint(string sequence)
    {
        using var project = ProjectFixture.Initialized("all");
        project.AddLegacyModels();
        project.Write(".claude/agents/reviewer.md", "model: legacy-pin\n");
        project.Write(".codex/agents/reviewer.toml", "model = \"legacy-pin\"\n");
        project.Write(".codex/agents/project-owned.toml", "CUSTOM\n");

        project.RunSequence(sequence);
        var first = project.Snapshot("dydo.json", "dydo/_system/templates", ".claude", ".agents", ".codex");
        project.RunSequence(sequence);
        project.AssertSnapshot(first);
        Assert.False(project.ConfigJson().ContainsKey("models"));
        project.AssertSelectableAgents();
    }

    private static void VerifyDelegatorDoctrine()
    {
        foreach (var template in new[] { "skill-admiral.template.md", "skill-issue-captain.template.md" })
        {
            var text = File.ReadAllText(Path.Combine(ProjectFixture.RepositoryRoot, "Templates", template));
            foreach (var phrase in new[] { "difficulty", "uncertainty", "consequence of error", "required independence", "context size", "likely retries", "adequate", "escalate" })
                Assert.Contains(phrase, text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("review", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("gate", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("role-to-model", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("role-to-effort", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void VerifyClaudeDocumentation()
    {
        var text = CurrentDocumentation();
        foreach (var phrase in new[] { "per-invocation", "model: inherit", "environment", "parent model", "organization policy", "requested model", "configured model", "effective model", "session effort", "effective effort" })
            Assert.Contains(phrase, text, StringComparison.OrdinalIgnoreCase);
        Assert.Matches(new Regex(@"(?:does not|no)\s+(?:expose|support|accept|have|provide|declare|invent)?\s*(?:an?\s+)?per-Agent-call effort", RegexOptions.IgnoreCase), text);
    }

    private static void VerifyCodexDocumentation()
    {
        var text = CurrentDocumentation();
        foreach (var phrase in new[] { "explicit spawn", "agents default", "parent", "model_reasoning_effort", "requested", "configured", "effective" })
            Assert.Contains(phrase, text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("model and", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("effort", text, StringComparison.OrdinalIgnoreCase);
    }

    private static string CurrentDocumentation() => string.Join('\n', new[]
    {
        "dydo/guides/customizing-roles.md",
        "dydo/reference/configuration.md",
        "dydo/understand/control-flow.md"
    }.Select(relative => File.ReadAllText(Path.Combine(ProjectFixture.RepositoryRoot, relative))));

    private sealed class ProjectFixture : IDisposable
    {
        public static string RepositoryRoot { get; } = FindRepositoryRoot();
        private readonly string _originalDirectory = Environment.CurrentDirectory;
        public string Root { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dyd134-{Guid.NewGuid():N}");

        public ProjectFixture()
        {
            Directory.CreateDirectory(Root);
            Environment.CurrentDirectory = Root;
        }

        public static ProjectFixture Initialized(string integration)
        {
            var project = new ProjectFixture();
            Assert.Equal(0, project.Run(InitCommand.Create(), integration));
            return project;
        }

        public int Run(Command command, params string[] arguments) =>
            ConsoleCapture.All(() => command.Parse(arguments).Invoke()).exitCode;

        public void RunSequence(string sequence)
        {
            foreach (var operation in sequence.Split(';', StringSplitOptions.TrimEntries))
            {
                var exit = operation switch
                {
                    "init all --join" => Run(InitCommand.Create(), "all", "--join"),
                    "template update" => Run(TemplateCommand.Create(), "update"),
                    "sync" => SyncCommand.Execute(Root),
                    _ => throw new Xunit.Sdk.XunitException($"Unknown fixed-point operation '{operation}'.")
                };
                Assert.Equal(0, exit);
            }
        }

        public JsonObject ConfigJson() => JsonNode.Parse(Read("dydo.json"))!.AsObject();

        public void AddLegacyModels()
        {
            var config = ConfigJson();
            config["models"] = JsonNode.Parse("""{"agents":{"reviewer":"strong"},"tiers":{"anthropic":{"strong":"legacy"},"openai":{"strong":"legacy"}}}""");
            File.WriteAllText(Path("dydo.json"), config.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }

        public void AssertSelectableAgents()
        {
            var config = new ConfigService().LoadConfigStrict(Root)!;
            var (claude, codex) = SyncCommand.ResolveIntegrationTargets(config.Integrations);
            var agents = SkillTemplateService.DiscoverLocalCatalog(Root, config).Where(skill => skill.EmitAgent && config.Skills[skill.Name].Enabled == true).ToList();
            foreach (var skill in agents)
            {
                if (claude) AssertClaudeSelectable(skill.Name);
                if (codex) AssertCodexSelectable(skill.Name);
            }
            foreach (var skill in SkillTemplateService.DiscoverLocalCatalog(Root, config).Where(skill => !skill.EmitAgent))
            {
                Assert.False(File.Exists(Path($".claude/agents/{skill.Name}.md")));
                Assert.False(File.Exists(Path($".codex/agents/{skill.Name}.toml")));
            }
        }

        public void AssertClaudeSelectable(string name)
        {
            var fields = ParseFrontmatter(Read($".claude/agents/{name}.md"));
            Assert.Equal("inherit", fields["model"]);
            Assert.False(fields.ContainsKey("effort"));
            foreach (var field in new[] { "name", "description", "tools", "skills" })
                Assert.True(fields.ContainsKey(field), $"Claude agent {name} omitted {field}.");
        }

        public void AssertCodexSelectable() => AssertCodexSelectable("reviewer");

        public void AssertCodexSelectable(string name)
        {
            var fields = ParseTopLevelToml(Read($".codex/agents/{name}.toml"));
            Assert.False(fields.ContainsKey("model"));
            Assert.False(fields.ContainsKey("model_reasoning_effort"));
            foreach (var field in new[] { "name", "description", "sandbox_mode", "developer_instructions" })
                Assert.True(fields.ContainsKey(field), $"Codex agent {name} omitted {field}.");
        }

        public Dictionary<string, byte[]> Snapshot(params string[] relativeRoots)
        {
            var files = new List<string>();
            foreach (var relative in relativeRoots)
            {
                var full = Path(relative);
                if (File.Exists(full)) files.Add(full);
                else if (Directory.Exists(full)) files.AddRange(Directory.GetFiles(full, "*", SearchOption.AllDirectories));
            }
            return files.OrderBy(file => file, StringComparer.Ordinal)
                .ToDictionary(file => System.IO.Path.GetRelativePath(Root, file).Replace('\\', '/'), File.ReadAllBytes, StringComparer.Ordinal);
        }

        public void AssertSnapshot(Dictionary<string, byte[]> expected) => AssertSnapshot(null, expected);

        public void AssertSnapshot(string? root, Dictionary<string, byte[]> expected)
        {
            var actual = root == null ? Snapshot(expected.Keys.ToArray()) : Snapshot(root);
            Assert.Equal(expected.Keys, actual.Keys);
            foreach (var entry in expected)
                Assert.Equal(entry.Value, actual[entry.Key]);
        }

        public string Path(string relative) => System.IO.Path.Combine(Root, relative.Replace('/', System.IO.Path.DirectorySeparatorChar));
        public string Read(string relative) => File.ReadAllText(Path(relative));
        public void Write(string relative, string content)
        {
            var path = Path(relative);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        public void Dispose()
        {
            Environment.CurrentDirectory = _originalDirectory;
            try { Directory.Delete(Root, recursive: true); } catch { }
        }

        private static Dictionary<string, string> ParseFrontmatter(string content)
        {
            var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            Assert.Equal("---", lines[0]);
            var end = Array.IndexOf(lines, "---", 1);
            Assert.True(end > 0);
            return lines[1..end].Select(line => line.Split(':', 2))
                .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim(), StringComparer.Ordinal);
        }

        private static Dictionary<string, string> ParseTopLevelToml(string content)
        {
            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            var inMultiline = false;
            foreach (var line in content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
            {
                if (line.StartsWith("[", StringComparison.Ordinal)) break;
                if (inMultiline)
                {
                    if (line == "\"\"\"") inMultiline = false;
                    continue;
                }
                var equals = line.IndexOf('=');
                if (equals <= 0) continue;
                var key = line[..equals].Trim();
                fields[key] = line[(equals + 1)..].Trim();
                if (fields[key] == "\"\"\"") inMultiline = true;
            }
            return fields;
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(System.IO.Path.Combine(directory.FullName, "DynaDocs.sln")))
                directory = directory.Parent;
            return directory?.FullName ?? throw new DirectoryNotFoundException("DynaDocs repository root not found.");
        }
    }
}
