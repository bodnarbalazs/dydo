namespace DynaDocs.Tests.Steps;

using System.Text.RegularExpressions;
using Reqnroll;

[Binding]
public class WorkflowRetirementSteps(CliScenario scenario)
{
    private static readonly Dictionary<string, string> CustomFiles = new()
    {
        ["custom.js"] = "EFBBBF2F2F20636166C3A90D0A",
        ["inquisition.js.bak"] = "2F2F206261636B75700A",
        ["run-sprint.js.bak"] = "00FF0D0A",
        ["nested/inquisition.js"] = "2F2F206E65737465640D0A"
    };

    private string WorkflowDirectory => Path.Combine(scenario.DirectoryPath, ".claude", "workflows");

    [Then("synchronization reports only the native artifacts for {string}")]
    public void NativeSummary(string integration) => AssertNativeSummary(scenario.Result, integration);

    internal static void AssertNativeSummary(CliResult result, string integration)
    {
        result.AssertSuccess();
        Assert.Empty(result.Stderr);
        var (claude, codex) = Hosts(integration);
        List<string> patterns = [];
        if (claude)
        {
            patterns.Add(@"Synced [1-9][0-9]* agent\(s\) to \.claude/ \(agents \+ skills\): \S[^\r\n]*");
            patterns.Add(@"Synced [1-9][0-9]* skill\(s\) to \.claude/ \(skills only\): \S[^\r\n]*");
        }
        if (codex)
            patterns.Add(@"Synced Codex artifacts to \.agents/skills and \.codex/agents\.");
        if (!claude || !codex)
            patterns.Add($@"Skipped {(claude ? "Codex" : "Claude")} artifacts (?:—|-) not recorded in dydo\.json integrations \(add it with 'dydo init <integration> --join'\)\.");
        Assert.True(Regex.IsMatch(result.Stdout.Replace("\r\n", "\n", StringComparison.Ordinal),
            @"\A" + string.Join("\n", patterns) + @"\n?\z"), result.Stdout);
    }

    [Then("the selected native agents and skills for {string} are nonempty")]
    public void NativeArtifacts(string integration) => AssertNativeArtifacts(scenario.DirectoryPath, integration);

    internal static void AssertNativeArtifacts(string projectRoot, string integration)
    {
        var (claude, codex) = Hosts(integration);
        AssertArtifacts(projectRoot, claude,
            [".claude/agents/implementer.md", ".claude/skills/implementer/SKILL.md", ".claude/skills/co-thinker/SKILL.md"]);
        AssertArtifacts(projectRoot, codex,
            [".codex/agents/implementer.toml", ".agents/skills/implementer/SKILL.md", ".agents/skills/co-thinker/SKILL.md"]);
    }

    private static void AssertArtifacts(string projectRoot, bool expected, string[] paths)
    {
        foreach (var path in paths)
        {
            var fullPath = Path.Combine(projectRoot, path);
            Assert.Equal(expected, File.Exists(fullPath));
            if (expected)
                Assert.NotEmpty(File.ReadAllBytes(fullPath));
        }
    }

    private static (bool Claude, bool Codex) Hosts(string integration) => integration switch
    {
        "none" or "all" => (true, true),
        "claude" => (true, false),
        "codex" => (false, true),
        _ => throw new ArgumentException("Unknown fixture integration.", nameof(integration))
    };

    [Given("both retired workflow files and the project-owned workflow byte fixtures")]
    public void MigrationFiles()
    {
        RetiredFiles();
        foreach (var (relativePath, hex) in CustomFiles)
        {
            var path = Path.Combine(WorkflowDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, Convert.FromHexString(hex));
        }
    }

    private void RetiredFiles()
    {
        Directory.CreateDirectory(WorkflowDirectory);
        File.WriteAllBytes(Path.Combine(WorkflowDirectory, "run-sprint.js"), "retired\n"u8.ToArray());
        File.WriteAllBytes(Path.Combine(WorkflowDirectory, "inquisition.js"), "retired\n"u8.ToArray());
    }

    [Then("both retired workflow files are absent")]
    public void RetiredFilesAbsent()
    {
        Assert.False(File.Exists(Path.Combine(WorkflowDirectory, "run-sprint.js")));
        Assert.False(File.Exists(Path.Combine(WorkflowDirectory, "inquisition.js")));
    }

    [Then("the project-owned workflow files retain their exact paths and bytes")]
    public void CustomFilesPreserved()
    {
        Assert.True(Directory.Exists(WorkflowDirectory));
        var paths = Directory.EnumerateFiles(WorkflowDirectory, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(WorkflowDirectory, path).Replace('\\', '/'));
        Assert.Equal(CustomFiles.Keys.Order(StringComparer.Ordinal), paths.Order(StringComparer.Ordinal));
        foreach (var (relativePath, hex) in CustomFiles)
            Assert.Equal(Convert.FromHexString(hex), File.ReadAllBytes(Path.Combine(WorkflowDirectory, relativePath)));
    }

    [Given("the retired workflow directory starts {string}")]
    public void DirectoryState(string state)
    {
        RetiredDirectoryAbsent();
        switch (state)
        {
            case "absent": break;
            case "empty": Directory.CreateDirectory(WorkflowDirectory); break;
            case "retired-only": RetiredFiles(); break;
            default: throw new ArgumentException("Unknown fixture directory state.", nameof(state));
        }
    }

    [Then("the retired workflow directory is absent")]
    public void RetiredDirectoryAbsent() => Assert.False(Directory.Exists(WorkflowDirectory));
}
