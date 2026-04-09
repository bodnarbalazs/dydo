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
        Assert.Contains("agent", output);
        Assert.Contains("guard", output);
        Assert.Contains("dispatch", output);
        Assert.Contains("inbox", output);
        Assert.Contains("task", output);
        Assert.Contains("review", output);
        Assert.Contains("workspace", output);
        Assert.Contains("whoami", output);
        Assert.Contains("audit", output);
        Assert.Contains("completions", output);
        Assert.Contains("template", output);
        Assert.Contains("roles", output);
        Assert.Contains("validate", output);
        Assert.Contains("issue", output);
        Assert.Contains("inquisition", output);
        Assert.Contains("queue", output);
        Assert.Contains("worktree", output);
        Assert.Contains("version", output);
        Assert.Contains("help", output);
    }

    [Fact]
    public void Help_ListsAllAgentSubcommands()
    {
        var output = CaptureHelpOutput();

        Assert.Contains("agent claim", output);
        Assert.Contains("agent release", output);
        Assert.Contains("agent status", output);
        Assert.Contains("agent list", output);
        Assert.Contains("agent tree", output);
        Assert.Contains("agent role", output);
        Assert.Contains("agent new", output);
        Assert.Contains("agent rename", output);
        Assert.Contains("agent remove", output);
        Assert.Contains("agent reassign", output);
        Assert.Contains("agent clean", output);
    }

    [Fact]
    public void Help_ListsAllTaskSubcommands()
    {
        var output = CaptureHelpOutput();

        Assert.Contains("task create", output);
        Assert.Contains("task ready-for-review", output);
        Assert.Contains("task approve", output);
        Assert.Contains("task reject", output);
        Assert.Contains("task list", output);
        Assert.Contains("task compact", output);
    }

    [Fact]
    public void Help_ListsAllInboxSubcommands()
    {
        var output = CaptureHelpOutput();

        Assert.Contains("inbox list", output);
        Assert.Contains("inbox show", output);
        Assert.Contains("inbox clear", output);
    }

    [Fact]
    public void Help_ListsAllWorkspaceSubcommands()
    {
        var output = CaptureHelpOutput();

        Assert.Contains("workspace init", output);
        Assert.Contains("workspace check", output);
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
    public void Help_DocumentsEnvironmentVariables()
    {
        var output = CaptureHelpOutput();

        Assert.Contains("Environment Variables:", output);
        Assert.Contains("DYDO_HUMAN", output);
    }

    [Fact]
    public void Help_HasCategorizedSections()
    {
        var output = CaptureHelpOutput();

        Assert.Contains("Setup Commands:", output);
        Assert.Contains("Documentation Commands:", output);
        Assert.Contains("Agent Workflow Commands:", output);
        Assert.Contains("Agent Management Commands:", output);
        Assert.Contains("Dispatch & Inbox Commands:", output);
        Assert.Contains("Messaging Commands:", output);
        Assert.Contains("Workspace Commands:", output);
        Assert.Contains("Audit Commands:", output);
        Assert.Contains("Role Commands:", output);
        Assert.Contains("Validation Commands:", output);
        Assert.Contains("Template Commands:", output);
        Assert.Contains("Task Commands:", output);
        Assert.Contains("Issue Commands:", output);
        Assert.Contains("Inquisition Commands:", output);
        Assert.Contains("Queue Commands:", output);
        Assert.Contains("Worktree Commands:", output);
        Assert.Contains("Utility:", output);
    }
}
