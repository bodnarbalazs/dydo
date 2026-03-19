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
        command.Subcommands.Add(CreateCompactCommand());

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
            return TaskCreateHandler.Execute(name, description, area);
        });

        return command;
    }

    private static Command CreateReadyForReviewCommand()
    {
        var nameArgument = new Argument<string>("name")
        {
            Description = "Task name"
        };

        var summaryOption = new Option<string?>("--summary")
        {
            Description = "Review summary (required)"
        };

        var command = new Command("ready-for-review", "Mark task ready for review");
        command.Arguments.Add(nameArgument);
        command.Options.Add(summaryOption);

        command.SetAction(parseResult =>
        {
            var name = parseResult.GetValue(nameArgument)!;
            var summary = parseResult.GetValue(summaryOption);
            return TaskReviewHandler.ExecuteReadyForReview(name, summary);
        });

        return command;
    }

    private static Command CreateApproveCommand()
    {
        var nameArgument = new Argument<string?>("name")
        {
            Description = "Task name (or use --all to approve all)",
            Arity = ArgumentArity.ZeroOrOne
        };

        var allOption = new Option<bool>("--all", "-a")
        {
            Description = "Approve all pending tasks"
        };

        var notesOption = new Option<string?>("--notes")
        {
            Description = "Approval notes"
        };

        var command = new Command("approve", "Approve a task (human only)");
        command.Arguments.Add(nameArgument);
        command.Options.Add(allOption);
        command.Options.Add(notesOption);

        command.SetAction(parseResult =>
        {
            var name = parseResult.GetValue(nameArgument);
            var all = parseResult.GetValue(allOption);
            var notes = parseResult.GetValue(notesOption);

            if (all || name == "*")
                return TaskApproveHandler.ExecuteApproveAll(notes);

            if (string.IsNullOrEmpty(name))
            {
                ConsoleOutput.WriteError("Specify a task name or use --all to approve all tasks.");
                return ExitCodes.ToolError;
            }

            return TaskApproveHandler.ExecuteApprove(name, notes);
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
            return TaskReviewHandler.ExecuteReject(name, notes);
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
            return TaskListHandler.Execute(needsReview, all);
        });

        return command;
    }

    private static Command CreateCompactCommand()
    {
        var command = new Command("compact", "Compact audit snapshots");

        command.SetAction(parseResult =>
        {
            return TaskCompactHandler.Execute();
        });

        return command;
    }

    internal static string GetTasksPath()
    {
        var configService = new ConfigService();
        return configService.GetTasksPath();
    }

    /// <summary>
    /// Transition a task file to review-pending state with the given summary.
    /// Delegates to TaskReviewHandler. Kept for backward compatibility with DispatchCommand.
    /// </summary>
    internal static bool TransitionToReviewPending(string taskName, string summary)
        => TaskReviewHandler.TransitionToReviewPending(taskName, summary);
}
