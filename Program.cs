using System.CommandLine;
using DynaDocs.Commands;
using DynaDocs.Utils;

if (args.Length > 0 && args[0] is "__statechart-cleanup" or "__statechart-probe" or
    "__statechart-posix-owner" or "__statechart-posix-anchor" or "__statechart-posix-child")
{
    try
    {
        return args[0] switch
        {
            "__statechart-cleanup" => await DynaDocs.Services.Statechart.StatechartCleanup.RunAsync(args[1..], Console.In, Console.Out),
            "__statechart-probe" => await DynaDocs.Services.Statechart.StatechartProbe.ChildAsync(args[1..]),
            "__statechart-posix-owner" => await DynaDocs.Services.Statechart.PosixStatechartOwner.RunAsync(args[1..]),
            "__statechart-posix-anchor" => await DynaDocs.Services.Statechart.PosixStatechartAnchor.RunAsync(args[1..]),
            _ => DynaDocs.Services.Statechart.PosixStatechartChild.Run(args[1..])
        };
    }
    catch (Exception error) when (error is IOException or InvalidDataException or InvalidOperationException or System.ComponentModel.Win32Exception)
    {
        Console.Error.WriteLine(error.Message);
        return 2;
    }
}

if (args.Length > 0 && (args[0] == "gap-check" || args[0] == "statechart" && !args.Skip(1).Any(argument => argument is "--help" or "-h" or "-?")))
{
    using var cancellation = new CancellationTokenSource();
    if (args[0] == "statechart" && Environment.GetEnvironmentVariable("DYDO_STATECHART_NPM_LIFETIME") == "1")
        _ = DynaDocs.Services.Statechart.StatechartOuterLifetime.ObserveAsync(Console.OpenStandardInput(), cancellation);
    ConsoleCancelEventHandler handler = (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellation.Cancel();
    };
    Console.CancelKeyPress += handler;
    try
    {
        return args[0] == "statechart"
            ? await StatechartCommand.ExecuteAsync(args[1..], Environment.CurrentDirectory, cancellation.Token)
            : await GapCheckCommand.ExecuteAsync(args[1..], Environment.CurrentDirectory, cancellation.Token);
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
rootCommand.Subcommands.Add(SurveyCommand.Create());
rootCommand.Subcommands.Add(ModeCommand.Create());
rootCommand.Subcommands.Add(CleanCommand.Create());
rootCommand.Subcommands.Add(PrReadyCommand.Create());
rootCommand.Subcommands.Add(MergeCommand.Create());
rootCommand.Subcommands.Add(StatechartCommand.Create());

var versionCommand = new Command("version", "Display version information");
versionCommand.SetAction(_ =>
{
    Console.WriteLine($"dydo version {DydoVersion.Current}");
    return 0;
});
rootCommand.Subcommands.Add(versionCommand);

rootCommand.Subcommands.Add(HelpCommand.Create());

return await CliParsing.Parse(rootCommand, args).InvokeAsync();
