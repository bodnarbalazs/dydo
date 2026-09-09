using System.CommandLine;
using System.Reflection;
using DynaDocs.Commands;

if (args.Length > 0 && args[0] == "gap-check")
{
    using var cancellation = new CancellationTokenSource();
    ConsoleCancelEventHandler handler = (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellation.Cancel();
    };
    Console.CancelKeyPress += handler;
    try
    {
        return await GapCheckCommand.ExecuteAsync(args[1..], Environment.CurrentDirectory, cancellation.Token);
    }
    finally
    {
        Console.CancelKeyPress -= handler;
    }
}

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
rootCommand.Subcommands.Add(GapCheckCommand.Create());

var versionCommand = new Command("version", "Display version information");
versionCommand.SetAction(_ =>
{
    var version = Assembly.GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
        ?? "1.0.0";
    Console.WriteLine($"dydo version {version}");
    return 0;
});
rootCommand.Subcommands.Add(versionCommand);

rootCommand.Subcommands.Add(HelpCommand.Create());

return await rootCommand.Parse(args).InvokeAsync();
