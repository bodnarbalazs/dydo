namespace DynaDocs.Commands;

using System.CommandLine;
using DynaDocs.Services;

public static class ValidateCommand
{
    public static Command Create()
    {
        var command = new Command("validate", "Validate dydo configuration, role files, and agent state");

        command.SetAction((_, _) =>
        {
            var basePath = Environment.CurrentDirectory;
            var service = new ValidationService();
            var issues = service.ValidateSystem(basePath);

            if (issues.Count == 0)
            {
                Console.WriteLine("Validation passed. No issues found.");
                return Task.FromResult(0);
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

            return Task.FromResult(errors.Count > 0 ? 1 : 0);
        });

        return command;
    }
}
