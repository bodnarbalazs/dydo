namespace DynaDocs.Tests.Commands;

using DynaDocs.Commands;

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

        Assert.Contains("init", completions);
        Assert.DoesNotContain("task", completions);
        Assert.DoesNotContain("issue", completions);
        Assert.DoesNotContain("review", completions);
        Assert.DoesNotContain("clean", completions);
        Assert.Contains("completions", completions);
    }

    [Theory]
    [InlineData("--area", "frontend")]
    [InlineData("--area", "backend")]
    [InlineData("--action", "edit")]
    [InlineData("--action", "write")]
    public void OptionValue_ReturnsCorrectCompletions(string option, string expectedValue)
    {
        var completions = CompleteCommand.GetCompletions(3, ["dydo", "dispatch", option]).ToList();

        Assert.Contains(expectedValue, completions);
    }

    [Fact]
    public void Init_ReturnsIntegrations()
    {
        var completions = CompleteCommand.GetCompletions(2, ["dydo", "init"]).ToList();

        Assert.Contains("claude", completions);
        Assert.Contains("codex", completions);
        Assert.Contains("all", completions);
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
        Assert.Contains("check", output);
        Assert.DoesNotContain("task", output);
        Assert.DoesNotContain("issue", output);
        Assert.DoesNotContain("review", output);
    }
}
