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
            ConsoleOutput.WriteError("You must mark the task ready for review first:");
            ConsoleOutput.WriteError($"  dydo task ready-for-review {taskName} --summary \"Brief description of completed work\"");
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

            if (agent != null)
            {
                RouteVerdictMessages(registry, agent, taskName, notes);

                // If reviewer is in a worktree, require merge dispatch before release
                var workspace = registry.GetAgentWorkspace(agent.Name);
                var worktreeMarker = Path.Combine(workspace, ".worktree");
                Console.WriteLine($"  [review-debug] workspace={workspace}, worktreeMarker exists={File.Exists(worktreeMarker)}");
                if (File.Exists(worktreeMarker))
                {
                    File.WriteAllText(Path.Combine(workspace, ".needs-merge"), taskName);
                    Console.WriteLine($"  [review-debug] Created .needs-merge with value: {taskName}");
                    Console.WriteLine();
                    Console.WriteLine("Worktree branch needs merging. Dispatch a code-writer to merge before releasing:");
                    Console.WriteLine($"  dydo dispatch --no-wait --auto-close --queue merge --role code-writer --task {taskName}-merge --brief \"Merge worktree branch into base. See .merge-source and .worktree-base markers in your workspace.\"");
                }
            }
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

    private static void RouteVerdictMessages(AgentRegistry registry, Models.AgentState reviewer,
        string taskName, string? notes)
    {
        if (string.IsNullOrEmpty(reviewer.DispatchedBy)) return;

        var dispatcher = reviewer.DispatchedBy;
        var baseBody = string.IsNullOrEmpty(notes)
            ? $"Review passed for {taskName}."
            : $"Review passed for {taskName}.\n\n{notes}";

        MessageService.DeliverInboxMessage(registry, reviewer.Name, dispatcher, baseBody, taskName);
        if (registry.RemoveReplyPendingMarker(reviewer.Name, taskName))
            Console.WriteLine($"  Reply obligation fulfilled for '{taskName}'.");
        Console.WriteLine($"  Verdict sent to {dispatcher}.");

        var ancestor = FindNearestCanOrchestrateAncestor(registry, dispatcher);
        if (string.IsNullOrEmpty(ancestor)) return;
        if (ancestor.Equals(dispatcher, StringComparison.OrdinalIgnoreCase)) return;
        if (ancestor.Equals(reviewer.Name, StringComparison.OrdinalIgnoreCase)) return;

        var ccBody = string.IsNullOrEmpty(notes)
            ? $"[CC] Review passed for {taskName} (code-writer: {dispatcher})."
            : $"[CC] Review passed for {taskName} (code-writer: {dispatcher}).\n\n{notes}";
        MessageService.DeliverInboxMessage(registry, reviewer.Name, ancestor, ccBody, taskName);
        Console.WriteLine($"  CC'd orchestrator {ancestor}.");
    }

    private static string? FindNearestCanOrchestrateAncestor(AgentRegistry registry, string startAgent)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = startAgent;
        while (!string.IsNullOrEmpty(current) && visited.Add(current))
        {
            var state = registry.GetAgentState(current);
            if (state == null) return null;
            if (!string.IsNullOrEmpty(state.Role))
            {
                var def = registry.GetRoleDefinition(state.Role);
                if (def?.CanOrchestrate == true)
                    return current;
            }
            current = state.DispatchedBy ?? "";
        }
        return null;
    }
}
