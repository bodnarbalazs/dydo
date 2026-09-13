namespace DynaDocs.Tests.Steps;

using System.Text.Json.Nodes;
using DynaDocs.Services;
using Reqnroll;

[Binding]
public class ExperimentalBetaSteps(CliScenario scenario)
{
    private readonly Dictionary<string, byte[]> _customFiles = new();
    private string? _customHooks;

    [Given("the project looks like a dydo source checkout with a conflicting skill template")]
    public void SourceLookingDecoy()
    {
        File.WriteAllText(Path.Combine(scenario.DirectoryPath, "DynaDocs.csproj"), "<Project />");
        var templates = Path.Combine(scenario.DirectoryPath, "Templates");
        Directory.CreateDirectory(templates);
        File.WriteAllText(Path.Combine(templates, "skill-conflicting.template.md"), "---\nname: conflicting\n---\n");
        File.WriteAllText(Path.Combine(templates, "skill-reviewer.template.md"), "---\nname: reviewer\n---\n# Decoy\n");
    }

    [Given("custom native host files and hook entries have been recorded")]
    public void RecordCustomHostFiles()
    {
        Record(".claude/agents/custom.md", "custom claude agent\n"u8.ToArray());
        Record(".agents/skills/custom/SKILL.md", "custom codex skill\n"u8.ToArray());
        Record(".codex/config.toml", "custom = true\n"u8.ToArray());

        var hooksPath = Path.Combine(scenario.DirectoryPath, ".codex", "hooks.json");
        var hooks = JsonNode.Parse(File.ReadAllText(hooksPath))!.AsObject();
        var custom = new JsonArray
        {
            new JsonObject
            {
                ["matcher"] = "CustomTool",
                ["hooks"] = new JsonArray(new JsonObject { ["type"] = "command", ["command"] = "echo custom" })
            }
        };
        hooks["hooks"]!.AsObject()["PostToolUse"] = custom;
        File.WriteAllText(hooksPath, hooks.ToJsonString());
        _customHooks = custom.ToJsonString();
    }

    [Then("the conflicting source template is not discovered or emitted")]
    public void DecoyNotDiscovered()
    {
        Assert.DoesNotContain("skill-conflicting.template.md", TemplateGenerator.GetBuiltInSkillTemplateNames());
        Assert.False(Directory.Exists(Path.Combine(scenario.DirectoryPath, ".agents", "skills", "conflicting")));
        Assert.False(File.Exists(Path.Combine(scenario.DirectoryPath, ".codex", "agents", "conflicting.toml")));
    }

    [Then("every emitted native skill matches the beta's embedded template inventory")]
    public void NativeInventoryMatchesEmbeddedTemplates()
    {
        var skills = SkillTemplateService.DiscoverSkills();
        foreach (var skill in skills)
        {
            Assert.True(File.Exists(Path.Combine(scenario.DirectoryPath, ".claude", "skills", skill.Name, "SKILL.md")), skill.Name);
            Assert.True(File.Exists(Path.Combine(scenario.DirectoryPath, ".agents", "skills", skill.Name, "SKILL.md")), skill.Name);
            Assert.Equal(skill.EmitAgent, File.Exists(Path.Combine(scenario.DirectoryPath, ".claude", "agents", $"{skill.Name}.md")));
            Assert.Equal(skill.EmitAgent, File.Exists(Path.Combine(scenario.DirectoryPath, ".codex", "agents", $"{skill.Name}.toml")));
        }
    }

    [Then("custom native host files outside managed hooks retain their paths and bytes")]
    public void CustomFilesPreserved()
    {
        Assert.NotEmpty(_customFiles);
        foreach (var (relativePath, expected) in _customFiles)
        {
            var path = Path.Combine(scenario.DirectoryPath, relativePath);
            Assert.True(File.Exists(path), relativePath);
            Assert.Equal(expected, File.ReadAllBytes(path));
        }
    }

    [Then("custom Codex hook entries remain semantically intact")]
    public void CustomHooksPreserved()
    {
        Assert.NotNull(_customHooks);
        var hooks = JsonNode.Parse(File.ReadAllText(Path.Combine(scenario.DirectoryPath, ".codex", "hooks.json")))!
            ["hooks"]!.AsObject()["PostToolUse"]!;
        Assert.Equal(_customHooks, hooks.ToJsonString());
    }

    [Then(@"^the Codex agent ""(.*)"" has agents enabled (true|false)$")]
    public void AgentsEnabled(string role, string expected) =>
        Assert.Equal(expected, AgentTable(role)["enabled"]);

    [Then(@"^the Codex agent ""(.*)"" has V1 maximum delegation depth (3|omitted)$")]
    public void MaxDepth(string role, string expected)
    {
        var values = AgentTable(role);
        Assert.Equal(expected, values.ContainsKey("max_depth") ? values["max_depth"] : "omitted");
    }

    [Then(@"^the Codex agent ""(.*)"" has top-level web search mode (live|omitted)$")]
    public void WebSearch(string role, string expected)
    {
        var contents = File.ReadAllText(Path.Combine(scenario.DirectoryPath, ".codex", "agents", $"{role}.toml"));
        var table = contents.IndexOf("[agents]", StringComparison.Ordinal);
        Assert.True(table > 0, contents);
        var topLevel = contents[..table];
        var value = topLevel.Split('\n')
            .Select(line => line.Trim())
            .SingleOrDefault(line => line.StartsWith("web_search = ", StringComparison.Ordinal));
        Assert.Equal(expected == "omitted" ? null : $"web_search = \"{expected}\"", value);
    }

    private void Record(string relativePath, byte[] bytes)
    {
        var path = Path.Combine(scenario.DirectoryPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        _customFiles[relativePath] = bytes;
    }

    private Dictionary<string, string> AgentTable(string role)
    {
        var contents = File.ReadAllText(Path.Combine(scenario.DirectoryPath, ".codex", "agents", $"{role}.toml"));
        var table = contents.IndexOf("[agents]", StringComparison.Ordinal);
        Assert.True(table > 0, contents);
        return contents[(table + "[agents]".Length)..].Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split(" = ", 2, StringSplitOptions.None))
            .ToDictionary(parts => parts[0], parts => parts[1]);
    }
}
