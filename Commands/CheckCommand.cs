namespace DynaDocs.Commands;

using System.CommandLine;
using DynaDocs.Services;
using DynaDocs.Utils;

public static class CheckCommand
{
    public static Command Create()
    {
        var pathArgument = new Argument<string?>("path")
        {
            DefaultValueFactory = _ => null,
            Description = "Path to docs folder or file to check"
        };

        var command = new Command("check", "Validate documentation and report violations");
        command.Arguments.Add(pathArgument);

        command.SetAction(parseResult =>
        {
            var path = parseResult.GetValue(pathArgument);
            return Execute(path);
        });

        return command;
    }

    private static int Execute(string? path)
    {
        try
        {
            var hasErrors = false;
            var hasWarnings = false;
            var configService = new ConfigService();
            var config = configService.LoadConfig();

            // Documentation validation
            var basePath = ResolvePath(path);
            if (basePath != null)
            {
                Console.WriteLine($"Checking {basePath}...");
                Console.WriteLine();

                var result = CheckDocValidator.Validate(basePath);
                ConsoleOutput.WriteViolations(result);

                if (result.HasErrors)
                    hasErrors = true;
                if (result.WarningCount > 0)
                    hasWarnings = true;
            }
            else if (string.IsNullOrEmpty(path))
            {
                Console.WriteLine("No docs folder found.");
            }
            else
            {
                ConsoleOutput.WriteError($"Path not found: {path}");
                return ExitCodes.ToolError;
            }

            // Agent validation (only if config exists)
            if (config != null)
            {
                Console.WriteLine();
                Console.WriteLine("Checking agent assignments...");

                var agentWarnings = CheckAgentValidator.Validate(config, configService);
                if (agentWarnings.Count > 0)
                {
                    hasWarnings = true;
                    foreach (var warning in agentWarnings)
                    {
                        ConsoleOutput.WriteWarning(warning);
                    }
                }
                else
                {
                    Console.WriteLine("  No issues found.");
                }
            }

            Console.WriteLine();
            if (hasErrors)
            {
                Console.WriteLine("Found errors.");
                return ExitCodes.ValidationErrors;
            }
            else if (hasWarnings)
            {
                Console.WriteLine("Found warnings (no errors).");
                return ExitCodes.Success;
            }
            else
            {
                Console.WriteLine("All checks passed.");
                return ExitCodes.Success;
            }
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Error: {ex.Message}");
            return ExitCodes.ToolError;
        }
    }

    private static string? ResolvePath(string? path)
    {
        if (!string.IsNullOrEmpty(path))
        {
            if (File.Exists(path) || Directory.Exists(path))
                return Path.GetFullPath(path);
            return null;
        }

        return PathUtils.FindDocsFolder(Environment.CurrentDirectory);
    }
}
