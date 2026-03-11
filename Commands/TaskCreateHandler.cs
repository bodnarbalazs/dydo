namespace DynaDocs.Commands;

using System.Text.RegularExpressions;
using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

internal static class TaskCreateHandler
{
    public static int Execute(string name, string? description, string area)
    {
        if (!Frontmatter.ValidAreas.Contains(area))
        {
            ConsoleOutput.WriteError($"Invalid area '{area}'. Must be one of: {string.Join(", ", Frontmatter.ValidAreas)}");
            return ExitCodes.ToolError;
        }

        var registry = new AgentRegistry();
        var sessionId = registry.GetSessionContext();
        var agent = registry.GetCurrentAgent(sessionId);

        var tasksPath = TaskCommand.GetTasksPath();
        Directory.CreateDirectory(tasksPath);

        var sanitizedName = PathUtils.SanitizeForFilename(name);
        if (sanitizedName != name)
        {
            Console.WriteLine($"  Warning: Task name sanitized for filesystem safety.");
            Console.WriteLine($"    Original: \"{name}\"");
            Console.WriteLine($"    Filename: \"{sanitizedName}\"");
        }

        var taskPath = Path.Combine(tasksPath, $"{sanitizedName}.md");
        if (File.Exists(taskPath))
        {
            ConsoleOutput.WriteError($"Task already exists: {name}");
            return ExitCodes.ToolError;
        }

        var configService = new ConfigService();
        var todayChangelogDir = Path.Combine(configService.GetChangelogPath(),
            DateTime.UtcNow.ToString("yyyy"), DateTime.UtcNow.ToString("yyyy-MM-dd"));
        if (File.Exists(Path.Combine(todayChangelogDir, $"{sanitizedName}.md")))
        {
            ConsoleOutput.WriteError($"A changelog entry named '{name}' already exists for today. Choose a different name.");
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
}
