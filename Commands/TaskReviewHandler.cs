namespace DynaDocs.Commands;

using System.Text.RegularExpressions;
using DynaDocs.Utils;

internal static class TaskReviewHandler
{
    /// <summary>
    /// Transition a task file to in-review state with the given summary.
    /// Returns true on success, false if the task file doesn't exist (non-fatal).
    /// Idempotent: safe to call if the task is already in-review.
    /// </summary>
    public static bool TransitionToReviewPending(string taskName, string summary)
    {
        var tasksPath = TaskCommand.GetTasksPath();
        var taskPath = Path.Combine(tasksPath, $"{PathUtils.SanitizeForFilename(taskName)}.md");

        if (!File.Exists(taskPath))
            return false;

        var content = File.ReadAllText(taskPath);

        content = Regex.Replace(content, @"status: [\w-]+", "status: in-review");

        if (content.Contains("updated:"))
            content = Regex.Replace(content, @"updated: .+", $"updated: {DateTime.UtcNow:o}");
        else
            content = content.Replace("---\n\n#", $"updated: {DateTime.UtcNow:o}\n---\n\n#");

        content = Regex.Replace(
            content,
            @"## Review Summary\s+\(Pending\)",
            $"## Review Summary\n\n{summary}");

        File.WriteAllText(taskPath, content);
        return true;
    }

    public static int ExecuteReadyForReview(string name, string? summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            ConsoleOutput.WriteError("--summary is required. Describe what you did:");
            ConsoleOutput.WriteError("  dydo task ready-for-review <name> --summary \"Brief description of completed work\"");
            return ExitCodes.ToolError;
        }

        if (!TransitionToReviewPending(name, summary))
        {
            ConsoleOutput.WriteError($"Task not found: {name}");
            return ExitCodes.ToolError;
        }

        Console.WriteLine($"Task {name} marked ready for review");
        return ExitCodes.Success;
    }
}
