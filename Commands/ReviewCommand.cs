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
        var tasksPath = GetTasksPath();
        var taskPath = Path.Combine(tasksPath, $"{taskName}.md");

        if (!File.Exists(taskPath))
        {
            ConsoleOutput.WriteError($"Task not found: {taskName}");
            return ExitCodes.ToolError;
        }

        var content = File.ReadAllText(taskPath);

        // Check current status
        var currentStatus = ReadCurrentStatus(content);
        if (currentStatus != "in-review")
        {
            ConsoleOutput.WriteError($"Task is not in review state (current: {currentStatus})");
            ConsoleOutput.WriteError("You must mark the task ready for review first:");
            ConsoleOutput.WriteError($"  dydo task ready-for-review {taskName} --summary \"Brief description of completed work\"");
            return ExitCodes.ToolError;
        }

        if (status == "pass")
            return CompletePass(taskPath, content, taskName, notes);

        return CompleteFail(taskPath, content, taskName, notes);
    }

    private static string ReadCurrentStatus(string content)
    {
        var statusMatch = Regex.Match(content, @"status: ([\w-]+)");
        return statusMatch.Success ? statusMatch.Groups[1].Value : "unknown";
    }

    // Reviewer identity/provenance stamping was carved out with the claim ceremony (DR-041):
    // there is no runtime agent identity, so the reviewer is recorded as "Unknown".
    private const string ReviewerName = "Unknown";

    private static int CompletePass(string taskPath, string content, string taskName, string? notes)
    {
        var reviewTime = DateTime.UtcNow;

        content = Regex.Replace(content, @"status: [\w-]+", "status: done");

        var reviewSection = $"""


            ## Code Review

            - Reviewed by: {ReviewerName}
            - Date: {reviewTime:yyyy-MM-dd HH:mm}
            - Result: PASSED
            {(string.IsNullOrEmpty(notes) ? "" : $"- Notes: {notes}")}

            """;

        content += reviewSection;
        File.WriteAllText(taskPath, content);

        Console.WriteLine($"Review PASSED for {taskName}");
        return ExitCodes.Success;
    }

    private static int CompleteFail(string taskPath, string content, string taskName, string? notes)
    {
        var reviewTime = DateTime.UtcNow;

        content = Regex.Replace(content, @"status: [\w-]+", "status: in-progress");

        var reviewSection = $"""


            ## Code Review ({reviewTime:yyyy-MM-dd HH:mm})

            - Reviewed by: {ReviewerName}
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

        return ExitCodes.Success;
    }
}
