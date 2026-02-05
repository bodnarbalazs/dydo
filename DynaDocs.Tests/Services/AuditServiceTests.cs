namespace DynaDocs.Tests.Services;

using System.Text.Json;
using DynaDocs.Models;
using DynaDocs.Services;

public class AuditServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _dydoDir;
    private readonly string _auditDir;

    public AuditServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-audit-test-" + Guid.NewGuid().ToString("N")[..8]);
        _dydoDir = Path.Combine(_testDir, "dydo");
        _auditDir = Path.Combine(_dydoDir, "_system", "audit");
        Directory.CreateDirectory(_auditDir);

        // Create minimal dydo.json
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), """
            {
                "version": 1,
                "structure": { "root": "dydo" },
                "agents": { "pool": [], "assignments": {} }
            }
            """);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    #region LogEvent Tests

    [Fact]
    public void LogEvent_CreatesSessionFileInYearFolder()
    {
        var service = new AuditService(basePath: _testDir);
        var sessionId = "test-session-123";

        service.LogEvent(sessionId, new AuditEvent
        {
            EventType = AuditEventType.Read,
            Path = "test/file.md"
        });

        var year = DateTime.UtcNow.Year.ToString();
        var yearDir = Path.Combine(_auditDir, year);
        Assert.True(Directory.Exists(yearDir), "Year folder should be created");

        var files = Directory.GetFiles(yearDir, $"*-{sessionId}.json");
        Assert.Single(files);
    }

    [Fact]
    public void LogEvent_AppendsToExistingSession()
    {
        var service = new AuditService(basePath: _testDir);
        var sessionId = "multi-event-session";

        // Log first event
        service.LogEvent(sessionId, new AuditEvent
        {
            EventType = AuditEventType.Read,
            Path = "file1.md"
        });

        // Log second event
        service.LogEvent(sessionId, new AuditEvent
        {
            EventType = AuditEventType.Edit,
            Path = "file2.md"
        });

        var session = service.GetSession(sessionId);
        Assert.NotNull(session);
        Assert.Equal(2, session.Events.Count);
        Assert.Equal(AuditEventType.Read, session.Events[0].EventType);
        Assert.Equal(AuditEventType.Edit, session.Events[1].EventType);
    }

    [Fact]
    public void LogEvent_SetsTimestampIfNotProvided()
    {
        var service = new AuditService(basePath: _testDir);
        var sessionId = "timestamp-test";
        var before = DateTime.UtcNow;

        service.LogEvent(sessionId, new AuditEvent
        {
            EventType = AuditEventType.Read,
            Path = "test.md"
        });

        var after = DateTime.UtcNow;
        var session = service.GetSession(sessionId);

        Assert.NotNull(session);
        Assert.True(session.Events[0].Timestamp >= before);
        Assert.True(session.Events[0].Timestamp <= after);
    }

    [Fact]
    public void LogEvent_StoresAgentAndHumanMetadata()
    {
        var service = new AuditService(basePath: _testDir);
        var sessionId = "metadata-test";

        service.LogEvent(sessionId, new AuditEvent
        {
            EventType = AuditEventType.Claim
        }, agentName: "Alpha", human: "john");

        var session = service.GetSession(sessionId);

        Assert.NotNull(session);
        Assert.Equal("Alpha", session.AgentName);
        Assert.Equal("john", session.Human);
    }

    [Fact]
    public void LogEvent_IgnoresEmptySessionId()
    {
        var service = new AuditService(basePath: _testDir);

        // Should not throw
        service.LogEvent("", new AuditEvent
        {
            EventType = AuditEventType.Read,
            Path = "test.md"
        });

        service.LogEvent(null!, new AuditEvent
        {
            EventType = AuditEventType.Read,
            Path = "test.md"
        });

        // No sessions should be created
        var files = service.ListSessionFiles();
        Assert.Empty(files);
    }

    #endregion

    #region GetSession Tests

    [Fact]
    public void GetSession_ReturnsNullForNonExistentSession()
    {
        var service = new AuditService(basePath: _testDir);

        var session = service.GetSession("nonexistent");

        Assert.Null(session);
    }

    [Fact]
    public void GetSession_LoadsExistingSession()
    {
        // Manually create a session file
        var year = DateTime.UtcNow.Year.ToString();
        var yearDir = Path.Combine(_auditDir, year);
        Directory.CreateDirectory(yearDir);

        var sessionData = new AuditSession
        {
            SessionId = "manual-session",
            AgentName = "Beta",
            Human = "jane",
            Started = DateTime.UtcNow,
            Events = [
                new AuditEvent { EventType = AuditEventType.Read, Path = "a.md" }
            ]
        };

        var filename = $"{DateTime.UtcNow:yyyy-MM-dd}-manual-session.json";
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(sessionData, options);
        File.WriteAllText(Path.Combine(yearDir, filename), json);

        var service = new AuditService(basePath: _testDir);
        var session = service.GetSession("manual-session");

        Assert.NotNull(session);
        Assert.Equal("Beta", session.AgentName);
        Assert.Equal("jane", session.Human);
        Assert.Single(session.Events);
    }

    #endregion

    #region LoadSessions Tests

    [Fact]
    public void LoadSessions_ReturnsEmptyWhenNoSessions()
    {
        var service = new AuditService(basePath: _testDir);

        var (sessions, limitReached) = service.LoadSessions();

        Assert.Empty(sessions);
        Assert.False(limitReached);
    }

    [Fact]
    public void LoadSessions_LoadsAllSessions()
    {
        var service = new AuditService(basePath: _testDir);

        // Create multiple sessions
        service.LogEvent("session-1", new AuditEvent { EventType = AuditEventType.Read, Path = "a.md" });
        service.LogEvent("session-2", new AuditEvent { EventType = AuditEventType.Edit, Path = "b.md" });
        service.LogEvent("session-3", new AuditEvent { EventType = AuditEventType.Write, Path = "c.md" });

        var (sessions, limitReached) = service.LoadSessions();

        Assert.Equal(3, sessions.Count);
        Assert.False(limitReached);
    }

    [Fact]
    public void LoadSessions_FiltersbyYear()
    {
        var service = new AuditService(basePath: _testDir);
        var currentYear = DateTime.UtcNow.Year.ToString();

        // Create session in current year
        service.LogEvent("current-year", new AuditEvent { EventType = AuditEventType.Read, Path = "a.md" });

        // Create session in different year folder
        var otherYear = (DateTime.UtcNow.Year - 1).ToString();
        var otherYearDir = Path.Combine(_auditDir, otherYear);
        Directory.CreateDirectory(otherYearDir);

        var oldSession = new AuditSession
        {
            SessionId = "old-session",
            Started = DateTime.UtcNow.AddYears(-1),
            Events = [new AuditEvent { EventType = AuditEventType.Read, Path = "old.md" }]
        };
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(oldSession, options);
        File.WriteAllText(Path.Combine(otherYearDir, $"{DateTime.UtcNow.AddYears(-1):yyyy-MM-dd}-old-session.json"), json);

        // Load only current year
        var (sessions, _) = service.LoadSessions(currentYear);

        Assert.Single(sessions);
        Assert.Equal("current-year", sessions[0].SessionId);
    }

    #endregion

    #region ListSessionFiles Tests

    [Fact]
    public void ListSessionFiles_ReturnsFilesNewestFirst()
    {
        var service = new AuditService(basePath: _testDir);
        var year = DateTime.UtcNow.Year.ToString();
        var yearDir = Path.Combine(_auditDir, year);
        Directory.CreateDirectory(yearDir);

        // Create files with different dates
        File.WriteAllText(Path.Combine(yearDir, "2025-01-01-session-a.json"), "{}");
        File.WriteAllText(Path.Combine(yearDir, "2025-01-15-session-b.json"), "{}");
        File.WriteAllText(Path.Combine(yearDir, "2025-01-10-session-c.json"), "{}");

        var files = service.ListSessionFiles();

        Assert.Equal(3, files.Count);
        Assert.Contains("session-b", Path.GetFileName(files[0])); // newest
        Assert.Contains("session-c", Path.GetFileName(files[1]));
        Assert.Contains("session-a", Path.GetFileName(files[2])); // oldest
    }

    [Fact]
    public void ListSessionFiles_FiltersbyYear()
    {
        var service = new AuditService(basePath: _testDir);

        // Create year folders with files
        Directory.CreateDirectory(Path.Combine(_auditDir, "2024"));
        Directory.CreateDirectory(Path.Combine(_auditDir, "2025"));

        File.WriteAllText(Path.Combine(_auditDir, "2024", "2024-06-01-old.json"), "{}");
        File.WriteAllText(Path.Combine(_auditDir, "2025", "2025-01-01-new.json"), "{}");

        var files2024 = service.ListSessionFiles("2024");
        var files2025 = service.ListSessionFiles("2025");

        Assert.Single(files2024);
        Assert.Contains("old", Path.GetFileName(files2024[0]));

        Assert.Single(files2025);
        Assert.Contains("new", Path.GetFileName(files2025[0]));
    }

    #endregion

    #region EnsureAuditFolder Tests

    [Fact]
    public void EnsureAuditFolder_CreatesStructure()
    {
        // Delete audit folder to test creation
        if (Directory.Exists(_auditDir))
            Directory.Delete(_auditDir, true);

        var service = new AuditService(basePath: _testDir);
        service.EnsureAuditFolder();

        Assert.True(Directory.Exists(_auditDir));
        Assert.True(Directory.Exists(Path.Combine(_auditDir, "reports")));
    }

    #endregion

    #region Event Type Tests

    [Theory]
    [InlineData(AuditEventType.Claim)]
    [InlineData(AuditEventType.Release)]
    [InlineData(AuditEventType.Role)]
    [InlineData(AuditEventType.Read)]
    [InlineData(AuditEventType.Write)]
    [InlineData(AuditEventType.Edit)]
    [InlineData(AuditEventType.Delete)]
    [InlineData(AuditEventType.Bash)]
    [InlineData(AuditEventType.Commit)]
    [InlineData(AuditEventType.Blocked)]
    public void LogEvent_SupportsAllEventTypes(AuditEventType eventType)
    {
        var service = new AuditService(basePath: _testDir);
        var sessionId = $"event-type-{eventType}";

        service.LogEvent(sessionId, new AuditEvent
        {
            EventType = eventType,
            Path = eventType == AuditEventType.Bash ? null : "test.md",
            Command = eventType == AuditEventType.Bash ? "echo test" : null
        });

        var session = service.GetSession(sessionId);
        Assert.NotNull(session);
        Assert.Equal(eventType, session.Events[0].EventType);
    }

    #endregion

    #region Serialization Tests

    [Fact]
    public void Session_SerializesCorrectly()
    {
        var service = new AuditService(basePath: _testDir);
        var sessionId = "serialize-test";

        service.LogEvent(sessionId, new AuditEvent
        {
            EventType = AuditEventType.Read,
            Path = "dydo/docs/test.md",
            Tool = "read"
        }, agentName: "Alpha", human: "developer");

        service.LogEvent(sessionId, new AuditEvent
        {
            EventType = AuditEventType.Bash,
            Command = "npm test",
            Tool = "bash"
        });

        // Read the raw file
        var year = DateTime.UtcNow.Year.ToString();
        var files = Directory.GetFiles(Path.Combine(_auditDir, year), $"*-{sessionId}.json");
        var json = File.ReadAllText(files[0]);

        // Verify JSON structure
        Assert.Contains("\"session\"", json);
        Assert.Contains("\"agent\"", json);
        Assert.Contains("\"human\"", json);
        Assert.Contains("\"events\"", json);
        Assert.Contains("\"Read\"", json);
        Assert.Contains("\"Bash\"", json);
    }

    [Fact]
    public void Session_OmitsNullFields()
    {
        var service = new AuditService(basePath: _testDir);
        var sessionId = "null-fields-test";

        service.LogEvent(sessionId, new AuditEvent
        {
            EventType = AuditEventType.Read,
            Path = "test.md"
            // Other fields left null
        });

        var year = DateTime.UtcNow.Year.ToString();
        var files = Directory.GetFiles(Path.Combine(_auditDir, year), $"*-{sessionId}.json");
        var json = File.ReadAllText(files[0]);

        // Null fields should not appear in JSON
        Assert.DoesNotContain("\"cmd\"", json);
        Assert.DoesNotContain("\"role\"", json);
        Assert.DoesNotContain("\"hash\"", json);
        Assert.DoesNotContain("\"reason\"", json);
    }

    #endregion
}
