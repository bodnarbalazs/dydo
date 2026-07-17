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
        Assert.Contains("init", completions);
        Assert.Contains("review", completions);
        Assert.DoesNotContain("clean", completions);
        Assert.Contains("completions", completions);
    }

    [Fact]
    public void TaskSubcommand_ReturnsTaskSubcommands()
    {
        var completions = CompleteCommand.GetCompletions(2, ["dydo", "task"]).ToList();

        Assert.Contains("create", completions);
        Assert.Contains("done", completions);
        Assert.Contains("list", completions);
        Assert.Contains("ready-for-review", completions);
    }

    [Fact]
    public void TaskDone_ReturnsTaskNames()
    {
        SetupProjectWithTasks("fix-login", "add-search");

        var completions = CompleteCommand.GetCompletions(3, ["dydo", "task", "done"]).ToList();

        Assert.Contains("fix-login", completions);
        Assert.Contains("add-search", completions);
    }

    [Fact]
    public void TaskDone_SkipsUnderscorePrefixed()
    {
        SetupProjectWithTasks("fix-login");
        var tasksPath = new ConfigService().GetTasksPath();
        File.WriteAllText(Path.Combine(tasksPath, "_template.md"), "template");

        var completions = CompleteCommand.GetCompletions(3, ["dydo", "task", "done"]).ToList();

        Assert.Contains("fix-login", completions);
        Assert.DoesNotContain("_template", completions);
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
    public void Graph_ReturnsSubcommands()
    {
        var completions = CompleteCommand.GetCompletions(2, ["dydo", "graph"]).ToList();

        Assert.Contains("stats", completions);
    }

    [Fact]
    public void TaskUnknownSubcommand_ReturnsEmpty()
    {
        var completions = CompleteCommand.GetCompletions(3, ["dydo", "task", "list"]).ToList();

        Assert.Empty(completions);
    }

    [Fact]
    public void TaskPosition4_ReturnsEmpty()
    {
        var completions = CompleteCommand.GetCompletions(4, ["dydo", "task", "approve", "my-task"]).ToList();

        Assert.Empty(completions);
    }

    [Fact]
    public void ReviewUnknownSubcommand_ReturnsEmpty()
    {
        var completions = CompleteCommand.GetCompletions(3, ["dydo", "review", "unknown"]).ToList();

        Assert.Empty(completions);
    }

    [Fact]
    public void ReviewPosition4_ReturnsEmpty()
    {
        var completions = CompleteCommand.GetCompletions(4, ["dydo", "review", "complete", "task"]).ToList();

        Assert.Empty(completions);
    }

    [Fact]
    public void UnknownTopCommand_ReturnsEmpty()
    {
        var completions = CompleteCommand.GetCompletions(2, ["dydo", "nonexistent"]).ToList();

        Assert.Empty(completions);
    }

    [Fact]
    public void CommandExitsZero_EvenOnInvalidInput()
    {
        var command = CompleteCommand.Create();
        var result = command.Parse("999 dydo nonexistent garbage").Invoke();
        Assert.Equal(0, result);
    }

    [Fact]
    public void Command_WritesCompletionsToStdout()
    {
        var (exitCode, output, _) = ConsoleCapture.All(() =>
        {
            var command = CompleteCommand.Create();
            return command.Parse("1 dydo").Invoke();
        });
        Assert.Equal(0, exitCode);
        Assert.Contains("task", output);
    }

    private void SetupProject()
    {
        var config = new
        {
            version = 1,
            structure = new { root = "dydo", tasks = "project/tasks" }
        };

        File.WriteAllText(
            Path.Combine(_testDir, "dydo.json"),
            System.Text.Json.JsonSerializer.Serialize(config));
    }

    private void SetupProjectWithTasks(params string[] taskNames)
    {
        SetupProject();

        var tasksPath = Path.Combine(_testDir, "dydo", "project", "tasks");
        Directory.CreateDirectory(tasksPath);

        foreach (var name in taskNames)
        {
            File.WriteAllText(Path.Combine(tasksPath, $"{name}.md"), $"---\nname: {name}\n---\n");
        }
    }
}
