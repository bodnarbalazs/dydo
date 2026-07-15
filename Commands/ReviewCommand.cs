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
        var agent = registry.GetCurrentOwnedAgent(sessionId);

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
            return CompletePass(registry, agent, taskPath, content, taskName, notes);

        return CompleteFail(registry, agent, taskPath, content, taskName, notes);
    }

    private static string ReadCurrentStatus(string content)
    {
        var statusMatch = Regex.Match(content, @"status: ([\w-]+)");
        return statusMatch.Success ? statusMatch.Groups[1].Value : "unknown";
    }

    private static (string Name, string ProvenanceLines) ResolveReviewer(AgentRegistry registry, Models.AgentState? agent)
    {
        var provenance = agent == null ? null : ArtifactProvenance.FromSession(registry, agent.Name);
        return (agent?.Name ?? "Unknown", RenderReviewProvenance(provenance));
    }

    private static int CompletePass(AgentRegistry registry, Models.AgentState? agent, string taskPath,
        string content, string taskName, string? notes)
    {
        var reviewer = ResolveReviewer(registry, agent);
        var reviewTime = DateTime.UtcNow;

        content = Regex.Replace(content, @"status: [\w-]+", "status: done");

        var reviewSection = $"""


            ## Code Review

            - Reviewed by: {reviewer.Name}
            {reviewer.ProvenanceLines}
            - Date: {reviewTime:yyyy-MM-dd HH:mm}
            - Result: PASSED
            {(string.IsNullOrEmpty(notes) ? "" : $"- Notes: {notes}")}

            """;

        content += reviewSection;
        File.WriteAllText(taskPath, content);

        Console.WriteLine($"Review PASSED for {taskName}");

        if (agent != null)
        {
            RequireMergeDispatch(registry, agent, taskName);
        }

        return ExitCodes.Success;
    }

    private static int CompleteFail(AgentRegistry registry, Models.AgentState? agent, string taskPath,
        string content, string taskName, string? notes)
    {
        var reviewer = ResolveReviewer(registry, agent);
        var reviewTime = DateTime.UtcNow;

        content = Regex.Replace(content, @"status: [\w-]+", "status: in-progress");

        var reviewSection = $"""


            ## Code Review ({reviewTime:yyyy-MM-dd HH:mm})

            - Reviewed by: {reviewer.Name}
            {reviewer.ProvenanceLines}
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

    // If reviewer is in a worktree, require merge dispatch before release
    private static void RequireMergeDispatch(AgentRegistry registry, Models.AgentState agent, string taskName)
    {
        var workspace = registry.GetAgentWorkspace(agent.Name);
        var worktreeMarker = Path.Combine(workspace, ".worktree");
        if (!File.Exists(worktreeMarker))
            return;

        File.WriteAllText(Path.Combine(workspace, ".needs-merge"), taskName);
        Console.WriteLine();
        Console.WriteLine("Worktree branch needs merging. Dispatch a code-writer to merge before releasing:");
        Console.WriteLine($"  dydo dispatch --auto-close --role code-writer --task {taskName}-merge --brief \"Merge worktree branch into base. See .merge-source and .worktree-base markers in your workspace.\"");
    }

    private static string RenderReviewProvenance(ArtifactProvenance? provenance)
    {
        if (provenance == null) return "";

        return $"""
            - reviewed-by-vendor: {provenance.Vendor}
            - reviewed-by-model: {provenance.Model}
            """;
    }

}
