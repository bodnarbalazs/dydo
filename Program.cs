using System.CommandLine;
using System.Reflection;
using DynaDocs.Commands;

var rootCommand = new RootCommand("DynaDocs (dydo) - Dynamic Documentation validation and management tool");

rootCommand.AddCommand(CheckCommand.Create());
rootCommand.AddCommand(FixCommand.Create());
rootCommand.AddCommand(IndexCommand.Create());
rootCommand.AddCommand(InitCommand.Create());

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
    Console.WriteLine("DynaDocs (dydo) - Dynamic Documentation validation tool");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  check [path]  Validate docs, report violations");
    Console.WriteLine("  fix [path]    Auto-fix issues where possible");
    Console.WriteLine("  index [path]  Regenerate Index.md from structure");
    Console.WriteLine("  init <path>   Scaffold folder structure");
    Console.WriteLine("  version       Display version information");
    Console.WriteLine("  help          Display this help");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --version     Display version");
    Console.WriteLine("  --help        Display help");
    Console.WriteLine();
    Console.WriteLine("Exit codes:");
    Console.WriteLine("  0 - Success");
    Console.WriteLine("  1 - Validation errors found");
    Console.WriteLine("  2 - Tool error");
});
rootCommand.AddCommand(helpCommand);

return await rootCommand.InvokeAsync(args);
