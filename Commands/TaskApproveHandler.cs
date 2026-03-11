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
        var failed = 0;

        foreach (var file in taskFiles)
        {
            var taskName = Path.GetFileNameWithoutExtension(file);
            var result = ExecuteApprove(taskName, notes);
            if (result == ExitCodes.Success)
                approved++;
            else
                failed++;
        }

        Console.WriteLine($"Approved {approved} task(s).");
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

        var assignedMatch = Regex.Match(content, @"assigned: (\w+)");
        var assigned = assignedMatch.Success ? assignedMatch.Groups[1].Value : null;

        var createdMatch = Regex.Match(content, @"created: (.+)");
        DateTime? taskCreated = null;
        if (createdMatch.Success && DateTime.TryParse(
                createdMatch.Groups[1].Value,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var dt))
            taskCreated = dt;

        var areaMatch = Regex.Match(content, @"area: ([\w-]+)");
        var area = areaMatch.Success ? areaMatch.Groups[1].Value : "general";

        var (created, modified, deleted) = CollectFileChanges(configService, assigned, taskCreated);

        PrintFileChanges(created, modified, deleted);

        content = TransformFrontmatter(content);
        content = UpdateFilesChangedSection(content, created, modified, deleted);
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

        CompactAuditSnapshots(configService);

        return ExitCodes.Success;
    }

    private static (List<string> Created, List<string> Modified, List<string> Deleted) CollectFileChanges(
        ConfigService configService, string? assigned, DateTime? taskCreated)
    {
        var created = new List<string>();
        var modified = new List<string>();
        var deleted = new List<string>();

        try
        {
            var auditService = new AuditService(configService);
            var (sessions, _) = auditService.LoadSessions();

            foreach (var session in sessions)
            {
                if (!string.IsNullOrEmpty(assigned) &&
                    !string.Equals(session.AgentName, assigned, StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var evt in session.Events)
                {
                    if (taskCreated.HasValue && evt.Timestamp < taskCreated.Value)
                        continue;

                    if (string.IsNullOrEmpty(evt.Path)) continue;

                    var normalizedPath = evt.Path.Replace('\\', '/');
                    if (normalizedPath.Contains("dydo/", StringComparison.OrdinalIgnoreCase)) continue;

                    switch (evt.EventType)
                    {
                        case AuditEventType.Write:
                            if (!created.Contains(evt.Path) && !modified.Contains(evt.Path))
                                created.Add(evt.Path);
                            break;
                        case AuditEventType.Edit:
                            if (!modified.Contains(evt.Path) && !created.Contains(evt.Path))
                                modified.Add(evt.Path);
                            break;
                        case AuditEventType.Delete:
                            created.Remove(evt.Path);
                            modified.Remove(evt.Path);
                            if (!deleted.Contains(evt.Path))
                                deleted.Add(evt.Path);
                            break;
                    }
                }
            }
        }
        catch
        {
            // Audit service failure should not block approval
        }

        return (created, modified, deleted);
    }

    private static void PrintFileChanges(List<string> created, List<string> modified, List<string> deleted)
    {
        if (created.Count > 0 || modified.Count > 0 || deleted.Count > 0)
        {
            Console.WriteLine("Files changed (from audit logs):");
            foreach (var f in created) Console.WriteLine($"  + {f}");
            foreach (var f in modified) Console.WriteLine($"  ~ {f}");
            foreach (var f in deleted) Console.WriteLine($"  - {f}");
            Console.WriteLine();
        }
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

    private static string UpdateFilesChangedSection(string content, List<string> created, List<string> modified, List<string> deleted)
    {
        if (created.Count == 0 && modified.Count == 0 && deleted.Count == 0)
            return content;

        var filesSection = "## Files Changed\n\n";
        foreach (var f in created) filesSection += $"{f} — Created\n";
        foreach (var f in modified) filesSection += $"{f} — Modified\n";
        foreach (var f in deleted) filesSection += $"{f} — Deleted\n";

        content = Regex.Replace(content, @"## Files Changed\s*\n[\s\S]*?(?=\n## |\z)",
            filesSection.TrimEnd() + "\n\n");

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

    private static void CompactAuditSnapshots(ConfigService configService)
    {
        try
        {
            var currentYearDir = Path.Combine(configService.GetAuditPath(), DateTime.UtcNow.ToString("yyyy"));
            if (Directory.Exists(currentYearDir))
            {
                var compactionResult = SnapshotCompactionService.Compact(currentYearDir);
                if (compactionResult.SessionsProcessed > 0)
                    Console.WriteLine($"Audit snapshots compacted: {compactionResult.SessionsProcessed} sessions, {compactionResult.CompressionRatio:P0} reduction.");
            }
        }
        catch
        {
            // Compaction failure should not block approval
        }
    }
}
