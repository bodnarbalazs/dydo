namespace DynaDocs.Tests.Commands;

using DynaDocs.Commands;
using DynaDocs.Services;

[Collection("Integration")]
public class CompleteCommandTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _originalDir;

    public CompleteCommandTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-complete-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);

        _originalDir = Environment.CurrentDirectory;
        Environment.CurrentDirectory = _testDir;
    }

    public void Dispose()
    {
        Environment.CurrentDirectory = _originalDir;
        if (Directory.Exists(_testDir))
        {
            for (var i = 0; i < 3; i++)
            {
                try
                {
                    Directory.Delete(_testDir, true);
                    return;
                }
                catch (IOException) when (i < 2)
                {
                    Thread.Sleep(50 * (i + 1));
                }
            }
        }
    }

    [Fact]
    public void TopLevel_ReturnsAllSubcommands()
    {
        var completions = CompleteCommand.GetCompletions(1, ["dydo"]).ToList();

        Assert.Contains("task", completions);
        Assert.Contains("agent", completions);
        Assert.Contains("init", completions);
        Assert.Contains("dispatch", completions);
        Assert.Contains("review", completions);
        Assert.Contains("clean", completions);
        Assert.Contains("completions", completions);
    }

    [Fact]
    public void TaskSubcommand_ReturnsTaskSubcommands()
    {
        var completions = CompleteCommand.GetCompletions(2, ["dydo", "task"]).ToList();

        Assert.Contains("approve", completions);
        Assert.Contains("create", completions);
        Assert.Contains("list", completions);
        Assert.Contains("ready-for-review", completions);
        Assert.Contains("reject", completions);
    }

    [Fact]
    public void TaskApprove_ReturnsTaskNames()
    {
        SetupProjectWithTasks("fix-login", "add-search");

        var completions = CompleteCommand.GetCompletions(3, ["dydo", "task", "approve"]).ToList();

        Assert.Contains("fix-login", completions);
        Assert.Contains("add-search", completions);
    }

    [Fact]
    public void TaskApprove_SkipsUnderscorePrefixed()
    {
        SetupProjectWithTasks("fix-login");
        var tasksPath = new ConfigService().GetTasksPath();
        File.WriteAllText(Path.Combine(tasksPath, "_template.md"), "template");

        var completions = CompleteCommand.GetCompletions(3, ["dydo", "task", "approve"]).ToList();

        Assert.Contains("fix-login", completions);
        Assert.DoesNotContain("_template", completions);
    }

    [Fact]
    public void AgentClaim_ReturnsAgentNamesWithAuto()
    {
        SetupProject(["Adele", "Boris"]);

        var completions = CompleteCommand.GetCompletions(3, ["dydo", "agent", "claim"]).ToList();

        Assert.Contains("auto", completions);
        Assert.Contains("Adele", completions);
        Assert.Contains("Boris", completions);
    }

    [Fact]
    public void AgentRole_ReturnsRoles()
    {
        var completions = CompleteCommand.GetCompletions(3, ["dydo", "agent", "role"]).ToList();

        Assert.Contains("code-writer", completions);
        Assert.Contains("reviewer", completions);
        Assert.Contains("co-thinker", completions);
        Assert.Contains("docs-writer", completions);
        Assert.Contains("planner", completions);
        Assert.Contains("tester", completions);
    }

    [Fact]
    public void AgentStatus_ReturnsAgentNames()
    {
        SetupProject(["Adele", "Boris"]);

        var completions = CompleteCommand.GetCompletions(3, ["dydo", "agent", "status"]).ToList();

        Assert.Contains("Adele", completions);
        Assert.Contains("Boris", completions);
    }

    [Theory]
    [InlineData("--role", "code-writer")]
    [InlineData("--role", "reviewer")]
    [InlineData("--area", "frontend")]
    [InlineData("--area", "backend")]
    [InlineData("--status", "pass")]
    [InlineData("--status", "fail")]
    [InlineData("--action", "edit")]
    [InlineData("--action", "write")]
    public void OptionValue_ReturnsCorrectCompletions(string option, string expectedValue)
    {
        var completions = CompleteCommand.GetCompletions(3, ["dydo", "dispatch", option]).ToList();

        Assert.Contains(expectedValue, completions);
    }

    [Fact]
    public void TaskOption_ReturnsTaskNames()
    {
        SetupProjectWithTasks("my-task");

        var completions = CompleteCommand.GetCompletions(3, ["dydo", "dispatch", "--task"]).ToList();

        Assert.Contains("my-task", completions);
    }

    [Fact]
    public void ToOption_ReturnsAgentNames()
    {
        SetupProject(["Adele"]);

        var completions = CompleteCommand.GetCompletions(3, ["dydo", "dispatch", "--to"]).ToList();

        Assert.Contains("Adele", completions);
    }

    [Fact]
    public void ReviewComplete_ReturnsTaskNames()
    {
        SetupProjectWithTasks("fix-bug");

        var completions = CompleteCommand.GetCompletions(3, ["dydo", "review", "complete"]).ToList();

        Assert.Contains("fix-bug", completions);
    }

    [Fact]
    public void Init_ReturnsIntegrations()
    {
        var completions = CompleteCommand.GetCompletions(2, ["dydo", "init"]).ToList();

        Assert.Contains("claude", completions);
        Assert.Contains("none", completions);
    }

    [Fact]
    public void Completions_ReturnsShells()
    {
        var completions = CompleteCommand.GetCompletions(2, ["dydo", "completions"]).ToList();

        Assert.Contains("bash", completions);
        Assert.Contains("zsh", completions);
        Assert.Contains("powershell", completions);
    }

    [Fact]
    public void Clean_ReturnsAgentNames()
    {
        SetupProject(["Adele"]);

        var completions = CompleteCommand.GetCompletions(2, ["dydo", "clean"]).ToList();

        Assert.Contains("Adele", completions);
    }

    [Fact]
    public void NoConfig_DynamicValues_ReturnsEmpty()
    {
        var completions = CompleteCommand.GetCompletions(3, ["dydo", "agent", "claim"]).ToList();

        // "auto" is static, always present
        Assert.Contains("auto", completions);
        // No config → no dynamic agent names (just "auto")
        Assert.Single(completions);
    }

    [Fact]
    public void CommandExitsZero_EvenOnInvalidInput()
    {
        var command = CompleteCommand.Create();
        var result = command.Parse("999 dydo nonexistent garbage").Invoke();
        Assert.Equal(0, result);
    }

    private void SetupProject(List<string> agents)
    {
        var config = new
        {
            version = 1,
            structure = new { root = "dydo", tasks = "project/tasks" },
            agents = new
            {
                pool = agents,
                assignments = new Dictionary<string, List<string>> { ["testuser"] = agents }
            }
        };

        File.WriteAllText(
            Path.Combine(_testDir, "dydo.json"),
            System.Text.Json.JsonSerializer.Serialize(config));
    }

    private void SetupProjectWithTasks(params string[] taskNames)
    {
        SetupProject(["Adele"]);

        var tasksPath = Path.Combine(_testDir, "dydo", "project", "tasks");
        Directory.CreateDirectory(tasksPath);

        foreach (var name in taskNames)
        {
            File.WriteAllText(Path.Combine(tasksPath, $"{name}.md"), $"---\nname: {name}\n---\n");
        }
    }
}
