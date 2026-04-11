namespace DynaDocs.Tests.Services;

using System.Text.Json;
using System.Text.Json.Serialization;
using DynaDocs.Models;
using DynaDocs.Services;

/// <summary>
/// Edge case tests for the audit system, exploring boundary conditions
/// in AuditService and SnapshotCompactionService.
/// Dispatched by inquisitor Charlie — task inquisition-audit-system-edges-1.
/// </summary>
public class AuditEdgeCaseTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _testDir;
    private readonly string _auditDir;

    public AuditEdgeCaseTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-edge-test-" + Guid.NewGuid().ToString("N")[..8]);
        _auditDir = Path.Combine(_testDir, "dydo", "_system", "audit");
        Directory.CreateDirectory(_auditDir);

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

    #region 1. ListSessionFiles includes baseline files

    [Fact]
    public void ListSessionFiles_ExcludesBaselineFiles()
    {
        var year = DateTime.UtcNow.Year.ToString();
        var yearDir = Path.Combine(_auditDir, year);
        Directory.CreateDirectory(yearDir);

        // Create a real session file
        File.WriteAllText(Path.Combine(yearDir, $"{DateTime.UtcNow:yyyy-MM-dd}-real-session.json"),
            JsonSerializer.Serialize(new AuditSession
            {
                SessionId = "real-session",
                Started = DateTime.UtcNow,
                Events = [new AuditEvent { EventType = AuditEventType.Read, Path = "a.md" }]
            }, JsonOpts));

        // Create a baseline file in the same directory
        File.WriteAllText(Path.Combine(yearDir, "_baseline-abc123.json"),
            JsonSerializer.Serialize(new SnapshotBaseline
            {
                Id = "abc123",
                Created = DateTime.UtcNow,
                Snapshot = new ProjectSnapshot
                {
                    GitCommit = "abc",
                    Files = ["a.cs"],
                    Folders = ["src"]
                }
            }, JsonOpts));

        var service = new AuditService(basePath: _testDir);
        var files = service.ListSessionFiles();

        Assert.Single(files);
        Assert.DoesNotContain(files, f => Path.GetFileName(f).StartsWith("_baseline-"));
    }

    #endregion

    #region LoadSessionFile surfaces corruption warning

    [Fact]
    public void LoadSessions_CorruptFile_WritesWarningToStderr()
    {
        var year = DateTime.UtcNow.Year.ToString();
        var yearDir = Path.Combine(_auditDir, year);
        Directory.CreateDirectory(yearDir);

        // Create a valid session file
        File.WriteAllText(Path.Combine(yearDir, $"{DateTime.UtcNow:yyyy-MM-dd}-good-session.json"),
            JsonSerializer.Serialize(new AuditSession
            {
                SessionId = "good-session",
                Started = DateTime.UtcNow,
                Events = [new AuditEvent { EventType = AuditEventType.Read, Path = "a.md" }]
            }, JsonOpts));

        // Create a corrupt file
        File.WriteAllText(Path.Combine(yearDir, $"{DateTime.UtcNow:yyyy-MM-dd}-corrupt-session.json"),
            "NOT VALID JSON {{{");

        var service = new AuditService(basePath: _testDir);

        var oldStderr = Console.Error;
        var stderrWriter = new StringWriter();
        Console.SetError(stderrWriter);
        try
        {
            var (sessions, _) = service.LoadSessions();

            // Good session is still loaded
            Assert.Single(sessions);
            Assert.Equal("good-session", sessions[0].SessionId);

            // Warning was written to stderr
            var stderr = stderrWriter.ToString();
            Assert.Contains("WARNING", stderr);
            Assert.Contains("corrupt-session", stderr);
        }
        finally
        {
            Console.SetError(oldStderr);
        }
    }

    #endregion

    #region 2. GetSession with path separator in session ID

    [Fact]
    public void GetSession_SessionIdWithForwardSlash_ThrowsDirectoryNotFound()
    {
        // BUG: sessionId containing "/" is interpolated into the glob pattern
        // "*-{sessionId}.json" and passed to Directory.GetFiles, which interprets
        // the slashes as path separators, escaping the year directory.
        // GetSession does NOT sanitize the input or catch the exception.
        var year = DateTime.UtcNow.Year.ToString();
        var yearDir = Path.Combine(_auditDir, year);
        Directory.CreateDirectory(yearDir);

        var service = new AuditService(basePath: _testDir);

        Assert.Throws<DirectoryNotFoundException>(
            () => service.GetSession("../../etc/passwd"));
    }

    [Fact]
    public void GetSession_SessionIdWithBackslash_ThrowsDirectoryNotFound()
    {
        // Backslash is only a path separator on Windows; on Linux it's a valid filename char.
        if (!OperatingSystem.IsWindows()) return;

        var year = DateTime.UtcNow.Year.ToString();
        var yearDir = Path.Combine(_auditDir, year);
        Directory.CreateDirectory(yearDir);

        var service = new AuditService(basePath: _testDir);

        Assert.Throws<DirectoryNotFoundException>(
            () => service.GetSession(@"..\..\etc\passwd"));
    }

    #endregion

    #region 3. ComputeBaselineId ordering sensitivity

    [Fact]
    public void ComputeBaselineId_DifferentFileOrder_ProducesDifferentHash()
    {
        // Hypothesis: ComputeBaselineId hashes files in list order without sorting.
        // Two snapshots with identical files in different order produce different IDs.
        var snapshot1 = new ProjectSnapshot
        {
            GitCommit = "abc",
            Files = ["b.cs", "a.cs", "c.cs"],
            Folders = ["src"]
        };

        var snapshot2 = new ProjectSnapshot
        {
            GitCommit = "abc",
            Files = ["a.cs", "b.cs", "c.cs"],
            Folders = ["src"]
        };

        // Use Compact to force baseline creation and compare — but we can test
        // more directly by comparing the delta between identical-content snapshots.
        // Since ComputeBaselineId is private static, we test indirectly via Compact.
        var yearDir1 = Path.Combine(_auditDir, "2090");
        var yearDir2 = Path.Combine(_auditDir, "2091");
        Directory.CreateDirectory(yearDir1);
        Directory.CreateDirectory(yearDir2);

        WriteSession(yearDir1, new AuditSession
        {
            SessionId = "s1",
            Started = DateTime.UtcNow,
            Events = [new AuditEvent { EventType = AuditEventType.Read, Path = "a.md" }],
            Snapshot = snapshot1
        });

        WriteSession(yearDir2, new AuditSession
        {
            SessionId = "s2",
            Started = DateTime.UtcNow,
            Events = [new AuditEvent { EventType = AuditEventType.Read, Path = "a.md" }],
            Snapshot = snapshot2
        });

        SnapshotCompactionService.Compact(yearDir1);
        SnapshotCompactionService.Compact(yearDir2);

        var baseline1 = Directory.GetFiles(yearDir1, "_baseline-*.json").Single();
        var baseline2 = Directory.GetFiles(yearDir2, "_baseline-*.json").Single();

        var id1 = Path.GetFileNameWithoutExtension(baseline1).Replace("_baseline-", "");
        var id2 = Path.GetFileNameWithoutExtension(baseline2).Replace("_baseline-", "");

        // BUG: Same logical content, different file order → different baseline IDs.
        // ComputeBaselineId should sort Files and Folders before hashing.
        Assert.NotEqual(id1, id2);
    }

    #endregion

    #region 4. Concurrent LogEvent data loss

    [Fact]
    public void LogEvent_CrossInstanceWrites_PreservesAllEvents()
    {
        // Sidecar-based append prevents data loss across service instances.
        // Cross-process writes append to a sidecar file instead of rewriting the session,
        // so no events are overwritten regardless of stale caches.
        var service0 = new AuditService(basePath: _testDir);
        var sessionId = "cross-instance-test";

        // Step 1: Create session
        service0.LogEvent(sessionId, new AuditEvent
        {
            EventType = AuditEventType.Read,
            Path = "event0.md"
        });

        // Steps 2-4: Cross-instance writes append to sidecar
        var serviceA = new AuditService(basePath: _testDir);
        serviceA.LogEvent(sessionId, new AuditEvent
        {
            EventType = AuditEventType.Edit,
            Path = "eventA.md"
        });

        var serviceB = new AuditService(basePath: _testDir);
        serviceB.LogEvent(sessionId, new AuditEvent
        {
            EventType = AuditEventType.Write,
            Path = "eventB.md"
        });

        serviceA.LogEvent(sessionId, new AuditEvent
        {
            EventType = AuditEventType.Edit,
            Path = "eventA2.md"
        });

        // All 4 events preserved — sidecar append doesn't overwrite
        var finalService = new AuditService(basePath: _testDir);
        var finalSession = finalService.GetSession(sessionId);
        Assert.NotNull(finalSession);
        Assert.Equal(4, finalSession.Events.Count);
        Assert.Contains(finalSession.Events, e => e.Path == "eventB.md");
    }

    #endregion

    #region 5. Compact with empty session

    [Fact]
    public void Compact_EmptySession_NoEventsNoSnapshotNoRef_HandlesGracefully()
    {
        // Hypothesis: A session with no events, no snapshot, and no ref
        // should not crash Compact.
        var yearDir = Path.Combine(_auditDir, "2092");
        Directory.CreateDirectory(yearDir);

        var emptySession = new AuditSession
        {
            SessionId = "empty-session",
            Started = DateTime.UtcNow,
            Events = []
        };

        WriteSession(yearDir, emptySession);

        var result = SnapshotCompactionService.Compact(yearDir);

        Assert.Equal(1, result.SessionsProcessed);
        // No baseline should be created since there are no snapshots
        Assert.Empty(Directory.GetFiles(yearDir, "_baseline-*.json"));
    }

    [Fact]
    public void Compact_MixOfEmptyAndNonEmptySessions_ProcessesAll()
    {
        var yearDir = Path.Combine(_auditDir, "2093");
        Directory.CreateDirectory(yearDir);

        // Empty session
        WriteSession(yearDir, new AuditSession
        {
            SessionId = "empty",
            Started = DateTime.UtcNow.AddHours(-1),
            Events = []
        });

        // Session with snapshot
        WriteSession(yearDir, new AuditSession
        {
            SessionId = "has-snapshot",
            Started = DateTime.UtcNow,
            Events = [new AuditEvent { EventType = AuditEventType.Read, Path = "a.md" }],
            Snapshot = new ProjectSnapshot
            {
                GitCommit = "abc",
                Files = ["a.cs"],
                Folders = ["src"]
            }
        });

        var result = SnapshotCompactionService.Compact(yearDir);

        Assert.Equal(2, result.SessionsProcessed);
        // Baseline should be created from the session that has a snapshot
        Assert.Single(Directory.GetFiles(yearDir, "_baseline-*.json"));

        // Empty session should still exist and be unchanged
        var emptyFile = Directory.GetFiles(yearDir, "*empty.json")
            .First(f => !Path.GetFileName(f).StartsWith("_baseline-"));
        var empty = JsonSerializer.Deserialize<AuditSession>(File.ReadAllText(emptyFile), JsonOpts)!;
        Assert.Null(empty.Snapshot);
        Assert.Null(empty.SnapshotRef);
    }

    [Fact]
    public void Compact_AllEmptySessions_ReturnsZeroSizeResult()
    {
        var yearDir = Path.Combine(_auditDir, "2094");
        Directory.CreateDirectory(yearDir);

        for (int i = 0; i < 3; i++)
        {
            WriteSession(yearDir, new AuditSession
            {
                SessionId = $"empty-{i}",
                Started = DateTime.UtcNow.AddHours(-i),
                Events = []
            });
        }

        var result = SnapshotCompactionService.Compact(yearDir);

        Assert.Equal(3, result.SessionsProcessed);
        Assert.Empty(Directory.GetFiles(yearDir, "_baseline-*.json"));
    }

    #endregion

    #region Append-only event writes (O(n²) fix)

    [Fact]
    public void LogEvent_SubsequentCalls_CreateSidecarFile()
    {
        var service1 = new AuditService(basePath: _testDir);
        var sessionId = "sidecar-test";

        // First event — writes full session JSON
        service1.LogEvent(sessionId, new AuditEvent
        {
            EventType = AuditEventType.Read,
            Path = "first.md"
        });

        // Second event from new service instance (simulates separate guard invocation)
        var service2 = new AuditService(basePath: _testDir);
        service2.LogEvent(sessionId, new AuditEvent
        {
            EventType = AuditEventType.Edit,
            Path = "second.md"
        });

        // Sidecar file should exist
        var year = DateTime.UtcNow.Year.ToString();
        var yearDir = Path.Combine(_auditDir, year);
        var sidecarPath = Path.Combine(yearDir, $"{sessionId}.events");
        Assert.True(File.Exists(sidecarPath));
    }

    [Fact]
    public void GetSession_MergesSidecarEvents()
    {
        var service1 = new AuditService(basePath: _testDir);
        var sessionId = "merge-sidecar-test";

        // First event
        service1.LogEvent(sessionId, new AuditEvent
        {
            EventType = AuditEventType.Read,
            Path = "first.md"
        });

        // Subsequent events from separate service instances
        for (int i = 2; i <= 5; i++)
        {
            var svc = new AuditService(basePath: _testDir);
            svc.LogEvent(sessionId, new AuditEvent
            {
                EventType = AuditEventType.Edit,
                Path = $"file{i}.md"
            });
        }

        // Verify sidecar exists and has content
        var year = DateTime.UtcNow.Year.ToString();
        var yearDir = Path.Combine(_auditDir, year);
        var sidecarPath = Path.Combine(yearDir, $"{sessionId}.events");
        Assert.True(File.Exists(sidecarPath), $"Sidecar not at {sidecarPath}");
        var sidecarContent = File.ReadAllText(sidecarPath);
        Assert.Contains("file2.md", sidecarContent);

        // Verify audit path consistency
        var reader = new AuditService(basePath: _testDir);
        var readerAuditPath = reader.GetAuditPath();
        Assert.Equal(Path.GetFullPath(_auditDir), Path.GetFullPath(readerAuditPath));

        // Verify session can be found
        var session = reader.GetSession(sessionId);
        Assert.NotNull(session);
        Assert.Equal(sessionId, session.SessionId);

        // Check sidecar from reader's perspective
        var readerYearDir = Path.Combine(readerAuditPath, year);
        var readerSidecar = Path.Combine(readerYearDir, $"{sessionId}.events");
        Assert.True(File.Exists(readerSidecar), $"Reader sidecar not at {readerSidecar}");

        // Test direct merge on a copy of the session
        var sessionCopy = new AuditSession { SessionId = sessionId, Started = DateTime.UtcNow, Events = [] };
        AuditService.MergeSidecarEvents(readerYearDir, sessionId, sessionCopy);
        Assert.Equal(4, sessionCopy.Events.Count);

        Assert.Equal(5, session.Events.Count);
        Assert.Equal("first.md", session.Events[0].Path);
        Assert.Equal("file5.md", session.Events[4].Path);
    }

    #endregion

    #region Helpers

    private static void WriteSession(string yearDir, AuditSession session)
    {
        var date = session.Started.ToString("yyyy-MM-dd");
        var path = Path.Combine(yearDir, $"{date}-{session.SessionId}.json");
        var json = JsonSerializer.Serialize(session, JsonOpts);
        File.WriteAllText(path, json);
    }

    #endregion
}
