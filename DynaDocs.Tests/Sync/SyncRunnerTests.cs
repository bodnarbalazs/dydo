namespace DynaDocs.Tests.Sync;

using DynaDocs.Models;
using DynaDocs.Sync;

/// <summary>
/// End-to-end bidirectional sync via the in-memory <see cref="FakeSyncAdapter"/>: repo-only edits,
/// external-only edits, non-overlapping auto-merge, and true conflicts — no Notion.
/// </summary>
public class SyncRunnerTests : IDisposable
{
    private readonly string _dir;
    private readonly string _tasksDir;
    private readonly FakeSyncAdapter _adapter = new();
    private readonly BaseSnapshotStore _base;

    public SyncRunnerTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "dydo-runner-" + Guid.NewGuid().ToString("N"));
        _tasksDir = Path.Combine(_dir, "tasks");
        Directory.CreateDirectory(_tasksDir);
        _base = new BaseSnapshotStore(Path.Combine(_dir, "snapshot.json"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private SyncRunner NewRunner() =>
        new(_adapter, _base, (localId, _, _) => Path.Combine(_tasksDir, localId + ".md"));

    /// <summary>A runner whose layout routes status "closed" into a "closed/" subfolder and everything
    /// else to the dir root — the Issue folder convention (slice brief §3).</summary>
    private SyncRunner NewFolderRunner()
    {
        var layout = new RepoFolderLayout(_tasksDir, "status", new Dictionary<string, string> { ["closed"] = "closed" });
        return new(_adapter, _base, layout.PathFor);
    }

    /// <summary>A runner modeling the live Issue corpus: 'resolved' routes into a "resolved/" subfolder,
    /// 'open' has no mapping and lives at the dir root (orchestrator decision, slice brief §1).</summary>
    private SyncRunner NewIssueRunner()
    {
        var layout = new RepoFolderLayout(_tasksDir, "status", new Dictionary<string, string> { ["resolved"] = "resolved" });
        return new(_adapter, _base, layout.PathFor);
    }

    private string OpenPath(string localId) => Path.Combine(_tasksDir, localId + ".md");
    private string ClosedPath(string localId) => Path.Combine(_tasksDir, "closed", localId + ".md");
    private string ResolvedPath(string localId) => Path.Combine(_tasksDir, "resolved", localId + ".md");

    private SyncDoc RepoDoc(string localId, string body, params (string, string)[] fields) => new()
    {
        LocalId = localId,
        Fields = fields.Select(f => new SyncField { Key = f.Item1, Value = f.Item2 }).ToList(),
        Body = body,
        SourcePath = Path.Combine(_tasksDir, localId + ".md"),
    };

    private static List<SyncField> F(params (string, string)[] fields) =>
        fields.Select(f => new SyncField { Key = f.Item1, Value = f.Item2 }).ToList();

    private static SyncDoc DocAt(string sourcePath, string localId, string body, params (string, string)[] fields) => new()
    {
        LocalId = localId,
        Fields = F(fields),
        Body = body,
        SourcePath = sourcePath,
    };

    private string RepoBody(string localId) =>
        SyncDocFile.Read(Path.Combine(_tasksDir, localId + ".md"), localId, "").Body;

    private SyncRecord External(string externalId) =>
        _adapter.ReadExternalState().First(r => r.ExternalId == externalId);

    [Fact]
    public void FirstRun_RepoOnly_CreatesExternalAndRecordsMapping()
    {
        NewRunner().Run([RepoDoc("t", "body", ("status", "open"))]);

        var ext = _adapter.ReadExternalState();
        Assert.Single(ext);
        Assert.Equal("open", ext[0].Fields.First(f => f.Key == "status").Value);
        Assert.NotNull(_base.Get("t")!.ExternalId);
    }

    [Fact]
    public void RepoEditAfterSync_PushesToExternal()
    {
        NewRunner().Run([RepoDoc("t", "body", ("status", "open"))]);
        var extId = _base.Get("t")!.ExternalId!;

        NewRunner().Run([RepoDoc("t", "body", ("status", "done"))]);

        Assert.Equal("done", External(extId).Fields.First(f => f.Key == "status").Value);
    }

    [Fact]
    public void ExternalEditAfterSync_WritesToRepo()
    {
        NewRunner().Run([RepoDoc("t", "# T\n\nbody", ("status", "open"))]);
        var extId = _base.Get("t")!.ExternalId!;

        _adapter.Edit(extId, F(("status", "done")), "# T\n\nbody");
        NewRunner().Run([RepoDoc("t", "# T\n\nbody", ("status", "open"))]);

        var written = SyncDocFile.Read(Path.Combine(_tasksDir, "t.md"), "t", "");
        Assert.Equal("done", written.GetField("status"));
    }

    [Fact]
    public void NonOverlappingBothEdits_AutoMerge_NoConflict()
    {
        NewRunner().Run([RepoDoc("t", "alpha\nbeta", ("status", "open"), ("priority", "low"))]);
        var extId = _base.Get("t")!.ExternalId!;

        // External changes priority; repo changes status.
        _adapter.Edit(extId, F(("status", "open"), ("priority", "high")), "alpha\nbeta");
        var result = NewRunner().Run([RepoDoc("t", "alpha\nbeta", ("status", "done"), ("priority", "low"))]);

        Assert.Equal(0, result.ConflictCount);
        var written = SyncDocFile.Read(Path.Combine(_tasksDir, "t.md"), "t", "");
        Assert.Equal("done", written.GetField("status"));
        Assert.Equal("high", written.GetField("priority"));
        Assert.Equal("high", External(extId).Fields.First(f => f.Key == "priority").Value);
    }

    [Fact]
    public void OverlappingBothEdits_Conflict_RepoWins_AndExternalGetsMarkers()
    {
        NewRunner().Run([RepoDoc("t", "line", ("status", "open"))]);
        var extId = _base.Get("t")!.ExternalId!;

        _adapter.Edit(extId, F(("status", "blocked")), "line");
        var result = NewRunner().Run([RepoDoc("t", "line", ("status", "done"))]);

        Assert.Equal(1, result.ConflictCount);
        Assert.Contains("t", result.ConflictedLocalIds);
        var written = SyncDocFile.Read(Path.Combine(_tasksDir, "t.md"), "t", "");
        Assert.Equal("done", written.GetField("status"));
    }

    [Fact]
    public void OverlappingBodyEdits_RecordVisibleConflictMarkers()
    {
        NewRunner().Run([RepoDoc("t", "one\ntwo\nthree", ("status", "open"))]);
        var extId = _base.Get("t")!.ExternalId!;

        _adapter.Edit(extId, F(("status", "open")), "one\nEXT\nthree");
        NewRunner().Run([RepoDoc("t", "one\nREPO\nthree", ("status", "open"))]);

        var written = RepoBody("t");
        Assert.Contains("<<<<<<< repo", written);
        Assert.Contains("REPO", written);
        Assert.Contains("EXT", written);
    }

    [Fact]
    public void ExternalCreatedObject_LandsInRepoUnderLocalId()
    {
        // A colleague creates a new object in the external view carrying its local id.
        _adapter.Seed("ext-new", F((SyncRunner.LocalIdField, "from-notion"), ("status", "open")), "# New\n\nbody");

        NewRunner().Run([]);

        var path = Path.Combine(_tasksDir, "from-notion.md");
        Assert.True(File.Exists(path));
        Assert.Equal("open", SyncDocFile.Read(path, "from-notion", "").GetField("status"));
    }

    [Fact]
    public void ExternalCreatedObject_LocalIdField_RoundTripsAndStaysStable()
    {
        // The reserved local-id field is the cross-boundary id carrier (SyncRunner.MapExternalToLocalId
        // keeps it in record.Fields). Pin its round-trip: it lands in the repo file's frontmatter,
        // and a second steady-state tick must NOT see a spurious change/conflict because of it.
        _adapter.Seed("ext-new", F((SyncRunner.LocalIdField, "from-notion"), ("status", "open")), "# New\n\nbody");

        NewRunner().Run([]);

        var path = Path.Combine(_tasksDir, "from-notion.md");
        var written = SyncDocFile.Read(path, "from-notion", "");
        Assert.Equal("from-notion", written.GetField(SyncRunner.LocalIdField));

        // Feed the repo doc back in unchanged; the external side is unchanged too -> pure no-op.
        var result = NewRunner().Run([written]);
        Assert.All(result.Results, r => Assert.Equal(ReconcileAction.None, r.Action));
    }

    [Fact]
    public void RepoDeletedAfterSync_DeletesExternal()
    {
        NewRunner().Run([RepoDoc("t", "body", ("status", "open"))]);
        Assert.Single(_adapter.ReadExternalState());

        NewRunner().Run([]); // repo no longer lists the task

        Assert.Empty(_adapter.ReadExternalState());
        Assert.Null(_base.Get("t"));
    }

    [Fact]
    public void ExternalDeletedAfterSync_DeletesRepoFile()
    {
        NewRunner().Run([RepoDoc("t", "body", ("status", "open"))]);
        var extId = _base.Get("t")!.ExternalId!;
        // Materialize the repo file so deletion has something to remove.
        SyncDocFile.Write(Path.Combine(_tasksDir, "t.md"), RepoDoc("t", "body", ("status", "open")));

        _adapter.DeleteExternal(extId);
        NewRunner().Run([RepoDoc("t", "body", ("status", "open"))]);

        Assert.False(File.Exists(Path.Combine(_tasksDir, "t.md")));
        Assert.Null(_base.Get("t"));
    }

    [Theory]
    [InlineData("../../evil", "evil")]
    [InlineData("/etc/passwd", "passwd")]
    [InlineData(@"C:\x", "x")]
    public void ExternalLocalId_PathTraversal_StaysWithinCanonicalDir(string evilId, string safeName)
    {
        // A colleague — or an attacker — controls the external record's local-id, which becomes a repo
        // file path. It must be sanitized to a bare name inside the canonical dir, never escape it.
        _adapter.Seed("ext-x", F((SyncRunner.LocalIdField, evilId), ("status", "open")), "body");

        NewRunner().Run([]);

        var landed = Path.Combine(_tasksDir, safeName + ".md");
        Assert.True(File.Exists(landed));
        Assert.StartsWith(_tasksDir, Path.GetFullPath(landed));
        // Nothing escaped above the tasks dir.
        var parent = Directory.GetParent(_tasksDir)!.FullName;
        Assert.False(File.Exists(Path.Combine(parent, safeName + ".md")));
    }

    [Fact]
    public void ExternalLocalId_ReducingToNothingUsable_IsRejected()
    {
        _adapter.Seed("ext-x", F((SyncRunner.LocalIdField, ".."), ("status", "open")), "body");

        Assert.Throws<SyncSecurityException>(() => NewRunner().Run([]));
    }

    [Fact]
    public void SteadyState_NoChanges_NoOps()
    {
        NewRunner().Run([RepoDoc("t", "body", ("status", "open"))]);
        var result = NewRunner().Run([RepoDoc("t", "body", ("status", "open"))]);

        Assert.All(result.Results, r => Assert.Equal(ReconcileAction.None, r.Action));
    }

    // --- Frontmatter-canonical status + folder auto-move (slice brief §3) ---

    [Fact]
    public void ExternalStatusChangeToClosed_MovesFileToClosedFolder_NotDeleteCreate()
    {
        // First sync: an open issue at the dir root creates the external row.
        var open = DocAt(OpenPath("bug"), "bug", "# Bug\n\nbody", ("title", "bug"), ("status", "open"));
        SyncDocFile.Write(OpenPath("bug"), open);
        NewFolderRunner().Run([open]);
        var extId = _base.Get("bug")!.ExternalId!;

        // A colleague flips status to closed in the external view. Feed the still-at-root repo doc.
        _adapter.Edit(extId, F(("title", "bug"), ("status", "closed")), "# Bug\n\nbody");
        var result = NewFolderRunner().Run([SyncDocFile.Read(OpenPath("bug"), "bug", OpenPath("bug"))]);

        // The file re-files into closed/ (folder derived from status); no orphan remains at the root.
        Assert.True(File.Exists(ClosedPath("bug")));
        Assert.False(File.Exists(OpenPath("bug")));
        Assert.Equal("closed", SyncDocFile.Read(ClosedPath("bug"), "bug", "").GetField("status"));

        // The move is NOT delete+create: same local id keeps the same external id and the one row.
        Assert.DoesNotContain(result.Results, r => r.Action is ReconcileAction.Delete or ReconcileAction.Create);
        Assert.Equal(extId, _base.Get("bug")!.ExternalId);
        Assert.Single(_adapter.ReadExternalState());
    }

    [Fact]
    public void RepoStatusChangeToClosed_MovesFileAndPushesToExternal()
    {
        var open = DocAt(OpenPath("bug"), "bug", "body", ("title", "bug"), ("status", "open"));
        SyncDocFile.Write(OpenPath("bug"), open);
        NewFolderRunner().Run([open]);
        var extId = _base.Get("bug")!.ExternalId!;

        // The user edits the file's frontmatter to closed; the file is still physically at the root.
        SyncDocFile.Write(OpenPath("bug"), DocAt(OpenPath("bug"), "bug", "body", ("title", "bug"), ("status", "closed")));
        NewFolderRunner().Run([SyncDocFile.Read(OpenPath("bug"), "bug", OpenPath("bug"))]);

        Assert.True(File.Exists(ClosedPath("bug")));
        Assert.False(File.Exists(OpenPath("bug")));
        Assert.Equal("closed", External(extId).Fields.First(f => f.Key == "status").Value);
        Assert.Equal(extId, _base.Get("bug")!.ExternalId);
    }

    [Fact]
    public void ReopenedIssue_UnmappedStatus_StaysInFolder_PushesStatusOnly()
    {
        // Reopening flips status to an UNMAPPED value ('open' has no folder entry). Per the unmapped-status
        // safety rule (finding 1) the sync engine never moves such a file — it keeps its current path; a CLI
        // handler owns any physical relocation. The status change still propagates to the external view.
        var closed = DocAt(ClosedPath("bug"), "bug", "body", ("title", "bug"), ("status", "closed"));
        SyncDocFile.Write(ClosedPath("bug"), closed);
        NewFolderRunner().Run([closed]);
        var extId = _base.Get("bug")!.ExternalId!;

        SyncDocFile.Write(ClosedPath("bug"), DocAt(ClosedPath("bug"), "bug", "body", ("title", "bug"), ("status", "open")));
        NewFolderRunner().Run([SyncDocFile.Read(ClosedPath("bug"), "bug", ClosedPath("bug"))]);

        Assert.True(File.Exists(ClosedPath("bug")));
        Assert.False(File.Exists(OpenPath("bug")));
        Assert.Equal("open", External(extId).Fields.First(f => f.Key == "status").Value);
        Assert.Equal(extId, _base.Get("bug")!.ExternalId);
    }

    [Fact]
    public void UnmappedStatusInArbitrarySubfolder_StaysPut_AcrossTick()
    {
        // A doc whose status has no folder mapping, sitting in an arbitrary subfolder, must not be yanked to
        // the dir root on a sync tick (finding 1) — folder placement for unmapped statuses is left untouched.
        var subPath = Path.Combine(_tasksDir, "archive", "bug.md");
        var doc = DocAt(subPath, "bug", "body", ("title", "bug"), ("status", "open"));
        SyncDocFile.Write(subPath, doc);
        NewFolderRunner().Run([doc]);

        var result = NewFolderRunner().Run([SyncDocFile.Read(subPath, "bug", subPath)]);

        Assert.True(File.Exists(subPath));
        Assert.False(File.Exists(OpenPath("bug")));
        Assert.All(result.Results, r => Assert.Equal(ReconcileAction.None, r.Action));
    }

    [Fact]
    public void LiveIssueCorpus_RootPlusResolved_NoChangeTick_MovesZeroFiles()
    {
        // Model the real corpus: 'open' issues at the dir root, 'resolved' issues under resolved/. A tick that
        // changes nothing must move ZERO files (orchestrator decision, slice brief §1).
        var open = DocAt(OpenPath("open-bug"), "open-bug", "body", ("title", "o"), ("status", "open"));
        var resolved = DocAt(ResolvedPath("done-bug"), "done-bug", "body", ("title", "d"), ("status", "resolved"));
        SyncDocFile.Write(OpenPath("open-bug"), open);
        SyncDocFile.Write(ResolvedPath("done-bug"), resolved);
        NewIssueRunner().Run([open, resolved]);

        var result = NewIssueRunner().Run(
        [
            SyncDocFile.Read(OpenPath("open-bug"), "open-bug", OpenPath("open-bug")),
            SyncDocFile.Read(ResolvedPath("done-bug"), "done-bug", ResolvedPath("done-bug")),
        ]);

        Assert.True(File.Exists(OpenPath("open-bug")));
        Assert.True(File.Exists(ResolvedPath("done-bug")));
        Assert.False(File.Exists(ResolvedPath("open-bug")));
        Assert.False(File.Exists(OpenPath("done-bug")));
        Assert.All(result.Results, r => Assert.Equal(ReconcileAction.None, r.Action));
    }

    [Fact]
    public void DuplicateLocalId_AcrossSubfolders_Run_FailsNamingBothPaths()
    {
        var atRoot = DocAt(OpenPath("bug"), "bug", "body", ("title", "b"), ("status", "open"));
        var inClosed = DocAt(ClosedPath("bug"), "bug", "body", ("title", "b"), ("status", "closed"));

        var ex = Assert.Throws<InvalidOperationException>(() => NewFolderRunner().Run([atRoot, inClosed]));
        Assert.Contains(OpenPath("bug"), ex.Message);
        Assert.Contains(ClosedPath("bug"), ex.Message);
    }

    [Fact]
    public void DuplicateLocalId_AcrossSubfolders_Plan_FailsNamingBothPaths()
    {
        var atRoot = DocAt(OpenPath("bug"), "bug", "body", ("title", "b"), ("status", "open"));
        var inClosed = DocAt(ClosedPath("bug"), "bug", "body", ("title", "b"), ("status", "closed"));

        var ex = Assert.Throws<InvalidOperationException>(() => NewFolderRunner().Plan([atRoot, inClosed]));
        Assert.Contains(OpenPath("bug"), ex.Message);
        Assert.Contains(ClosedPath("bug"), ex.Message);
    }

    [Fact]
    public void ClosedIssueAlreadyInClosedFolder_SteadyState_StaysPut()
    {
        var closed = DocAt(ClosedPath("bug"), "bug", "body", ("title", "bug"), ("status", "closed"));
        SyncDocFile.Write(ClosedPath("bug"), closed);
        NewFolderRunner().Run([closed]);

        var result = NewFolderRunner().Run([SyncDocFile.Read(ClosedPath("bug"), "bug", ClosedPath("bug"))]);

        Assert.All(result.Results, r => Assert.Equal(ReconcileAction.None, r.Action));
        Assert.True(File.Exists(ClosedPath("bug")));
        Assert.False(File.Exists(OpenPath("bug")));
    }

    [Fact]
    public void RepoClosedIssueAtRoot_FirstSync_FiledIntoClosedFolder()
    {
        // A closed issue sitting at the dir root is filed into closed/ on its first sync — folder is
        // derived presentation, so placement is corrected to match the canonical status.
        var closedAtRoot = DocAt(OpenPath("bug"), "bug", "body", ("title", "bug"), ("status", "closed"));
        SyncDocFile.Write(OpenPath("bug"), closedAtRoot);

        NewFolderRunner().Run([SyncDocFile.Read(OpenPath("bug"), "bug", OpenPath("bug"))]);

        Assert.True(File.Exists(ClosedPath("bug")));
        Assert.False(File.Exists(OpenPath("bug")));
    }
}
