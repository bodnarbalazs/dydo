namespace DynaDocs.Services;

using DynaDocs.Models;

/// <summary>
/// Represents a merged event from multiple sessions for timeline visualization.
/// Public version for testability — mirrors the internal MergedEvent in AuditCommand.
/// </summary>
public class TimelineEntry
{
    public DateTime Timestamp { get; set; }
    public string Agent { get; set; } = "";
    public string EventType { get; set; } = "";
    public string? Path { get; set; }
    public string? Command { get; set; }
    public string? Role { get; set; }
    public string? Task { get; set; }
}

/// <summary>
/// Pure logic for audit visualization — agent color assignment,
/// timeline merging, and combined snapshot building.
/// Extracted from AuditCommand to enable unit testing.
/// </summary>
public static class AuditVisualizationService
{
    public static readonly string[] AgentColors =
    [
        "#FF6B6B", "#4ECDC4", "#45B7D1", "#96CEB4",
        "#FFEAA7", "#DDA0DD", "#98D8C8", "#F7DC6F"
    ];

    public static Dictionary<string, string> AssignAgentColors(IReadOnlyList<AuditSession> sessions)
    {
        var colors = new Dictionary<string, string>();
        var colorIndex = 0;

        foreach (var session in sessions)
        {
            var agent = session.AgentName ?? session.SessionId;
            if (!colors.ContainsKey(agent))
            {
                colors[agent] = AgentColors[colorIndex % AgentColors.Length];
                colorIndex++;
            }
        }

        return colors;
    }

    public static List<TimelineEntry> MergeTimelines(IReadOnlyList<AuditSession> sessions)
    {
        var merged = new List<TimelineEntry>();

        foreach (var session in sessions)
        {
            var agent = session.AgentName ?? session.SessionId;
            foreach (var evt in session.Events)
            {
                merged.Add(new TimelineEntry
                {
                    Timestamp = evt.Timestamp,
                    Agent = agent,
                    EventType = evt.EventType.ToString(),
                    Path = evt.Path,
                    Command = evt.Command,
                    Role = evt.Role,
                    Task = evt.Task
                });
            }
        }

        return merged.OrderBy(e => e.Timestamp).ToList();
    }

    public static ProjectSnapshot BuildCombinedSnapshot(
        IReadOnlyList<AuditSession> sessions,
        Func<string, SnapshotBaseline?> loadBaseline,
        Func<string, AuditSession?> loadSession)
    {
        var resolveCache = new Dictionary<string, ProjectSnapshot>();
        var combined = new ProjectSnapshot { GitCommit = "unknown" };
        var allFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allLinks = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var session in sessions)
        {
            var snapshot = SnapshotCompactionService.ResolveSnapshot(
                session, loadBaseline, loadSession, resolveCache);
            if (snapshot == null) continue;

            if (combined.GitCommit == "unknown")
                combined.GitCommit = snapshot.GitCommit;

            foreach (var file in snapshot.Files)
                allFiles.Add(file);
            foreach (var folder in snapshot.Folders)
                allFolders.Add(folder);
            foreach (var (source, targets) in snapshot.DocLinks)
            {
                if (!allLinks.ContainsKey(source))
                    allLinks[source] = new List<string>();
                foreach (var target in targets)
                {
                    if (!allLinks[source].Contains(target, StringComparer.OrdinalIgnoreCase))
                        allLinks[source].Add(target);
                }
            }
        }

        combined.Files = allFiles.OrderBy(f => f).ToList();
        combined.Folders = allFolders.OrderBy(f => f).ToList();
        combined.DocLinks = allLinks;

        return combined;
    }

    public static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes / (1024.0 * 1024.0):F1} MB"
    };

    public static string TruncateCommand(string command, int maxLength = 50)
    {
        if (command.Length <= maxLength)
            return command;
        return command[..maxLength] + "...";
    }

    public static string FormatEventDetails(AuditEvent e)
    {
        return e.EventType switch
        {
            AuditEventType.Read or AuditEventType.Write or AuditEventType.Edit or AuditEventType.Delete
                => e.Path ?? "",
            AuditEventType.Bash => TruncateCommand(e.Command ?? ""),
            AuditEventType.Role => $"{e.Role}" + (e.Task != null ? $" on {e.Task}" : ""),
            AuditEventType.Claim or AuditEventType.Release => e.AgentName ?? "",
            AuditEventType.Commit => $"{e.CommitHash} {TruncateCommand(e.CommitMessage ?? "")}",
            AuditEventType.Blocked => $"{e.Path ?? e.Command} - {e.BlockReason}",
            _ => ""
        };
    }
}
