namespace DynaDocs.Commands;

using System.CommandLine;
using System.Text.RegularExpressions;
using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

/// <summary>
/// dydo's <b>runtime agent task-tracker</b>: <c>dydo task create/ready-for-review/done/list</c> over
/// the task files in <c>dydo/project/tasks/</c> (schema: <c>name</c> / <c>assigned</c> / <c>status</c>
/// backlog→in-progress→in-review→done). This is the in-session work-tracking lifecycle agents and humans use day to day.
/// <para>
/// It is NOT the Notion-synced PM board. The board's leaf object is the separate <b>Slice</b> type
/// (Campaign → Sprint → Slice) declared in the sync model (<c>Templates/sync-model.template.json</c>,
/// canonical dir <c>dydo/project/slices/</c>) and reconciled by <c>dydo notion sync</c>. The two are
/// distinct systems with different schemas and directories — do not conflate them.
/// </para>
/// </summary>
public static class TaskCommand
{
    public static Command Create()
    {
        var command = new Command("task", "Manage tasks");

        command.Subcommands.Add(CreateCreateCommand());
        command.Subcommands.Add(CreateReadyForReviewCommand());
        command.Subcommands.Add(CreateDoneCommand());
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

    private static Command CreateDoneCommand()
    {
        var nameArgument = new Argument<string>("name")
        {
            Description = "Task name"
        };

        var command = new Command("done", "Mark a task done after verification");
        command.Arguments.Add(nameArgument);

        command.SetAction(parseResult =>
        {
            var name = parseResult.GetValue(nameArgument)!;
            return TaskDoneHandler.Execute(name);
        });

        return command;
    }

    private static Command CreateListCommand()
    {
        var needsReviewOption = new Option<bool>("--needs-review")
        {
            Description = "Show only tasks needing review"
        };

        var allOption = new Option<bool>("--all")
        {
            Description = "Show all tasks including done"
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


    internal static string GetTasksPath()
    {
        var configService = new ConfigService();
        return configService.GetTasksPath();
    }

    /// <summary>
    /// Transition a task file to in-review state with the given summary.
    /// Delegates to TaskReviewHandler. Kept for backward compatibility with DispatchCommand.
    /// </summary>
    internal static bool TransitionToReviewPending(string taskName, string summary)
        => TaskReviewHandler.TransitionToReviewPending(taskName, summary);
}
