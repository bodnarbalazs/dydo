namespace DynaDocs.Tests.Steps;

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Reqnroll;

[Binding]
public class FreshInstallationSteps(CliScenario scenario)
{
    private Dictionary<string, byte[]> _artifacts = [];
    private Dictionary<string, byte[]> _foundationDocuments = [];

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

    [Then("the documentation has no validation errors and exactly these onboarding warnings:")]
    public void OnboardingWarnings(Table expected) => AssertOnboardingWarnings(scenario.Result, expected);

    internal static void AssertOnboardingWarnings(CliResult result, Table expected)
    {
        var output = $"Exit: {result.ExitCode}\n{result.Stdout}\n{result.Stderr}";
        Assert.True(result.ExitCode == 0 && result.Stderr == "", output);
        Assert.True(expected.Header.SequenceEqual(["document", "warning"]), output);
        var lines = result.Stdout.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        Assert.True(lines.Count(line => Regex.IsMatch(line,
            @"^Found 0 errors, 2 warnings in [1-9][0-9]* files\.$")) == 1, output);
        Assert.True(lines.Count(line => line == "Found warnings (no errors).") == 1, output);
        Assert.True(lines.Count(line => line == "WARNINGS:") == 1, output);
        var start = Array.IndexOf(lines, "WARNINGS:");
        var end = Array.FindIndex(lines, start + 1, line => line == "");
        Assert.True(end > start, output);
        Assert.True(lines.Take(start).Concat(lines.Skip(end + 1))
            .All(line => !line.StartsWith("  ", StringComparison.Ordinal)), output);
        var observed = ParseWarningBlock(lines[(start + 1)..end], output);
        var wanted = expected.Rows.Select(row => (Document: row["document"], Warning: row["warning"])).ToList();
        Assert.True(observed.Count == 2 && wanted.Count == 2, output);
        Assert.True(observed.OrderBy(item => item.Document, StringComparer.Ordinal)
            .ThenBy(item => item.Warning, StringComparer.Ordinal)
            .SequenceEqual(wanted.OrderBy(item => item.Document, StringComparer.Ordinal)
                .ThenBy(item => item.Warning, StringComparer.Ordinal)), output);
    }

    private static List<(string Document, string Warning)> ParseWarningBlock(string[] lines, string output)
    {
        List<(string Document, string Warning)> warnings = [];
        string? document = null;
        var groupHasWarning = false;
        foreach (var line in lines)
        {
            if (line.StartsWith("    - ", StringComparison.Ordinal))
            {
                Assert.True(document != null, output);
                warnings.Add((document!, line[6..]));
                groupHasWarning = true;
                continue;
            }
            Assert.True(Regex.IsMatch(line, @"^  \S.*$"), output);
            Assert.True(document == null || groupHasWarning, output);
            document = line[2..].Replace('\\', '/');
            groupHasWarning = false;
        }
        Assert.True(groupHasWarning, output);
        return warnings;
    }

    [When("I customize the foundation documents for a tiny task-list project")]
    public void CustomizeFoundationDocuments()
    {
        _foundationDocuments = new Dictionary<string, byte[]>
        {
            ["dydo/understand/about.md"] = Encoding.UTF8.GetBytes(About.ReplaceLineEndings("\n") + "\n"),
            ["dydo/understand/architecture.md"] = Encoding.UTF8.GetBytes(Architecture.ReplaceLineEndings("\n") + "\n")
        };
        foreach (var path in _foundationDocuments.Keys)
            Assert.True(File.Exists(Path.Combine(scenario.DirectoryPath, path)), $"Missing initialized file: {path}");
        foreach (var (path, bytes) in _foundationDocuments)
            File.WriteAllBytes(Path.Combine(scenario.DirectoryPath, path), bytes);
    }

    [Then("the customized foundation documents retain their paths and bytes")]
    public void PreservedFoundationDocuments()
    {
        Assert.Equal(2, _foundationDocuments.Count);
        foreach (var (path, bytes) in _foundationDocuments)
        {
            var fullPath = Path.Combine(scenario.DirectoryPath, path);
            Assert.True(File.Exists(fullPath), $"Missing customized file: {path}");
            Assert.Equal(bytes, File.ReadAllBytes(fullPath));
        }
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

    private const string About = """
        ---
        area: understand
        type: context
        ---

        # About This Project

        Pocket Tasks is a small command-line task list for one person's daily work.
        It lets the user add a task, list open tasks, and mark a task complete in a local JSON file.
        The project runs offline and has no accounts or external service dependencies.

        ---

        *See [architecture.md](./architecture.md) for technical structure.*
        """;

    private const string Architecture = """
        ---
        area: understand
        type: concept
        ---

        # Architecture Overview

        Pocket Tasks is a Python command-line application with a task service and a local JSON store.
        The command layer validates arguments; the task service owns task identifiers and completion state.

        ## Project Structure

        - `src/` contains command handling, task operations, and the JSON store.
        - `tests/` contains command and storage tests using temporary task files.
        - `dydo/` contains durable project documentation.

        ## Key Components

        - Commands translate add, list, and complete requests into task-service calls.
        - The task service applies task rules without reading terminal input or writing files.
        - The JSON store loads and saves task records at the path supplied by the command layer.

        ## Data Flow

        Arguments enter the command layer, which loads records through the JSON store and calls the task service.
        Read requests print the current tasks; successful changes save the updated records before printing confirmation.
        Malformed arguments leave the stored records unchanged.

        ## Knowledge and Work Boundary

        - **Linear** owns Initiatives, Projects, Issues, optional Milestones and Cycles, plus live status,
          priority, assignment, dependencies, updates, and review state.
        - **Git/dydo** owns architecture, Decisions, reviewed Project plans, guides, audits, assimilation
          evidence, and changelog. Linear owns FutureFeatures with the rest of the work graph.
        - Link between the two; do not mirror volatile Linear state into repository documents.

        ## Related

        - [dydo Glossary](../reference/dydo-glossary.md) — Work and knowledge vocabulary
        - [Coding Standards](../guides/coding-standards.md) — Code conventions
        """;
}
