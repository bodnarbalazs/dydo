using System.CommandLine;
using System.Reflection;
using DynaDocs.Commands;

var rootCommand = new RootCommand("DynaDocs (dydo) - Dynamic Documentation validation and management tool");

rootCommand.AddCommand(CheckCommand.Create());
rootCommand.AddCommand(FixCommand.Create());
rootCommand.AddCommand(IndexCommand.Create());
rootCommand.AddCommand(InitCommand.Create());
rootCommand.AddCommand(GraphCommand.Create());
rootCommand.AddCommand(AgentCommand.Create());
rootCommand.AddCommand(GuardCommand.Create());
rootCommand.AddCommand(DispatchCommand.Create());
rootCommand.AddCommand(InboxCommand.Create());
rootCommand.AddCommand(TaskCommand.Create());
rootCommand.AddCommand(ReviewCommand.Create());
rootCommand.AddCommand(CleanCommand.Create());
rootCommand.AddCommand(WorkspaceCommand.Create());

var versionCommand = new Command("version", "Display version information");
versionCommand.SetHandler(() =>
{
    var version = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
    Console.WriteLine($"dydo version {version.Major}.{version.Minor}.{version.Build}");
});
rootCommand.AddCommand(versionCommand);

var helpCommand = new Command("help", "Display help information");
helpCommand.SetHandler(() =>
{
    Console.WriteLine("DynaDocs (dydo) - Dynamic Documentation validation and workflow tool");
    Console.WriteLine();
    Console.WriteLine("Documentation Commands:");
    Console.WriteLine("  check [path]    Validate docs, report violations");
    Console.WriteLine("  fix [path]      Auto-fix issues where possible");
    Console.WriteLine("  index [path]    Regenerate Index.md from structure");
    Console.WriteLine("  init <path>     Scaffold folder structure");
    Console.WriteLine("  graph <file>    Show graph connections for a file");
    Console.WriteLine();
    Console.WriteLine("Agent Workflow Commands:");
    Console.WriteLine("  agent claim <name>     Claim an agent for this terminal");
    Console.WriteLine("  agent release          Release current agent");
    Console.WriteLine("  agent status [name]    Show agent status");
    Console.WriteLine("  agent list [--free]    List all agents");
    Console.WriteLine("  agent role <role>      Set current agent's role");
    Console.WriteLine();
    Console.WriteLine("  dispatch               Dispatch work to another agent");
    Console.WriteLine("  inbox list             List agents with inbox items");
    Console.WriteLine("  inbox show             Show current agent's inbox");
    Console.WriteLine("  inbox clear            Clear processed inbox items");
    Console.WriteLine();
    Console.WriteLine("  guard                  Check if action is allowed (for hooks)");
    Console.WriteLine("  clean <agent>          Clean agent workspace");
    Console.WriteLine("  workspace init         Initialize all 26 agent workspaces");
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
    Console.WriteLine("  version         Display version information");
    Console.WriteLine("  help            Display this help");
    Console.WriteLine();
    Console.WriteLine("Exit codes:");
    Console.WriteLine("  0 - Success");
    Console.WriteLine("  1 - Validation errors found");
    Console.WriteLine("  2 - Tool error");
});
rootCommand.AddCommand(helpCommand);

return await rootCommand.InvokeAsync(args);
