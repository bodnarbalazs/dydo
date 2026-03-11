namespace DynaDocs.Commands;

using System.Text.RegularExpressions;
using DynaDocs.Utils;

internal static class IssueListHandler
{
    public static int Execute(string? areaFilter, string? statusFilter, bool all)
    {
        var issuesPath = IssueCommand.GetIssuesPath();
        var issues = new List<(int Id, string Severity, string Area, string Status, string Date, string Title)>();

        ScanIssues(issuesPath, issues);
        if (all)
            ScanIssues(Path.Combine(issuesPath, "resolved"), issues);

        if (areaFilter != null)
            issues.RemoveAll(i => !i.Area.Equals(areaFilter, StringComparison.OrdinalIgnoreCase));
        if (statusFilter != null)
            issues.RemoveAll(i => !i.Status.Equals(statusFilter, StringComparison.OrdinalIgnoreCase));

        if (issues.Count == 0)
        {
            Console.WriteLine("No issues found.");
            return ExitCodes.Success;
        }

        Console.WriteLine($"{"ID",-6} {"Severity",-10} {"Area",-12} {"Status",-10} {"Date",-12} Title");
        Console.WriteLine(new string('-', 75));

        foreach (var (id, sev, area, status, date, title) in issues.OrderByDescending(i => i.Id))
        {
            var displayTitle = title.Length > 30 ? title[..28] + ".." : title;
            Console.WriteLine($"#{id,-5} {sev,-10} {area,-12} {status,-10} {date,-12} {displayTitle}");
        }

        return ExitCodes.Success;
    }

    private static void ScanIssues(string dir, List<(int Id, string Severity, string Area, string Status, string Date, string Title)> issues)
    {
        if (!Directory.Exists(dir)) return;

        foreach (var file in Directory.GetFiles(dir, "*.md"))
        {
            var content = File.ReadAllText(file);

            var idMatch = Regex.Match(content, @"(?m)^id: (\d+)");
            if (!idMatch.Success) continue;
            var id = int.Parse(idMatch.Groups[1].Value);

            var sevMatch = Regex.Match(content, @"(?m)^severity: (\w+)");
            var severity = sevMatch.Success ? sevMatch.Groups[1].Value : "unknown";

            var areaMatch = Regex.Match(content, @"(?m)^area: ([\w-]+)");
            var area = areaMatch.Success ? areaMatch.Groups[1].Value : "unknown";

            var statusMatch = Regex.Match(content, @"(?m)^status: ([\w-]+)");
            var status = statusMatch.Success ? statusMatch.Groups[1].Value : "unknown";

            var dateMatch = Regex.Match(content, @"(?m)^date: ([\d-]+)");
            var date = dateMatch.Success ? dateMatch.Groups[1].Value : "";

            var titleMatch = Regex.Match(content, @"(?m)^# (.+)$");
            var title = titleMatch.Success ? titleMatch.Groups[1].Value.Trim() : Path.GetFileNameWithoutExtension(file);

            issues.Add((id, severity, area, status, date, title));
        }
    }
}
