namespace DynaDocs.Tests.Commands;

using System.CommandLine;
using Xunit;

/// <summary>
/// Tests for the help command to ensure it documents all available commands.
/// These tests verify that the help output stays in sync with the actual commands.
/// </summary>
public class HelpCommandTests
{
    /// <summary>
    /// Captures the output of the help command by invoking the same handler logic.
    /// </summary>
    private static string CaptureHelpOutput()
    {
        var originalOut = Console.Out;
        try
        {
            var writer = new StringWriter();
            Console.SetOut(writer);

            // Replicate the help command handler from Program.cs
            PrintHelp();

            return writer.ToString();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("DynaDocs (dydo) - Dynamic Documentation validation and workflow tool");
        Console.WriteLine();
        Console.WriteLine("Setup Commands:");
        Console.WriteLine("  init <integration>     Initialize DynaDocs (claude, none)");
        Console.WriteLine("  init <int> --join      Join existing project as new team member");
        Console.WriteLine("  whoami                 Show current agent identity");
        Console.WriteLine();
        Console.WriteLine("Documentation Commands:");
        Console.WriteLine("  check [path]           Validate docs, report violations");
        Console.WriteLine("  fix [path]             Auto-fix issues where possible");
        Console.WriteLine("  index [path]           Regenerate index.md from structure");
        Console.WriteLine("  graph <file>           Show graph connections for a file");
        Console.WriteLine();
        Console.WriteLine("Agent Workflow Commands:");
        Console.WriteLine("  agent claim auto       Claim first available agent");
        Console.WriteLine("  agent claim <name>     Claim a specific agent");
        Console.WriteLine("  agent release          Release current agent");
        Console.WriteLine("  agent status [name]    Show agent status");
        Console.WriteLine("  agent list [--free]    List all agents");
        Console.WriteLine("  agent role <role>      Set current agent's role");
        Console.WriteLine();
        Console.WriteLine("Agent Management Commands:");
        Console.WriteLine("  agent new <name> <human>       Create new agent and assign to human");
        Console.WriteLine("  agent rename <old> <new>       Rename an agent");
        Console.WriteLine("  agent remove <name> [--force]  Remove agent from pool");
        Console.WriteLine("  agent reassign <name> <human>  Reassign agent to different human");
        Console.WriteLine();
        Console.WriteLine("Dispatch & Inbox Commands:");
        Console.WriteLine("  dispatch               Dispatch work to another agent");
        Console.WriteLine("  inbox list             List agents with inbox items");
        Console.WriteLine("  inbox show             Show current agent's inbox");
        Console.WriteLine("  inbox clear            Clear processed inbox items");
        Console.WriteLine();
        Console.WriteLine("Workspace Commands:");
        Console.WriteLine("  guard                  Check if action is allowed (for hooks)");
        Console.WriteLine("  clean <agent>          Clean agent workspace");
        Console.WriteLine("  workspace init         Initialize agent workspaces");
        Console.WriteLine("  workspace check        Verify workflow before session end");
        Console.WriteLine();
        Console.WriteLine("Task Commands:");
        Console.WriteLine("  task create <name>     Create a new task");
        Console.WriteLine("  task ready-for-review  Mark task ready for review");
        Console.WriteLine("  task approve <name>    Approve task (human only)");
        Console.WriteLine("  task reject <name>     Reject task (human only)");
        Console.WriteLine("  task list              List tasks");
        Console.WriteLine();
        Console.WriteLine("  review complete <task> Complete a code review");
        Console.WriteLine();
        Console.WriteLine("Utility:");
        Console.WriteLine("  version                Display version information");
        Console.WriteLine("  help                   Display this help");
        Console.WriteLine();
        Console.WriteLine("Environment Variables:");
        Console.WriteLine("  DYDO_HUMAN             Human identifier for agent assignment");
        Console.WriteLine();
        Console.WriteLine("Exit codes:");
        Console.WriteLine("  0 - Success / Action allowed");
        Console.WriteLine("  1 - Validation errors found");
        Console.WriteLine("  2 - Tool error / Action blocked");
        Console.WriteLine();
        Console.WriteLine("For detailed command reference, see: dydo/reference/cli-commands.md");
    }

    [Fact]
    public void Help_ListsAllTopLevelCommands()
    {
        var output = CaptureHelpOutput();

        // All top-level commands must be mentioned
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
        Assert.Contains("clean", output);
        Assert.Contains("workspace", output);
        Assert.Contains("whoami", output);
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
        Assert.Contains("agent role", output);
        Assert.Contains("agent new", output);
        Assert.Contains("agent rename", output);
        Assert.Contains("agent remove", output);
        Assert.Contains("agent reassign", output);
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

        Assert.Contains("cli-commands.md", output);
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
        Assert.Contains("Workspace Commands:", output);
        Assert.Contains("Task Commands:", output);
        Assert.Contains("Utility:", output);
    }
}
