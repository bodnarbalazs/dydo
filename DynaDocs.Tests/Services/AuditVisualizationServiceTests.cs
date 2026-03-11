namespace DynaDocs.Tests.Services;

using DynaDocs.Models;
using DynaDocs.Services;

public class AuditVisualizationServiceTests
{
    #region FormatBytes

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1.0 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(10240, "10.0 KB")]
    [InlineData(1048576, "1.0 MB")]
    [InlineData(1572864, "1.5 MB")]
    [InlineData(10485760, "10.0 MB")]
    public void FormatBytes_FormatsCorrectly(long bytes, string expected)
    {
        Assert.Equal(expected, AuditVisualizationService.FormatBytes(bytes));
    }

    #endregion

    #region TruncateCommand

    [Fact]
    public void TruncateCommand_ShortString_ReturnsUnchanged()
    {
        Assert.Equal("ls -la", AuditVisualizationService.TruncateCommand("ls -la"));
    }

    [Fact]
    public void TruncateCommand_ExactLength_ReturnsUnchanged()
    {
        var exact = new string('a', 50);
        Assert.Equal(exact, AuditVisualizationService.TruncateCommand(exact));
    }

    [Fact]
    public void TruncateCommand_LongString_TruncatesWithEllipsis()
    {
        var long60 = new string('x', 60);
        var result = AuditVisualizationService.TruncateCommand(long60);
        Assert.Equal(53, result.Length); // 50 + "..."
        Assert.EndsWith("...", result);
    }

    [Fact]
    public void TruncateCommand_CustomMaxLength()
    {
        var result = AuditVisualizationService.TruncateCommand("abcdefghij", 5);
        Assert.Equal("abcde...", result);
    }

    #endregion

    #region AssignAgentColors

    [Fact]
    public void AssignAgentColors_EmptySessions_ReturnsEmpty()
    {
        var result = AuditVisualizationService.AssignAgentColors([]);
        Assert.Empty(result);
    }

    [Fact]
    public void AssignAgentColors_SingleAgent_AssignsFirstColor()
    {
        var sessions = new List<AuditSession>
        {
            new() { SessionId = "s1", AgentName = "Alice", Events = [] }
        };

        var result = AuditVisualizationService.AssignAgentColors(sessions);
        Assert.Single(result);
        Assert.Equal(AuditVisualizationService.AgentColors[0], result["Alice"]);
    }

    [Fact]
    public void AssignAgentColors_MultipleAgents_AssignsDifferentColors()
    {
        var sessions = new List<AuditSession>
        {
            new() { SessionId = "s1", AgentName = "Alice", Events = [] },
            new() { SessionId = "s2", AgentName = "Bob", Events = [] },
            new() { SessionId = "s3", AgentName = "Charlie", Events = [] }
        };

        var result = AuditVisualizationService.AssignAgentColors(sessions);
        Assert.Equal(3, result.Count);
        Assert.Equal(AuditVisualizationService.AgentColors[0], result["Alice"]);
        Assert.Equal(AuditVisualizationService.AgentColors[1], result["Bob"]);
        Assert.Equal(AuditVisualizationService.AgentColors[2], result["Charlie"]);
    }

    [Fact]
    public void AssignAgentColors_DuplicateAgent_AssignsSameColor()
    {
        var sessions = new List<AuditSession>
        {
            new() { SessionId = "s1", AgentName = "Alice", Events = [] },
            new() { SessionId = "s2", AgentName = "Alice", Events = [] }
        };

        var result = AuditVisualizationService.AssignAgentColors(sessions);
        Assert.Single(result);
    }

    [Fact]
    public void AssignAgentColors_NullAgentName_UsesSessionId()
    {
        var sessions = new List<AuditSession>
        {
            new() { SessionId = "sess-123", AgentName = null, Events = [] }
        };

        var result = AuditVisualizationService.AssignAgentColors(sessions);
        Assert.True(result.ContainsKey("sess-123"));
    }

    [Fact]
    public void AssignAgentColors_MoreAgentsThanColors_WrapsAround()
    {
        var sessions = new List<AuditSession>();
        for (int i = 0; i < AuditVisualizationService.AgentColors.Length + 2; i++)
        {
            sessions.Add(new() { SessionId = $"s{i}", AgentName = $"Agent{i}", Events = [] });
        }

        var result = AuditVisualizationService.AssignAgentColors(sessions);
        // The (AgentColors.Length)th agent should wrap to color[0]
        Assert.Equal(
            AuditVisualizationService.AgentColors[0],
            result[$"Agent{AuditVisualizationService.AgentColors.Length}"]);
    }

    #endregion

    #region MergeTimelines

    [Fact]
    public void MergeTimelines_EmptySessions_ReturnsEmpty()
    {
        var result = AuditVisualizationService.MergeTimelines([]);
        Assert.Empty(result);
    }

    [Fact]
    public void MergeTimelines_SingleSessionSingleEvent_ReturnsSingleMergedEvent()
    {
        var sessions = new List<AuditSession>
        {
            new()
            {
                SessionId = "s1",
                AgentName = "Alice",
                Events =
                [
                    new AuditEvent
                    {
                        Timestamp = new DateTime(2026, 1, 1, 10, 0, 0),
                        EventType = AuditEventType.Read,
                        Path = "file.cs"
                    }
                ]
            }
        };

        var result = AuditVisualizationService.MergeTimelines(sessions);
        Assert.Single(result);
        Assert.Equal("Alice", result[0].Agent);
        Assert.Equal("Read", result[0].EventType);
        Assert.Equal("file.cs", result[0].Path);
    }

    [Fact]
    public void MergeTimelines_MultipleSessions_SortsByTimestamp()
    {
        var sessions = new List<AuditSession>
        {
            new()
            {
                SessionId = "s1",
                AgentName = "Alice",
                Events =
                [
                    new AuditEvent
                    {
                        Timestamp = new DateTime(2026, 1, 1, 10, 0, 0),
                        EventType = AuditEventType.Read,
                        Path = "alice-file.cs"
                    },
                    new AuditEvent
                    {
                        Timestamp = new DateTime(2026, 1, 1, 10, 5, 0),
                        EventType = AuditEventType.Write,
                        Path = "alice-write.cs"
                    }
                ]
            },
            new()
            {
                SessionId = "s2",
                AgentName = "Bob",
                Events =
                [
                    new AuditEvent
                    {
                        Timestamp = new DateTime(2026, 1, 1, 10, 2, 0),
                        EventType = AuditEventType.Edit,
                        Path = "bob-edit.cs"
                    }
                ]
            }
        };

        var result = AuditVisualizationService.MergeTimelines(sessions);
        Assert.Equal(3, result.Count);
        Assert.Equal("Alice", result[0].Agent);   // 10:00
        Assert.Equal("Bob", result[1].Agent);      // 10:02
        Assert.Equal("Alice", result[2].Agent);    // 10:05
    }

    [Fact]
    public void MergeTimelines_CopiesBashCommandField()
    {
        var sessions = new List<AuditSession>
        {
            new()
            {
                SessionId = "s1",
                AgentName = "Agent1",
                Events =
                [
                    new AuditEvent
                    {
                        Timestamp = DateTime.UtcNow,
                        EventType = AuditEventType.Bash,
                        Command = "dotnet build"
                    }
                ]
            }
        };

        var result = AuditVisualizationService.MergeTimelines(sessions);
        Assert.Equal("dotnet build", result[0].Command);
        Assert.Equal("Bash", result[0].EventType);
    }

    [Fact]
    public void MergeTimelines_CopiesRoleAndTaskFields()
    {
        var sessions = new List<AuditSession>
        {
            new()
            {
                SessionId = "s1",
                AgentName = "Agent1",
                Events =
                [
                    new AuditEvent
                    {
                        Timestamp = DateTime.UtcNow,
                        EventType = AuditEventType.Role,
                        Role = "code-writer",
                        Task = "fix-bug"
                    }
                ]
            }
        };

        var result = AuditVisualizationService.MergeTimelines(sessions);
        Assert.Equal("code-writer", result[0].Role);
        Assert.Equal("fix-bug", result[0].Task);
    }

    [Fact]
    public void MergeTimelines_NullAgentName_UsesSessionId()
    {
        var sessions = new List<AuditSession>
        {
            new()
            {
                SessionId = "abc-123",
                AgentName = null,
                Events =
                [
                    new AuditEvent
                    {
                        Timestamp = DateTime.UtcNow,
                        EventType = AuditEventType.Read,
                        Path = "test.cs"
                    }
                ]
            }
        };

        var result = AuditVisualizationService.MergeTimelines(sessions);
        Assert.Equal("abc-123", result[0].Agent);
    }

    #endregion

    #region BuildCombinedSnapshot

    [Fact]
    public void BuildCombinedSnapshot_EmptySessions_ReturnsEmptySnapshot()
    {
        var result = AuditVisualizationService.BuildCombinedSnapshot(
            [], _ => null, _ => null);

        Assert.Equal("unknown", result.GitCommit);
        Assert.Empty(result.Files);
        Assert.Empty(result.Folders);
        Assert.Empty(result.DocLinks);
    }

    [Fact]
    public void BuildCombinedSnapshot_SessionsWithNoSnapshot_ReturnsEmptySnapshot()
    {
        var sessions = new List<AuditSession>
        {
            new() { SessionId = "s1", Snapshot = null, SnapshotRef = null, Events = [] }
        };

        var result = AuditVisualizationService.BuildCombinedSnapshot(
            sessions, _ => null, _ => null);

        Assert.Equal("unknown", result.GitCommit);
        Assert.Empty(result.Files);
    }

    [Fact]
    public void BuildCombinedSnapshot_SingleSession_ReturnsItsSnapshot()
    {
        var snapshot = new ProjectSnapshot
        {
            GitCommit = "abc123",
            Files = ["src/a.cs", "src/b.cs"],
            Folders = ["src"],
            DocLinks = new Dictionary<string, List<string>>
            {
                ["doc1.md"] = ["doc2.md"]
            }
        };

        var sessions = new List<AuditSession>
        {
            new() { SessionId = "s1", Snapshot = snapshot, Events = [] }
        };

        var result = AuditVisualizationService.BuildCombinedSnapshot(
            sessions, _ => null, _ => null);

        Assert.Equal("abc123", result.GitCommit);
        Assert.Equal(2, result.Files.Count);
        Assert.Single(result.Folders);
        Assert.Single(result.DocLinks);
    }

    [Fact]
    public void BuildCombinedSnapshot_MultipleSessions_MergesFiles()
    {
        var snap1 = new ProjectSnapshot
        {
            GitCommit = "aaa",
            Files = ["a.cs", "b.cs"],
            Folders = ["src"],
            DocLinks = []
        };
        var snap2 = new ProjectSnapshot
        {
            GitCommit = "bbb",
            Files = ["b.cs", "c.cs"],
            Folders = ["src", "lib"],
            DocLinks = []
        };

        var sessions = new List<AuditSession>
        {
            new() { SessionId = "s1", Snapshot = snap1, Events = [] },
            new() { SessionId = "s2", Snapshot = snap2, Events = [] }
        };

        var result = AuditVisualizationService.BuildCombinedSnapshot(
            sessions, _ => null, _ => null);

        // Union of files: a.cs, b.cs, c.cs
        Assert.Equal(3, result.Files.Count);
        Assert.Contains("a.cs", result.Files);
        Assert.Contains("c.cs", result.Files);

        // Union of folders: src, lib
        Assert.Equal(2, result.Folders.Count);

        // First session's commit wins
        Assert.Equal("aaa", result.GitCommit);
    }

    [Fact]
    public void BuildCombinedSnapshot_MergesDocLinks()
    {
        var snap1 = new ProjectSnapshot
        {
            GitCommit = "aaa",
            Files = [],
            Folders = [],
            DocLinks = new Dictionary<string, List<string>>
            {
                ["doc1.md"] = ["doc2.md"]
            }
        };
        var snap2 = new ProjectSnapshot
        {
            GitCommit = "bbb",
            Files = [],
            Folders = [],
            DocLinks = new Dictionary<string, List<string>>
            {
                ["doc1.md"] = ["doc3.md"],
                ["doc4.md"] = ["doc5.md"]
            }
        };

        var sessions = new List<AuditSession>
        {
            new() { SessionId = "s1", Snapshot = snap1, Events = [] },
            new() { SessionId = "s2", Snapshot = snap2, Events = [] }
        };

        var result = AuditVisualizationService.BuildCombinedSnapshot(
            sessions, _ => null, _ => null);

        // doc1.md should have both doc2.md and doc3.md
        Assert.Equal(2, result.DocLinks["doc1.md"].Count);
        Assert.Contains("doc2.md", result.DocLinks["doc1.md"]);
        Assert.Contains("doc3.md", result.DocLinks["doc1.md"]);

        // doc4.md -> doc5.md
        Assert.Single(result.DocLinks["doc4.md"]);
    }

    [Fact]
    public void BuildCombinedSnapshot_DocLinks_NoDuplicates()
    {
        var snap1 = new ProjectSnapshot
        {
            GitCommit = "aaa",
            Files = [],
            Folders = [],
            DocLinks = new Dictionary<string, List<string>>
            {
                ["doc1.md"] = ["doc2.md"]
            }
        };
        var snap2 = new ProjectSnapshot
        {
            GitCommit = "bbb",
            Files = [],
            Folders = [],
            DocLinks = new Dictionary<string, List<string>>
            {
                ["doc1.md"] = ["doc2.md"]  // Same link
            }
        };

        var sessions = new List<AuditSession>
        {
            new() { SessionId = "s1", Snapshot = snap1, Events = [] },
            new() { SessionId = "s2", Snapshot = snap2, Events = [] }
        };

        var result = AuditVisualizationService.BuildCombinedSnapshot(
            sessions, _ => null, _ => null);

        Assert.Single(result.DocLinks["doc1.md"]);
    }

    [Fact]
    public void BuildCombinedSnapshot_UsesSnapshotRef_WithBaseline()
    {
        var baselineSnapshot = new ProjectSnapshot
        {
            GitCommit = "base",
            Files = ["x.cs"],
            Folders = ["root"],
            DocLinks = []
        };

        var baseline = new SnapshotBaseline
        {
            Id = "baseline-1",
            Snapshot = baselineSnapshot
        };

        var sessions = new List<AuditSession>
        {
            new()
            {
                SessionId = "s1",
                Snapshot = null,
                SnapshotRef = new SnapshotRef
                {
                    BaseId = "baseline-1",
                    Depth = 1,
                    Delta = null  // Identical to baseline
                },
                Events = []
            }
        };

        var result = AuditVisualizationService.BuildCombinedSnapshot(
            sessions,
            id => id == "baseline-1" ? baseline : null,
            _ => null);

        Assert.Equal("base", result.GitCommit);
        Assert.Contains("x.cs", result.Files);
    }

    #endregion

    #region FormatEventDetails

    [Fact]
    public void FormatEventDetails_ReadEvent_ReturnsPath()
    {
        var e = new AuditEvent { EventType = AuditEventType.Read, Path = "src/file.cs" };
        Assert.Equal("src/file.cs", AuditVisualizationService.FormatEventDetails(e));
    }

    [Fact]
    public void FormatEventDetails_WriteEvent_ReturnsPath()
    {
        var e = new AuditEvent { EventType = AuditEventType.Write, Path = "out.txt" };
        Assert.Equal("out.txt", AuditVisualizationService.FormatEventDetails(e));
    }

    [Fact]
    public void FormatEventDetails_EditEvent_ReturnsPath()
    {
        var e = new AuditEvent { EventType = AuditEventType.Edit, Path = "edit.cs" };
        Assert.Equal("edit.cs", AuditVisualizationService.FormatEventDetails(e));
    }

    [Fact]
    public void FormatEventDetails_DeleteEvent_ReturnsPath()
    {
        var e = new AuditEvent { EventType = AuditEventType.Delete, Path = "del.cs" };
        Assert.Equal("del.cs", AuditVisualizationService.FormatEventDetails(e));
    }

    [Fact]
    public void FormatEventDetails_BashEvent_TruncatesCommand()
    {
        var longCmd = new string('a', 60);
        var e = new AuditEvent { EventType = AuditEventType.Bash, Command = longCmd };
        var result = AuditVisualizationService.FormatEventDetails(e);
        Assert.Equal(53, result.Length);
        Assert.EndsWith("...", result);
    }

    [Fact]
    public void FormatEventDetails_BashEvent_NullCommand_ReturnsEmpty()
    {
        var e = new AuditEvent { EventType = AuditEventType.Bash, Command = null };
        Assert.Equal("", AuditVisualizationService.FormatEventDetails(e));
    }

    [Fact]
    public void FormatEventDetails_RoleEvent_WithTask()
    {
        var e = new AuditEvent { EventType = AuditEventType.Role, Role = "writer", Task = "fix-bug" };
        Assert.Equal("writer on fix-bug", AuditVisualizationService.FormatEventDetails(e));
    }

    [Fact]
    public void FormatEventDetails_RoleEvent_WithoutTask()
    {
        var e = new AuditEvent { EventType = AuditEventType.Role, Role = "writer" };
        Assert.Equal("writer", AuditVisualizationService.FormatEventDetails(e));
    }

    [Fact]
    public void FormatEventDetails_ClaimEvent_ReturnsAgentName()
    {
        var e = new AuditEvent { EventType = AuditEventType.Claim, AgentName = "Mia" };
        Assert.Equal("Mia", AuditVisualizationService.FormatEventDetails(e));
    }

    [Fact]
    public void FormatEventDetails_ReleaseEvent_ReturnsAgentName()
    {
        var e = new AuditEvent { EventType = AuditEventType.Release, AgentName = "Bob" };
        Assert.Equal("Bob", AuditVisualizationService.FormatEventDetails(e));
    }

    [Fact]
    public void FormatEventDetails_CommitEvent_IncludesHashAndMessage()
    {
        var e = new AuditEvent
        {
            EventType = AuditEventType.Commit,
            CommitHash = "abc123",
            CommitMessage = "Fix the thing"
        };
        Assert.Equal("abc123 Fix the thing", AuditVisualizationService.FormatEventDetails(e));
    }

    [Fact]
    public void FormatEventDetails_CommitEvent_NullMessage()
    {
        var e = new AuditEvent
        {
            EventType = AuditEventType.Commit,
            CommitHash = "abc123",
            CommitMessage = null
        };
        Assert.Equal("abc123 ", AuditVisualizationService.FormatEventDetails(e));
    }

    [Fact]
    public void FormatEventDetails_BlockedEvent_IncludesPathAndReason()
    {
        var e = new AuditEvent
        {
            EventType = AuditEventType.Blocked,
            Path = "secret.env",
            BlockReason = "Not allowed"
        };
        Assert.Equal("secret.env - Not allowed", AuditVisualizationService.FormatEventDetails(e));
    }

    [Fact]
    public void FormatEventDetails_BlockedEvent_NullPath_UsesCommand()
    {
        var e = new AuditEvent
        {
            EventType = AuditEventType.Blocked,
            Path = null,
            Command = "rm -rf /",
            BlockReason = "Destructive"
        };
        Assert.Equal("rm -rf / - Destructive", AuditVisualizationService.FormatEventDetails(e));
    }

    [Fact]
    public void FormatEventDetails_ReadEvent_NullPath_ReturnsEmpty()
    {
        var e = new AuditEvent { EventType = AuditEventType.Read, Path = null };
        Assert.Equal("", AuditVisualizationService.FormatEventDetails(e));
    }

    #endregion
}
