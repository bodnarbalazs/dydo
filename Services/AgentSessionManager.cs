namespace DynaDocs.Services;

using System.Text.Json;
using DynaDocs.Models;
using DynaDocs.Serialization;
using DynaDocs.Utils;

public class AgentSessionManager
{
    private readonly Func<string, string> _getAgentWorkspace;
    private readonly string _agentsPath;
    private readonly IReadOnlyList<string> _agentNames;
    private readonly Func<string, bool> _isValidAgentName;
    private readonly Func<string, AgentState?> _getAgentState;

    public AgentSessionManager(
        Func<string, string> getAgentWorkspace,
        string agentsPath,
        IReadOnlyList<string> agentNames,
        Func<string, bool> isValidAgentName,
        Func<string, AgentState?> getAgentState)
    {
        _getAgentWorkspace = getAgentWorkspace;
        _agentsPath = agentsPath;
        _agentNames = agentNames;
        _isValidAgentName = isValidAgentName;
        _getAgentState = getAgentState;
    }

    private string GetPendingSessionPath(string agentName) =>
        Path.Combine(_getAgentWorkspace(agentName), ".pending-session");

    private string GetSessionContextPath() =>
        Path.Combine(_agentsPath, ".session-context");

    public string GetAgentHintPath() =>
        Path.Combine(_agentsPath, ".session-agent");

    public AgentSession? GetSession(string agentName)
    {
        var sessionPath = Path.Combine(_getAgentWorkspace(agentName), ".session");
        if (!File.Exists(sessionPath))
            return null;

        try
        {
            var json = FileReadRetry.Read(sessionPath);
            if (json == null) return null;
            return JsonSerializer.Deserialize(json, DydoDefaultJsonContext.Default.AgentSession);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the current agent for a given session ID.
    /// Uses a hint file to avoid scanning all agents when possible.
    /// </summary>
    public AgentState? GetCurrentAgent(string? sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
            return null;

        // Fast path: check agent hint file
        var hintPath = GetAgentHintPath();
        if (File.Exists(hintPath))
        {
            try
            {
                var hint = FileReadRetry.Read(hintPath)?.Trim();
                if (!string.IsNullOrEmpty(hint) && _isValidAgentName(hint))
                {
                    var session = GetSession(hint);
                    if (session?.SessionId == sessionId)
                        return _getAgentState(hint);
                }
            }
            catch { }
        }

        // Slow path: scan all agents with timeout guard
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            foreach (var name in _agentNames)
            {
                cts.Token.ThrowIfCancellationRequested();
                var session = GetSession(name);
                if (session?.SessionId == sessionId)
                {
                    try { File.WriteAllText(hintPath, name); } catch { }
                    return _getAgentState(name);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("dydo whoami timed out — likely filesystem contention from concurrent agents. Try again.");
        }

        return null;
    }

    /// <summary>
    /// Gets and clears the pending session ID for an agent.
    /// </summary>
    public string? GetPendingSessionId(string agentName)
    {
        var pending = GetPendingSession(agentName);
        return pending?.SessionId;
    }

    internal (string SessionId, string Host)? GetPendingSession(string agentName)
    {
        var path = GetPendingSessionPath(agentName);
        if (!File.Exists(path)) return null;

        try
        {
            var content = FileReadRetry.Read(path);
            File.Delete(path);
            var (sessionId, host) = ParsePendingSession(content ?? "");
            return string.IsNullOrEmpty(sessionId) ? null : (sessionId, host);
        }
        catch
        {
            return null;
        }
    }

    internal static (string? SessionId, string Host) ParsePendingSession(string content)
    {
        var lines = content.Replace("\r\n", "\n").Split('\n');
        var sessionId = lines.Length > 0 ? lines[0].Trim() : null;
        var host = lines.Length > 1 ? lines[1].Trim() : null;
        return (sessionId, AgentSession.NormalizeHost(host));
    }

    /// <summary>
    /// Stores a pending session ID for an agent.
    /// </summary>
    public void StorePendingSessionId(string agentName, string sessionId, string host = AgentSession.UnknownHost)
    {
        var path = GetPendingSessionPath(agentName);
        var dir = Path.GetDirectoryName(path);
        if (dir != null) Directory.CreateDirectory(dir);
        var normalizedHost = AgentSession.NormalizeHost(host);
        var content = normalizedHost == AgentSession.UnknownHost
            ? sessionId
            : $"{sessionId}\n{normalizedHost}";

        for (var i = 0; i < 3; i++)
        {
            try
            {
                File.WriteAllText(path, content);
                return;
            }
            catch (IOException) when (i < 2)
            {
                Thread.Sleep(10 * (i + 1));
            }
        }
    }

    /// <summary>
    /// Gets the current session ID from context file.
    /// Verifies against the per-agent .session file when agent name is present
    /// to detect cross-terminal overwrites of the shared .session-context file.
    /// </summary>
    public string? GetSessionContext()
    {
        var path = GetSessionContextPath();
        if (!File.Exists(path)) return null;

        try
        {
            var content = FileReadRetry.Read(path);
            if (content == null) return null;

            var (sessionId, agentName, _) = ParseSessionContext(content);
            if (string.IsNullOrEmpty(sessionId)) return null;

            // #0196: drop the legacy single-line read. Only the verified two-line format
            // (sessionId\nagentName cross-checked against the per-agent .session file) is
            // accepted; unverifiable single-line content is discarded.
            if (string.IsNullOrEmpty(agentName)) return null;

            // Verify: the agent's .session file should confirm this session ID
            var agentSession = GetSession(agentName);
            if (agentSession?.SessionId == sessionId) return sessionId;

            // Race detected — another terminal overwrote .session-context.
            // Fall back: find a working agent whose session is still valid.
            return ResolveSessionFallback();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parses the session context file content. Format is either:
    /// - Legacy: "{sessionId}" (single line)
    /// - Verified: "{sessionId}\n{agentName}" (two lines)
    /// </summary>
    internal static (string? SessionId, string? AgentName, string Host) ParseSessionContext(string content)
    {
        var lines = content.Replace("\r\n", "\n").Split('\n');
        var sessionId = lines.Length > 0 ? lines[0].Trim() : null;
        var agentName = lines.Length > 1 ? lines[1].Trim() : null;
        var host = lines.Length > 2 ? lines[2].Trim() : null;
        return (
            sessionId,
            string.IsNullOrEmpty(agentName) ? null : agentName,
            AgentSession.NormalizeHost(host));
    }

    /// <summary>
    /// Fallback when .session-context was overwritten by another terminal.
    /// Scans all agents for a working agent assigned to the current human
    /// and returns its session ID.
    /// </summary>
    private string? ResolveSessionFallback()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        string? candidateSessionId = null;

        try
        {
            foreach (var name in _agentNames)
            {
                cts.Token.ThrowIfCancellationRequested();
                var state = _getAgentState(name);
                if (state?.Status != AgentStatus.Working) continue;

                var session = GetSession(name);
                if (session == null) continue;

                // Ambiguous — multiple working agents, can't determine which is ours
                if (candidateSessionId != null) return null;

                candidateSessionId = session.SessionId;
            }
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        return candidateSessionId;
    }

    /// <summary>
    /// Stores the session ID to context file, optionally with the agent name
    /// for cross-terminal race detection.
    /// </summary>
    public void StoreSessionContext(string sessionId, string? agentName = null, string host = AgentSession.UnknownHost)
    {
        var path = GetSessionContextPath();
        var dir = Path.GetDirectoryName(path);
        if (dir != null) Directory.CreateDirectory(dir);

        var normalizedHost = AgentSession.NormalizeHost(host);
        var content = agentName != null
            ? normalizedHost == AgentSession.UnknownHost
                ? $"{sessionId}\n{agentName}"
                : $"{sessionId}\n{agentName}\n{normalizedHost}"
            : sessionId;

        for (var i = 0; i < 3; i++)
        {
            try
            {
                File.WriteAllText(path, content);
                return;
            }
            catch (IOException) when (i < 2)
            {
                Thread.Sleep(10 * (i + 1));
            }
        }
    }
}
