namespace DynaDocs.Services;

using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using DynaDocs.Models;
using DynaDocs.Serialization;

/// <summary>
/// Service for logging and retrieving audit events.
/// Audit logs are stored in dydo/_system/audit/YYYY/yyyy-mm-dd-sessionid.json
/// </summary>
public partial class AuditService : IAuditService
{
    private const int MaxSessionFiles = 10000;
    private readonly IConfigService _configService;
    private readonly string _basePath;

    // Cache of active sessions to avoid repeated file reads during a guard invocation
    private readonly Dictionary<string, AuditSession> _sessionCache = new();

    public AuditService(IConfigService? configService = null, string? basePath = null)
    {
        _configService = configService ?? new ConfigService();
        _basePath = basePath ?? Environment.CurrentDirectory;
    }

    public string GetAuditPath() => _configService.GetAuditPath(_basePath);

    public void EnsureAuditFolder()
    {
        var auditPath = GetAuditPath();
        Directory.CreateDirectory(auditPath);
        Directory.CreateDirectory(Path.Combine(auditPath, "reports"));
    }

    public void LogEvent(string sessionId, AuditEvent @event, string? agentName = null, string? human = null, ProjectSnapshot? snapshot = null)
    {
        if (string.IsNullOrEmpty(sessionId))
            return;

        // Set timestamp if not already set
        if (@event.Timestamp == default)
            @event.Timestamp = DateTime.UtcNow;

        // If session is in our in-memory cache (same process, multiple calls), use full write
        if (_sessionCache.TryGetValue(sessionId, out var cached))
        {
            if (!string.IsNullOrEmpty(agentName) && string.IsNullOrEmpty(cached.AgentName))
                cached.AgentName = agentName;
            if (!string.IsNullOrEmpty(human) && string.IsNullOrEmpty(cached.Human))
                cached.Human = human;
            if (snapshot != null && cached.Snapshot == null)
                cached.Snapshot = snapshot;
            cached.Events.Add(@event);
            WriteSession(cached);
            return;
        }

        // Cross-process fast path: session file exists on disk, append only the new event (O(1))
        var yearDir = FindSessionYearDir(sessionId);
        if (yearDir != null)
        {
            AppendEventToSidecar(yearDir, sessionId, @event);
            return;
        }

        // New session — create full session and write it
        var session = new AuditSession
        {
            SessionId = sessionId,
            AgentName = agentName,
            Human = human,
            Started = DateTime.UtcNow,
            GitHead = GetCurrentGitHead(),
            Snapshot = snapshot,
            Events = [@event]
        };
        _sessionCache[sessionId] = session;
        WriteSession(session);
    }

    public AuditSession? GetSession(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
            return null;

        // Check cache first
        if (_sessionCache.TryGetValue(sessionId, out var cached))
            return cached;

        // Search for session file
        var auditPath = GetAuditPath();
        if (!Directory.Exists(auditPath))
            return null;

        // Session ID is the last part of the filename: yyyy-mm-dd-{sessionId}.json
        foreach (var yearDir in Directory.GetDirectories(auditPath))
        {
            var pattern = $"*-{sessionId}.json";
            var files = Directory.GetFiles(yearDir, pattern);
            if (files.Length > 0)
            {
                var session = LoadSessionFile(files[0]);
                if (session != null)
                    MergeSidecarEvents(yearDir, sessionId, session);
                return session;
            }
        }

        return null;
    }

    public (IReadOnlyList<AuditSession> Sessions, bool LimitReached) LoadSessions(string? yearFilter = null)
    {
        var sessions = new List<AuditSession>();
        var files = ListSessionFiles(yearFilter);
        var limitReached = files.Count >= MaxSessionFiles;

        foreach (var file in files.Take(MaxSessionFiles))
        {
            var session = LoadSessionFile(file);
            if (session != null)
            {
                var yearDir = Path.GetDirectoryName(file)!;
                MergeSidecarEvents(yearDir, session.SessionId, session);
                sessions.Add(session);
            }
        }

        return (sessions, limitReached);
    }

    public IReadOnlyList<string> ListSessionFiles(string? yearFilter = null)
    {
        var auditPath = GetAuditPath();
        if (!Directory.Exists(auditPath))
            return [];

        var files = new List<string>();

        // Get year directories to search
        IEnumerable<string> yearDirs;
        if (!string.IsNullOrEmpty(yearFilter))
        {
            // Filter to specific year
            var yearPath = Path.Combine(auditPath, yearFilter.TrimStart('/'));
            yearDirs = Directory.Exists(yearPath) ? [yearPath] : [];
        }
        else
        {
            // All year directories
            yearDirs = Directory.GetDirectories(auditPath)
                .Where(d => YearFolderRegex().IsMatch(Path.GetFileName(d)));
        }

        foreach (var yearDir in yearDirs)
        {
            files.AddRange(Directory.GetFiles(yearDir, "*.json")
                .Where(f => !Path.GetFileName(f).StartsWith("_baseline-")));
        }

        // Sort by filename (which includes date) in descending order (newest first)
        return files.OrderByDescending(f => Path.GetFileName(f)).ToList();
    }

    private string? FindSessionYearDir(string sessionId)
    {
        var auditPath = GetAuditPath();
        if (!Directory.Exists(auditPath))
            return null;

        foreach (var yearDir in Directory.GetDirectories(auditPath))
        {
            if (Directory.GetFiles(yearDir, $"*-{sessionId}.json").Length > 0)
                return yearDir;
        }
        return null;
    }

    private static string SidecarPath(string yearDir, string sessionId)
        => Path.Combine(yearDir, $"{sessionId}.events");

    private static void AppendEventToSidecar(string yearDir, string sessionId, AuditEvent @event)
    {
        var json = JsonSerializer.Serialize(@event, CompactJsonContext.Default.AuditEvent);
        File.AppendAllText(SidecarPath(yearDir, sessionId), json + "\n");
    }

    internal static void MergeSidecarEvents(string yearDir, string sessionId, AuditSession session)
    {
        var sidecarPath = SidecarPath(yearDir, sessionId);
        if (!File.Exists(sidecarPath)) return;

        foreach (var line in File.ReadLines(sidecarPath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var evt = JsonSerializer.Deserialize(line, CompactJsonContext.Default.AuditEvent);
                if (evt != null)
                    session.Events.Add(evt);
            }
            catch { }
        }
    }

    private void WriteSession(AuditSession session)
    {
        var auditPath = GetAuditPath();

        // Determine year folder
        var year = session.Started.Year.ToString();
        var yearPath = Path.Combine(auditPath, year);
        Directory.CreateDirectory(yearPath);

        // Generate filename: yyyy-mm-dd-sessionid.json
        var date = session.Started.ToString("yyyy-MM-dd");
        var filename = $"{date}-{session.SessionId}.json";
        var filePath = Path.Combine(yearPath, filename);

        // Write atomically using temp file + rename
        var tempPath = filePath + ".tmp";
        try
        {
            var json = JsonSerializer.Serialize(session, DydoDefaultJsonContext.Default.AuditSession);
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, filePath, overwrite: true);

            // Clean up sidecar — events are now in the main file
            var sidecarPath = SidecarPath(yearPath, session.SessionId);
            if (File.Exists(sidecarPath))
            {
                try { File.Delete(sidecarPath); } catch { }
            }
        }
        catch
        {
            // Clean up temp file if it exists
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
            throw;
        }
    }

    private static AuditSession? LoadSessionFile(string filePath, bool mergeSidecar = false)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            var session = JsonSerializer.Deserialize(json, DydoDefaultJsonContext.Default.AuditSession);
            if (session != null && mergeSidecar)
            {
                var yearDir = Path.GetDirectoryName(filePath)!;
                MergeSidecarEvents(yearDir, session.SessionId, session);
            }
            return session;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[dydo] WARNING: Failed to load audit session {Path.GetFileName(filePath)}: {ex.Message}");
            return null;
        }
    }

    private static string? GetCurrentGitHead()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "rev-parse --short HEAD",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                return null;

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(1000);

            return process.ExitCode == 0 && !string.IsNullOrEmpty(output) ? output : null;
        }
        catch
        {
            return null;
        }
    }

    [GeneratedRegex(@"^\d{4}$")]
    private static partial Regex YearFolderRegex();
}
