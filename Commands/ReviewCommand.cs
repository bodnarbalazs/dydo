namespace DynaDocs.Commands;

using System.CommandLine;
using System.Text.RegularExpressions;
using DynaDocs.Services;
using DynaDocs.Utils;

public static class ReviewCommand
{
    public static Command Create()
    {
        var command = new Command("review", "Manage code reviews");

        command.Subcommands.Add(CreateCompleteCommand());

        return command;
    }

    private static Command CreateCompleteCommand()
    {
        var taskArgument = new Argument<string>("task")
        {
            Description = "Task name being reviewed"
        };

        var statusOption = new Option<string>("--status")
        {
            Description = "Review result: pass or fail",
            Required = true
        };
        statusOption.Validators.Add(result =>
        {
            var value = result.GetValue(statusOption);
            if (value != "pass" && value != "fail")
            {
                result.AddError("Status must be 'pass' or 'fail'");
            }
        });

        var notesOption = new Option<string?>("--notes")
        {
            Description = "Review notes"
        };

        var command = new Command("complete", "Complete a code review");
        command.Arguments.Add(taskArgument);
        command.Options.Add(statusOption);
        command.Options.Add(notesOption);

        command.SetAction(parseResult =>
        {
            var task = parseResult.GetValue(taskArgument)!;
            var status = parseResult.GetValue(statusOption)!;
            var notes = parseResult.GetValue(notesOption);
            return ExecuteComplete(task, status, notes);
        });

        return command;
    }

    private static string GetTasksPath()
    {
        var configService = new ConfigService();
        return configService.GetTasksPath();
    }

    private static int ExecuteComplete(string taskName, string status, string? notes)
    {
        var registry = new AgentRegistry();
        var sessionId = registry.GetSessionContext();
        var agent = registry.GetCurrentAgent(sessionId);

        var tasksPath = GetTasksPath();
        var taskPath = Path.Combine(tasksPath, $"{taskName}.md");

        if (!File.Exists(taskPath))
        {
            ConsoleOutput.WriteError($"Task not found: {taskName}");
            return ExitCodes.ToolError;
        }

        var content = File.ReadAllText(taskPath);

        // Check current status
        var statusMatch = Regex.Match(content, @"status: ([\w-]+)");
        var currentStatus = statusMatch.Success ? statusMatch.Groups[1].Value : "unknown";

        if (currentStatus != "review-pending" && currentStatus != "active")
        {
            ConsoleOutput.WriteError($"Task is not in review state (current: {currentStatus})");
            return ExitCodes.ToolError;
        }

        var reviewerName = agent?.Name ?? "Unknown";
        var reviewTime = DateTime.UtcNow;

        if (status == "pass")
        {
            // Update status to human-reviewed (needs human approval)
            content = Regex.Replace(content, @"status: [\w-]+", "status: human-reviewed");

            var reviewSection = $"""


                ## Code Review

                - Reviewed by: {reviewerName}
                - Date: {reviewTime:yyyy-MM-dd HH:mm}
                - Result: PASSED
                {(string.IsNullOrEmpty(notes) ? "" : $"- Notes: {notes}")}

                Awaiting human approval.
                """;

            content += reviewSection;
            File.WriteAllText(taskPath, content);

            Console.WriteLine($"Review PASSED for {taskName}");
            Console.WriteLine("Task now awaits human approval");
            Console.WriteLine("Human can run: dydo task approve " + taskName);
        }
        else
        {
            // Update status back to active for rework
            content = Regex.Replace(content, @"status: [\w-]+", "status: review-failed");

            var reviewSection = $"""


                ## Code Review ({reviewTime:yyyy-MM-dd HH:mm})

                - Reviewed by: {reviewerName}
                - Result: FAILED
                - Issues: {notes ?? "(No details provided)"}

                Requires rework.
                """;

            content += reviewSection;
            File.WriteAllText(taskPath, content);

            Console.WriteLine($"Review FAILED for {taskName}");
            Console.WriteLine("Task returned for rework");

            if (!string.IsNullOrEmpty(notes))
                Console.WriteLine($"Issues: {notes}");
        }

        return ExitCodes.Success;
    }
}
