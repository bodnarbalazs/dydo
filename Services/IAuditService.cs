namespace DynaDocs.Services;

using DynaDocs.Models;

/// <summary>
/// Service for logging and retrieving audit events.
/// Audit logs track all agent file access, modifications, and commands.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Log an event for a session. Creates the session file if it doesn't exist.
    /// </summary>
    /// <param name="sessionId">The session ID from the AI assistant</param>
    /// <param name="event">The event to log</param>
    /// <param name="agentName">Optional agent name (for session metadata)</param>
    /// <param name="human">Optional human name (for session metadata)</param>
    /// <param name="snapshot">Optional project snapshot (only stored on session creation)</param>
    void LogEvent(string sessionId, AuditEvent @event, string? agentName = null, string? human = null, ProjectSnapshot? snapshot = null);

    /// <summary>
    /// Get a specific session by ID.
    /// </summary>
    AuditSession? GetSession(string sessionId);

    /// <summary>
    /// Load all sessions, optionally filtered by year.
    /// </summary>
    /// <param name="yearFilter">Optional year to filter (e.g., "2025")</param>
    /// <returns>List of sessions, limited to 10000 files</returns>
    (IReadOnlyList<AuditSession> Sessions, bool LimitReached) LoadSessions(string? yearFilter = null);

    /// <summary>
    /// List available session files without loading full content.
    /// </summary>
    /// <param name="yearFilter">Optional year to filter</param>
    IReadOnlyList<string> ListSessionFiles(string? yearFilter = null);

    /// <summary>
    /// Get the audit folder path.
    /// </summary>
    string GetAuditPath();

    /// <summary>
    /// Ensure the audit folder structure exists.
    /// </summary>
    void EnsureAuditFolder();
}
