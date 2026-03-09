namespace DynaDocs.Commands;

using System.CommandLine;
using System.Text.RegularExpressions;
using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

public static partial class IssueCommand
{
    public static Command Create()
    {
        var command = new Command("issue", "Manage issues");

        command.Subcommands.Add(CreateCreateCommand());
        command.Subcommands.Add(CreateListCommand());
        command.Subcommands.Add(CreateResolveCommand());

        return command;
    }

    private static Command CreateCreateCommand()
    {
        var titleOption = new Option<string>("--title")
        {
            Description = "Issue title",
            Required = true
        };

        var areaOption = new Option<string>("--area")
        {
            Description = "Issue area (e.g., backend, frontend, general)",
            Required = true
        };

        var severityOption = new Option<string>("--severity")
        {
            Description = "Issue severity (low, medium, high, critical)",
            Required = true
        };

        var foundByOption = new Option<string?>("--found-by")
        {
            Description = "How the issue was found (inquisition, review, manual). Defaults to manual."
        };

        var command = new Command("create", "Create a new issue");
        command.Options.Add(titleOption);
        command.Options.Add(areaOption);
        command.Options.Add(severityOption);
        command.Options.Add(foundByOption);

        command.SetAction(parseResult =>
        {
            var title = parseResult.GetValue(titleOption)!;
            var area = parseResult.GetValue(areaOption)!;
            var severity = parseResult.GetValue(severityOption)!;
            var foundBy = parseResult.GetValue(foundByOption);
            return ExecuteCreate(title, area, severity, foundBy);
        });

        return command;
    }

    private static Command CreateListCommand()
    {
        var areaOption = new Option<string?>("--area")
        {
            Description = "Filter by area"
        };

        var statusOption = new Option<string?>("--status")
        {
            Description = "Filter by status"
        };

        var allOption = new Option<bool>("--all")
        {
            Description = "Include resolved issues"
        };

        var command = new Command("list", "List issues");
        command.Options.Add(areaOption);
        command.Options.Add(statusOption);
        command.Options.Add(allOption);

        command.SetAction(parseResult =>
        {
            var area = parseResult.GetValue(areaOption);
            var status = parseResult.GetValue(statusOption);
            var all = parseResult.GetValue(allOption);
            return ExecuteList(area, status, all);
        });

        return command;
    }

    private static Command CreateResolveCommand()
    {
        var idArgument = new Argument<int>("id")
        {
            Description = "Issue ID to resolve"
        };

        var summaryOption = new Option<string>("--summary")
        {
            Description = "Resolution summary (required)",
            Required = true
        };

        var command = new Command("resolve", "Resolve an issue");
        command.Arguments.Add(idArgument);
        command.Options.Add(summaryOption);

        command.SetAction(parseResult =>
        {
            var id = parseResult.GetValue(idArgument);
            var summary = parseResult.GetValue(summaryOption)!;
            return ExecuteResolve(id, summary);
        });

        return command;
    }

    private static string GetIssuesPath()
    {
        var configService = new ConfigService();
        return configService.GetIssuesPath();
    }

    private static string Slugify(string title)
    {
        var slug = title.ToLowerInvariant();
        slug = SlugNonAlnumRegex().Replace(slug, "-");
        slug = MultipleHyphensRegex().Replace(slug, "-");
        slug = slug.Trim('-');
        if (slug.Length > 80)
            slug = slug[..80].TrimEnd('-');
        return slug;
    }

    private static int ScanMaxId(string issuesPath)
    {
        var maxId = 0;
        var dirs = new[] { issuesPath, Path.Combine(issuesPath, "resolved") };

        foreach (var dir in dirs)
        {
            if (!Directory.Exists(dir)) continue;

            foreach (var file in Directory.GetFiles(dir, "*.md"))
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var match = IdPrefixRegex().Match(fileName);
                if (match.Success && int.TryParse(match.Groups[1].Value, out var id))
                {
                    if (id > maxId) maxId = id;
                }
            }
        }

        return maxId;
    }

    private static int ExecuteCreate(string title, string area, string severity, string? foundBy)
    {
        if (!Frontmatter.ValidAreas.Contains(area))
        {
            ConsoleOutput.WriteError($"Invalid area '{area}'. Must be one of: {string.Join(", ", Frontmatter.ValidAreas)}");
            return ExitCodes.ToolError;
        }

        var severityLower = severity.ToLowerInvariant();
        if (severityLower is not ("low" or "medium" or "high" or "critical"))
        {
            ConsoleOutput.WriteError($"Invalid severity '{severity}'. Must be one of: low, medium, high, critical");
            return ExitCodes.ToolError;
        }

        var foundByLower = (foundBy ?? "manual").ToLowerInvariant();
        if (foundByLower is not ("inquisition" or "review" or "manual"))
        {
            ConsoleOutput.WriteError($"Invalid found-by '{foundBy}'. Must be one of: inquisition, review, manual");
            return ExitCodes.ToolError;
        }

        var issuesPath = GetIssuesPath();
        Directory.CreateDirectory(issuesPath);

        var newId = ScanMaxId(issuesPath) + 1;
        var slug = Slugify(title);
        var fileName = $"{newId:D4}-{slug}.md";
        var filePath = Path.Combine(issuesPath, fileName);

        var content = $"""
            ---
            id: {newId}
            area: {area}
            type: issue
            severity: {severityLower}
            status: open
            found-by: {foundByLower}
            date: {DateTime.UtcNow:yyyy-MM-dd}
            ---

            # {title}

            ## Description

            (Describe the issue)

            ## Reproduction

            (Steps to reproduce, if applicable)

            ## Resolution

            (Filled when resolved)
            """;

        File.WriteAllText(filePath, content);
        Console.WriteLine($"Created issue #{newId}: {fileName}");

        return ExitCodes.Success;
    }

    private static int ExecuteList(string? areaFilter, string? statusFilter, bool all)
    {
        var issuesPath = GetIssuesPath();
        var issues = new List<(int Id, string Severity, string Area, string Status, string Date, string Title)>();

        ScanIssues(issuesPath, issues);
        if (all)
            ScanIssues(Path.Combine(issuesPath, "resolved"), issues);

        // Apply filters
        if (areaFilter != null)
            issues.RemoveAll(i => !i.Area.Equals(areaFilter, StringComparison.OrdinalIgnoreCase));
        if (statusFilter != null)
            issues.RemoveAll(i => !i.Status.Equals(statusFilter, StringComparison.OrdinalIgnoreCase));

        if (issues.Count == 0)
        {
            Console.WriteLine("No issues found.");
            return ExitCodes.Success;
        }

        Console.WriteLine($"{"ID",-6} {"Severity",-10} {"Area",-12} {"Status",-10} {"Date",-12} Title");
        Console.WriteLine(new string('-', 75));

        foreach (var (id, sev, area, status, date, title) in issues.OrderByDescending(i => i.Id))
        {
            var displayTitle = title.Length > 30 ? title[..28] + ".." : title;
            Console.WriteLine($"#{id,-5} {sev,-10} {area,-12} {status,-10} {date,-12} {displayTitle}");
        }

        return ExitCodes.Success;
    }

    private static void ScanIssues(string dir, List<(int Id, string Severity, string Area, string Status, string Date, string Title)> issues)
    {
        if (!Directory.Exists(dir)) return;

        foreach (var file in Directory.GetFiles(dir, "*.md"))
        {
            var content = File.ReadAllText(file);

            var idMatch = Regex.Match(content, @"(?m)^id: (\d+)");
            if (!idMatch.Success) continue;
            var id = int.Parse(idMatch.Groups[1].Value);

            var sevMatch = Regex.Match(content, @"(?m)^severity: (\w+)");
            var severity = sevMatch.Success ? sevMatch.Groups[1].Value : "unknown";

            var areaMatch = Regex.Match(content, @"(?m)^area: ([\w-]+)");
            var area = areaMatch.Success ? areaMatch.Groups[1].Value : "unknown";

            var statusMatch = Regex.Match(content, @"(?m)^status: ([\w-]+)");
            var status = statusMatch.Success ? statusMatch.Groups[1].Value : "unknown";

            var dateMatch = Regex.Match(content, @"(?m)^date: ([\d-]+)");
            var date = dateMatch.Success ? dateMatch.Groups[1].Value : "";

            var titleMatch = Regex.Match(content, @"(?m)^# (.+)$");
            var title = titleMatch.Success ? titleMatch.Groups[1].Value.Trim() : Path.GetFileNameWithoutExtension(file);

            issues.Add((id, severity, area, status, date, title));
        }
    }

    private static int ExecuteResolve(int id, string summary)
    {
        var issuesPath = GetIssuesPath();
        var resolvedPath = Path.Combine(issuesPath, "resolved");

        // Check if already resolved
        if (Directory.Exists(resolvedPath))
        {
            var resolvedFile = FindIssueById(resolvedPath, id);
            if (resolvedFile != null)
            {
                ConsoleOutput.WriteError($"Issue #{id} is already resolved.");
                return ExitCodes.ToolError;
            }
        }

        var issueFile = FindIssueById(issuesPath, id);
        if (issueFile == null)
        {
            ConsoleOutput.WriteError($"Issue #{id} not found.");
            return ExitCodes.ToolError;
        }

        var content = File.ReadAllText(issueFile);

        // Update frontmatter status
        content = Regex.Replace(content, @"(?m)^status: [\w-]+", "status: resolved");

        // Add resolved-date to frontmatter after the date line
        content = Regex.Replace(content, @"(date: [\d-]+)", $"$1\nresolved-date: {DateTime.UtcNow:yyyy-MM-dd}");

        // Replace the Resolution placeholder
        content = Regex.Replace(content, @"## Resolution\s+\(Filled when resolved\)", $"## Resolution\n\n{summary}");

        // Move to resolved/
        Directory.CreateDirectory(resolvedPath);
        var destPath = Path.Combine(resolvedPath, Path.GetFileName(issueFile));
        File.WriteAllText(destPath, content);
        File.Delete(issueFile);

        var fileName = Path.GetFileName(issueFile);
        Console.WriteLine($"Resolved issue #{id}: {fileName}");

        return ExitCodes.Success;
    }

    private static string? FindIssueById(string dir, int id)
    {
        if (!Directory.Exists(dir)) return null;

        var prefix = $"{id:D4}-";
        foreach (var file in Directory.GetFiles(dir, "*.md"))
        {
            if (Path.GetFileName(file).StartsWith(prefix))
                return file;
        }

        return null;
    }

    [GeneratedRegex(@"^(\d+)-")]
    private static partial Regex IdPrefixRegex();

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex SlugNonAlnumRegex();

    [GeneratedRegex(@"-+")]
    private static partial Regex MultipleHyphensRegex();
}
