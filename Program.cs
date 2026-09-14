using System.CommandLine;
using System.Reflection;
using DynaDocs.Commands;

var rootCommand = new RootCommand("DynaDocs (dydo) - Durable project knowledge and shared role compilation for AI coding assistants.");

rootCommand.Subcommands.Add(CheckCommand.Create());
rootCommand.Subcommands.Add(FixCommand.Create());
rootCommand.Subcommands.Add(IndexCommand.Create());
rootCommand.Subcommands.Add(InitCommand.Create());
rootCommand.Subcommands.Add(GraphCommand.Create());
rootCommand.Subcommands.Add(GuardCommand.Create());
rootCommand.Subcommands.Add(SyncCommand.Create());
rootCommand.Subcommands.Add(CompletionsCommand.Create());
rootCommand.Subcommands.Add(CompleteCommand.Create());
rootCommand.Subcommands.Add(TemplateCommand.Create());
rootCommand.Subcommands.Add(ValidateCommand.Create());

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
