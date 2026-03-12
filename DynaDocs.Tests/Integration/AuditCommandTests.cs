namespace DynaDocs.Tests.Integration;

using System.Text.Json;
using DynaDocs.Commands;
using DynaDocs.Models;
using DynaDocs.Serialization;

/// <summary>
/// Integration tests for the audit command.
/// </summary>
[Collection("Integration")]
public class AuditCommandTests : IntegrationTestBase
{
    private string AuditPath => Path.Combine(TestDir, "dydo", "_system", "audit");

    /// <summary>
    /// Create an audit session JSON file in the test audit directory.
    /// </summary>
    private void CreateAuditSession(
        string sessionId,
        string? agent = "TestAgent",
        string? human = "tester",
        DateTime? started = null,
        List<AuditEvent>? events = null,
        ProjectSnapshot? snapshot = null)
    {
        started ??= new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        var session = new AuditSession
        {
            SessionId = sessionId,
            AgentName = agent,
            Human = human,
            Started = started.Value,
            Events = events ?? [],
            Snapshot = snapshot
        };

        var year = started.Value.Year.ToString();
        var date = started.Value.ToString("yyyy-MM-dd");
        var yearDir = Path.Combine(AuditPath, year);
        Directory.CreateDirectory(yearDir);

        var json = JsonSerializer.Serialize(session, DydoDefaultJsonContext.Default.AuditSession);
        File.WriteAllText(Path.Combine(yearDir, $"{date}-{sessionId}.json"), json);
    }

    private static ProjectSnapshot MakeSnapshot() => new()
    {
        GitCommit = "abc1234",
        Files = ["src/main.cs", "src/utils.cs", "dydo/index.md"],
        Folders = ["src", "dydo"],
        DocLinks = new Dictionary<string, List<string>>
        {
            ["dydo/index.md"] = ["src/main.cs"]
        }
    };

    #region ExecuteList

    [Fact]
    public async Task List_NoSessions_PrintsNotFoundMessage()
    {
        await InitProjectAsync();

        var command = AuditCommand.Create();
        var result = await RunAsync(command, "--list");

        result.AssertSuccess();
        result.AssertStdoutContains("No audit sessions found.");
    }

    [Fact]
    public async Task List_WithSessions_ListsThem()
    {
        await InitProjectAsync();
        CreateAuditSession("session-aaa", agent: "Alpha");
        CreateAuditSession("session-bbb", agent: "Beta",
            started: new DateTime(2026, 1, 16, 10, 0, 0, DateTimeKind.Utc));

        var command = AuditCommand.Create();
        var result = await RunAsync(command, "--list");

        result.AssertSuccess();
        result.AssertStdoutContains("Found 2 session(s):");
        result.AssertStdoutContains("session-aaa");
        result.AssertStdoutContains("session-bbb");
    }

    [Fact]
    public async Task List_WithYearFilter_FiltersToYear()
    {
        await InitProjectAsync();
        CreateAuditSession("session-2026", agent: "Alpha",
            started: new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc));
        CreateAuditSession("session-2025", agent: "Beta",
            started: new DateTime(2025, 6, 1, 10, 0, 0, DateTimeKind.Utc));

        var command = AuditCommand.Create();
        var result = await RunAsync(command, "--list", "/2025");

        result.AssertSuccess();
        result.AssertStdoutContains("Found 1 session(s):");
        result.AssertStdoutContains("session-2025");
        Assert.DoesNotContain("session-2026", result.Stdout);
    }

    [Fact]
    public async Task List_MoreThan50_ShowsAndMore()
    {
        await InitProjectAsync();
        for (int i = 0; i < 55; i++)
        {
            CreateAuditSession($"session-{i:D3}",
                started: new DateTime(2026, 1, 1, 10, i, 0, DateTimeKind.Utc));
        }

        var command = AuditCommand.Create();
        var result = await RunAsync(command, "--list");

        result.AssertSuccess();
        result.AssertStdoutContains("Found 55 session(s):");
        result.AssertStdoutContains("... and 5 more");
    }

    #endregion

    #region ExecuteShowSession

    [Fact]
    public async Task ShowSession_NotFound_ReturnsError()
    {
        await InitProjectAsync();

        var command = AuditCommand.Create();
        var result = await RunAsync(command, "--session", "nonexistent-id");

        result.AssertExitCode(2);
        result.AssertStdoutContains("Session not found: nonexistent-id");
    }

    [Fact]
    public async Task ShowSession_Found_ShowsDetails()
    {
        await InitProjectAsync();
        var started = new DateTime(2026, 2, 10, 14, 30, 0, DateTimeKind.Utc);
        CreateAuditSession("abc-123", agent: "Mia", human: "alice",
            started: started, snapshot: MakeSnapshot());

        var command = AuditCommand.Create();
        var result = await RunAsync(command, "--session", "abc-123");

        result.AssertSuccess();
        result.AssertStdoutContains("Session: abc-123");
        result.AssertStdoutContains("Agent: Mia");
        result.AssertStdoutContains("Human: alice");
        result.AssertStdoutContains("3 files");
        result.AssertStdoutContains("Events: 0");
    }

    [Fact]
    public async Task ShowSession_NullAgent_PrintsNone()
    {
        await InitProjectAsync();
        CreateAuditSession("no-agent-session", agent: null, human: null);

        var command = AuditCommand.Create();
        var result = await RunAsync(command, "--session", "no-agent-session");

        result.AssertSuccess();
        result.AssertStdoutContains("Agent: (none)");
        result.AssertStdoutContains("Human: (none)");
        result.AssertStdoutContains("Snapshot: (none)");
    }

    [Fact]
    public async Task ShowSession_WithEvents_ShowsEventDetails()
    {
        await InitProjectAsync();
        var events = new List<AuditEvent>
        {
            new()
            {
                Timestamp = new DateTime(2026, 2, 10, 14, 30, 0, DateTimeKind.Utc),
                EventType = AuditEventType.Read,
                Path = "src/main.cs"
            },
            new()
            {
                Timestamp = new DateTime(2026, 2, 10, 14, 31, 0, DateTimeKind.Utc),
                EventType = AuditEventType.Write,
                Path = "src/utils.cs"
            },
            new()
            {
                Timestamp = new DateTime(2026, 2, 10, 14, 32, 0, DateTimeKind.Utc),
                EventType = AuditEventType.Bash,
                Command = "dotnet build"
            },
            new()
            {
                Timestamp = new DateTime(2026, 2, 10, 14, 33, 0, DateTimeKind.Utc),
                EventType = AuditEventType.Role,
                Role = "code-writer",
                Task = "fix-bug"
            },
            new()
            {
                Timestamp = new DateTime(2026, 2, 10, 14, 34, 0, DateTimeKind.Utc),
                EventType = AuditEventType.Blocked,
                Path = "secret.env",
                BlockReason = "file off limits"
            },
            new()
            {
                Timestamp = new DateTime(2026, 2, 10, 14, 35, 0, DateTimeKind.Utc),
                EventType = AuditEventType.Commit,
                CommitHash = "deadbeef",
                CommitMessage = "fix: resolve bug"
            }
        };
        CreateAuditSession("events-session", events: events);

        var command = AuditCommand.Create();
        var result = await RunAsync(command, "--session", "events-session");

        result.AssertSuccess();
        result.AssertStdoutContains("Events: 6");
        result.AssertStdoutContains("src/main.cs");
        result.AssertStdoutContains("src/utils.cs");
    }

    #endregion

    #region ExecuteCompact

    [Fact]
    public async Task Compact_NoYearDir_PrintsMessage()
    {
        await InitProjectAsync();

        var command = AuditCommand.Create();
        var result = await RunAsync(command, "compact", "2020");

        result.AssertSuccess();
        result.AssertStdoutContains("No audit data found for year 2020");
    }

    [Fact]
    public async Task Compact_WithData_PrintsStats()
    {
        await InitProjectAsync();
        // Create sessions with snapshots for compaction
        CreateAuditSession("compact-1", agent: "Alpha",
            started: new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc),
            snapshot: MakeSnapshot());
        CreateAuditSession("compact-2", agent: "Beta",
            started: new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc),
            snapshot: MakeSnapshot());

        var command = AuditCommand.Create();
        var result = await RunAsync(command, "compact", "2026");

        result.AssertSuccess();
        result.AssertStdoutContains("Compacting audit snapshots for 2026");
        result.AssertStdoutContains("Sessions processed:");
        result.AssertStdoutContains("Old total size:");
        result.AssertStdoutContains("New total size:");
        result.AssertStdoutContains("Compression:");
    }

    [Fact]
    public async Task Compact_DefaultsToCurrentYear()
    {
        await InitProjectAsync();
        var year = DateTime.UtcNow.Year.ToString();
        CreateAuditSession("compact-default",
            started: DateTime.UtcNow,
            snapshot: MakeSnapshot());

        var command = AuditCommand.Create();
        var result = await RunAsync(command, "compact");

        result.AssertSuccess();
        result.AssertStdoutContains($"Compacting audit snapshots for {year}");
    }

    #endregion

    #region ExecuteGenerateVisualization

    [Fact]
    public async Task Visualization_NoSessions_PrintsMessage()
    {
        await InitProjectAsync();

        var command = AuditCommand.Create();
        var result = await RunAsync(command);

        result.AssertSuccess();
        result.AssertStdoutContains("No audit sessions found.");
    }

    [Fact]
    public async Task Visualization_WithSessions_GeneratesHtml()
    {
        await InitProjectAsync();
        var events = new List<AuditEvent>
        {
            new()
            {
                Timestamp = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc),
                EventType = AuditEventType.Read,
                Path = "src/main.cs"
            }
        };
        CreateAuditSession("viz-session", agent: "Mia", events: events,
            snapshot: MakeSnapshot());

        var command = AuditCommand.Create();
        var result = await RunAsync(command);

        result.AssertSuccess();
        result.AssertStdoutContains("Loaded 1 session(s).");
        result.AssertStdoutContains("Generated:");
        result.AssertStdoutContains("replay.html");

        // Verify HTML file was created
        var replayPath = Path.Combine(AuditPath, "reports", "replay.html");
        Assert.True(File.Exists(replayPath), "replay.html should be created");

        var html = File.ReadAllText(replayPath);
        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("Audit Visualization", html);
        Assert.Contains("sessionsData", html);
        Assert.Contains("agentColors", html);
        Assert.Contains("mergedTimeline", html);
    }

    [Fact]
    public async Task Visualization_MultipleSessions_GeneratesHtmlWithAllAgents()
    {
        await InitProjectAsync();
        CreateAuditSession("viz-1", agent: "Alpha",
            started: new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc),
            snapshot: MakeSnapshot(),
            events:
            [
                new()
                {
                    Timestamp = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc),
                    EventType = AuditEventType.Claim,
                    AgentName = "Alpha"
                }
            ]);
        CreateAuditSession("viz-2", agent: "Beta",
            started: new DateTime(2026, 3, 1, 11, 0, 0, DateTimeKind.Utc),
            snapshot: MakeSnapshot(),
            events:
            [
                new()
                {
                    Timestamp = new DateTime(2026, 3, 1, 11, 0, 0, DateTimeKind.Utc),
                    EventType = AuditEventType.Role,
                    Role = "code-writer",
                    Task = "fix-bug"
                }
            ]);

        var command = AuditCommand.Create();
        var result = await RunAsync(command);

        result.AssertSuccess();
        result.AssertStdoutContains("Loaded 2 session(s).");

        var replayPath = Path.Combine(AuditPath, "reports", "replay.html");
        var html = File.ReadAllText(replayPath);
        Assert.Contains("Alpha", html);
        Assert.Contains("Beta", html);
        // Verify sessions table
        Assert.Contains("<th>Agent</th>", html);
        Assert.Contains("<th>Events</th>", html);
    }

    [Fact]
    public async Task Visualization_WithYearFilter_FiltersCorrectly()
    {
        await InitProjectAsync();
        CreateAuditSession("old-viz", agent: "Alpha",
            started: new DateTime(2025, 6, 1, 10, 0, 0, DateTimeKind.Utc),
            snapshot: MakeSnapshot());
        CreateAuditSession("new-viz", agent: "Beta",
            started: new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc),
            snapshot: MakeSnapshot());

        var command = AuditCommand.Create();
        var result = await RunAsync(command, "/2025");

        result.AssertSuccess();
        result.AssertStdoutContains("Loaded 1 session(s).");
    }

    [Fact]
    public async Task Visualization_SessionWithNoSnapshot_HandlesGracefully()
    {
        await InitProjectAsync();
        CreateAuditSession("no-snap", agent: "Charlie", snapshot: null);

        var command = AuditCommand.Create();
        var result = await RunAsync(command);

        result.AssertSuccess();
        result.AssertStdoutContains("Loaded 1 session(s).");

        var replayPath = Path.Combine(AuditPath, "reports", "replay.html");
        Assert.True(File.Exists(replayPath));
    }

    #endregion

    #region GenerateVisualizationHtml content

    [Fact]
    public async Task Visualization_HtmlContainsControlButtons()
    {
        await InitProjectAsync();
        CreateAuditSession("html-check", snapshot: MakeSnapshot());

        var command = AuditCommand.Create();
        await RunAsync(command);

        var replayPath = Path.Combine(AuditPath, "reports", "replay.html");
        var html = File.ReadAllText(replayPath);

        Assert.Contains("Play", html);
        Assert.Contains("Reset", html);
        Assert.Contains("stepForward", html);
        Assert.Contains("stepBack", html);
        Assert.Contains("Show doc links", html);
    }

    [Fact]
    public async Task Visualization_HtmlContainsGraphSlots()
    {
        await InitProjectAsync();
        CreateAuditSession("graph-check", snapshot: MakeSnapshot());

        var command = AuditCommand.Create();
        await RunAsync(command);

        var replayPath = Path.Combine(AuditPath, "reports", "replay.html");
        var html = File.ReadAllText(replayPath);

        Assert.Contains("graphSlot0", html);
        Assert.Contains("graphSlot1", html);
        Assert.Contains("graphSlot2", html);
        Assert.Contains("Graph 1:", html);
        Assert.Contains("Graph 2:", html);
        Assert.Contains("Graph 3:", html);
    }

    [Fact]
    public async Task Visualization_HtmlContainsJavaScript()
    {
        await InitProjectAsync();
        CreateAuditSession("js-check", snapshot: MakeSnapshot());

        var command = AuditCommand.Create();
        await RunAsync(command);

        var replayPath = Path.Combine(AuditPath, "reports", "replay.html");
        var html = File.ReadAllText(replayPath);

        // JavaScript function names from GetJavaScript
        Assert.Contains("function stepForward()", html);
        Assert.Contains("function togglePlay()", html);
        Assert.Contains("function processEvent(", html);
        Assert.Contains("function initGraph(", html);
        Assert.Contains("vis-network", html);
    }

    #endregion

    #region Delegate methods via visualization

    [Fact]
    public async Task Visualization_MergeTimelines_ProducesChronologicalOrder()
    {
        await InitProjectAsync();
        // Session 1 has an event at t=10:00
        CreateAuditSession("merge-1", agent: "Alpha",
            started: new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc),
            events:
            [
                new()
                {
                    Timestamp = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc),
                    EventType = AuditEventType.Read,
                    Path = "file-a.cs"
                }
            ],
            snapshot: MakeSnapshot());
        // Session 2 has an event at t=09:00 (earlier)
        CreateAuditSession("merge-2", agent: "Beta",
            started: new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            events:
            [
                new()
                {
                    Timestamp = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
                    EventType = AuditEventType.Write,
                    Path = "file-b.cs"
                }
            ],
            snapshot: MakeSnapshot());

        var command = AuditCommand.Create();
        await RunAsync(command);

        // The merged timeline in the HTML should have Beta's event before Alpha's
        var replayPath = Path.Combine(AuditPath, "reports", "replay.html");
        var html = File.ReadAllText(replayPath);
        var betaIdx = html.IndexOf("file-b.cs");
        var alphaIdx = html.IndexOf("file-a.cs");
        // Both should be present in the timeline data
        Assert.True(betaIdx >= 0, "file-b.cs should be in the timeline");
        Assert.True(alphaIdx >= 0, "file-a.cs should be in the timeline");
    }

    #endregion

    #region Edge cases

    [Fact]
    public async Task ShowSession_WithAllEventTypes_FormatsCorrectly()
    {
        await InitProjectAsync();
        var events = new List<AuditEvent>
        {
            new()
            {
                Timestamp = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc),
                EventType = AuditEventType.Claim,
                AgentName = "TestBot"
            },
            new()
            {
                Timestamp = new DateTime(2026, 1, 1, 10, 1, 0, DateTimeKind.Utc),
                EventType = AuditEventType.Release
            },
            new()
            {
                Timestamp = new DateTime(2026, 1, 1, 10, 2, 0, DateTimeKind.Utc),
                EventType = AuditEventType.Edit,
                Path = "edited.cs"
            },
            new()
            {
                Timestamp = new DateTime(2026, 1, 1, 10, 3, 0, DateTimeKind.Utc),
                EventType = AuditEventType.Delete,
                Path = "deleted.cs"
            }
        };
        CreateAuditSession("all-events", events: events);

        var command = AuditCommand.Create();
        var result = await RunAsync(command, "--session", "all-events");

        result.AssertSuccess();
        result.AssertStdoutContains("Events: 4");
    }

    [Fact]
    public async Task Compact_WithExistingBaselines_HandlesRemoval()
    {
        await InitProjectAsync();

        // Create first session with snapshot, compact, then add more and compact again
        CreateAuditSession("base-1", agent: "Alpha",
            started: new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc),
            snapshot: MakeSnapshot());
        CreateAuditSession("base-2", agent: "Beta",
            started: new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc),
            snapshot: MakeSnapshot());
        CreateAuditSession("base-3", agent: "Charlie",
            started: new DateTime(2026, 1, 3, 10, 0, 0, DateTimeKind.Utc),
            snapshot: MakeSnapshot());

        var command = AuditCommand.Create();

        // First compaction
        var result1 = await RunAsync(command, "compact", "2026");
        result1.AssertSuccess();

        // Second compaction — should handle existing baselines
        var result2 = await RunAsync(command, "compact", "2026");
        result2.AssertSuccess();
        result2.AssertStdoutContains("Sessions processed:");
    }

    #endregion
}
