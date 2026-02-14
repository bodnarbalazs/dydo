namespace DynaDocs.Commands;

using System.CommandLine;
using System.Text.RegularExpressions;
using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

public static class TaskCommand
{
    public static Command Create()
    {
        var command = new Command("task", "Manage tasks");

        command.Subcommands.Add(CreateCreateCommand());
        command.Subcommands.Add(CreateReadyForReviewCommand());
        command.Subcommands.Add(CreateApproveCommand());
        command.Subcommands.Add(CreateRejectCommand());
        command.Subcommands.Add(CreateListCommand());

        return command;
    }

    private static Command CreateCreateCommand()
    {
        var nameArgument = new Argument<string>("name")
        {
            Description = "Task name (kebab-case)"
        };

        var descriptionOption = new Option<string?>("--description")
        {
            Description = "Task description"
        };

        var areaOption = new Option<string>("--area")
        {
            Description = "Task area (e.g., backend, frontend, general)",
            Required = true
        };

        var command = new Command("create", "Create a new task");
        command.Arguments.Add(nameArgument);
        command.Options.Add(descriptionOption);
        command.Options.Add(areaOption);

        command.SetAction(parseResult =>
        {
            var name = parseResult.GetValue(nameArgument)!;
            var description = parseResult.GetValue(descriptionOption);
            var area = parseResult.GetValue(areaOption)!;
            return ExecuteCreate(name, description, area);
        });

        return command;
    }

    private static Command CreateReadyForReviewCommand()
    {
        var nameArgument = new Argument<string>("name")
        {
            Description = "Task name"
        };

        var summaryOption = new Option<string>("--summary")
        {
            Description = "Review summary",
            Required = true
        };

        var command = new Command("ready-for-review", "Mark task ready for review");
        command.Arguments.Add(nameArgument);
        command.Options.Add(summaryOption);

        command.SetAction(parseResult =>
        {
            var name = parseResult.GetValue(nameArgument)!;
            var summary = parseResult.GetValue(summaryOption)!;
            return ExecuteReadyForReview(name, summary);
        });

        return command;
    }

    private static Command CreateApproveCommand()
    {
        var nameArgument = new Argument<string>("name")
        {
            Description = "Task name"
        };

        var notesOption = new Option<string?>("--notes")
        {
            Description = "Approval notes"
        };

        var command = new Command("approve", "Approve a task (human only)");
        command.Arguments.Add(nameArgument);
        command.Options.Add(notesOption);

        command.SetAction(parseResult =>
        {
            var name = parseResult.GetValue(nameArgument)!;
            var notes = parseResult.GetValue(notesOption);
            return ExecuteApprove(name, notes);
        });

        return command;
    }

    private static Command CreateRejectCommand()
    {
        var nameArgument = new Argument<string>("name")
        {
            Description = "Task name"
        };

        var notesOption = new Option<string>("--notes")
        {
            Description = "Rejection reason",
            Required = true
        };

        var command = new Command("reject", "Reject a task (human only)");
        command.Arguments.Add(nameArgument);
        command.Options.Add(notesOption);

        command.SetAction(parseResult =>
        {
            var name = parseResult.GetValue(nameArgument)!;
            var notes = parseResult.GetValue(notesOption)!;
            return ExecuteReject(name, notes);
        });

        return command;
    }

    private static Command CreateListCommand()
    {
        var needsReviewOption = new Option<bool>("--needs-review")
        {
            Description = "Show only tasks needing human review"
        };

        var allOption = new Option<bool>("--all")
        {
            Description = "Show all tasks including closed"
        };

        var command = new Command("list", "List tasks");
        command.Options.Add(needsReviewOption);
        command.Options.Add(allOption);

        command.SetAction(parseResult =>
        {
            var needsReview = parseResult.GetValue(needsReviewOption);
            var all = parseResult.GetValue(allOption);
            return ExecuteList(needsReview, all);
        });

        return command;
    }

    private static string GetTasksPath()
    {
        var configService = new ConfigService();
        return configService.GetTasksPath();
    }

    private static int ExecuteCreate(string name, string? description, string area)
    {
        if (!Frontmatter.ValidAreas.Contains(area))
        {
            ConsoleOutput.WriteError($"Invalid area '{area}'. Must be one of: {string.Join(", ", Frontmatter.ValidAreas)}");
            return ExitCodes.ToolError;
        }

        var registry = new AgentRegistry();
        var sessionId = registry.GetSessionContext();
        var agent = registry.GetCurrentAgent(sessionId);

        var tasksPath = GetTasksPath();
        Directory.CreateDirectory(tasksPath);

        var taskPath = Path.Combine(tasksPath, $"{name}.md");
        if (File.Exists(taskPath))
        {
            ConsoleOutput.WriteError($"Task already exists: {name}");
            return ExitCodes.ToolError;
        }

        var content = $"""
            ---
            area: {area}
            name: {name}
            status: pending
            created: {DateTime.UtcNow:o}
            assigned: {agent?.Name ?? "unassigned"}
            ---

            # Task: {name}

            {description ?? "(No description)"}

            ## Progress

            - [ ] (Not started)

            ## Files Changed

            (None yet)

            ## Review Summary

            (Pending)
            """;

        File.WriteAllText(taskPath, content);
        Console.WriteLine($"Created task: {name}");
        Console.WriteLine($"Path: {taskPath}");

        return ExitCodes.Success;
    }

    private static int ExecuteReadyForReview(string name, string summary)
    {
        var tasksPath = GetTasksPath();
        var taskPath = Path.Combine(tasksPath, $"{name}.md");

        if (!File.Exists(taskPath))
        {
            ConsoleOutput.WriteError($"Task not found: {name}");
            return ExitCodes.ToolError;
        }

        var content = File.ReadAllText(taskPath);

        // Update status in frontmatter
        content = Regex.Replace(content, @"status: \w+", "status: review-pending");

        // Add/update updated timestamp
        if (content.Contains("updated:"))
            content = Regex.Replace(content, @"updated: .+", $"updated: {DateTime.UtcNow:o}");
        else
            content = content.Replace("---\n\n#", $"updated: {DateTime.UtcNow:o}\n---\n\n#");

        // Update review summary section
        content = Regex.Replace(
            content,
            @"## Review Summary\s+\(Pending\)",
            $"## Review Summary\n\n{summary}");

        File.WriteAllText(taskPath, content);
        Console.WriteLine($"Task {name} marked ready for review");

        return ExitCodes.Success;
    }

    private static int ExecuteApprove(string name, string? notes)
    {
        var configService = new ConfigService();
        var tasksPath = GetTasksPath();
        var taskPath = Path.Combine(tasksPath, $"{name}.md");

        if (!File.Exists(taskPath))
        {
            ConsoleOutput.WriteError($"Task not found: {name}");
            return ExitCodes.ToolError;
        }

        var content = File.ReadAllText(taskPath);

        // Parse task metadata from frontmatter
        var assignedMatch = Regex.Match(content, @"assigned: (\w+)");
        var assigned = assignedMatch.Success ? assignedMatch.Groups[1].Value : null;

        var createdMatch = Regex.Match(content, @"created: (.+)");
        DateTime? taskCreated = null;
        if (createdMatch.Success && DateTime.TryParse(
                createdMatch.Groups[1].Value,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var dt))
            taskCreated = dt;

        var areaMatch = Regex.Match(content, @"area: ([\w-]+)");
        var area = areaMatch.Success ? areaMatch.Groups[1].Value : "general";

        // Gather file changes from audit logs
        var created = new List<string>();
        var modified = new List<string>();
        var deleted = new List<string>();

        try
        {
            var auditService = new AuditService(configService);
            var (sessions, _) = auditService.LoadSessions();

            foreach (var session in sessions)
            {
                // Filter: matching agent
                if (!string.IsNullOrEmpty(assigned) &&
                    !string.Equals(session.AgentName, assigned, StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var evt in session.Events)
                {
                    // Filter: only events after the task was created
                    if (taskCreated.HasValue && evt.Timestamp < taskCreated.Value)
                        continue;

                    if (string.IsNullOrEmpty(evt.Path)) continue;

                    // Exclude internal dydo paths
                    var normalizedPath = evt.Path.Replace('\\', '/');
                    if (normalizedPath.Contains("dydo/", StringComparison.OrdinalIgnoreCase)) continue;

                    switch (evt.EventType)
                    {
                        case AuditEventType.Write:
                            if (!created.Contains(evt.Path) && !modified.Contains(evt.Path))
                                created.Add(evt.Path);
                            break;
                        case AuditEventType.Edit:
                            if (!modified.Contains(evt.Path) && !created.Contains(evt.Path))
                                modified.Add(evt.Path);
                            break;
                        case AuditEventType.Delete:
                            // Remove from created/modified if it was there
                            created.Remove(evt.Path);
                            modified.Remove(evt.Path);
                            if (!deleted.Contains(evt.Path))
                                deleted.Add(evt.Path);
                            break;
                    }
                }
            }
        }
        catch
        {
            // Audit service failure should not block approval
        }

        // Print file changes
        if (created.Count > 0 || modified.Count > 0 || deleted.Count > 0)
        {
            Console.WriteLine("Files changed (from audit logs):");
            foreach (var f in created) Console.WriteLine($"  + {f}");
            foreach (var f in modified) Console.WriteLine($"  ~ {f}");
            foreach (var f in deleted) Console.WriteLine($"  - {f}");
            Console.WriteLine();
        }

        // Transform frontmatter: remove task-specific fields, add changelog fields
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        content = Regex.Replace(content, @"(?m)^name: .+\n", "");
        content = Regex.Replace(content, @"(?m)^status: .+\n", "");
        content = Regex.Replace(content, @"(?m)^created: .+\n", "");
        content = Regex.Replace(content, @"(?m)^assigned: .+\n", "");
        content = Regex.Replace(content, @"(?m)^updated: .+\n", "");

        // Add type and date after area
        content = Regex.Replace(content, @"(?m)^(area: .+)$", $"$1\ntype: changelog\ndate: {today}");

        // Update "Files Changed" section with audit-derived list
        if (created.Count > 0 || modified.Count > 0 || deleted.Count > 0)
        {
            var filesSection = "## Files Changed\n\n";
            foreach (var f in created) filesSection += $"{f} — Created\n";
            foreach (var f in modified) filesSection += $"{f} — Modified\n";
            foreach (var f in deleted) filesSection += $"{f} — Deleted\n";

            content = Regex.Replace(content, @"## Files Changed\s*\n[\s\S]*?(?=\n## |\z)",
                filesSection.TrimEnd() + "\n\n");
        }

        // Add approval info
        var approvalSection = $"## Approval\n\n- Approved: {DateTime.UtcNow:yyyy-MM-dd HH:mm}";
        if (!string.IsNullOrEmpty(notes))
            approvalSection += $"\n- Notes: {notes}";
        content = content.TrimEnd() + "\n\n" + approvalSection + "\n";

        // Move file to changelog directory
        var changelogPath = configService.GetChangelogPath();
        var datePath = Path.Combine(changelogPath, DateTime.UtcNow.ToString("yyyy"), today);
        Directory.CreateDirectory(datePath);

        var changelogFilePath = Path.Combine(datePath, $"{name}.md");
        File.WriteAllText(changelogFilePath, content);
        File.Delete(taskPath);

        // Ensure hub files exist for changelog directory structure
        EnsureChangelogHubs(changelogPath, DateTime.UtcNow, name);

        Console.WriteLine($"Task {name} approved.");
        var relativeChangelogPath = Path.Combine("project", "changelog", DateTime.UtcNow.ToString("yyyy"), today, $"{name}.md");
        Console.WriteLine($"Changelog entry created: {relativeChangelogPath}");
        Console.WriteLine("Tip: Run 'dydo fix' to regenerate hub files.");

        return ExitCodes.Success;
    }

    private static void EnsureChangelogHubs(string changelogPath, DateTime date, string taskName)
    {
        var yearStr = date.ToString("yyyy");
        var dateStr = date.ToString("yyyy-MM-dd");

        // Top-level changelog _index.md
        var topIndex = Path.Combine(changelogPath, "_index.md");
        if (!File.Exists(topIndex))
        {
            var topContent = """
                ---
                area: project
                type: hub
                ---

                # Changelog

                """;
            File.WriteAllText(topIndex, topContent);
        }

        // Year folder _index.md
        var yearPath = Path.Combine(changelogPath, yearStr);
        Directory.CreateDirectory(yearPath);
        var yearIndex = Path.Combine(yearPath, "_index.md");
        if (!File.Exists(yearIndex))
        {
            var yearContent = $"""
                ---
                area: project
                type: hub
                ---

                # {yearStr}

                """;
            File.WriteAllText(yearIndex, yearContent);
        }

        // Date folder _index.md
        var datePath = Path.Combine(yearPath, dateStr);
        Directory.CreateDirectory(datePath);
        var dateIndex = Path.Combine(datePath, "_index.md");
        if (!File.Exists(dateIndex))
        {
            var dateContent = $"""
                ---
                area: project
                type: hub
                ---

                # {dateStr}

                """;
            File.WriteAllText(dateIndex, dateContent);
        }

        // Append link to date _index.md if not already there
        var dateIndexContent = File.ReadAllText(dateIndex);
        var taskLink = $"- [{taskName}](./{taskName}.md)";
        if (!dateIndexContent.Contains($"{taskName}.md"))
        {
            File.AppendAllText(dateIndex, taskLink + "\n");
        }

        // Append link to year _index.md if not already there
        var yearIndexContent = File.ReadAllText(yearIndex);
        var dateLink = $"- [{dateStr}](./{dateStr}/_index.md)";
        if (!yearIndexContent.Contains($"{dateStr}/"))
        {
            File.AppendAllText(yearIndex, dateLink + "\n");
        }

        // Append link to top _index.md if not already there
        var topIndexContent = File.ReadAllText(topIndex);
        var yearLink = $"- [{yearStr}](./{yearStr}/_index.md)";
        if (!topIndexContent.Contains($"{yearStr}/"))
        {
            File.AppendAllText(topIndex, yearLink + "\n");
        }
    }

    private static int ExecuteReject(string name, string notes)
    {
        var tasksPath = GetTasksPath();
        var taskPath = Path.Combine(tasksPath, $"{name}.md");

        if (!File.Exists(taskPath))
        {
            ConsoleOutput.WriteError($"Task not found: {name}");
            return ExitCodes.ToolError;
        }

        var content = File.ReadAllText(taskPath);

        // Update status back to active
        content = Regex.Replace(content, @"status: \w+(-\w+)?", "status: review-failed");

        // Add rejection note
        var rejectionSection = $"\n\n## Review Feedback ({DateTime.UtcNow:yyyy-MM-dd HH:mm})\n\n{notes}";
        content += rejectionSection;

        File.WriteAllText(taskPath, content);
        Console.WriteLine($"Task {name} rejected, needs rework");

        return ExitCodes.Success;
    }

    private static int ExecuteList(bool needsReview, bool all)
    {
        var tasksPath = GetTasksPath();
        if (!Directory.Exists(tasksPath))
        {
            Console.WriteLine("No tasks found");
            return ExitCodes.Success;
        }

        var tasks = new List<(string Name, string Status, string? Assigned, DateTime Created)>();

        foreach (var file in Directory.GetFiles(tasksPath, "*.md"))
        {
            var fileName = Path.GetFileName(file);
            if (fileName.StartsWith('_')) continue;

            var content = File.ReadAllText(file);
            var name = Path.GetFileNameWithoutExtension(file);

            var statusMatch = Regex.Match(content, @"status: ([\w-]+)");
            var status = statusMatch.Success ? statusMatch.Groups[1].Value : "unknown";

            var assignedMatch = Regex.Match(content, @"assigned: (\w+)");
            var assigned = assignedMatch.Success ? assignedMatch.Groups[1].Value : null;

            var createdMatch = Regex.Match(content, @"created: (.+)");
            var created = DateTime.UtcNow;
            if (createdMatch.Success && DateTime.TryParse(createdMatch.Groups[1].Value, out var dt))
                created = dt;

            if (!all && status == "closed") continue;
            if (needsReview && status != "review-pending") continue;

            tasks.Add((name, status, assigned, created));
        }

        if (tasks.Count == 0)
        {
            Console.WriteLine(needsReview ? "No tasks awaiting review" : "No tasks found");
            return ExitCodes.Success;
        }

        Console.WriteLine($"{"Task",-25} {"Status",-15} {"Assigned",-10} {"Created",-12}");
        Console.WriteLine(new string('-', 65));

        foreach (var (name, status, assigned, created) in tasks.OrderByDescending(t => t.Created))
        {
            var displayName = name.Length > 23 ? name[..23] + ".." : name;
            Console.WriteLine($"{displayName,-25} {status,-15} {assigned ?? "-",-10} {created:yyyy-MM-dd}");
        }

        return ExitCodes.Success;
    }
}
