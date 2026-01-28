namespace DynaDocs.Commands;

using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.RegularExpressions;
using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

public static class TaskCommand
{
    private const string TasksFolder = "project/tasks";

    public static Command Create()
    {
        var command = new Command("task", "Manage tasks");

        command.AddCommand(CreateCreateCommand());
        command.AddCommand(CreateReadyForReviewCommand());
        command.AddCommand(CreateApproveCommand());
        command.AddCommand(CreateRejectCommand());
        command.AddCommand(CreateListCommand());

        return command;
    }

    private static Command CreateCreateCommand()
    {
        var nameArgument = new Argument<string>("name", "Task name (kebab-case)");
        var descriptionOption = new Option<string?>("--description", "Task description");

        var command = new Command("create", "Create a new task")
        {
            nameArgument,
            descriptionOption
        };

        command.SetHandler((InvocationContext ctx) =>
        {
            var name = ctx.ParseResult.GetValueForArgument(nameArgument);
            var description = ctx.ParseResult.GetValueForOption(descriptionOption);
            ctx.ExitCode = ExecuteCreate(name, description);
        });

        return command;
    }

    private static Command CreateReadyForReviewCommand()
    {
        var nameArgument = new Argument<string>("name", "Task name");
        var summaryOption = new Option<string>("--summary", "Review summary")
        {
            IsRequired = true
        };

        var command = new Command("ready-for-review", "Mark task ready for review")
        {
            nameArgument,
            summaryOption
        };

        command.SetHandler((InvocationContext ctx) =>
        {
            var name = ctx.ParseResult.GetValueForArgument(nameArgument);
            var summary = ctx.ParseResult.GetValueForOption(summaryOption)!;
            ctx.ExitCode = ExecuteReadyForReview(name, summary);
        });

        return command;
    }

    private static Command CreateApproveCommand()
    {
        var nameArgument = new Argument<string>("name", "Task name");
        var notesOption = new Option<string?>("--notes", "Approval notes");

        var command = new Command("approve", "Approve a task (human only)")
        {
            nameArgument,
            notesOption
        };

        command.SetHandler((InvocationContext ctx) =>
        {
            var name = ctx.ParseResult.GetValueForArgument(nameArgument);
            var notes = ctx.ParseResult.GetValueForOption(notesOption);
            ctx.ExitCode = ExecuteApprove(name, notes);
        });

        return command;
    }

    private static Command CreateRejectCommand()
    {
        var nameArgument = new Argument<string>("name", "Task name");
        var notesOption = new Option<string>("--notes", "Rejection reason")
        {
            IsRequired = true
        };

        var command = new Command("reject", "Reject a task (human only)")
        {
            nameArgument,
            notesOption
        };

        command.SetHandler((InvocationContext ctx) =>
        {
            var name = ctx.ParseResult.GetValueForArgument(nameArgument);
            var notes = ctx.ParseResult.GetValueForOption(notesOption)!;
            ctx.ExitCode = ExecuteReject(name, notes);
        });

        return command;
    }

    private static Command CreateListCommand()
    {
        var needsReviewOption = new Option<bool>("--needs-review", "Show only tasks needing human review");
        var allOption = new Option<bool>("--all", "Show all tasks including closed");

        var command = new Command("list", "List tasks")
        {
            needsReviewOption,
            allOption
        };

        command.SetHandler((InvocationContext ctx) =>
        {
            var needsReview = ctx.ParseResult.GetValueForOption(needsReviewOption);
            var all = ctx.ParseResult.GetValueForOption(allOption);
            ctx.ExitCode = ExecuteList(needsReview, all);
        });

        return command;
    }

    private static string GetTasksPath()
    {
        var docsPath = PathUtils.FindDocsFolder(Environment.CurrentDirectory);
        if (docsPath == null)
            return Path.Combine(Environment.CurrentDirectory, TasksFolder);

        return Path.Combine(Path.GetDirectoryName(docsPath)!, TasksFolder);
    }

    private static int ExecuteCreate(string name, string? description)
    {
        var registry = new AgentRegistry();
        var agent = registry.GetCurrentAgent();

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
        var tasksPath = GetTasksPath();
        var taskPath = Path.Combine(tasksPath, $"{name}.md");

        if (!File.Exists(taskPath))
        {
            ConsoleOutput.WriteError($"Task not found: {name}");
            return ExitCodes.ToolError;
        }

        var content = File.ReadAllText(taskPath);

        // Update status
        content = Regex.Replace(content, @"status: \w+(-\w+)?", "status: closed");

        // Add approval info
        var approvalSection = $"\n\n## Approval\n\n- Approved: {DateTime.UtcNow:yyyy-MM-dd HH:mm}";
        if (!string.IsNullOrEmpty(notes))
            approvalSection += $"\n- Notes: {notes}";

        content += approvalSection;

        File.WriteAllText(taskPath, content);
        Console.WriteLine($"Task {name} approved and closed");

        return ExitCodes.Success;
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
            if (Path.GetFileName(file) == "_index.md") continue;

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
