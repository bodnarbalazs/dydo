namespace DynaDocs.Commands;

using System.CommandLine;
using DynaDocs.Services;
using DynaDocs.Utils;

public static class ValidateCommand
{
    public static Command Create()
    {
        var rootOption = new Option<string?>("--root")
        {
            Description = "Validate the dydo project containing this directory or file instead of the current one"
        };

        var command = new Command("validate", "Validate dydo configuration and nudges");
        command.Options.Add(rootOption);

        command.SetAction((parseResult, _) => Task.FromResult(Execute(parseResult.GetValue(rootOption))));

        return command;
    }

    private static int Execute(string? root)
    {
        var basePath = PathUtils.ResolveCommandRoot(root);
        if (basePath == null)
            return ExitCodes.ToolError;

        var service = new ValidationService();
        var issues = service.ValidateSystem(basePath);

        if (issues.Count == 0)
        {
            Console.WriteLine("Validation passed. No issues found.");
            return 0;
        }

        var errors = issues.Where(i => i.Severity == "error").ToList();
        var warnings = issues.Where(i => i.Severity == "warning").ToList();

        if (errors.Count > 0)
        {
            Console.Error.WriteLine($"Errors ({errors.Count}):");
            foreach (var issue in errors)
                Console.Error.WriteLine($"  {issue.File}: {issue.Message}");
        }

        if (warnings.Count > 0)
        {
            if (errors.Count > 0) Console.Error.WriteLine();
            Console.Error.WriteLine($"Warnings ({warnings.Count}):");
            foreach (var issue in warnings)
                Console.Error.WriteLine($"  {issue.File}: {issue.Message}");
        }

        return errors.Count > 0 ? 1 : 0;
    }
}
