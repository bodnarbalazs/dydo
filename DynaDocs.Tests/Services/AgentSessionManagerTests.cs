namespace DynaDocs.Tests.Services;

using System.Text.Json;
using DynaDocs.Models;
using DynaDocs.Serialization;
using DynaDocs.Services;

public class AgentSessionManagerTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _agentsPath;
    private readonly AgentSessionManager _manager;

    public AgentSessionManagerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-session-" + Guid.NewGuid().ToString("N")[..8]);
        _agentsPath = Path.Combine(_testDir, "agents");
        Directory.CreateDirectory(_agentsPath);

        _manager = new AgentSessionManager(
            agent => Path.Combine(_agentsPath, agent),
            _agentsPath,
            ["Alice", "Bob"],
            name => name == "Alice" || name == "Bob",
            name => new AgentState { Name = name, Role = "code-writer", Status = AgentStatus.Working });
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    private void WriteSession(string agent, string sessionId)
    {
        var dir = Path.Combine(_agentsPath, agent);
        Directory.CreateDirectory(dir);
        var session = new AgentSession { Agent = agent, SessionId = sessionId, Claimed = DateTime.UtcNow };
        var json = JsonSerializer.Serialize(session, DydoDefaultJsonContext.Default.AgentSession);
        File.WriteAllText(Path.Combine(dir, ".session"), json);
    }

    #region GetSession

    [Fact]
    public void GetSession_NoFile_ReturnsNull()
    {
        Assert.Null(_manager.GetSession("Alice"));
    }

    [Fact]
    public void GetSession_ValidFile_ReturnsSession()
    {
        WriteSession("Alice", "sess-1");

        var session = _manager.GetSession("Alice");
        Assert.NotNull(session);
        Assert.Equal("sess-1", session.SessionId);
    }

    [Fact]
    public void GetSession_CorruptFile_ReturnsNull()
    {
        var dir = Path.Combine(_agentsPath, "Alice");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, ".session"), "not json");

        Assert.Null(_manager.GetSession("Alice"));
    }

    #endregion

    #region GetCurrentAgent

    [Fact]
    public void GetCurrentAgent_NullSessionId_ReturnsNull()
    {
        Assert.Null(_manager.GetCurrentAgent(null));
    }

    [Fact]
    public void GetCurrentAgent_EmptySessionId_ReturnsNull()
    {
        Assert.Null(_manager.GetCurrentAgent(""));
    }

    [Fact]
    public void GetCurrentAgent_MatchingSession_ReturnsAgent()
    {
        WriteSession("Alice", "sess-1");

        var agent = _manager.GetCurrentAgent("sess-1");
        Assert.NotNull(agent);
        Assert.Equal("Alice", agent.Name);
    }

    [Fact]
    public void GetCurrentAgent_NoMatch_ReturnsNull()
    {
        WriteSession("Alice", "other-session");

        Assert.Null(_manager.GetCurrentAgent("no-match"));
    }

    [Fact]
    public void GetCurrentAgent_UsesHintFile()
    {
        WriteSession("Alice", "sess-1");
        // Pre-write hint
        File.WriteAllText(_manager.GetAgentHintPath(), "Alice");

        var agent = _manager.GetCurrentAgent("sess-1");
        Assert.NotNull(agent);
        Assert.Equal("Alice", agent.Name);
    }

    [Fact]
    public void GetCurrentAgent_StaleHintFile_FallsToScan()
    {
        WriteSession("Bob", "sess-2");
        // Hint points to Alice but Bob has the session
        File.WriteAllText(_manager.GetAgentHintPath(), "Alice");

        var agent = _manager.GetCurrentAgent("sess-2");
        Assert.NotNull(agent);
        Assert.Equal("Bob", agent.Name);
    }

    #endregion

    #region PendingSession

    [Fact]
    public void StorePendingSessionId_ThenGetAndClear()
    {
        _manager.StorePendingSessionId("Alice", "pending-123");

        var result = _manager.GetPendingSessionId("Alice");
        Assert.Equal("pending-123", result);

        // File should be deleted after get
        Assert.Null(_manager.GetPendingSessionId("Alice"));
    }

    [Fact]
    public void GetPendingSessionId_NoFile_ReturnsNull()
    {
        Assert.Null(_manager.GetPendingSessionId("Alice"));
    }

    #endregion

    #region SessionContext

    [Fact]
    public void StoreSessionContext_ThenGet()
    {
        _manager.StoreSessionContext("ctx-456");

        var result = _manager.GetSessionContext();
        Assert.Equal("ctx-456", result);
    }

    [Fact]
    public void StoreSessionContext_WithAgentName_WritesVerifiedFormat()
    {
        WriteSession("Alice", "ctx-789");
        _manager.StoreSessionContext("ctx-789", "Alice");

        var result = _manager.GetSessionContext();
        Assert.Equal("ctx-789", result);
    }

    [Fact]
    public void GetSessionContext_VerifiedFormat_ValidatesAgentSession()
    {
        WriteSession("Alice", "ctx-abc");
        _manager.StoreSessionContext("ctx-abc", "Alice");

        // Alice's session matches — should return the session ID
        Assert.Equal("ctx-abc", _manager.GetSessionContext());
    }

    [Fact]
    public void GetSessionContext_RaceDetected_FallsBackToWorkingAgent()
    {
        // Alice is working with session "sess-alice"
        WriteSession("Alice", "sess-alice");

        // But .session-context was overwritten by another terminal with Bob's data
        _manager.StoreSessionContext("sess-bob", "Bob");
        WriteSession("Bob", "sess-bob");

        // Bob's verification succeeds (his data is consistent), so returns Bob's session.
        // This simulates the case where Bob's terminal wrote last.
        var result = _manager.GetSessionContext();
        Assert.Equal("sess-bob", result);
    }

    [Fact]
    public void GetSessionContext_RaceDetected_AgentSessionMismatch_FallsBack()
    {
        // Alice is working with session "sess-alice"
        WriteSession("Alice", "sess-alice");

        // .session-context says "sess-bob" for Alice — but Alice's .session file says "sess-alice"
        // This is the race: another terminal wrote Bob's session ID but Alice's name
        var contextPath = Path.Combine(_agentsPath, ".session-context");
        File.WriteAllText(contextPath, "sess-bob\nAlice");

        // Verification fails (Alice's session is "sess-alice", not "sess-bob")
        // Fallback scans for working agents — Alice is working
        var result = _manager.GetSessionContext();
        Assert.Equal("sess-alice", result);
    }

    [Fact]
    public void GetSessionContext_RaceDetected_MultipleWorkingAgents_ReturnsNull()
    {
        // Both Alice and Bob are working — ambiguous
        WriteSession("Alice", "sess-alice");
        WriteSession("Bob", "sess-bob");

        // Write mismatched context to trigger fallback
        var contextPath = Path.Combine(_agentsPath, ".session-context");
        File.WriteAllText(contextPath, "sess-unknown\nAlice");

        // Fallback finds two working agents — can't determine which is ours
        var result = _manager.GetSessionContext();
        Assert.Null(result);
    }

    [Fact]
    public void GetSessionContext_LegacyFormat_StillWorks()
    {
        // Old format: just the session ID, no agent name
        var contextPath = Path.Combine(_agentsPath, ".session-context");
        Directory.CreateDirectory(_agentsPath);
        File.WriteAllText(contextPath, "legacy-session-id");

        var result = _manager.GetSessionContext();
        Assert.Equal("legacy-session-id", result);
    }

    [Fact]
    public void GetSessionContext_NoFile_ReturnsNull()
    {
        Assert.Null(_manager.GetSessionContext());
    }

    #endregion

    #region ParseSessionContext

    [Fact]
    public void ParseSessionContext_LegacyFormat_ReturnsSessionIdOnly()
    {
        var (sessionId, agentName) = AgentSessionManager.ParseSessionContext("abc-123");
        Assert.Equal("abc-123", sessionId);
        Assert.Null(agentName);
    }

    [Fact]
    public void ParseSessionContext_VerifiedFormat_ReturnsBoth()
    {
        var (sessionId, agentName) = AgentSessionManager.ParseSessionContext("abc-123\nAlice");
        Assert.Equal("abc-123", sessionId);
        Assert.Equal("Alice", agentName);
    }

    [Fact]
    public void ParseSessionContext_EmptyAgentName_ReturnsNull()
    {
        var (sessionId, agentName) = AgentSessionManager.ParseSessionContext("abc-123\n");
        Assert.Equal("abc-123", sessionId);
        Assert.Null(agentName);
    }

    [Fact]
    public void ParseSessionContext_WhitespaceHandled()
    {
        var (sessionId, agentName) = AgentSessionManager.ParseSessionContext("  abc-123  \n  Alice  ");
        Assert.Equal("abc-123", sessionId);
        Assert.Equal("Alice", agentName);
    }

    #endregion

    #region FileReadRetry (moved to Utils/FileReadRetry)

    [Fact]
    public void FileReadRetry_ValidFile_ReturnsContent()
    {
        var filePath = Path.Combine(_testDir, "test.txt");
        File.WriteAllText(filePath, "hello");

        Assert.Equal("hello", DynaDocs.Utils.FileReadRetry.Read(filePath));
    }

    [Fact]
    public void FileReadRetry_MissingFile_ReturnsNull()
    {
        Assert.Null(DynaDocs.Utils.FileReadRetry.Read(Path.Combine(_testDir, "nope.txt")));
    }

    #endregion
}
