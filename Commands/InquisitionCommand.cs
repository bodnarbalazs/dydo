namespace DynaDocs.Commands;

using System.CommandLine;
using System.Diagnostics;
using System.Text.RegularExpressions;
using DynaDocs.Services;
using DynaDocs.Utils;

public static partial class InquisitionCommand
{
    public static Command Create()
    {
        var command = new Command("inquisition", "Manage inquisitions");
        command.Subcommands.Add(CreateCoverageCommand());
        return command;
    }

    private static Command CreateCoverageCommand()
    {
        var command = new Command("coverage", "Show inquisition coverage across project areas");

        command.SetAction(_ => ExecuteCoverage());

        return command;
    }

    private static int ExecuteCoverage()
    {
        var configService = new ConfigService();
        var dydoRoot = configService.GetDydoRoot();
        var inquisitionsPath = Path.Combine(dydoRoot, "project", "inquisitions");

        if (!Directory.Exists(inquisitionsPath))
        {
            Directory.CreateDirectory(inquisitionsPath);
            Console.WriteLine("No inquisitions found.");
            return ExitCodes.Success;
        }

        var reports = Directory.GetFiles(inquisitionsPath, "*.md")
            .Where(f => !Path.GetFileName(f).StartsWith("_"))
            .ToList();

        if (reports.Count == 0)
        {
            Console.WriteLine("No inquisitions found.");
            return ExitCodes.Success;
        }

        Console.WriteLine("Inquisition Coverage:");
        Console.WriteLine();
        Console.WriteLine($"  {"Area",-30} {"Last Inquisition",-20} {"Status",-10}");
        Console.WriteLine($"  {"----",-30} {"----------------",-20} {"------",-10}");

        foreach (var report in reports.OrderBy(Path.GetFileNameWithoutExtension))
        {
            var area = Path.GetFileNameWithoutExtension(report);
            var lastDate = ParseLastInquisitionDate(report);

            string status;
            if (lastDate == null)
            {
                status = "gap";
            }
            else
            {
                var hasChanges = HasChangesSince(lastDate.Value, dydoRoot);
                status = hasChanges == null ? "unknown" : hasChanges.Value ? "stale" : "covered";
            }

            var dateStr = lastDate?.ToString("yyyy-MM-dd") ?? "none";
            Console.WriteLine($"  {area,-30} {dateStr,-20} {status,-10}");
        }

        return ExitCodes.Success;
    }

    private static DateTime? ParseLastInquisitionDate(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath);
            // Match section headers like "## 2026-03-10 — AgentName"
            var matches = DateHeaderRegex().Matches(content);
            if (matches.Count == 0) return null;

            // Return the most recent date
            return matches
                .Select(m => DateTime.TryParse(m.Groups[1].Value, out var d) ? d : (DateTime?)null)
                .Where(d => d != null)
                .Max();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Checks if there are git changes since the given date.
    /// Returns null if git is unavailable.
    /// </summary>
    private static bool? HasChangesSince(DateTime since, string workingDir)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"diff --stat HEAD@{{{since:yyyy-MM-dd}}}",
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return null;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            if (process.ExitCode != 0) return null;
            return !string.IsNullOrWhiteSpace(output);
        }
        catch
        {
            return null;
        }
    }

    [GeneratedRegex(@"^## (\d{4}-\d{2}-\d{2})", RegexOptions.Multiline)]
    private static partial Regex DateHeaderRegex();
}
