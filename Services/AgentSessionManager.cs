namespace DynaDocs.Services;

using System.Text.Json;
using DynaDocs.Models;
using DynaDocs.Serialization;

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
            var json = FileReadWithRetry(sessionPath);
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
                var hint = FileReadWithRetry(hintPath)?.Trim();
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
        var path = GetPendingSessionPath(agentName);
        if (!File.Exists(path)) return null;

        try
        {
            var sessionId = FileReadWithRetry(path)?.Trim();
            File.Delete(path);
            return sessionId;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Stores a pending session ID for an agent.
    /// </summary>
    public void StorePendingSessionId(string agentName, string sessionId)
    {
        var path = GetPendingSessionPath(agentName);
        var dir = Path.GetDirectoryName(path);
        if (dir != null) Directory.CreateDirectory(dir);

        for (var i = 0; i < 3; i++)
        {
            try
            {
                File.WriteAllText(path, sessionId);
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
    /// </summary>
    public string? GetSessionContext()
    {
        var path = GetSessionContextPath();
        if (!File.Exists(path)) return null;

        try
        {
            return FileReadWithRetry(path)?.Trim();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Stores the session ID to context file.
    /// </summary>
    public void StoreSessionContext(string sessionId)
    {
        var path = GetSessionContextPath();
        var dir = Path.GetDirectoryName(path);
        if (dir != null) Directory.CreateDirectory(dir);

        for (var i = 0; i < 3; i++)
        {
            try
            {
                File.WriteAllText(path, sessionId);
                return;
            }
            catch (IOException) when (i < 2)
            {
                Thread.Sleep(10 * (i + 1));
            }
        }
    }

    /// <summary>
    /// Reads a file with FileShare.ReadWrite and retry on IOException.
    /// </summary>
    public static string? FileReadWithRetry(string path, int maxRetries = 3)
    {
        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs);
                return sr.ReadToEnd();
            }
            catch (IOException)
            {
                if (attempt < maxRetries - 1)
                    Thread.Sleep(50 * (int)Math.Pow(3, attempt));
            }
            catch (UnauthorizedAccessException)
            {
                if (attempt < maxRetries - 1)
                    Thread.Sleep(50 * (int)Math.Pow(3, attempt));
            }
        }

        return null;
    }
}
