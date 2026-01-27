namespace DynaDocs.Utils;

using DynaDocs.Models;

public static class ConsoleOutput
{
    public static void WriteHeader(string text)
    {
        Console.WriteLine(text);
        Console.WriteLine();
    }

    public static void WriteError(string text)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(text);
        Console.ResetColor();
    }

    public static void WriteWarning(string text)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(text);
        Console.ResetColor();
    }

    public static void WriteSuccess(string text)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(text);
        Console.ResetColor();
    }

    public static void WriteViolations(ValidationResult result)
    {
        var errors = result.Violations.Where(v => v.Severity == ViolationSeverity.Error).ToList();
        var warnings = result.Violations.Where(v => v.Severity == ViolationSeverity.Warning).ToList();

        if (errors.Count > 0)
        {
            WriteError("ERRORS:");
            foreach (var fileGroup in errors.GroupBy(v => v.FilePath).OrderBy(g => g.Key))
            {
                Console.WriteLine($"  {fileGroup.Key}");
                foreach (var violation in fileGroup)
                {
                    var lineInfo = violation.LineNumber.HasValue ? $"Line {violation.LineNumber}: " : "";
                    Console.WriteLine($"    - {lineInfo}{violation.Message}");
                }
            }
            Console.WriteLine();
        }

        if (warnings.Count > 0)
        {
            WriteWarning("WARNINGS:");
            foreach (var fileGroup in warnings.GroupBy(v => v.FilePath).OrderBy(g => g.Key))
            {
                Console.WriteLine($"  {fileGroup.Key}");
                foreach (var violation in fileGroup)
                {
                    var lineInfo = violation.LineNumber.HasValue ? $"Line {violation.LineNumber}: " : "";
                    Console.WriteLine($"    - {lineInfo}{violation.Message}");
                }
            }
            Console.WriteLine();
        }

        Console.WriteLine($"Found {result.ErrorCount} errors, {result.WarningCount} warnings in {result.TotalFilesChecked} files.");
    }
}
