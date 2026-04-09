using System.CommandLine;
using System.Reflection;
using DynaDocs.Commands;

var rootCommand = new RootCommand("DynaDocs (dydo) - Documentation-driven context and agent orchestration for AI coding assistants.");

rootCommand.Subcommands.Add(CheckCommand.Create());
rootCommand.Subcommands.Add(FixCommand.Create());
rootCommand.Subcommands.Add(IndexCommand.Create());
rootCommand.Subcommands.Add(InitCommand.Create());
rootCommand.Subcommands.Add(GraphCommand.Create());
rootCommand.Subcommands.Add(AgentCommand.Create());
rootCommand.Subcommands.Add(GuardCommand.Create());
rootCommand.Subcommands.Add(DispatchCommand.Create());
rootCommand.Subcommands.Add(InboxCommand.Create());
rootCommand.Subcommands.Add(MessageCommand.Create());
rootCommand.Subcommands.Add(WaitCommand.Create());
rootCommand.Subcommands.Add(TaskCommand.Create());
rootCommand.Subcommands.Add(IssueCommand.Create());
rootCommand.Subcommands.Add(ReviewCommand.Create());
rootCommand.Subcommands.Add(InquisitionCommand.Create());
rootCommand.Subcommands.Add(WorkspaceCommand.Create());
rootCommand.Subcommands.Add(WhoamiCommand.Create());
rootCommand.Subcommands.Add(AuditCommand.Create());
rootCommand.Subcommands.Add(CompletionsCommand.Create());
rootCommand.Subcommands.Add(CompleteCommand.Create());
rootCommand.Subcommands.Add(TemplateCommand.Create());
rootCommand.Subcommands.Add(RolesCommand.Create());
rootCommand.Subcommands.Add(ValidateCommand.Create());
rootCommand.Subcommands.Add(WatchdogCommand.Create());
rootCommand.Subcommands.Add(WorktreeCommand.Create());
rootCommand.Subcommands.Add(QueueCommand.Create());

var versionCommand = new Command("version", "Display version information");
versionCommand.SetAction(_ =>
{
    var version = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
    Console.WriteLine($"dydo version {version.Major}.{version.Minor}.{version.Build}");
    return 0;
});
rootCommand.Subcommands.Add(versionCommand);

rootCommand.Subcommands.Add(HelpCommand.Create());

return await rootCommand.Parse(args).InvokeAsync();
