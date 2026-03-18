namespace DynaDocs.Commands;

using System.CommandLine;
using System.Diagnostics;
using System.Text.RegularExpressions;
using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

public static partial class InquisitionCommand
{
    // Test seam: override to avoid spawning git in test host
    public static Func<DateTime, string, bool?>? HasChangesSinceOverride;

    public static Command Create()
    {
        var command = new Command("inquisition", "Manage inquisitions");
        command.Subcommands.Add(CreateCoverageCommand());
        return command;
    }

    private static Command CreateCoverageCommand()
    {
        var filesOption = new Option<bool>("--files") { Description = "File-level coverage heatmap" };
        var pathOption = new Option<string?>("--path") { Description = "Scope to subtree" };
        var gapsOnlyOption = new Option<bool>("--gaps-only") { Description = "Only gap + low files" };
        var sinceOption = new Option<int>("--since") { DefaultValueFactory = _ => 365, Description = "Days lookback" };
        var summaryOption = new Option<bool>("--summary") { Description = "Folder-level aggregates only" };

        var command = new Command("coverage", "Show inquisition coverage across project areas");
        command.Options.Add(filesOption);
        command.Options.Add(pathOption);
        command.Options.Add(gapsOnlyOption);
        command.Options.Add(sinceOption);
        command.Options.Add(summaryOption);

        command.SetAction(parseResult =>
        {
            if (parseResult.GetValue(filesOption))
            {
                return ExecuteFileCoverage(new FileCoverageOptions(
                    SinceDays: parseResult.GetValue(sinceOption),
                    PathFilter: parseResult.GetValue(pathOption),
                    GapsOnly: parseResult.GetValue(gapsOnlyOption),
                    SummaryOnly: parseResult.GetValue(summaryOption)));
            }
            return ExecuteCoverage();
        });

        return command;
    }

    private static int ExecuteFileCoverage(FileCoverageOptions options)
    {
        var service = new FileCoverageService();
        var report = service.GenerateReport(options);
        var markdown = service.RenderMarkdown(report, options);

        // Determine output path
        var configService = new ConfigService();
        var dydoRoot = configService.GetDydoRoot();
        string outputPath;

        if (options.OutputPath != null)
        {
            outputPath = options.OutputPath;
        }
        else
        {
            var registry = new AgentRegistry();
            var sessionId = registry.GetSessionContext();
            var agent = sessionId != null ? registry.GetCurrentAgent(sessionId) : null;

            outputPath = agent != null
                ? Path.Combine(registry.GetAgentWorkspace(agent.Name), "inquisition-coverage.md")
                : Path.Combine(dydoRoot, "project", "inquisitions", "_coverage.md");
        }

        var dir = Path.GetDirectoryName(outputPath);
        if (dir != null) Directory.CreateDirectory(dir);
        File.WriteAllText(outputPath, markdown);

        Console.WriteLine($"File coverage report written to: {outputPath}");
        Console.WriteLine($"  Total: {report.TotalFiles} | Covered: {report.CoveredCount} | Low: {report.LowCount} | Gaps: {report.GapCount} | Stale: {report.StaleCount}");

        return ExitCodes.Success;
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
        if (HasChangesSinceOverride != null)
            return HasChangesSinceOverride(since, workingDir);

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
