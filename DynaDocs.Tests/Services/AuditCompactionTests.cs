namespace DynaDocs.Tests.Services;

using System.Text.Json;
using System.Text.Json.Serialization;
using DynaDocs.Models;
using DynaDocs.Services;

public class AuditCompactionTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _testDir;
    private readonly string _auditDir;
    private readonly string _yearDir;

    public AuditCompactionTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-compact-test-" + Guid.NewGuid().ToString("N")[..8]);
        _auditDir = Path.Combine(_testDir, "dydo", "_system", "audit");
        _yearDir = Path.Combine(_auditDir, "2026");
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

    #region Delta Computation

    [Fact]
    public void ComputeDelta_IdenticalSnapshots_ProducesEmptyDelta()
    {
        var snapshot = MakeSnapshot(["a.cs", "b.cs"], ["src"], commit: "abc");
        var delta = SnapshotCompactionService.ComputeDelta(snapshot, snapshot);

        Assert.True(delta.IsEmpty);
    }

    [Fact]
    public void ComputeDelta_FilesAdded()
    {
        var baseSnap = MakeSnapshot(["a.cs", "b.cs"], ["src"]);
        var current = MakeSnapshot(["a.cs", "b.cs", "c.cs"], ["src"]);

        var delta = SnapshotCompactionService.ComputeDelta(current, baseSnap);

        Assert.Single(delta.FilesAdded);
        Assert.Contains("c.cs", delta.FilesAdded);
        Assert.Empty(delta.FilesRemoved);
    }

    [Fact]
    public void ComputeDelta_FilesRemoved()
    {
        var baseSnap = MakeSnapshot(["a.cs", "b.cs", "c.cs"], ["src"]);
        var current = MakeSnapshot(["a.cs"], ["src"]);

        var delta = SnapshotCompactionService.ComputeDelta(current, baseSnap);

        Assert.Empty(delta.FilesAdded);
        Assert.Equal(2, delta.FilesRemoved.Count);
        Assert.Contains("b.cs", delta.FilesRemoved);
        Assert.Contains("c.cs", delta.FilesRemoved);
    }

    [Fact]
    public void ComputeDelta_DocLinksAddedAndRemoved()
    {
        var baseSnap = MakeSnapshot(["a.md", "b.md"], ["docs"]);
        baseSnap.DocLinks["a.md"] = ["b.md"];

        var current = MakeSnapshot(["a.md", "b.md", "c.md"], ["docs"]);
        current.DocLinks["a.md"] = ["c.md"];  // b.md removed, c.md added

        var delta = SnapshotCompactionService.ComputeDelta(current, baseSnap);

        Assert.Contains("a.md", delta.DocLinksAdded.Keys);
        Assert.Contains("c.md", delta.DocLinksAdded["a.md"]);
        Assert.Contains("a.md", delta.DocLinksRemoved.Keys);
        Assert.Contains("b.md", delta.DocLinksRemoved["a.md"]);
    }

    #endregion

    #region Apply Delta

    [Fact]
    public void ApplyDelta_ProducesCorrectSnapshot()
    {
        var baseSnap = MakeSnapshot(
            ["a.cs", "b.cs", "old.cs"],
            ["src", "lib"],
            commit: "base-commit");

        var delta = new SnapshotDelta
        {
            FilesAdded = ["new.cs"],
            FilesRemoved = ["old.cs"],
            FoldersAdded = ["tests"],
            FoldersRemoved = ["lib"],
            DocLinksAdded = { ["a.md"] = ["b.md"] }
        };

        var result = SnapshotCompactionService.ApplyDelta(baseSnap, delta, "new-commit");

        Assert.Equal("new-commit", result.GitCommit);
        Assert.Contains("a.cs", result.Files);
        Assert.Contains("b.cs", result.Files);
        Assert.Contains("new.cs", result.Files);
        Assert.DoesNotContain("old.cs", result.Files);
        Assert.Contains("src", result.Folders);
        Assert.Contains("tests", result.Folders);
        Assert.DoesNotContain("lib", result.Folders);
        Assert.Contains("a.md", result.DocLinks.Keys);
    }

    [Fact]
    public void ComputeDelta_ThenApply_RoundTrips()
    {
        var baseSnap = MakeSnapshot(
            ["a.cs", "b.cs", "c.cs"],
            ["src", "lib"],
            commit: "base");
        baseSnap.DocLinks["index.md"] = ["about.md", "guide.md"];

        var target = MakeSnapshot(
            ["a.cs", "d.cs", "e.cs"],
            ["src", "tests"],
            commit: "target");
        target.DocLinks["index.md"] = ["about.md", "new.md"];
        target.DocLinks["new.md"] = ["other.md"];

        var delta = SnapshotCompactionService.ComputeDelta(target, baseSnap);
        var restored = SnapshotCompactionService.ApplyDelta(baseSnap, delta, "target");

        Assert.Equal(target.Files.OrderBy(f => f).ToList(), restored.Files.OrderBy(f => f).ToList());
        Assert.Equal(target.Folders.OrderBy(f => f).ToList(), restored.Folders.OrderBy(f => f).ToList());
    }

    #endregion

    #region Snapshot Resolution

    [Fact]
    public void ResolveSnapshot_InlineSnapshot_ReturnedDirectly()
    {
        var session = MakeSession("s1", MakeSnapshot(["a.cs"], ["src"]));

        var resolved = SnapshotCompactionService.ResolveSnapshot(
            session, _ => null, _ => null);

        Assert.NotNull(resolved);
        Assert.Contains("a.cs", resolved.Files);
    }

    [Fact]
    public void ResolveSnapshot_DeltaFromBaseline()
    {
        var baselineSnapshot = MakeSnapshot(["a.cs", "b.cs"], ["src"], commit: "base");
        var baseline = new SnapshotBaseline
        {
            Id = "bl-1",
            Created = DateTime.UtcNow,
            Snapshot = baselineSnapshot
        };

        var session = MakeSession("s1");
        session.SnapshotRef = new SnapshotRef
        {
            BaseId = "bl-1",
            Depth = 1,
            Delta = new SnapshotDelta { FilesAdded = ["c.cs"] }
        };

        var resolved = SnapshotCompactionService.ResolveSnapshot(
            session,
            id => id == "bl-1" ? baseline : null,
            _ => null);

        Assert.NotNull(resolved);
        Assert.Contains("a.cs", resolved.Files);
        Assert.Contains("b.cs", resolved.Files);
        Assert.Contains("c.cs", resolved.Files);
    }

    [Fact]
    public void ResolveSnapshot_EmptyDelta_ReturnsBaselineDirectly()
    {
        var baselineSnapshot = MakeSnapshot(["a.cs"], ["src"], commit: "base");
        var baseline = new SnapshotBaseline
        {
            Id = "bl-1",
            Created = DateTime.UtcNow,
            Snapshot = baselineSnapshot
        };

        var session = MakeSession("s1");
        session.SnapshotRef = new SnapshotRef
        {
            BaseId = "bl-1",
            Depth = 1,
            Delta = null
        };

        var resolved = SnapshotCompactionService.ResolveSnapshot(
            session,
            id => id == "bl-1" ? baseline : null,
            _ => null);

        Assert.NotNull(resolved);
        Assert.Equal(baselineSnapshot.Files, resolved.Files);
    }

    [Fact]
    public void ResolveSnapshot_NestedDeltaChain_Depth3()
    {
        // baseline → session A (depth 1) → session B (depth 2) → session C (depth 3)
        var baselineSnapshot = MakeSnapshot(["a.cs"], ["src"], commit: "base");
        var baseline = new SnapshotBaseline
        {
            Id = "bl-1",
            Created = DateTime.UtcNow,
            Snapshot = baselineSnapshot
        };

        var sessionA = MakeSession("sA");
        sessionA.SnapshotRef = new SnapshotRef
        {
            BaseId = "bl-1",
            Depth = 1,
            Delta = new SnapshotDelta { FilesAdded = ["b.cs"] }
        };

        var sessionB = MakeSession("sB");
        sessionB.SnapshotRef = new SnapshotRef
        {
            BaseId = "sA",
            Depth = 2,
            Delta = new SnapshotDelta { FilesAdded = ["c.cs"] }
        };

        var sessionC = MakeSession("sC");
        sessionC.SnapshotRef = new SnapshotRef
        {
            BaseId = "sB",
            Depth = 3,
            Delta = new SnapshotDelta { FilesAdded = ["d.cs"] }
        };

        var sessions = new Dictionary<string, AuditSession>
        {
            ["sA"] = sessionA, ["sB"] = sessionB, ["sC"] = sessionC
        };

        var resolved = SnapshotCompactionService.ResolveSnapshot(
            sessionC,
            id => id == "bl-1" ? baseline : null,
            id => sessions.GetValueOrDefault(id));

        Assert.NotNull(resolved);
        Assert.Contains("a.cs", resolved.Files);
        Assert.Contains("b.cs", resolved.Files);
        Assert.Contains("c.cs", resolved.Files);
        Assert.Contains("d.cs", resolved.Files);
    }

    [Fact]
    public void ResolveSnapshot_DeepSessionChain_ResolvesCorrectly()
    {
        // Build a chain deeper than MaxChainDepth: baseline → s1 → s2 → ... → s8
        // Session chains resolve via caching, so deep chains work correctly.
        var baselineSnapshot = MakeSnapshot(["a.cs"], ["src"], commit: "base");
        var baseline = new SnapshotBaseline
        {
            Id = "bl-1",
            Created = DateTime.UtcNow,
            Snapshot = baselineSnapshot
        };

        var sessions = new Dictionary<string, AuditSession>();
        var prevId = "bl-1";
        for (var i = 1; i <= 8; i++)
        {
            var s = MakeSession($"s{i}");
            s.SnapshotRef = new SnapshotRef
            {
                BaseId = prevId,
                Depth = i,
                Delta = new SnapshotDelta { FilesAdded = [$"file{i}.cs"] }
            };
            sessions[$"s{i}"] = s;
            prevId = $"s{i}";
        }

        var cache = new Dictionary<string, ProjectSnapshot>();
        var resolved = SnapshotCompactionService.ResolveSnapshot(
            sessions["s8"],
            id => id == "bl-1" ? baseline : null,
            id => sessions.GetValueOrDefault(id),
            cache);

        Assert.NotNull(resolved);
        // Should have original file + all 8 added files
        Assert.Contains("a.cs", resolved.Files);
        for (var i = 1; i <= 8; i++)
            Assert.Contains($"file{i}.cs", resolved.Files);
    }

    [Fact]
    public void ResolveSnapshot_NoSnapshotOrRef_ReturnsNull()
    {
        var session = MakeSession("s1");

        var resolved = SnapshotCompactionService.ResolveSnapshot(
            session, _ => null, _ => null);

        Assert.Null(resolved);
    }

    [Fact]
    public void ResolveSnapshot_MissingBase_Throws()
    {
        var session = MakeSession("s1");
        session.SnapshotRef = new SnapshotRef
        {
            BaseId = "nonexistent",
            Depth = 1,
            Delta = new SnapshotDelta { FilesAdded = ["x.cs"] }
        };

        Assert.Throws<InvalidOperationException>(() =>
            SnapshotCompactionService.ResolveSnapshot(
                session, _ => null, _ => null));
    }

    [Fact]
    public void ResolveSnapshot_CachesResults()
    {
        var baselineSnapshot = MakeSnapshot(["a.cs"], ["src"], commit: "base");
        var baseline = new SnapshotBaseline
        {
            Id = "bl-1",
            Created = DateTime.UtcNow,
            Snapshot = baselineSnapshot
        };

        var session = MakeSession("s1");
        session.SnapshotRef = new SnapshotRef { BaseId = "bl-1", Depth = 1 };

        var cache = new Dictionary<string, ProjectSnapshot>();
        var loadCount = 0;

        var resolved1 = SnapshotCompactionService.ResolveSnapshot(
            session,
            id => { loadCount++; return id == "bl-1" ? baseline : null; },
            _ => null,
            cache);

        var resolved2 = SnapshotCompactionService.ResolveSnapshot(
            session,
            id => { loadCount++; return id == "bl-1" ? baseline : null; },
            _ => null,
            cache);

        Assert.Same(resolved1, resolved2);
        Assert.Equal(1, loadCount); // Only loaded once
    }

    #endregion

    #region Full Compaction

    [Fact]
    public void Compact_MultipleSessionsWithInlineSnapshots()
    {
        // Create 5 sessions with nearly identical inline snapshots
        var baseFiles = Enumerable.Range(1, 100).Select(i => $"src/file-{i:D3}.cs").ToList();
        var baseFolders = new List<string> { "src" };

        for (int i = 0; i < 5; i++)
        {
            var files = new List<string>(baseFiles);
            if (i > 0) files.Add($"src/new-{i}.cs"); // Slight variation

            var session = MakeSession($"session-{i}", MakeSnapshot(files, baseFolders, commit: $"commit-{i}"));
            session.Started = DateTime.UtcNow.AddHours(-5 + i);
            WriteSession(session);
        }

        var result = SnapshotCompactionService.Compact(_yearDir);

        Assert.Equal(5, result.SessionsProcessed);
        Assert.True(result.NewTotalSizeBytes < result.OldTotalSizeBytes,
            $"Expected compression. Old: {result.OldTotalSizeBytes}, New: {result.NewTotalSizeBytes}");
        Assert.True(result.CompressionRatio > 0.5, $"Expected >50% compression, got {result.CompressionRatio:P1}");

        // Verify baseline was created
        var baselineFiles = Directory.GetFiles(_yearDir, "_baseline-*.json");
        Assert.Single(baselineFiles);

        // Verify all sessions now have snapshot_ref
        foreach (var file in Directory.GetFiles(_yearDir, "*.json").Where(f => !Path.GetFileName(f).StartsWith("_baseline-")))
        {
            var session = JsonSerializer.Deserialize<AuditSession>(File.ReadAllText(file), JsonOpts)!;
            Assert.Null(session.Snapshot);
            Assert.NotNull(session.SnapshotRef);
            Assert.Equal(1, session.SnapshotRef.Depth);
        }
    }

    [Fact]
    public void Compact_PreservesSnapshotContent()
    {
        // Create sessions with different snapshots, compact, then verify resolution matches originals
        var sessions = new List<(string Id, ProjectSnapshot Original)>();

        for (int i = 0; i < 3; i++)
        {
            var files = Enumerable.Range(1, 50).Select(j => $"src/file-{j:D3}.cs").ToList();
            if (i >= 1) files.Add("src/extra-a.cs");
            if (i >= 2) files.Add("src/extra-b.cs");

            var snapshot = MakeSnapshot(files, ["src"], commit: $"commit-{i}");
            var session = MakeSession($"session-{i}", snapshot);
            session.Started = DateTime.UtcNow.AddHours(-3 + i);

            sessions.Add((session.SessionId, CloneSnapshot(snapshot)));
            WriteSession(session);
        }

        SnapshotCompactionService.Compact(_yearDir);

        // Load baselines for resolution
        var baselines = new Dictionary<string, SnapshotBaseline>();
        foreach (var file in Directory.GetFiles(_yearDir, "_baseline-*.json"))
        {
            var bl = JsonSerializer.Deserialize<SnapshotBaseline>(File.ReadAllText(file), JsonOpts)!;
            baselines[bl.Id] = bl;
        }

        // Resolve each session and verify it matches the original
        var sessionFiles = Directory.GetFiles(_yearDir, "*.json")
            .Where(f => !Path.GetFileName(f).StartsWith("_baseline-"))
            .ToList();

        foreach (var (id, original) in sessions)
        {
            var file = sessionFiles.First(f => f.Contains(id));
            var session = JsonSerializer.Deserialize<AuditSession>(File.ReadAllText(file), JsonOpts)!;

            var resolved = SnapshotCompactionService.ResolveSnapshot(
                session,
                bid => baselines.GetValueOrDefault(bid),
                sid => LoadSessionFromFiles(sessionFiles, sid));

            Assert.NotNull(resolved);
            Assert.Equal(
                original.Files.OrderBy(f => f).ToList(),
                resolved.Files.OrderBy(f => f).ToList());
            Assert.Equal(
                original.Folders.OrderBy(f => f).ToList(),
                resolved.Folders.OrderBy(f => f).ToList());
        }
    }

    [Fact]
    public void Compact_SessionsWithoutSnapshots_Preserved()
    {
        // Mix of sessions with and without snapshots
        var withSnap = MakeSession("with-snap", MakeSnapshot(["a.cs"], ["src"]));
        var withoutSnap = MakeSession("without-snap");

        WriteSession(withSnap);
        WriteSession(withoutSnap);

        var result = SnapshotCompactionService.Compact(_yearDir);

        Assert.Equal(2, result.SessionsProcessed);

        // Session without snapshot should still exist and be valid
        var files = Directory.GetFiles(_yearDir, "*without-snap.json");
        Assert.Single(files);
        var session = JsonSerializer.Deserialize<AuditSession>(File.ReadAllText(files[0]), JsonOpts)!;
        Assert.Null(session.Snapshot);
        Assert.Null(session.SnapshotRef);
    }

    [Fact]
    public void Compact_RemovesOldBaselines()
    {
        // Create an old baseline
        var oldBaseline = new SnapshotBaseline
        {
            Id = "old-baseline",
            Created = DateTime.UtcNow.AddDays(-1),
            Snapshot = MakeSnapshot(["old.cs"], ["old"])
        };
        File.WriteAllText(
            Path.Combine(_yearDir, "_baseline-old-baseline.json"),
            JsonSerializer.Serialize(oldBaseline, JsonOpts));

        // Create a session with inline snapshot
        WriteSession(MakeSession("s1", MakeSnapshot(["a.cs"], ["src"])));

        var result = SnapshotCompactionService.Compact(_yearDir);

        Assert.Equal(1, result.OldBaselinesRemoved);

        // Old baseline file should be gone
        Assert.Empty(Directory.GetFiles(_yearDir, "_baseline-old-baseline.json"));

        // New baseline should exist
        Assert.Single(Directory.GetFiles(_yearDir, "_baseline-*.json"));
    }

    [Fact]
    public void Compact_RecompactsAlreadyCompactedSessions()
    {
        // First compaction
        var files = Enumerable.Range(1, 50).Select(i => $"file-{i:D3}.cs").ToList();
        for (int i = 0; i < 3; i++)
        {
            var snap = MakeSnapshot(new List<string>(files), ["src"], commit: $"c{i}");
            if (i > 0) snap.Files.Add($"extra-{i}.cs");
            var s = MakeSession($"s{i}", snap);
            s.Started = DateTime.UtcNow.AddHours(-3 + i);
            WriteSession(s);
        }

        SnapshotCompactionService.Compact(_yearDir);

        // Second compaction should still work
        var result = SnapshotCompactionService.Compact(_yearDir);

        Assert.Equal(3, result.SessionsProcessed);

        // Verify snapshots still resolve correctly
        var baselines = LoadAllBaselines();
        var sessionFilesList = Directory.GetFiles(_yearDir, "*.json")
            .Where(f => !Path.GetFileName(f).StartsWith("_baseline-")).ToList();

        foreach (var sf in sessionFilesList)
        {
            var session = JsonSerializer.Deserialize<AuditSession>(File.ReadAllText(sf), JsonOpts)!;
            var resolved = SnapshotCompactionService.ResolveSnapshot(
                session,
                id => baselines.GetValueOrDefault(id),
                id => LoadSessionFromFiles(sessionFilesList, id));
            Assert.NotNull(resolved);
        }
    }

    #endregion

    #region Backward Compatibility

    [Fact]
    public void ResolveSnapshot_OldFormatInlineSnapshot_StillWorks()
    {
        // Old format: session has inline snapshot, no snapshot_ref
        var session = MakeSession("old-session", MakeSnapshot(["legacy.cs"], ["src"]));

        var resolved = SnapshotCompactionService.ResolveSnapshot(
            session, _ => null, _ => null);

        Assert.NotNull(resolved);
        Assert.Contains("legacy.cs", resolved.Files);
    }

    [Fact]
    public void Compact_MixedOldAndNewFormats()
    {
        // Old-format session (inline)
        WriteSession(MakeSession("old-s1", MakeSnapshot(["a.cs", "b.cs"], ["src"], commit: "c1")));

        // Already-compacted session (delta ref) — create a baseline first
        var baseline = new SnapshotBaseline
        {
            Id = "existing-bl",
            Created = DateTime.UtcNow.AddDays(-1),
            Snapshot = MakeSnapshot(["a.cs", "b.cs"], ["src"], commit: "c0")
        };
        File.WriteAllText(
            Path.Combine(_yearDir, "_baseline-existing-bl.json"),
            JsonSerializer.Serialize(baseline, JsonOpts));

        var compacted = MakeSession("new-s1");
        compacted.SnapshotRef = new SnapshotRef
        {
            BaseId = "existing-bl",
            Depth = 1,
            Delta = new SnapshotDelta { FilesAdded = ["c.cs"] }
        };
        WriteSession(compacted);

        var result = SnapshotCompactionService.Compact(_yearDir);

        Assert.Equal(2, result.SessionsProcessed);

        // Both sessions should now reference the new baseline
        var newBaselines = LoadAllBaselines();
        Assert.Single(newBaselines);

        foreach (var file in Directory.GetFiles(_yearDir, "*.json").Where(f => !Path.GetFileName(f).StartsWith("_baseline-")))
        {
            var s = JsonSerializer.Deserialize<AuditSession>(File.ReadAllText(file), JsonOpts)!;
            if (s.SnapshotRef != null)
                Assert.Contains(s.SnapshotRef.BaseId, newBaselines.Keys);
        }
    }

    #endregion

    #region Fixture-Based Tests

    [Fact]
    public void Compact_LargeFixtureDataset_SignificantReduction()
    {
        var fixtureDir = FindFixtureDir();
        if (fixtureDir == null) return; // Fixtures not available

        // Copy fixture files to test directory
        foreach (var file in Directory.GetFiles(fixtureDir, "*.json"))
            File.Copy(file, Path.Combine(_yearDir, Path.GetFileName(file)));

        var originalSize = Directory.GetFiles(_yearDir, "*.json").Sum(f => new FileInfo(f).Length);

        var result = SnapshotCompactionService.Compact(_yearDir);

        Assert.True(result.SessionsProcessed > 100, $"Expected >100 sessions, got {result.SessionsProcessed}");
        Assert.True(result.CompressionRatio > 0.3, $"Expected >30% compression, got {result.CompressionRatio:P1}");

        // Verify every session can be resolved
        var baselines = LoadAllBaselines();
        var sessionFiles = Directory.GetFiles(_yearDir, "*.json")
            .Where(f => !Path.GetFileName(f).StartsWith("_baseline-")).ToList();

        foreach (var sf in sessionFiles)
        {
            var session = JsonSerializer.Deserialize<AuditSession>(File.ReadAllText(sf), JsonOpts)!;
            var resolved = SnapshotCompactionService.ResolveSnapshot(
                session,
                id => baselines.GetValueOrDefault(id),
                id => LoadSessionFromFiles(sessionFiles, id));
            // Sessions without snapshots resolve to null — that's fine
        }
    }

    [Fact]
    public void Compact_LargeFixtureDataset_SnapshotsPreserved()
    {
        var fixtureDir = FindFixtureDir();
        if (fixtureDir == null) return;

        // Copy and snapshot originals before compaction
        var originals = new Dictionary<string, ProjectSnapshot>();
        foreach (var file in Directory.GetFiles(fixtureDir, "*.json"))
        {
            File.Copy(file, Path.Combine(_yearDir, Path.GetFileName(file)));
            var session = JsonSerializer.Deserialize<AuditSession>(File.ReadAllText(file), JsonOpts);
            if (session?.Snapshot != null)
                originals[session.SessionId] = CloneSnapshot(session.Snapshot);
        }

        SnapshotCompactionService.Compact(_yearDir);

        var baselines = LoadAllBaselines();
        var sessionFiles = Directory.GetFiles(_yearDir, "*.json")
            .Where(f => !Path.GetFileName(f).StartsWith("_baseline-")).ToList();

        // Verify each session's resolved snapshot matches pre-compaction
        foreach (var (sessionId, original) in originals)
        {
            var file = sessionFiles.FirstOrDefault(f => f.Contains(sessionId));
            if (file == null) continue;

            var session = JsonSerializer.Deserialize<AuditSession>(File.ReadAllText(file), JsonOpts)!;
            var resolved = SnapshotCompactionService.ResolveSnapshot(
                session,
                id => baselines.GetValueOrDefault(id),
                id => LoadSessionFromFiles(sessionFiles, id));

            Assert.NotNull(resolved);
            Assert.Equal(
                original.Files.OrderBy(f => f).ToList(),
                resolved.Files.OrderBy(f => f).ToList());
            Assert.Equal(
                original.Folders.OrderBy(f => f).ToList(),
                resolved.Folders.OrderBy(f => f).ToList());
        }
    }

    #endregion

    #region Helpers

    private static ProjectSnapshot MakeSnapshot(
        List<string> files, List<string> folders, string commit = "abc123",
        Dictionary<string, List<string>>? docLinks = null)
    {
        return new ProjectSnapshot
        {
            GitCommit = commit,
            Files = files,
            Folders = folders,
            DocLinks = docLinks ?? new()
        };
    }

    private static ProjectSnapshot CloneSnapshot(ProjectSnapshot s)
    {
        return new ProjectSnapshot
        {
            GitCommit = s.GitCommit,
            Files = new List<string>(s.Files),
            Folders = new List<string>(s.Folders),
            DocLinks = s.DocLinks.ToDictionary(kv => kv.Key, kv => new List<string>(kv.Value))
        };
    }

    private static AuditSession MakeSession(string id, ProjectSnapshot? snapshot = null)
    {
        return new AuditSession
        {
            SessionId = id,
            Started = DateTime.UtcNow,
            Events = [new AuditEvent { EventType = AuditEventType.Read, Path = "test.md" }],
            Snapshot = snapshot
        };
    }

    private void WriteSession(AuditSession session)
    {
        var date = session.Started.ToString("yyyy-MM-dd");
        var path = Path.Combine(_yearDir, $"{date}-{session.SessionId}.json");
        var json = JsonSerializer.Serialize(session, JsonOpts);
        File.WriteAllText(path, json);
    }

    private Dictionary<string, SnapshotBaseline> LoadAllBaselines()
    {
        var baselines = new Dictionary<string, SnapshotBaseline>();
        foreach (var file in Directory.GetFiles(_yearDir, "_baseline-*.json"))
        {
            var bl = JsonSerializer.Deserialize<SnapshotBaseline>(File.ReadAllText(file), JsonOpts)!;
            baselines[bl.Id] = bl;
        }
        return baselines;
    }

    private static AuditSession? LoadSessionFromFiles(List<string> files, string sessionId)
    {
        var file = files.FirstOrDefault(f => f.Contains(sessionId));
        if (file == null) return null;
        return JsonSerializer.Deserialize<AuditSession>(File.ReadAllText(file), JsonOpts);
    }

    private static string? FindFixtureDir()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "DynaDocs.sln")))
            {
                var fixture = Path.Combine(dir, "DynaDocs.Tests", "Fixtures", "audit-large", "2026");
                return Directory.Exists(fixture) ? fixture : null;
            }
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    #endregion
}
