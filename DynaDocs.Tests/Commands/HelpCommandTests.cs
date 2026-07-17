namespace DynaDocs.Tests.Commands;

using DynaDocs.Commands;
using Xunit;

/// <summary>
/// Tests for the help command to ensure it documents all available commands.
/// These tests verify that the help output stays in sync with the actual commands.
/// </summary>
[Collection("ConsoleOutput")]
public class HelpCommandTests
{
    private static string CaptureHelpOutput()
    {
        return ConsoleCapture.Stdout(HelpCommand.PrintHelp);
    }

    [Fact]
    public void Help_ListsAllTopLevelCommands()
    {
        var output = CaptureHelpOutput();

        Assert.Contains("check", output);
        Assert.Contains("fix", output);
        Assert.Contains("init", output);
        Assert.Contains("index", output);
        Assert.Contains("graph", output);
        Assert.Contains("guard", output);
        Assert.Contains("task", output);
        Assert.Contains("review", output);
        Assert.Contains("completions", output);
        Assert.Contains("template", output);
        Assert.Contains("validate", output);
        Assert.Contains("issue", output);
        Assert.Contains("version", output);
        Assert.Contains("help", output);
    }

    [Fact]
    public void Help_ListsAllTaskSubcommands()
    {
        var output = CaptureHelpOutput();

        Assert.Contains("task create", output);
        Assert.Contains("task ready-for-review", output);
        Assert.Contains("task done", output);
        Assert.Contains("task list", output);
    }

    [Fact]
    public void Help_ContainsReferenceToDocumentation()
    {
        var output = CaptureHelpOutput();

        Assert.Contains("dydo-commands.md", output);
    }

    [Fact]
    public void Help_DocumentsExitCodes()
    {
        var output = CaptureHelpOutput();

        Assert.Contains("Exit codes:", output);
        Assert.Contains("0 - Success", output);
        Assert.Contains("1 - Validation errors", output);
        Assert.Contains("2 - Tool error", output);
    }

    [Fact]
    public void Help_HasCategorizedSections()
    {
        var output = CaptureHelpOutput();

        Assert.Contains("Setup Commands:", output);
        Assert.Contains("Documentation Commands:", output);
        Assert.Contains("Workspace Commands:", output);
        Assert.Contains("Role Commands:", output);
        Assert.Contains("Validation Commands:", output);
        Assert.Contains("Template Commands:", output);
        Assert.Contains("Task Commands:", output);
        Assert.Contains("Issue Commands:", output);
        Assert.Contains("Utility:", output);
    }
}
