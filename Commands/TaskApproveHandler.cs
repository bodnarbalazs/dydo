namespace DynaDocs.Commands;

using System.Text.RegularExpressions;
using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

internal static class TaskApproveHandler
{
    public static int ExecuteApproveAll(string? notes)
    {
        var tasksPath = TaskCommand.GetTasksPath();
        if (!Directory.Exists(tasksPath))
        {
            Console.WriteLine("No tasks to approve.");
            return ExitCodes.Success;
        }

        var taskFiles = Directory.GetFiles(tasksPath, "*.md")
            .Where(f => !Path.GetFileName(f).StartsWith('_'))
            .ToList();

        if (taskFiles.Count == 0)
        {
            Console.WriteLine("No tasks to approve.");
            return ExitCodes.Success;
        }

        var approved = 0;
        var skipped = 0;
        var failed = 0;
        var workingAgents = new AgentRegistry().GetAllAgentStates()
            .Where(agent => agent.Status == AgentStatus.Working)
            .ToList();

        foreach (var file in taskFiles)
        {
            var taskName = Path.GetFileNameWithoutExtension(file);
            var fields = FrontmatterParser.ParseFields(File.ReadAllText(file));
            var status = fields != null && fields.TryGetValue("status", out var parsedStatus)
                ? parsedStatus
                : "unknown";

            if (!string.Equals(status, "human-reviewed", StringComparison.Ordinal))
            {
                Console.WriteLine($"Skipped {taskName} (status: {status})");
                skipped++;
                continue;
            }

            var claimingAgent = workingAgents.FirstOrDefault(agent =>
                string.Equals(agent.Task, taskName, StringComparison.OrdinalIgnoreCase));
            if (claimingAgent != null)
            {
                Console.WriteLine($"Skipped {taskName} (claimed by {claimingAgent.Name})");
                skipped++;
                continue;
            }

            var result = ExecuteApprove(taskName, notes);
            if (result == ExitCodes.Success)
                approved++;
            else
                failed++;
        }

        Console.WriteLine($"Approved {approved}, skipped {skipped}.");
        if (failed > 0)
            ConsoleOutput.WriteError($"Failed to approve {failed} task(s).");

        return failed > 0 ? ExitCodes.ToolError : ExitCodes.Success;
    }

    public static int ExecuteApprove(string name, string? notes)
    {
        var configService = new ConfigService();
        var tasksPath = TaskCommand.GetTasksPath();
        var sanitizedName = PathUtils.SanitizeForFilename(name);
        var taskPath = Path.Combine(tasksPath, $"{sanitizedName}.md");

        if (!File.Exists(taskPath))
        {
            ConsoleOutput.WriteError($"Task not found: {name}");
            return ExitCodes.ToolError;
        }

        var content = File.ReadAllText(taskPath);

        content = TransformFrontmatter(content);
        content = AddApprovalSection(content, notes);

        var changelogFilePath = GetChangelogPath(configService, sanitizedName);
        if (changelogFilePath == null)
        {
            ConsoleOutput.WriteError($"A changelog entry named '{name}' already exists for today.");
            return ExitCodes.ToolError;
        }

        File.WriteAllText(changelogFilePath, content);
        File.Delete(taskPath);

        var changelogPath = configService.GetChangelogPath();
        EnsureChangelogHubs(changelogPath, configService);

        Console.WriteLine($"Task {name} approved.");
        var relativeChangelogPath = Path.Combine("project", "changelog", DateTime.UtcNow.ToString("yyyy"),
            DateTime.UtcNow.ToString("yyyy-MM-dd"), $"{sanitizedName}.md");
        Console.WriteLine($"Changelog entry created: {relativeChangelogPath}");
        Console.WriteLine("Hub files updated.");

        return ExitCodes.Success;
    }

    private static string TransformFrontmatter(string content)
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        content = Regex.Replace(content, @"(?m)^name: .+\n", "");
        content = Regex.Replace(content, @"(?m)^status: .+\n", "");
        content = Regex.Replace(content, @"(?m)^created: .+\n", "");
        content = Regex.Replace(content, @"(?m)^assigned: .+\n", "");
        content = Regex.Replace(content, @"(?m)^updated: .+\n", "");
        content = Regex.Replace(content, @"(?m)^(area: .+)$", $"$1\ntype: changelog\ndate: {today}");
        return content;
    }


    private static string AddApprovalSection(string content, string? notes)
    {
        var approvalSection = $"## Approval\n\n- Approved: {DateTime.UtcNow:yyyy-MM-dd HH:mm}";
        if (!string.IsNullOrEmpty(notes))
            approvalSection += $"\n- Notes: {notes}";
        return content.TrimEnd() + "\n\n" + approvalSection + "\n";
    }

    private static string? GetChangelogPath(ConfigService configService, string sanitizedName)
    {
        var changelogPath = configService.GetChangelogPath();
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var datePath = Path.Combine(changelogPath, DateTime.UtcNow.ToString("yyyy"), today);
        Directory.CreateDirectory(datePath);

        var changelogFilePath = Path.Combine(datePath, $"{sanitizedName}.md");
        if (File.Exists(changelogFilePath))
            return null;

        return changelogFilePath;
    }

    /// <summary>
    /// Regenerate changelog hub files using HubGenerator so they match 'dydo fix' output.
    /// </summary>
    private static void EnsureChangelogHubs(string changelogPath, ConfigService configService)
    {
        var basePath = configService.GetDocsPath();

        var allDocs = new List<DocFile>();
        foreach (var file in Directory.GetFiles(changelogPath, "*.md", SearchOption.AllDirectories))
        {
            var fileName = Path.GetFileName(file);
            var relativePath = PathUtils.NormalizePath(Path.GetRelativePath(basePath, file));
            allDocs.Add(new DocFile
            {
                FilePath = file,
                RelativePath = relativePath,
                FileName = fileName,
                Content = ""
            });
        }

        var changelogRelPath = PathUtils.NormalizePath(Path.GetRelativePath(basePath, changelogPath));
        var hubs = HubGenerator.GenerateAllHubs(basePath, allDocs);
        foreach (var (hubRelPath, content) in hubs)
        {
            if (!hubRelPath.StartsWith(changelogRelPath + "/", StringComparison.OrdinalIgnoreCase))
                continue;

            var hubFullPath = Path.Combine(basePath, hubRelPath);
            Directory.CreateDirectory(Path.GetDirectoryName(hubFullPath)!);
            File.WriteAllText(hubFullPath, content);
        }
    }

}
