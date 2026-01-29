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
rootCommand.AddCommand(WhoamiCommand.Create());

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
});
rootCommand.AddCommand(helpCommand);

return await rootCommand.InvokeAsync(args);
