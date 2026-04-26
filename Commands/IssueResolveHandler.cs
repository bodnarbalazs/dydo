namespace DynaDocs.Commands;

using System.Text.RegularExpressions;
using DynaDocs.Utils;

internal static class IssueResolveHandler
{
    public static int Execute(int id, string summary)
    {
        var issuesPath = IssueCommand.GetIssuesPath();
        var resolvedPath = Path.Combine(issuesPath, "resolved");

        // Open dir wins: a duplicate ID in resolved/ is a stale-state artifact (historical
        // create-side bug, manual file edits) and must not block the resolve. Surface it as
        // a warning so the duplicate gets renumbered.
        var issueFile = FindIssueById(issuesPath, id);
        var resolvedFile = FindIssueById(resolvedPath, id);

        if (issueFile == null)
        {
            if (resolvedFile != null)
            {
                ConsoleOutput.WriteError($"Issue #{id} is already resolved.");
                return ExitCodes.ToolError;
            }

            ConsoleOutput.WriteError($"Issue #{id} not found.");
            return ExitCodes.ToolError;
        }

        if (resolvedFile != null)
        {
            ConsoleOutput.WriteWarning(
                $"Warning: Issue #{id} also exists in resolved/ ({Path.GetFileName(resolvedFile)}). " +
                $"Resolving the open file ({Path.GetFileName(issueFile)}); the duplicate ID should be renumbered.");
        }

        var content = File.ReadAllText(issueFile);

        content = Regex.Replace(content, @"(?m)^status: [\w-]+", "status: resolved");
        content = Regex.Replace(content, @"(date: [\d-]+)", $"$1\nresolved-date: {DateTime.UtcNow:yyyy-MM-dd}");
        content = Regex.Replace(content, @"## Resolution\s+\(Filled when resolved\)", $"## Resolution\n\n{summary}");

        Directory.CreateDirectory(resolvedPath);
        var destPath = Path.Combine(resolvedPath, Path.GetFileName(issueFile));
        File.WriteAllText(destPath, content);
        File.Delete(issueFile);

        var fileName = Path.GetFileName(issueFile);
        Console.WriteLine($"Resolved issue #{id}: {fileName}");

        return ExitCodes.Success;
    }

    private static string? FindIssueById(string dir, int id)
    {
        if (!Directory.Exists(dir)) return null;

        var prefix = $"{id:D4}-";
        foreach (var file in Directory.GetFiles(dir, "*.md"))
        {
            if (Path.GetFileName(file).StartsWith(prefix))
                return file;
        }

        return null;
    }
}
