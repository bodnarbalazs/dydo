namespace DynaDocs.Tests.Services;

using System.Text.Json;
using System.Text.Json.Serialization;
using DynaDocs.Models;
using DynaDocs.Services;

/// <summary>
/// Tests that compaction writes are atomic — if interrupted mid-write,
/// original session files remain valid and no data is lost.
/// </summary>
public class CompactionAtomicWriteTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _testDir;
    private readonly string _yearDir;

    public CompactionAtomicWriteTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-atomic-test-" + Guid.NewGuid().ToString("N")[..8]);
        _yearDir = Path.Combine(_testDir, "dydo", "_system", "audit", "2026");
        Directory.CreateDirectory(_yearDir);

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

    [Fact]
    public void Compact_SessionFiles_NoTempFilesLeftBehind()
    {
        // After successful compaction, no .tmp files should remain
        WriteSession(MakeSession("s1", MakeSnapshot(["a.cs"], ["src"])));
        WriteSession(MakeSession("s2", MakeSnapshot(["a.cs", "b.cs"], ["src"])));

        SnapshotCompactionService.Compact(_yearDir);

        var tempFiles = Directory.GetFiles(_yearDir, "*.tmp");
        Assert.Empty(tempFiles);
    }

    [Fact]
    public void Compact_BaselineFile_IsValidJsonAfterWrite()
    {
        WriteSession(MakeSession("s1", MakeSnapshot(["a.cs"], ["src"])));
        WriteSession(MakeSession("s2", MakeSnapshot(["a.cs", "b.cs"], ["src"])));

        SnapshotCompactionService.Compact(_yearDir);

        var baselineFiles = Directory.GetFiles(_yearDir, "_baseline-*.json");
        Assert.Single(baselineFiles);

        // Baseline must be complete, valid JSON
        var json = File.ReadAllText(baselineFiles[0]);
        var baseline = JsonSerializer.Deserialize<SnapshotBaseline>(json, JsonOpts);
        Assert.NotNull(baseline);
        Assert.NotNull(baseline.Snapshot);
        Assert.NotEmpty(baseline.Snapshot.Files);
    }

    [Fact]
    public void Compact_SessionFileOverwrite_PreservesDataIntegrity()
    {
        // Create sessions with known content, compact, then verify every session
        // file is complete valid JSON with correct delta references
        var files = Enumerable.Range(1, 20).Select(i => $"src/file-{i:D3}.cs").ToList();
        for (int i = 0; i < 3; i++)
        {
            var snap = MakeSnapshot(new List<string>(files), ["src"], $"commit-{i}");
            if (i > 0) snap.Files.Add($"src/extra-{i}.cs");
            var s = MakeSession($"sess-{i}", snap);
            s.Started = DateTime.UtcNow.AddHours(-3 + i);
            WriteSession(s);
        }

        SnapshotCompactionService.Compact(_yearDir);

        // Every session file must be parseable and have a valid snapshot_ref
        var sessionFiles = Directory.GetFiles(_yearDir, "*.json")
            .Where(f => !Path.GetFileName(f).StartsWith("_baseline-"))
            .ToList();

        foreach (var file in sessionFiles)
        {
            var json = File.ReadAllText(file);
            Assert.False(string.IsNullOrWhiteSpace(json), $"Session file is empty: {file}");

            var session = JsonSerializer.Deserialize<AuditSession>(json, JsonOpts);
            Assert.NotNull(session);
            Assert.NotNull(session.SnapshotRef);
            Assert.NotNull(session.SnapshotRef.BaseId);
        }
    }

    [Fact]
    public void Compact_ReadOnlySessionFile_ThrowsWithoutCorruptingOthers()
    {
        // If one session file can't be written, other files should not be corrupted.
        // This tests that writes are independent and atomic per-file.
        var snap = MakeSnapshot(["a.cs"], ["src"]);
        WriteSession(MakeSession("s1", MakeSnapshot(["a.cs"], ["src"])));
        WriteSession(MakeSession("s2", MakeSnapshot(["a.cs", "b.cs"], ["src"])));

        // Make s1's file read-only to simulate a write failure
        var s1File = Directory.GetFiles(_yearDir, "*s1.json").Single();
        File.SetAttributes(s1File, FileAttributes.ReadOnly);

        try
        {
            // Compaction should throw on the read-only file
            Assert.ThrowsAny<Exception>(() => SnapshotCompactionService.Compact(_yearDir));

            // s2's file should still be valid (either unchanged or properly updated)
            var s2File = Directory.GetFiles(_yearDir, "*s2.json").Single();
            var json = File.ReadAllText(s2File);
            Assert.False(string.IsNullOrWhiteSpace(json));
            var session = JsonSerializer.Deserialize<AuditSession>(json, JsonOpts);
            Assert.NotNull(session);
        }
        finally
        {
            File.SetAttributes(s1File, FileAttributes.Normal);
        }
    }

    #region Helpers

    private static ProjectSnapshot MakeSnapshot(List<string> files, List<string> folders, string commit = "abc123")
        => new() { GitCommit = commit, Files = files, Folders = folders, DocLinks = [] };

    private static AuditSession MakeSession(string id, ProjectSnapshot? snapshot = null)
        => new()
        {
            SessionId = id,
            Started = DateTime.UtcNow,
            Events = [new AuditEvent { EventType = AuditEventType.Read, Path = "test.md" }],
            Snapshot = snapshot
        };

    private void WriteSession(AuditSession session)
    {
        var date = session.Started.ToString("yyyy-MM-dd");
        var path = Path.Combine(_yearDir, $"{date}-{session.SessionId}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(session, JsonOpts));
    }

    #endregion
}
