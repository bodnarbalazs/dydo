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

        // Get or create session
        var session = GetOrCreateSession(sessionId, agentName, human);

        // Update session metadata if provided
        if (!string.IsNullOrEmpty(agentName) && string.IsNullOrEmpty(session.AgentName))
            session.AgentName = agentName;
        if (!string.IsNullOrEmpty(human) && string.IsNullOrEmpty(session.Human))
            session.Human = human;

        // Store snapshot only on first event (when session was just created)
        if (snapshot != null && session.Snapshot == null)
            session.Snapshot = snapshot;

        // Add event
        session.Events.Add(@event);

        // Write session file
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
                return LoadSessionFile(files[0]);
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
                sessions.Add(session);
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
            files.AddRange(Directory.GetFiles(yearDir, "*.json"));
        }

        // Sort by filename (which includes date) in descending order (newest first)
        return files.OrderByDescending(f => Path.GetFileName(f)).ToList();
    }

    private AuditSession GetOrCreateSession(string sessionId, string? agentName, string? human)
    {
        // Check cache
        if (_sessionCache.TryGetValue(sessionId, out var cached))
            return cached;

        // Try to load existing session
        var existing = GetSession(sessionId);
        if (existing != null)
        {
            _sessionCache[sessionId] = existing;
            return existing;
        }

        // Create new session
        var now = DateTime.UtcNow;
        var session = new AuditSession
        {
            SessionId = sessionId,
            AgentName = agentName,
            Human = human,
            Started = now,
            GitHead = GetCurrentGitHead(),
            Events = []
        };

        _sessionCache[sessionId] = session;
        return session;
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

    private AuditSession? LoadSessionFile(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize(json, DydoDefaultJsonContext.Default.AuditSession);
        }
        catch
        {
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
