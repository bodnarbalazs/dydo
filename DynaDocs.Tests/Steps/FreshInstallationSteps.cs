namespace DynaDocs.Tests.Steps;

using System.Text;
using System.Text.Json;
using Reqnroll;

[Binding]
public class FreshInstallationSteps(CliScenario scenario)
{
    private Dictionary<string, byte[]> _artifacts = [];

    [Given("an empty project directory")]
    public void EmptyProject() => Assert.Empty(Directory.EnumerateFileSystemEntries(scenario.DirectoryPath));

    [When("I initialize dydo with {string}")]
    public Task Initialize(string integration) => scenario.RunAsync("init", integration);

    [Then("the command succeeds")]
    public void CommandSucceeds() => scenario.Result.AssertSuccess();

    [Then(@"^the recorded (Claude|Codex) integration is (true|false)$")]
    public void RecordedIntegration(string host, bool expected)
    {
        using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(scenario.DirectoryPath, "dydo.json")));
        var integrations = config.RootElement.GetProperty("integrations");
        var enabled = integrations.TryGetProperty(host.ToLowerInvariant(), out var value) && value.GetBoolean();
        Assert.Equal(expected, enabled);
    }

    [When("I synchronize the native artifacts")]
    public async Task FirstSync()
    {
        await scenario.RunAsync("sync");
        _artifacts = Snapshot();
    }

    [When("I synchronize the native artifacts again")]
    public Task SecondSync() => scenario.RunAsync("sync");

    [Then("the native artifacts have identical paths and bytes")]
    public void IdenticalArtifacts()
    {
        Assert.NotEmpty(_artifacts);
        var current = Snapshot();
        Assert.Equal(_artifacts.Keys.Order(), current.Keys.Order());
        foreach (var (path, bytes) in _artifacts)
            Assert.Equal(bytes, current[path]);
    }

    [Given("a user-owned file named {string} containing {string}")]
    public void UserFile(string name, string content) => File.WriteAllBytes(
        Path.Combine(scenario.DirectoryPath, name), Encoding.UTF8.GetBytes(content));

    [When("I update the framework templates")]
    public Task Update() => scenario.RunAsync("template", "update");

    [Then("the user-owned file named {string} still contains {string}")]
    public void PreservedUserFile(string name, string content) => Assert.Equal(
        Encoding.UTF8.GetBytes(content), File.ReadAllBytes(Path.Combine(scenario.DirectoryPath, name)));

    private Dictionary<string, byte[]> Snapshot()
    {
        string[] folders = [".claude/agents", ".claude/skills", ".claude/workflows", ".agents/skills", ".codex/agents"];
        var files = folders.Select(folder => Path.Combine(scenario.DirectoryPath, folder))
            .Where(Directory.Exists).SelectMany(folder => Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories));
        string[] config = [".codex/hooks.json", ".codex/config.toml"];
        return files.Concat(config.Select(path => Path.Combine(scenario.DirectoryPath, path)).Where(File.Exists))
            .ToDictionary(path => Path.GetRelativePath(scenario.DirectoryPath, path), File.ReadAllBytes);
    }
}
