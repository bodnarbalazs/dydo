namespace DynaDocs.Commands;

using System.Text.RegularExpressions;
using DynaDocs.Utils;

internal static class TaskListHandler
{
    public static int Execute(bool needsReview, bool all)
    {
        var tasksPath = TaskCommand.GetTasksPath();
        if (!Directory.Exists(tasksPath))
        {
            Console.WriteLine("No tasks found");
            return ExitCodes.Success;
        }

        var tasks = new List<(string Name, string Status, string? Assigned, DateTime Created)>();

        foreach (var file in Directory.GetFiles(tasksPath, "*.md"))
        {
            var parsed = ParseTaskFile(file);
            if (parsed == null) continue;

            var (name, status, assigned, created) = parsed.Value;

            if (!all && status == "closed") continue;
            if (needsReview && status != "review-pending") continue;

            tasks.Add((name, status, assigned, created));
        }

        if (tasks.Count == 0)
        {
            Console.WriteLine(needsReview ? "No tasks awaiting review" : "No tasks found");
            return ExitCodes.Success;
        }

        Console.WriteLine($"{"Task",-25} {"Status",-15} {"Assigned",-10} {"Created",-12}");
        Console.WriteLine(new string('-', 65));

        foreach (var (name, status, assigned, created) in tasks.OrderByDescending(t => t.Created))
        {
            PrintTaskRow(name, status, assigned, created);
        }

        return ExitCodes.Success;
    }

    private static (string Name, string Status, string? Assigned, DateTime Created)? ParseTaskFile(string file)
    {
        var fileName = Path.GetFileName(file);
        if (fileName.StartsWith('_')) return null;

        var content = File.ReadAllText(file);
        var name = Path.GetFileNameWithoutExtension(file);

        var statusMatch = Regex.Match(content, @"status: ([\w-]+)");
        var status = statusMatch.Success ? statusMatch.Groups[1].Value : "unknown";

        var assignedMatch = Regex.Match(content, @"assigned: (\w+)");
        var assigned = assignedMatch.Success ? assignedMatch.Groups[1].Value : null;

        var createdMatch = Regex.Match(content, @"created: (.+)");
        var created = DateTime.UtcNow;
        if (createdMatch.Success && DateTime.TryParse(createdMatch.Groups[1].Value, out var dt))
            created = dt;

        return (name, status, assigned, created);
    }

    private static void PrintTaskRow(string name, string status, string? assigned, DateTime created)
    {
        var displayName = name.Length > 23 ? name[..23] + ".." : name;
        Console.WriteLine($"{displayName,-25} {status,-15} {assigned ?? "-",-10} {created:yyyy-MM-dd}");
    }
}
