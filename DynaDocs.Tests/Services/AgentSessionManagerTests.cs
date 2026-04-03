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
    public void GetSessionContext_NoFile_ReturnsNull()
    {
        Assert.Null(_manager.GetSessionContext());
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
