using System.CommandLine;
using DynaDocs.Commands;
using DynaDocs.Utils;

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

var rootCommand = new RootCommand("DynaDocs (dydo) - Durable project knowledge and native skills for AI coding assistants.");

rootCommand.Subcommands.Add(CheckCommand.Create());
rootCommand.Subcommands.Add(FixCommand.Create());
rootCommand.Subcommands.Add(IndexCommand.Create());
rootCommand.Subcommands.Add(InitCommand.Create());
rootCommand.Subcommands.Add(AdoptCommand.Create());
rootCommand.Subcommands.Add(GraphCommand.Create());
rootCommand.Subcommands.Add(GuardCommand.Create());
rootCommand.Subcommands.Add(CompletionsCommand.Create());
rootCommand.Subcommands.Add(CompleteCommand.Create());
rootCommand.Subcommands.Add(ValidateCommand.Create());
rootCommand.Subcommands.Add(GapCheckCommand.Create());
rootCommand.Subcommands.Add(MapCommand.Create());
rootCommand.Subcommands.Add(ModeCommand.Create());
rootCommand.Subcommands.Add(CleanCommand.Create());
rootCommand.Subcommands.Add(PrReadyCommand.Create());
rootCommand.Subcommands.Add(MergeCommand.Create());

var versionCommand = new Command("version", "Display version information");
versionCommand.SetAction(_ =>
{
    Console.WriteLine($"dydo version {DydoVersion.Current}");
    return 0;
});
rootCommand.Subcommands.Add(versionCommand);

rootCommand.Subcommands.Add(HelpCommand.Create());

return await rootCommand.Parse(args).InvokeAsync();
