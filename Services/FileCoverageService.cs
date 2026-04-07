namespace DynaDocs.Services;

using System.Diagnostics;
using DynaDocs.Models;
using DynaDocs.Utils;

public class FileCoverageService : IFileCoverageService
{
    // Test seams: override to avoid spawning git in test host
    public static Func<string, List<string>>? GitLsFilesOverride;
    public static Func<string, string, DateTime, double?>? GetPercentChangeOverride;

    private readonly IAuditService _auditService;
    private readonly IConfigService _configService;

    public FileCoverageService(IAuditService? auditService = null, IConfigService? configService = null)
    {
        _auditService = auditService ?? new AuditService();
        _configService = configService ?? new ConfigService();
    }

    public FileCoverageReport GenerateReport(FileCoverageOptions options)
    {
        var projectRoot = _configService.GetProjectRoot() ?? Environment.CurrentDirectory;

        var trackedFiles = GetGitTrackedFiles(projectRoot);

        // Filter to source code files only using paths.source from dydo.json
        var config = _configService.LoadConfig();
        if (config?.Paths.Source is { Count: > 0 } sourcePatterns)
        {
            trackedFiles = trackedFiles
                .Where(f => sourcePatterns.Any(p => GlobMatcher.IsMatch(f, p)))
                .ToList();
        }

        if (options.PathFilter != null)
        {
            var filter = PathUtils.NormalizeForKey(options.PathFilter);
            trackedFiles = trackedFiles
                .Where(f => PathUtils.NormalizeForKey(f).StartsWith(filter))
                .ToList();
        }

        var groups = GetInquisitionGroups(options.SinceDays);
        var rawScores = CalculateRawScores(trackedFiles, groups, projectRoot);
        var lastReadDates = GetLastReadDates(groups, projectRoot);

        var entries = new List<FileCoverageEntry>();
        foreach (var file in trackedFiles)
        {
            var key = PathUtils.NormalizeForKey(file);
            var rawScore = rawScores.GetValueOrDefault(key, 0);
            var adjustedScore = rawScore;

            if (rawScore > 0 && lastReadDates.TryGetValue(key, out var lastRead))
            {
                var pctChange = GetPercentChange(projectRoot, file, lastRead);
                if (pctChange is > 0)
                {
                    var knockdown = (int)Math.Ceiling(rawScore * 0.5 * Math.Min(pctChange.Value / 20.0, 1.0));
                    adjustedScore = Math.Max(rawScore - knockdown, 0);
                }
            }

            var status = adjustedScore >= 7 ? "covered" : adjustedScore >= 1 ? "low" : "gap";
            entries.Add(new FileCoverageEntry(file, rawScore, adjustedScore, status));
        }

        if (options.GapsOnly)
            entries = entries.Where(e => e.Status != "covered").ToList();

        var folders = BuildFolderTree(entries);
        var staleCount = entries.Count(e => e.AdjustedScore < e.RawScore);

        return new FileCoverageReport(
            DateTime.UtcNow, options.SinceDays, folders,
            entries.Count,
            entries.Count(e => e.Status == "covered"),
            entries.Count(e => e.Status == "low"),
            entries.Count(e => e.Status == "gap"),
            staleCount);
    }

    public string RenderMarkdown(FileCoverageReport report, FileCoverageOptions options)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# File Coverage Heatmap");
        sb.AppendLine();
        sb.AppendLine($"Generated: {report.Generated:yyyy-MM-dd HH:mm} UTC");
        sb.AppendLine($"Lookback: {report.SinceDays} days");
        sb.AppendLine();
        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine($"- **Total files:** {report.TotalFiles}");
        sb.AppendLine($"- **Covered (7+):** {report.CoveredCount}");
        sb.AppendLine($"- **Low (1-6):** {report.LowCount}");
        sb.AppendLine($"- **Gaps (0):** {report.GapCount}");
        sb.AppendLine($"- **Stale (decayed):** {report.StaleCount}");
        sb.AppendLine();

        if (!options.SummaryOnly)
        {
            sb.AppendLine("## Files");
            sb.AppendLine();
            RenderFolders(sb, report.Folders, 0);
        }
        else
        {
            sb.AppendLine("## Folder Summary");
            sb.AppendLine();
            RenderFolderSummaries(sb, report.Folders, 0);
        }

        return sb.ToString();
    }

    private List<InquisitionGroup> GetInquisitionGroups(int sinceDays)
    {
        var cutoff = DateTime.UtcNow.AddDays(-sinceDays);
        var years = Enumerable.Range(cutoff.Year, DateTime.UtcNow.Year - cutoff.Year + 1)
            .Select(y => y.ToString());

        var allSessions = new List<AuditSession>();
        var seen = new HashSet<string>();
        foreach (var year in years)
        {
            var (sessions, _) = _auditService.LoadSessions(year);
            foreach (var s in sessions.Where(s => s.Started >= cutoff))
            {
                if (seen.Add(s.SessionId))
                    allSessions.Add(s);
            }
        }

        // Find inquisitor sessions (those with a Role event where role == "inquisitor")
        var inquisitorSessions = new List<(AuditSession Session, string Task)>();
        foreach (var session in allSessions)
        {
            var roleEvent = session.Events.FirstOrDefault(e =>
                e.EventType == AuditEventType.Role &&
                string.Equals(e.Role, "inquisitor", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(e.Task));

            if (roleEvent != null)
                inquisitorSessions.Add((session, roleEvent.Task!));
        }

        // Group: each inquisitor session + sessions whose task matches the prefix
        var groups = new List<InquisitionGroup>();
        foreach (var (inqSession, taskPrefix) in inquisitorSessions)
        {
            var matching = allSessions.Where(s =>
                s.SessionId == inqSession.SessionId ||
                (GetSessionTask(s) is { } task &&
                 (task == taskPrefix || task.StartsWith(taskPrefix + "-", StringComparison.OrdinalIgnoreCase))))
                .ToList();

            groups.Add(new InquisitionGroup(inqSession, taskPrefix, matching));
        }

        return groups;
    }

    private static string? GetSessionTask(AuditSession session)
    {
        return session.Events.FirstOrDefault(e => e.EventType == AuditEventType.Role)?.Task;
    }

    private static Dictionary<string, int> CalculateRawScores(
        List<string> trackedFiles,
        List<InquisitionGroup> groups,
        string projectRoot)
    {
        var trackedSet = new HashSet<string>(trackedFiles.Select(PathUtils.NormalizeForKey));
        var scores = new Dictionary<string, int>();

        foreach (var group in groups)
        {
            var fileReads = new Dictionary<string, int>();

            foreach (var session in group.AllSessions)
            {
                foreach (var evt in session.Events.Where(e => e.EventType == AuditEventType.Read && e.Path != null))
                {
                    var key = NormalizeEventPath(evt.Path!, projectRoot);
                    if (key != null && trackedSet.Contains(key))
                        fileReads[key] = fileReads.GetValueOrDefault(key, 0) + 1;
                }
            }

            // Cap contribution per group at 3
            foreach (var (file, reads) in fileReads)
            {
                scores[file] = scores.GetValueOrDefault(file, 0) + Math.Min(reads, 3);
            }
        }

        return scores;
    }

    private static Dictionary<string, DateTime> GetLastReadDates(
        List<InquisitionGroup> groups,
        string projectRoot)
    {
        var dates = new Dictionary<string, DateTime>();

        foreach (var group in groups)
        {
            foreach (var session in group.AllSessions)
            {
                foreach (var evt in session.Events.Where(e => e.EventType == AuditEventType.Read && e.Path != null))
                {
                    var key = NormalizeEventPath(evt.Path!, projectRoot);
                    if (key != null)
                    {
                        if (!dates.TryGetValue(key, out var existing) || evt.Timestamp > existing)
                            dates[key] = evt.Timestamp;
                    }
                }
            }
        }

        return dates;
    }

    private static string? NormalizeEventPath(string absolutePath, string projectRoot)
    {
        try
        {
            // Normalize backslashes so Windows audit paths resolve correctly on Linux
            var relative = Path.GetRelativePath(projectRoot, PathUtils.NormalizePath(absolutePath));
            if (relative.StartsWith("..")) return null;
            return PathUtils.NormalizeForKey(relative);
        }
        catch
        {
            return null;
        }
    }

    private static double? GetPercentChange(string projectRoot, string file, DateTime since)
    {
        if (GetPercentChangeOverride != null)
            return GetPercentChangeOverride(projectRoot, file, since);

        try
        {
            // Find the commit at or before the given date (avoids reflog dependency)
            var commitHash = RunGit(projectRoot, $"rev-list -1 --before=\"{since:yyyy-MM-ddTHH:mm:ss}\" HEAD");
            if (string.IsNullOrEmpty(commitHash)) return null;

            var numstat = RunGit(projectRoot, $"diff --numstat {commitHash} HEAD -- \"{file}\"");
            if (string.IsNullOrWhiteSpace(numstat)) return 0;

            var parts = numstat.Split('\t');
            if (parts.Length < 2) return null;
            if (!int.TryParse(parts[0], out var added) || !int.TryParse(parts[1], out var removed))
                return null;

            var totalChanges = added + removed;
            if (totalChanges == 0) return 0;

            var filePath = Path.Combine(projectRoot, file);
            if (!File.Exists(filePath)) return null;
            var lineCount = File.ReadAllLines(filePath).Length;
            if (lineCount == 0) return 100;

            return (double)totalChanges / lineCount * 100;
        }
        catch
        {
            return null;
        }
    }

    private static List<string> GetGitTrackedFiles(string projectRoot)
    {
        if (GitLsFilesOverride != null)
            return GitLsFilesOverride(projectRoot);

        var output = RunGit(projectRoot, "ls-files --full-name");
        if (output == null) return [];

        return output
            .Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(PathUtils.NormalizePath)
            .OrderBy(f => f)
            .ToList();
    }

    private static string? RunGit(string workingDir, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return null;

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(10000);

            return process.ExitCode == 0 ? output : null;
        }
        catch
        {
            return null;
        }
    }

    private static List<FolderCoverage> BuildFolderTree(List<FileCoverageEntry> entries)
    {
        var root = new FolderNode("");

        foreach (var entry in entries)
        {
            var parts = entry.RelativePath.Split('/');
            var current = root;

            for (var i = 0; i < parts.Length - 1; i++)
            {
                var folderName = parts[i];
                if (!current.Children.TryGetValue(folderName, out var child))
                {
                    child = new FolderNode(string.Join("/", parts.Take(i + 1)));
                    current.Children[folderName] = child;
                }
                current = child;
            }

            current.Files.Add(entry);
        }

        // Root-level files go into a "." folder
        if (root.Files.Count > 0)
        {
            var rootFolder = new FolderNode(".");
            rootFolder.Files.AddRange(root.Files);
            root.Children["."] = rootFolder;
            root.Files.Clear();
        }

        return root.Children.Count > 0
            ? ConvertToFolderCoverage(root.Children.Values.OrderBy(c => c.Path).ToList())
            : [];
    }

    private static List<FolderCoverage> ConvertToFolderCoverage(List<FolderNode> nodes)
    {
        return nodes.Select(node =>
        {
            var subFolders = ConvertToFolderCoverage(
                node.Children.Values.OrderBy(c => c.Path).ToList());

            var allFiles = GetAllFilesRecursive(node);
            var sortedFiles = node.Files
                .OrderBy(f => f.Status == "gap" ? 0 : f.Status == "low" ? 1 : 2)
                .ThenBy(f => f.RelativePath)
                .ToList();

            return new FolderCoverage(
                node.Path,
                sortedFiles,
                subFolders,
                allFiles.Count,
                allFiles.Count(f => f.Status == "covered"),
                allFiles.Count > 0 ? allFiles.Average(f => f.AdjustedScore) : 0);
        }).ToList();
    }

    private static List<FileCoverageEntry> GetAllFilesRecursive(FolderNode node)
    {
        var files = new List<FileCoverageEntry>(node.Files);
        foreach (var child in node.Children.Values)
            files.AddRange(GetAllFilesRecursive(child));
        return files;
    }

    private static void RenderFolders(System.Text.StringBuilder sb, List<FolderCoverage> folders, int depth)
    {
        foreach (var folder in folders)
        {
            var indent = new string(' ', depth * 2);
            sb.AppendLine($"{indent}### {folder.Path}/ ({folder.CoveredCount}/{folder.TotalFiles} covered, avg {folder.AverageScore:F1})");
            sb.AppendLine();

            foreach (var file in folder.Files)
            {
                var tag = file.Status switch { "covered" => "[covered]", "low" => "[low]", _ => "[gap]" };
                var fileName = Path.GetFileName(file.RelativePath);
                sb.AppendLine($"{indent}- {tag} `{fileName}` — score {file.AdjustedScore}");
            }

            if (folder.Files.Count > 0 && folder.SubFolders.Count > 0)
                sb.AppendLine();

            RenderFolders(sb, folder.SubFolders, depth + 1);
        }
    }

    private static void RenderFolderSummaries(System.Text.StringBuilder sb, List<FolderCoverage> folders, int depth)
    {
        foreach (var folder in folders)
        {
            var indent = new string(' ', depth * 2);
            sb.AppendLine($"{indent}- **{folder.Path}/** — {folder.CoveredCount}/{folder.TotalFiles} covered (avg {folder.AverageScore:F1})");
            RenderFolderSummaries(sb, folder.SubFolders, depth + 1);
        }
    }

    private record InquisitionGroup(
        AuditSession InquisitorSession,
        string TaskPrefix,
        List<AuditSession> AllSessions);

    private class FolderNode(string path)
    {
        public string Path { get; } = path;
        public Dictionary<string, FolderNode> Children { get; } = new();
        public List<FileCoverageEntry> Files { get; } = [];
    }
}
