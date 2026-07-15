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
            var configService = new ConfigService();
            var config = configService.LoadConfig();
            var configHasErrors = ValidateConfig(config);

            var docsOutcome = ValidateDocs(path);
            if (docsOutcome.IsToolError)
                return ExitCodes.ToolError;

            return WriteSummary(
                configHasErrors || docsOutcome.HasErrors,
                docsOutcome.HasWarnings);
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Error: {ex.Message}");
            return ExitCodes.ToolError;
        }
    }

    private static bool ValidateConfig(Models.DydoConfig? config)
    {
        if (config == null) return false;
        var errors = CheckConfigValidator.Validate(config);
        if (errors.Count == 0) return false;
        Console.WriteLine("Checking dydo.json...");
        foreach (var error in errors)
            ConsoleOutput.WriteError($"  {error}");
        Console.WriteLine();
        return true;
    }

    private record DocsOutcome(bool IsToolError, bool HasErrors, bool HasWarnings);

    private static DocsOutcome ValidateDocs(string? path)
    {
        var basePath = PathUtils.FindDocsFolder(Environment.CurrentDirectory);
        var reportScope = ResolveReportScope(path);

        if (!string.IsNullOrEmpty(path) && reportScope == null)
        {
            ConsoleOutput.WriteError($"Path not found: {path}");
            return new DocsOutcome(true, false, false);
        }
        if (basePath != null && reportScope != null)
        {
            if (CheckDocValidator.IsUnderScope(basePath, reportScope))
                reportScope = null;
            else if (!CheckDocValidator.IsUnderScope(reportScope, basePath))
            {
                ConsoleOutput.WriteError($"Path is outside the docs tree: {path}");
                return new DocsOutcome(true, false, false);
            }
        }
        if (basePath == null)
        {
            Console.WriteLine("No docs folder found.");
            return new DocsOutcome(false, false, false);
        }

        Console.WriteLine($"Checking {reportScope ?? basePath}...");
        Console.WriteLine();
        var result = CheckDocValidator.Validate(basePath, reportScope);
        ConsoleOutput.WriteViolations(result);
        return new DocsOutcome(false, result.HasErrors, result.WarningCount > 0);
    }

    private static int WriteSummary(bool hasErrors, bool hasWarnings)
    {
        Console.WriteLine();
        if (hasErrors)
        {
            Console.WriteLine("Found errors.");
            return ExitCodes.ValidationErrors;
        }
        Console.WriteLine(hasWarnings ? "Found warnings (no errors)." : "All checks passed.");
        return ExitCodes.Success;
    }

    private static string? ResolveReportScope(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return null;
        if (File.Exists(path) || Directory.Exists(path))
            return Path.GetFullPath(path);
        return null;
    }
}
