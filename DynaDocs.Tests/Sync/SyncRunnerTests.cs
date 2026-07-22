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
    public void CompletedTick_SweepsOrphanLastActivity_WithNoBaseEntry()
    {
        // Finding 7: a last-activity seeded for a doc whose create never confirmed a base entry is unreachable
        // by Retire (which only fires for objects that HAVE a base entry). A completed tick must sweep it, while
        // a live object's activity is left intact.
        NewRunner().Run([RepoDoc("t", "body", ("status", "open"))]);
        Assert.NotNull(_base.GetLastActivity("t"));

        _base.SetLastActivity("orphan", "2026-01-01"); // no base object for "orphan"
        NewRunner().Run([RepoDoc("t", "body", ("status", "open"))]); // a completed tick

        Assert.Null(_base.GetLastActivity("orphan")); // orphan swept
        Assert.NotNull(_base.GetLastActivity("t"));    // the live object's activity kept
    }

    [Fact]
    public void RetireCommits_EvenWhenApplyFailsInSameTick_HoldingOtherAdvancesBack()
    {
        // Finding 7 / brief §10: a Retire (both sides gone, stale base entry) removes its base entry AND its
        // last-activity in the same tick even when a failing Apply holds other advances back — because nothing
        // was pushed for the retired object, dropping it is safe regardless of whether the batch applied. The
        // failed push, by contrast, must NOT advance its base (it self-heals on retry).
        NewRunner().Run([RepoDoc("gone", "body", ("status", "open")), RepoDoc("t", "body", ("status", "open"))]);
        var goneExtId = _base.Get("gone")!.ExternalId!;
        _adapter.DeleteExternal(goneExtId); // external side of "gone" disappears
        _base.SetLastActivity("gone", "2026-01-01");

        // This tick: "gone" is absent from repo AND external -> Retire; "t" is edited -> a push that will fail.
        _adapter.FailApply = true;
        Assert.Throws<InvalidOperationException>(() =>
            NewRunner().Run([RepoDoc("t", "body", ("status", "done"))]));

        // Retire committed despite the failed Apply: the stale base entry and its last-activity are gone.
        Assert.Null(_base.Get("gone"));
        Assert.Null(_base.GetLastActivity("gone"));
        // The failed push did NOT advance "t"'s base — it still reads the pre-edit status.
        Assert.Equal("open", _base.Get("t")!.GetField("status"));
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

    // --- Mass-delete fuse (slice ns-2) ---

    /// <summary>Sync <paramref name="tracked"/> fresh docs (each gets an external row and a base entry), materialize
    /// every repo file on disk, then archive <paramref name="deletions"/> of them on the external side. A re-run then
    /// reconciles exactly <paramref name="deletions"/> LOCAL file deletions against <paramref name="tracked"/> tracked
    /// records — the mass-delete fuse's exact inputs.</summary>
    private List<SyncDoc> SeedTrackedThenExternallyDelete(int tracked, int deletions)
    {
        var docs = Enumerable.Range(0, tracked)
            .Select(i => RepoDoc($"t{i:D3}", "body", ("status", "open")))
            .ToList();
        NewRunner().Run(docs);
        foreach (var doc in docs)
            SyncDocFile.Write(OpenPath(doc.LocalId), doc);
        foreach (var doc in docs.Take(deletions))
            _adapter.DeleteExternal(_base.Get(doc.LocalId)!.ExternalId!);
        return docs;
    }

    [Fact]
    public void MassDeleteFuse_SixOfTen_Aborts_TouchingNothing()
    {
        // 6 > 5 AND 6 > 20% of 10 -> both arms fire, the run aborts before applying anything.
        var docs = SeedTrackedThenExternallyDelete(tracked: 10, deletions: 6);

        var result = NewRunner().Run(docs);

        Assert.True(result.FuseTripped);
        Assert.Equal(6, result.WouldDeletePaths.Count);
        Assert.Equal(docs.Take(6).Select(d => OpenPath(d.LocalId)).OrderBy(p => p), result.WouldDeletePaths.OrderBy(p => p));
        // Nothing applied: every repo file still on disk and every base entry intact.
        Assert.All(docs, d => Assert.True(File.Exists(OpenPath(d.LocalId))));
        Assert.All(docs, d => Assert.NotNull(_base.Get(d.LocalId)));
    }

    [Fact]
    public void MassDeleteFuse_SixOfHundred_BelowPercentArm_Applies()
    {
        // 6 > 5 but 6 is only 6% of 100 -> the 20% arm fails, so the deletions apply normally.
        var docs = SeedTrackedThenExternallyDelete(tracked: 100, deletions: 6);

        var result = NewRunner().Run(docs);

        Assert.False(result.FuseTripped);
        Assert.All(docs.Take(6), d => Assert.False(File.Exists(OpenPath(d.LocalId))));
        Assert.All(docs.Take(6), d => Assert.Null(_base.Get(d.LocalId)));
    }

    [Fact]
    public void MassDeleteFuse_ThreeOfFour_BelowCountArm_Applies()
    {
        // 3 is 75% of 4 but not > 5 -> the count arm fails, so the deletions apply normally.
        var docs = SeedTrackedThenExternallyDelete(tracked: 4, deletions: 3);

        var result = NewRunner().Run(docs);

        Assert.False(result.FuseTripped);
        Assert.All(docs.Take(3), d => Assert.False(File.Exists(OpenPath(d.LocalId))));
        Assert.All(docs.Take(3), d => Assert.Null(_base.Get(d.LocalId)));
    }

    [Fact]
    public void MassDeleteFuse_AllowMassDelete_AppliesDespiteThreshold()
    {
        // The same 6-of-10 plan that trips the fuse applies when the override is set.
        var docs = SeedTrackedThenExternallyDelete(tracked: 10, deletions: 6);
        var runner = new SyncRunner(_adapter, _base,
            (localId, _, _) => OpenPath(localId), allowMassDelete: true);

        var result = runner.Run(docs);

        Assert.False(result.FuseTripped);
        Assert.All(docs.Take(6), d => Assert.False(File.Exists(OpenPath(d.LocalId))));
        Assert.All(docs.Take(6), d => Assert.Null(_base.Get(d.LocalId)));
    }

    [Fact]
    public void MassDeleteFuse_SixOfThirtyTracked_AtStrictBoundary_DoesNotTrip_DenominatorIsBaseNotFileCount()
    {
        // Base tracks EXACTLY 30 records; 6 of them are deleted externally. 6 * 5 == 30 is NOT > 30, so the strict
        // boundary holds and the fuse must NOT trip (a > -> >= regression would flip this). Ten EXTRA untracked
        // local files (40 on disk, only 30 in the base) pin that the denominator is the base snapshot's tracked
        // count, never the repo file count.
        var tracked = Enumerable.Range(0, 30)
            .Select(i => RepoDoc($"t{i:D3}", "body", ("status", "open")))
            .ToList();
        NewRunner().Run(tracked); // base now tracks exactly 30
        foreach (var doc in tracked)
            SyncDocFile.Write(OpenPath(doc.LocalId), doc);
        foreach (var doc in tracked.Take(6))
            _adapter.DeleteExternal(_base.Get(doc.LocalId)!.ExternalId!);

        // Ten brand-new, unsynced local files: on disk but absent from the base snapshot.
        var untracked = Enumerable.Range(0, 10)
            .Select(i => RepoDoc($"n{i:D3}", "body", ("status", "open")))
            .ToList();
        foreach (var doc in untracked)
            SyncDocFile.Write(OpenPath(doc.LocalId), doc);
        Assert.Equal(30, _base.LocalIds.Count); // 40 files on disk, 30 tracked

        var result = NewRunner().Run(tracked.Concat(untracked).ToList());

        Assert.False(result.FuseTripped);
        // The 6 deletions applied and the 10 untracked docs were created — never counted as deletions.
        Assert.All(tracked.Take(6), d => Assert.Null(_base.Get(d.LocalId)));
        Assert.All(untracked, d => Assert.NotNull(_base.Get(d.LocalId)));
    }

    [Fact]
    public void RepoOwnedStructure_AllExternalGone_PlansZeroRepoDeletes_FuseCannotTrip()
    {
        // F3 pin: the docs mirror (DR 033 §2, repoOwnedStructure) owns its structure — a page gone from the external
        // read while its repo doc is present is re-created, never deleted (ReconcileEngine.ExternalDeleted ->
        // CreateToExternal). No repoOwnedStructure branch ever emits a RepoDelete, and the mass-delete fuse counts
        // RepoDeletes, so the fuse can NEVER trip on the docs path — which is why DocsTreeSync safely discards
        // runner.Run's result. Archiving EVERY tracked page externally (a spine adapter would trip on this) still
        // plans zero RepoDeletes and leaves the fuse untripped.
        _adapter.RepoOwnedStructure = true;
        var docs = Enumerable.Range(0, 10)
            .Select(i => RepoDoc($"d{i:D3}", "body", ("status", "open")))
            .ToList();
        NewRunner().Run(docs);
        foreach (var doc in docs)
            SyncDocFile.Write(OpenPath(doc.LocalId), doc);

        foreach (var doc in docs)
            _adapter.DeleteExternal(_base.Get(doc.LocalId)!.ExternalId!);

        var result = NewRunner().Run(docs);

        Assert.False(result.FuseTripped);
        Assert.Empty(result.WouldDeletePaths);
        Assert.DoesNotContain(result.Results, r => r.RepoDelete != null);
        Assert.All(docs, d => Assert.True(File.Exists(OpenPath(d.LocalId)))); // every repo file survives
    }

    [Fact]
    public void MassDeleteFuse_TripsWhileAConflictShadows_ShadowWritten_NothingApplied_ShadowExcludedFromDeletions()
    {
        // F4 composed spine tick (ns-2 fuse x ns-4 shadow): in ONE run, one record's two-sided body edit routes to
        // the shadow tree WHILE six external deletions trip the mass-delete fuse. The shadow is written during the
        // reconcile loop (before the fuse check), but the fuse then aborts the apply — so no repo file is deleted,
        // no base entry drops, and the shadowed conflict is counted as a conflict, never a deletion.
        var docs = Enumerable.Range(0, 10)
            .Select(i => RepoDoc($"t{i:D3}", "one\ntwo\nthree", ("status", "open")))
            .ToList();
        NewRunner().Run(docs);
        foreach (var doc in docs)
            SyncDocFile.Write(OpenPath(doc.LocalId), doc);

        // Six records archived externally (repo files unchanged) -> six pending Deletes: 6 > 5 AND 6 > 20% of 10.
        foreach (var doc in docs.Take(6))
            _adapter.DeleteExternal(_base.Get(doc.LocalId)!.ExternalId!);

        // A seventh record edited on both sides on the same line -> a genuine two-sided conflict.
        var conflictId = docs[9].LocalId;
        _adapter.Edit(_base.Get(conflictId)!.ExternalId!, F(("status", "open")), "one\nEXT\nthree");
        var runDocs = docs.Take(9)
            .Append(RepoDoc(conflictId, "one\nREPO\nthree", ("status", "open")))
            .ToList();

        var shadowDir = Path.Combine(_dir, "shadow");
        var runner = new SyncRunner(_adapter, _base, (localId, _, _) => OpenPath(localId),
            conflictShadowPathFor: localId => Path.Combine(shadowDir, localId + ".md"));

        var result = runner.Run(runDocs);

        // Fuse tripped; the conflict shadowed. Six deletions counted, the shadowed conflict excluded from them.
        Assert.True(result.FuseTripped);
        Assert.Equal([conflictId], result.ShadowedLocalIds);
        Assert.Equal(6, result.WouldDeletePaths.Count);
        Assert.DoesNotContain(OpenPath(conflictId), result.WouldDeletePaths);

        // The shadow file WAS written (loop side effect) and carries the conflict markers.
        var shadowPath = Path.Combine(shadowDir, conflictId + ".md");
        Assert.True(File.Exists(shadowPath));
        Assert.Contains("<<<<<<< repo", File.ReadAllText(shadowPath));

        // Nothing applied: every repo file intact, every base entry intact, and the canonical conflict file was NOT
        // overwritten with markers (its shadow was neither pushed nor committed — the base did not advance).
        Assert.All(docs, d => Assert.True(File.Exists(OpenPath(d.LocalId))));
        Assert.All(docs, d => Assert.NotNull(_base.Get(d.LocalId)));
        Assert.DoesNotContain("<<<<<<< repo", File.ReadAllText(OpenPath(conflictId)));
    }

    [Fact]
    public void ConflictShadow_ResolvedMarkerFreeShadow_IsNeverOverwritten()
    {
        // ns-13 F3 backstop: a shadow a human already RESOLVED (no markers) must never be clobbered with fresh
        // conflict markers when a reconcile re-detects the same two-sided edit before the promote pass consumes it.
        var doc = RepoDoc("t1", "one\ntwo\nthree", ("status", "open"));
        NewRunner().Run([doc]);
        SyncDocFile.Write(OpenPath("t1"), doc);

        // A genuine two-sided overlapping edit -> the reconcile decides Conflict for t1.
        _adapter.Edit(_base.Get("t1")!.ExternalId!, F(("status", "open")), "one\nEXT\nthree");
        var runDoc = RepoDoc("t1", "one\nREPO\nthree", ("status", "open"));

        var shadowDir = Path.Combine(_dir, "shadow");
        Directory.CreateDirectory(shadowDir);
        var shadowPath = Path.Combine(shadowDir, "t1.md");
        const string resolved = "---\nstatus: open\n---\n\nhuman resolved body";
        File.WriteAllText(shadowPath, resolved); // marker-FREE — the human's resolution

        var runner = new SyncRunner(_adapter, _base, (localId, _, _) => OpenPath(localId),
            conflictShadowPathFor: localId => Path.Combine(shadowDir, localId + ".md"));
        var result = runner.Run([runDoc]);

        Assert.Contains("t1", result.ShadowedLocalIds);          // still shadowed, base un-advanced
        Assert.Equal(resolved, File.ReadAllText(shadowPath));     // the resolution was left untouched
    }
}
