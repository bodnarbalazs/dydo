namespace DynaDocs.Commands;

using System.CommandLine;
using System.CommandLine.Invocation;
using DynaDocs.Services;
using DynaDocs.Utils;

public static class InitCommand
{
    public static Command Create()
    {
        var pathArgument = new Argument<string>("path", "Path where to create the docs structure");

        var command = new Command("init", "Scaffold documentation folder structure")
        {
            pathArgument
        };

        command.SetHandler((InvocationContext ctx) =>
        {
            var path = ctx.ParseResult.GetValueForArgument(pathArgument);
            ctx.ExitCode = Execute(path);
        });

        return command;
    }

    private static int Execute(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);

            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
            }

            Console.WriteLine($"Initializing docs structure at {fullPath}...");
            Console.WriteLine();

            var scaffolder = new FolderScaffolder();
            scaffolder.Scaffold(fullPath);

            ConsoleOutput.WriteSuccess("Created folder structure:");
            Console.WriteLine("  - understand/_index.md");
            Console.WriteLine("  - guides/_index.md");
            Console.WriteLine("  - reference/_index.md");
            Console.WriteLine("  - project/_index.md");
            Console.WriteLine("  - index.md");

            Console.WriteLine();
            Console.WriteLine("Next steps:");
            Console.WriteLine("  1. Add your documentation files to the appropriate folders");
            Console.WriteLine("  2. Run 'dydo check' to validate your docs");
            Console.WriteLine("  3. Run 'dydo index' to regenerate index.md");

            return ExitCodes.Success;
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Error: {ex.Message}");
            return ExitCodes.ToolError;
        }
    }
}
