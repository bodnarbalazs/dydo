namespace DynaDocs.Commands;

using System.Text.RegularExpressions;
using DynaDocs.Utils;

internal static class TaskDoneHandler
{
    public static int Execute(string name)
    {
        var taskPath = Path.Combine(TaskCommand.GetTasksPath(), $"{PathUtils.SanitizeForFilename(name)}.md");
        if (!File.Exists(taskPath))
        {
            ConsoleOutput.WriteError($"Task not found: {name}");
            return ExitCodes.ToolError;
        }

        var content = File.ReadAllText(taskPath);
        var status = GetFrontmatterValue(content, "status") ?? "unknown";
        if (status is not ("in-progress" or "in-review"))
        {
            ConsoleOutput.WriteError($"Task {name} cannot be marked done from status '{status}'. It must be in-progress or in-review.");
            return ExitCodes.ToolError;
        }

        content = Regex.Replace(content, @"status: [\w-]+", "status: done");
        File.WriteAllText(taskPath, content);
        Console.WriteLine($"Task {name} marked done");
        return ExitCodes.Success;
    }

    private static string? GetFrontmatterValue(string content, string key)
    {
        var match = Regex.Match(content, $@"^{Regex.Escape(key)}:\s*(.+)$", RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }
}
