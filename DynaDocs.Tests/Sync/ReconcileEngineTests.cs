// @test-tier: 2
namespace DynaDocs.Tests.Sync;

using DynaDocs.Models;
using DynaDocs.Sync;

public class ReconcileEngineTests
{
    private static SyncDoc Doc(string localId, string body, params (string Key, string Value)[] fields) => new()
    {
        LocalId = localId,
        Fields = fields.Select(f => new SyncField { Key = f.Key, Value = f.Value }).ToList(),
        Body = body,
        SourcePath = $"tasks/{localId}.md",
    };

    [Fact]
    public void NoChange_ReturnsNone()
    {
        var b = Doc("t", "body", ("status", "open"));
        var result = ReconcileEngine.Reconcile(b, Doc("t", "body", ("status", "open")), Doc("t", "body", ("status", "open")));
        Assert.Equal(ReconcileAction.None, result.Action);
    }

    [Fact]
    public void RepoOnlyChanged_PushesToExternal()
    {
        var b = Doc("t", "body", ("status", "open"));
        var repo = Doc("t", "body", ("status", "done"));
        var ext = Doc("t", "body", ("status", "open"));

        var result = ReconcileEngine.Reconcile(b, repo, ext);

        Assert.Equal(ReconcileAction.PushToExternal, result.Action);
        Assert.Equal("done", result.ExternalWrite!.GetField("status"));
        Assert.Null(result.RepoWrite);
    }

    [Fact]
    public void ExternalOnlyChanged_WritesToRepo()
    {
        var b = Doc("t", "body", ("status", "open"));
        var repo = Doc("t", "body", ("status", "open"));
        var ext = Doc("t", "body", ("status", "done"));

        var result = ReconcileEngine.Reconcile(b, repo, ext);

        Assert.Equal(ReconcileAction.WriteToRepo, result.Action);
        Assert.Equal("done", result.RepoWrite!.GetField("status"));
        Assert.Equal("tasks/t.md", result.RepoWrite.SourcePath);
    }

    [Fact]
    public void BothChanged_NonOverlapping_MergesCleanly()
    {
        var b = Doc("t", "line1\nline2\nline3", ("status", "open"), ("priority", "low"));
        var repo = Doc("t", "line1\nline2\nline3", ("status", "done"), ("priority", "low"));
        var ext = Doc("t", "line1\nline2\nline3", ("status", "open"), ("priority", "high"));

        var result = ReconcileEngine.Reconcile(b, repo, ext);

        Assert.Equal(ReconcileAction.Merged, result.Action);
        Assert.False(result.Conflicted);
        Assert.Equal("done", result.RepoWrite!.GetField("status"));
        Assert.Equal("high", result.RepoWrite.GetField("priority"));
        Assert.Same(result.RepoWrite, result.ExternalWrite);
    }

    [Fact]
    public void BothChanged_NonOverlappingBody_MergesBodyLines()
    {
        var b = Doc("t", "alpha\nbeta\ngamma", ("status", "open"));
        var repo = Doc("t", "ALPHA\nbeta\ngamma", ("status", "open"));
        var ext = Doc("t", "alpha\nbeta\nGAMMA", ("status", "open"));

        var result = ReconcileEngine.Reconcile(b, repo, ext);

        Assert.Equal(ReconcileAction.Merged, result.Action);
        Assert.Equal("ALPHA\nbeta\nGAMMA", result.RepoWrite!.Body);
    }

    [Fact]
    public void BothChanged_SameField_Conflicts_RepoWins()
    {
        var b = Doc("t", "body", ("status", "open"));
        var repo = Doc("t", "body", ("status", "done"));
        var ext = Doc("t", "body", ("status", "blocked"));

        var result = ReconcileEngine.Reconcile(b, repo, ext);

        Assert.Equal(ReconcileAction.Conflict, result.Action);
        Assert.True(result.Conflicted);
        Assert.Equal("done", result.RepoWrite!.GetField("status"));
    }

    [Fact]
    public void BothChanged_SameBodyLine_Conflicts_WithMarkers()
    {
        var b = Doc("t", "one\ntwo\nthree", ("status", "open"));
        var repo = Doc("t", "one\nREPO\nthree", ("status", "open"));
        var ext = Doc("t", "one\nEXTERNAL\nthree", ("status", "open"));

        var result = ReconcileEngine.Reconcile(b, repo, ext);

        Assert.Equal(ReconcileAction.Conflict, result.Action);
        Assert.Contains("<<<<<<< repo", result.RepoWrite!.Body);
        Assert.Contains("REPO", result.RepoWrite.Body);
        Assert.Contains("EXTERNAL", result.RepoWrite.Body);
        Assert.Contains(">>>>>>> external", result.RepoWrite.Body);
    }

    [Fact]
    public void BothChanged_MultiLineBodyOverlap_Conflicts_NoSilentLoss()
    {
        // Repo rewrites a two-line block into one line; external edits a line inside that block.
        // The overlapping body edit must surface as a conflict (markers + both sides' content),
        // never a silent drop of external's edit.
        var b = Doc("t", "L0\nL1\nL2\nL3", ("status", "open"));
        var repo = Doc("t", "L0\nREPO\nL3", ("status", "open"));
        var ext = Doc("t", "L0\nL1\nEXT\nL3", ("status", "open"));

        var result = ReconcileEngine.Reconcile(b, repo, ext);

        Assert.Equal(ReconcileAction.Conflict, result.Action);
        Assert.True(result.Conflicted);
        Assert.Contains("<<<<<<< repo", result.RepoWrite!.Body);
        Assert.Contains("REPO", result.RepoWrite.Body);
        Assert.Contains("EXT", result.RepoWrite.Body);
        Assert.Contains(">>>>>>> external", result.RepoWrite.Body);
    }

    [Fact]
    public void NewInRepoOnly_CreatesToExternal()
    {
        var repo = Doc("t", "body", ("status", "open"));
        var result = ReconcileEngine.Reconcile(null, repo, null);

        Assert.Equal(ReconcileAction.Create, result.Action);
        Assert.NotNull(result.ExternalWrite);
        Assert.Null(result.RepoWrite);
    }

    [Fact]
    public void NewInExternalOnly_CreatesToRepo()
    {
        var ext = Doc("t", "body", ("status", "open"));
        ext.ExternalId = "ext-1";
        var result = ReconcileEngine.Reconcile(null, null, ext);

        Assert.Equal(ReconcileAction.Create, result.Action);
        Assert.NotNull(result.RepoWrite);
        Assert.Null(result.ExternalWrite);
    }

    [Fact]
    public void NewOnBothSides_DivergingContent_Conflicts()
    {
        var repo = Doc("t", "repo body", ("status", "open"));
        var ext = Doc("t", "external body", ("status", "done"));

        var result = ReconcileEngine.Reconcile(null, repo, ext);

        Assert.Equal(ReconcileAction.Conflict, result.Action);
    }

    [Fact]
    public void DeletedInExternal_DeletesRepo()
    {
        var b = Doc("t", "body", ("status", "open"));
        b.ExternalId = "ext-1";
        var repo = Doc("t", "body", ("status", "open"));

        var result = ReconcileEngine.Reconcile(b, repo, null);

        Assert.Equal(ReconcileAction.Delete, result.Action);
        Assert.Equal("tasks/t.md", result.RepoDelete);
        Assert.Null(result.ExternalDelete);
    }

    [Fact]
    public void RepoOwnedStructure_ExternalMissingRepoPresent_ReCreatesPage_NeverDeletesRepoNorArchives()
    {
        // DR 033 §2: for the repo-owned docs mirror, a page missing from the external read while its repo doc
        // is present is eventual-consistency lag, not a deletion. The invariant is that a present repo doc's page
        // is NEVER archived and its file NEVER deleted — so the engine re-creates the page (fresh, no external
        // id) instead of taking the spine's delete-the-repo branch a bidirectional adapter would.
        var b = Doc("understand/foo", "body");
        b.ExternalId = "ext-1";
        var repo = Doc("understand/foo", "body"); // unchanged since base — the spine would DELETE the repo here

        var result = ReconcileEngine.Reconcile(b, repo, null, repoOwnedStructure: true);

        Assert.Equal(ReconcileAction.Create, result.Action);
        Assert.NotNull(result.ExternalWrite);
        Assert.Null(result.ExternalWrite!.ExternalId); // re-created as a fresh page
        Assert.Null(result.RepoDelete);                // repo file never deleted
        Assert.Null(result.ExternalDelete);            // page never archived
    }

    [Fact]
    public void DeletedInRepo_DeletesExternal()
    {
        var b = Doc("t", "body", ("status", "open"));
        b.ExternalId = "ext-1";
        var ext = Doc("t", "body", ("status", "open"));
        ext.ExternalId = "ext-1";

        var result = ReconcileEngine.Reconcile(b, null, ext);

        Assert.Equal(ReconcileAction.Delete, result.Action);
        Assert.Equal("ext-1", result.ExternalDelete);
        Assert.Null(result.RepoDelete);
    }

    [Fact]
    public void DeleteVsModify_ExternalDeletedRepoEdited()
    {
        // External deleted the page, but repo edited it since base. The edit must win: re-create the
        // external page with the repo edits, report a conflict, advance base to the surviving repo doc.
        var b = Doc("t", "body", ("status", "open"));
        b.ExternalId = "ext-1";
        var repo = Doc("t", "body", ("status", "done")); // edited since base

        var result = ReconcileEngine.Reconcile(b, repo, null);

        Assert.Equal(ReconcileAction.Conflict, result.Action);
        Assert.True(result.Conflicted);
        Assert.Null(result.RepoWrite);                       // repo already holds the edit
        Assert.Equal("done", result.ExternalWrite!.GetField("status"));
        Assert.Null(result.ExternalWrite.ExternalId);        // re-created as a fresh page
        Assert.Null(result.ExternalDelete);                  // never deleted
        Assert.Equal("done", result.NewBase!.GetField("status"));
    }

    [Fact]
    public void DeleteVsModify_RepoDeletedExternalEdited()
    {
        // Repo deleted the file, but a colleague edited the page in Notion since base. The edit wins:
        // resurrect the repo file with the external edits, report a conflict, advance base to the survivor.
        var b = Doc("t", "body", ("status", "open"));
        b.ExternalId = "ext-1";
        var ext = Doc("t", "body", ("status", "blocked")); // edited since base
        ext.ExternalId = "ext-1";

        var result = ReconcileEngine.Reconcile(b, null, ext);

        Assert.Equal(ReconcileAction.Conflict, result.Action);
        Assert.True(result.Conflicted);
        Assert.Equal("blocked", result.RepoWrite!.GetField("status"));
        Assert.Equal("ext-1", result.RepoWrite.ExternalId);
        Assert.Null(result.ExternalWrite);                   // external already holds the edit
        Assert.Null(result.RepoDelete);                      // never deleted
        Assert.Equal("blocked", result.NewBase!.GetField("status"));
    }

    [Fact]
    public void GoneFromBothSides_RetiresStaleBaseEntry()
    {
        // Both sides gone but a base entry lingers -> retire it, so the stale id cannot resurface as a silent
        // delete of a later git-restored file, leak into children's relation maps, or grow the snapshot (§2).
        var b = Doc("t", "body", ("status", "open"));
        var result = ReconcileEngine.Reconcile(b, null, null);
        Assert.Equal(ReconcileAction.Retire, result.Action);
        Assert.Equal("t", result.LocalId);
    }

    [Fact]
    public void AllNull_ReturnsNone_WithEmptyLocalId()
    {
        var result = ReconcileEngine.Reconcile(null, null, null);
        Assert.Equal(ReconcileAction.None, result.Action);
        Assert.Equal("", result.LocalId);
    }

    [Fact]
    public void RepoOnlyChanged_BaseLacksExternalId_FallsBackToExternalId()
    {
        var b = Doc("t", "body", ("status", "open")); // no ExternalId on base
        var repo = Doc("t", "body", ("status", "done"));
        var ext = Doc("t", "body", ("status", "open"));
        ext.ExternalId = "ext-9";

        var result = ReconcileEngine.Reconcile(b, repo, ext);

        Assert.Equal(ReconcileAction.PushToExternal, result.Action);
        Assert.Equal("ext-9", result.ExternalWrite!.ExternalId);
        Assert.Equal("ext-9", result.NewBase!.ExternalId);
    }

    [Fact]
    public void ExternalOnlyChanged_BaseLacksExternalId_FallsBackToExternalId()
    {
        var b = Doc("t", "body", ("status", "open")); // no ExternalId on base
        var repo = Doc("t", "body", ("status", "open"));
        var ext = Doc("t", "body", ("status", "done"));
        ext.ExternalId = "ext-9";

        var result = ReconcileEngine.Reconcile(b, repo, ext);

        Assert.Equal(ReconcileAction.WriteToRepo, result.Action);
        Assert.Equal("ext-9", result.NewBase!.ExternalId);
    }

    [Fact]
    public void DeletedInExternal_BaseLacksExternalId_UsesExternalIdFromExternal()
    {
        // Repo deleted; the external side is the one still present and carries the id.
        var b = Doc("t", "body", ("status", "open")); // base has no ExternalId
        var ext = Doc("t", "body", ("status", "open"));
        ext.ExternalId = "ext-9";

        var result = ReconcileEngine.Reconcile(b, null, ext);

        Assert.Equal(ReconcileAction.Delete, result.Action);
        Assert.Equal("ext-9", result.ExternalDelete);
    }

    [Fact]
    public void RepoRenamedFieldKey_SameCount_DetectedAsChange()
    {
        // Same field count, but the key differs — exercises the key-mismatch path in equality.
        var b = Doc("t", "body", ("status", "open"));
        var repo = Doc("t", "body", ("state", "open")); // renamed status -> state
        var ext = Doc("t", "body", ("status", "open"));

        var result = ReconcileEngine.Reconcile(b, repo, ext);

        Assert.Equal(ReconcileAction.PushToExternal, result.Action);
        Assert.Equal("open", result.ExternalWrite!.GetField("state"));
    }

    [Fact]
    public void ExternalRenamedFieldKey_SameCount_WritesToRepo()
    {
        var b = Doc("t", "body", ("status", "open"));
        var repo = Doc("t", "body", ("status", "open"));
        var ext = Doc("t", "body", ("state", "open")); // external renamed the key

        var result = ReconcileEngine.Reconcile(b, repo, ext);

        Assert.Equal(ReconcileAction.WriteToRepo, result.Action);
    }

    // A stand-in for a view that round-trips a relation lossily: a `sprint` value the adapter cannot
    // resolve (here "ghost") — or an empty one — is dropped, mirroring what read-back yields.
    private static SyncDoc DropUnresolvableSprint(SyncDoc d) => new()
    {
        LocalId = d.LocalId, ExternalId = d.ExternalId, Body = d.Body, SourcePath = d.SourcePath,
        Fields = d.Fields.Where(f => !(f.Key == "sprint" && f.Value is "" or "ghost")).ToList(),
    };

    [Fact]
    public void FieldNormalizer_MasksAdapterLossyField_NoSpuriousWriteToRepo()
    {
        // base and repo carry `sprint: ghost`, which the view drops and reads back empty. Without the field
        // normalizer the engine sees an external edit and blanks the repo value; with it, this is a no-op.
        var b = Doc("t", "body", ("title", "T"), ("sprint", "ghost"));
        var repo = Doc("t", "body", ("title", "T"), ("sprint", "ghost"));
        var ext = Doc("t", "body", ("title", "T"), ("sprint", "")); // echoed back empty

        var result = ReconcileEngine.Reconcile(b, repo, ext, fieldNormalizer: DropUnresolvableSprint);

        Assert.Equal(ReconcileAction.None, result.Action);
    }

    // A stand-in for a view whose schema lacks a "notes" property: the adapter cannot persist it, so it
    // reads back absent. The normalizer drops it, mirroring an out-of-schema / local-only frontmatter key.
    private static SyncDoc DropOutOfSchemaNotes(SyncDoc d) => new()
    {
        LocalId = d.LocalId, ExternalId = d.ExternalId, Body = d.Body, SourcePath = d.SourcePath,
        Fields = d.Fields.Where(f => f.Key != "notes").ToList(),
    };

    [Fact]
    public void FieldNormalizer_DoesNotMaskGenuineEdit_WritesToRepo_PreservingLossyField()
    {
        // A real external value change to a DIFFERENT field must still be detected, AND the adapter-invisible
        // relation — which the view echoes back EMPTY (the realistic read, not the old "ghost" fixture) —
        // must be preserved from the repo, never blanked by the external doc's empty value.
        var b = Doc("t", "body", ("title", "T"), ("status", "open"), ("sprint", "ghost"));
        var repo = Doc("t", "body", ("title", "T"), ("status", "open"), ("sprint", "ghost"));
        var ext = Doc("t", "body", ("title", "T"), ("status", "done"), ("sprint", "")); // relation echoed back empty

        var result = ReconcileEngine.Reconcile(b, repo, ext, fieldNormalizer: DropUnresolvableSprint);

        Assert.Equal(ReconcileAction.WriteToRepo, result.Action);
        Assert.Equal("done", result.RepoWrite!.GetField("status")); // the genuine edit takes the external value
        Assert.Equal("ghost", result.RepoWrite.GetField("sprint")); // adapter-invisible field kept, not blanked
    }

    [Fact]
    public void FieldNormalizer_TwoSidedMerge_AdapterInvisibleFieldSurvives()
    {
        // Both sides changed different representable fields (repo edits status, external adds owner), so this
        // 3-way merges. The relation the adapter cannot resolve reads back EMPTY; the raw merge would let that
        // empty value win and blank it. The overlay must keep the repo's value on the two-sided path too.
        var b = Doc("t", "body", ("title", "T"), ("status", "open"), ("sprint", "ghost"));
        var repo = Doc("t", "body", ("title", "T"), ("status", "done"), ("sprint", "ghost"));
        var ext = Doc("t", "body", ("title", "T"), ("status", "open"), ("sprint", ""), ("owner", "kim"));

        var result = ReconcileEngine.Reconcile(b, repo, ext, fieldNormalizer: DropUnresolvableSprint);

        Assert.Equal(ReconcileAction.Merged, result.Action);
        Assert.Equal("done", result.RepoWrite!.GetField("status")); // repo's representable edit
        Assert.Equal("kim", result.RepoWrite.GetField("owner"));    // external's representable add
        Assert.Equal("ghost", result.RepoWrite.GetField("sprint")); // adapter-invisible field survives the merge
    }

    [Fact]
    public void FieldNormalizer_LocalOnlyKey_PreservedOnGenuineEditWriteBack()
    {
        // A local-only, out-of-schema frontmatter key ("notes") the view cannot persist reads back ABSENT.
        // A genuine external edit to status must still write to repo, but the local-only key must be preserved
        // from the repo — exercising the overlay's re-append path for a field the external side dropped entirely.
        var b = Doc("t", "body", ("title", "T"), ("status", "open"), ("notes", "mine"));
        var repo = Doc("t", "body", ("title", "T"), ("status", "open"), ("notes", "mine"));
        var ext = Doc("t", "body", ("title", "T"), ("status", "done")); // notes dropped by the adapter

        var result = ReconcileEngine.Reconcile(b, repo, ext, fieldNormalizer: DropOutOfSchemaNotes);

        Assert.Equal(ReconcileAction.WriteToRepo, result.Action);
        Assert.Equal("done", result.RepoWrite!.GetField("status"));
        Assert.Equal("mine", result.RepoWrite.GetField("notes")); // local-only key preserved, not lost
    }

    [Fact]
    public void FieldNormalizer_RepoDeletedField_StillDeleted_NotResurrectedByOverlay()
    {
        // Repo genuinely deleted "status" while external added "note" and the relation reads back empty.
        // The overlay must not resurrect the deleted field — it only restores adapter-invisible keys the repo
        // still holds (sprint), never a field the repo intentionally removed.
        var b = Doc("t", "body", ("title", "T"), ("status", "open"), ("sprint", "ghost"));
        var repo = Doc("t", "body", ("title", "T"), ("sprint", "ghost")); // deleted status
        var ext = Doc("t", "body", ("title", "T"), ("status", "open"), ("sprint", ""), ("note", "hi"));

        var result = ReconcileEngine.Reconcile(b, repo, ext, fieldNormalizer: DropUnresolvableSprint);

        Assert.Equal(ReconcileAction.Merged, result.Action);
        Assert.Null(result.RepoWrite!.GetField("status"));          // stays deleted, not resurrected
        Assert.Equal("hi", result.RepoWrite.GetField("note"));      // external add kept
        Assert.Equal("ghost", result.RepoWrite.GetField("sprint")); // adapter-invisible field preserved
    }

    [Fact]
    public void MergeBoth_ExternalIdFallsBackToRepo_WhenBaseAndExternalLackIt()
    {
        // New on both sides (base null) with diverging fields: only repo carries an external id.
        var repo = Doc("t", "body", ("status", "done"));
        repo.ExternalId = "ext-repo";
        var ext = Doc("t", "body", ("status", "blocked")); // no ExternalId

        var result = ReconcileEngine.Reconcile(null, repo, ext);

        Assert.Equal(ReconcileAction.Conflict, result.Action);
        Assert.Equal("ext-repo", result.RepoWrite!.ExternalId);
    }

    // Finding 1 (direct NewBase coverage). Each of the four base-advancing paths that carry an adapter-invisible
    // field must record the NORMALIZED base — the subset the external view can round-trip — never the raw doc.
    // An un-normalized base would keep the dropped field (an as-yet-unresolvable relation), so once it resolves
    // the next tick the engine would misread the external's absence as a deletion and blank the repo value.
    // These pin each fixed line directly: reverting any of the four fieldNorm(NewBase) changes fails its test.

    [Fact]
    public void PushToExternal_NewBase_ExcludesAdapterDroppedField()
    {
        // Repo changed a representable field (status) AND carries a relation the view drops (sprint: ghost).
        // The push externalizes status but not the dropped relation, so the base must record status only.
        var b = Doc("t", "body", ("title", "T"), ("status", "open"));
        var repo = Doc("t", "body", ("title", "T"), ("status", "done"), ("sprint", "ghost"));
        var ext = Doc("t", "body", ("title", "T"), ("status", "open"));

        var result = ReconcileEngine.Reconcile(b, repo, ext, fieldNormalizer: DropUnresolvableSprint);

        Assert.Equal(ReconcileAction.PushToExternal, result.Action);
        Assert.Equal("done", result.ExternalWrite!.GetField("status"));
        Assert.Equal("done", result.NewBase!.GetField("status"));
        Assert.Null(result.NewBase.GetField("sprint")); // adapter-invisible field never recorded in the base
    }

    [Fact]
    public void WriteToRepo_NewBase_ExcludesAdapterDroppedField()
    {
        // External changed a representable field (status); the relation the view cannot round-trip (sprint) is
        // preserved onto the repo by the overlay, but the base must still record only the externalizable subset.
        var b = Doc("t", "body", ("title", "T"), ("status", "open"), ("sprint", "ghost"));
        var repo = Doc("t", "body", ("title", "T"), ("status", "open"), ("sprint", "ghost"));
        var ext = Doc("t", "body", ("title", "T"), ("status", "done"), ("sprint", "")); // relation echoed back empty

        var result = ReconcileEngine.Reconcile(b, repo, ext, fieldNormalizer: DropUnresolvableSprint);

        Assert.Equal(ReconcileAction.WriteToRepo, result.Action);
        Assert.Equal("ghost", result.RepoWrite!.GetField("sprint")); // overlay keeps it on the file
        Assert.Equal("done", result.NewBase!.GetField("status"));
        Assert.Null(result.NewBase.GetField("sprint")); // but the base records only what the view round-trips
    }

    [Fact]
    public void MergeBoth_NewBase_ExcludesAdapterDroppedField()
    {
        // Both sides changed different representable fields (repo status, external owner) so this 3-way merges;
        // the relation the view drops (sprint) survives onto the merged doc but must not enter the base raw.
        var b = Doc("t", "body", ("title", "T"), ("status", "open"), ("sprint", "ghost"));
        var repo = Doc("t", "body", ("title", "T"), ("status", "done"), ("sprint", "ghost"));
        var ext = Doc("t", "body", ("title", "T"), ("status", "open"), ("sprint", ""), ("owner", "kim"));

        var result = ReconcileEngine.Reconcile(b, repo, ext, fieldNormalizer: DropUnresolvableSprint);

        Assert.Equal(ReconcileAction.Merged, result.Action);
        Assert.Equal("ghost", result.RepoWrite!.GetField("sprint")); // survives on the merged doc
        Assert.Equal("done", result.NewBase!.GetField("status"));
        Assert.Equal("kim", result.NewBase.GetField("owner"));
        Assert.Null(result.NewBase.GetField("sprint")); // base records only the round-trippable subset
    }

    // A per-ENTRY relation normalizer faithful to NotionSyncAdapter.ResolveRelationSubset: an empty relation is
    // a valid clear and KEPT as ""; a non-empty value keeps only its resolvable entries; and an all-unresolvable
    // value drops the key whole (its target is not yet synced, so it reads back absent).
    // "b" and "e" are resolvable local ids; everything else (c, z, ...) is a not-yet-syncable target. In
    // production the read and write relation maps derive from the SAME base snapshots, so any external entry that
    // is a local id round-trips through both — an external LOCAL-id entry is always write-resolvable, and the only
    // external entry the write map cannot resolve is RenderRelation's raw-page-id fallback for an unmapped page.
    // "e" is therefore modelled as resolvable so an external swap b -> e is a realistic local-id edit, not a raw id.
    private static readonly HashSet<string> ResolvableBlockers = new(StringComparer.Ordinal) { "b", "e" };
    private static SyncDoc ResolveBlockedBySubset(SyncDoc d) => new()
    {
        LocalId = d.LocalId, ExternalId = d.ExternalId, Body = d.Body, SourcePath = d.SourcePath,
        Fields = d.Fields.SelectMany(f =>
        {
            if (f.Key != "blocked-by")
                return new[] { f };
            var ids = f.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (ids.Length == 0)
                return new[] { new SyncField { Key = "blocked-by", Value = "" } };
            var kept = ids.Where(ResolvableBlockers.Contains).ToList();
            return kept.Count == 0 ? [] : new[] { new SyncField { Key = "blocked-by", Value = string.Join(", ", kept) } };
        }).ToList(),
    };

    [Fact]
    public void WriteToRepo_PendingRelationEntry_SurvivesRewrite_MergedBackPerEntry()
    {
        // Finding 1a: repo has `blocked-by: b, c` where c is not yet resolvable, so it normalizes to `b` == base
        // and repoChanged is false. A same-tick external edit to a DIFFERENT field takes the WriteToRepo branch.
        // The overlay must merge the pending entry c back PER ENTRY, or the rewrite drops it permanently while
        // keeping the (non-empty) resolvable subset — the exact loss the whole-field overlay could not prevent.
        var b = Doc("t", "body", ("title", "T"), ("status", "open"), ("blocked-by", "b"));
        var repo = Doc("t", "body", ("title", "T"), ("status", "open"), ("blocked-by", "b, c"));
        var ext = Doc("t", "body", ("title", "T"), ("status", "done"), ("blocked-by", "b")); // c never externalized

        var result = ReconcileEngine.Reconcile(b, repo, ext, fieldNormalizer: ResolveBlockedBySubset);

        Assert.Equal(ReconcileAction.WriteToRepo, result.Action);
        Assert.Equal("done", result.RepoWrite!.GetField("status"));    // the genuine external edit is honored
        Assert.Equal("b, c", result.RepoWrite.GetField("blocked-by")); // pending entry c survives the rewrite
        Assert.Equal("b", result.NewBase!.GetField("blocked-by"));     // base still records only the resolvable subset
    }

    [Fact]
    public void WriteToRepo_ExternalReplacedResolvableEntry_PendingEntryStillMergedBack()
    {
        // The external swapped its resolvable target (b -> e, both representable) while the repo still carries the
        // pending c. The external's resolvable value wins for the representable entries; the pending c is appended.
        var b = Doc("t", "body", ("title", "T"), ("status", "open"), ("blocked-by", "b"));
        var repo = Doc("t", "body", ("title", "T"), ("status", "open"), ("blocked-by", "b, c"));
        var ext = Doc("t", "body", ("title", "T"), ("status", "done"), ("blocked-by", "e"));

        var result = ReconcileEngine.Reconcile(b, repo, ext, fieldNormalizer: ResolveBlockedBySubset);

        Assert.Equal(ReconcileAction.WriteToRepo, result.Action);
        Assert.Equal("e, c", result.RepoWrite!.GetField("blocked-by")); // external's edit + the pending entry
    }

    [Fact]
    public void WriteToRepo_ExternalDroppedRelationKeyEntirely_PendingEntryReappended()
    {
        // The external cleared the resolvable part of the relation, so its key reads back absent. The pending
        // unresolvable entry (c) must still be re-appended to the repo file, not lost — exercising the overlay's
        // re-append path for a partially-invisible relation the external dropped entirely.
        var b = Doc("t", "body", ("title", "T"), ("status", "open"), ("blocked-by", "b"));
        var repo = Doc("t", "body", ("title", "T"), ("status", "open"), ("blocked-by", "b, c"));
        var ext = Doc("t", "body", ("title", "T"), ("status", "done")); // no blocked-by key at all

        var result = ReconcileEngine.Reconcile(b, repo, ext, fieldNormalizer: ResolveBlockedBySubset);

        Assert.Equal(ReconcileAction.WriteToRepo, result.Action);
        Assert.Equal("done", result.RepoWrite!.GetField("status"));
        Assert.Equal("c", result.RepoWrite.GetField("blocked-by")); // pending entry survived via re-append
    }

    [Fact]
    public void DeletedInExternal_RepoHasPendingRelationEntry_NotSilentlyDeleted()
    {
        // Finding 1b: the external page is gone; the repo file carries an un-pushed, not-yet-resolvable relation
        // entry (c) absent from base. The delete-unchanged branch is the NORMALIZED compare guarded by
        // HasUnpushedRelation, which compares the repo's raw relation entries against the base's recorded entries;
        // c is absent from base, so it counts as un-pushed work — the file is resurrected/conflicted, never
        // silently deleted with c lost forever.
        var b = Doc("t", "body", ("title", "T"), ("blocked-by", "b"));
        b.ExternalId = "ext-1";
        var repo = Doc("t", "body", ("title", "T"), ("blocked-by", "b, c")); // c pending, never pushed

        var result = ReconcileEngine.Reconcile(b, repo, null, fieldNormalizer: ResolveBlockedBySubset);

        Assert.Equal(ReconcileAction.Conflict, result.Action);
        Assert.Null(result.RepoDelete);                                     // never a silent file delete
        Assert.Equal("b, c", result.ExternalWrite!.GetField("blocked-by")); // resurrected page re-created from the repo
    }

    [Fact]
    public void DeletedInExternal_RepoTrulyUnchanged_StillDeletes()
    {
        // The complement: with no pending entry the raw-field check still matches base, so a genuine unchanged
        // repo file is deleted when its page is gone (the delete-propagation behavior is preserved).
        var b = Doc("t", "body", ("title", "T"), ("blocked-by", "b"));
        b.ExternalId = "ext-1";
        var repo = Doc("t", "body", ("title", "T"), ("blocked-by", "b"));

        var result = ReconcileEngine.Reconcile(b, repo, null, fieldNormalizer: ResolveBlockedBySubset);

        Assert.Equal(ReconcileAction.Delete, result.Action);
        Assert.Equal("tasks/t.md", result.RepoDelete);
    }

    [Fact]
    public void DeletedInExternal_RepoHasLocalOnlyKey_StillDeletes()
    {
        // The wave-5 1b regression: the board page was archived and the repo file is untouched since push, but it
        // carries a permanently-local, out-of-schema key ("notes") the adapter never persists — as EVERY real dydo
        // doc does (area/type on a sprint-task, id/found-by/date on an issue). A RAW compare against the base — which
        // is recorded NORMALIZED and so never holds that key — judged the file "changed" forever, so the archive
        // re-created the page every tick instead of deleting the row. The normalized compare deletes it: a local-only
        // key is not un-pushed work and must not block delete propagation.
        var b = Doc("t", "body", ("title", "T"), ("status", "open"));
        b.ExternalId = "ext-1";
        var repo = Doc("t", "body", ("title", "T"), ("status", "open"), ("notes", "mine")); // local-only, never pushed

        var result = ReconcileEngine.Reconcile(b, repo, null, fieldNormalizer: DropOutOfSchemaNotes);

        Assert.Equal(ReconcileAction.Delete, result.Action);
        Assert.Equal("tasks/t.md", result.RepoDelete);
        Assert.Null(result.ExternalWrite); // the page is not resurrected
    }

    [Fact]
    public void DeletedInExternal_RepoHasAllUnresolvableRelation_NotSilentlyDeleted()
    {
        // The all-entries-unresolvable shape: the repo's only blocked-by target (c) is not yet syncable, so the
        // normalizer drops the WHOLE key — it never reaches PendingRelationEntries and normalizes equal to base.
        // Under a bare normalized compare the file would be silently deleted and c lost forever; the whole-field-
        // invisible-relation guard must instead resurrect the page (conflict), exactly like the partial-subset case.
        var b = Doc("t", "body", ("title", "T"));
        b.ExternalId = "ext-1";
        var repo = Doc("t", "body", ("title", "T"), ("blocked-by", "c")); // c never resolvable, never pushed

        var result = ReconcileEngine.Reconcile(b, repo, null, fieldNormalizer: ResolveBlockedBySubset);

        Assert.Equal(ReconcileAction.Conflict, result.Action);
        Assert.Null(result.RepoDelete);                                  // never a silent file delete
        Assert.Equal("c", result.ExternalWrite!.GetField("blocked-by")); // resurrected page re-created from the repo
    }

    [Fact]
    public void DeleteOneResurrect_NewBase_ExcludesAdapterDroppedField()
    {
        // External deleted the page while repo edited it AND carries a relation the view drops. The resurrect
        // re-creates the page from the repo edits, but the base advances to the externalizable subset only.
        var b = Doc("t", "body", ("title", "T"), ("status", "open"));
        b.ExternalId = "ext-1";
        var repo = Doc("t", "body", ("title", "T"), ("status", "done"), ("sprint", "ghost")); // edited since base

        var result = ReconcileEngine.Reconcile(b, repo, null, fieldNormalizer: DropUnresolvableSprint);

        Assert.Equal(ReconcileAction.Conflict, result.Action);
        Assert.Equal("ghost", result.ExternalWrite!.GetField("sprint")); // the resurrected page still carries it
        Assert.Equal("done", result.NewBase!.GetField("status"));
        Assert.Null(result.NewBase.GetField("sprint")); // base records only what the view round-trips
    }

    // A relation normalizer where NOTHING in blocked-by is resolvable: any non-empty value drops the key whole
    // (all targets unresolvable), while an empty value stays "" (a valid clear). Simulates a relation whose only
    // targets have all been retired — the regressed-to-unresolvable shape.
    private static SyncDoc ResolveNoneBlockedBy(SyncDoc d) => new()
    {
        LocalId = d.LocalId, ExternalId = d.ExternalId, Body = d.Body, SourcePath = d.SourcePath,
        Fields = d.Fields.SelectMany(f =>
        {
            if (f.Key != "blocked-by")
                return new[] { f };
            var ids = f.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return ids.Length == 0 ? new[] { new SyncField { Key = "blocked-by", Value = "" } } : [];
        }).ToList(),
    };

    // Combined normalizer: drops the out-of-schema "notes" key entirely (like a local-only frontmatter key), and
    // resolves blocked-by per ResolveBlockedBySubset (only "b" resolvable). Exercises the mixed shape.
    private static SyncDoc DropNotesAndResolveBlockedBy(SyncDoc d) =>
        ResolveBlockedBySubset(DropOutOfSchemaNotes(d));

    [Fact]
    public void WriteToRepo_BoardEditsWholeFieldInvisibleRelation_RepoPendingUnioned_NotDiscarded()
    {
        // Finding 2 (HIGH). The repo holds an all-unresolvable relation (blocked-by: c), so it is whole-field
        // invisible. c is a PENDING entry the base never recorded (an unresolvable entry is never externalized,
        // so a correct base — recorded normalized — never holds it). A colleague adds a resolvable blocker (b) on
        // the board, and in the same tick edits a different field (status). The overlay must UNION the board's
        // entry with the repo's pending one, not REPLACE the board edit with the raw repo value — else b is
        // discarded here and clobbered on push.
        var b = Doc("t", "body", ("title", "T"), ("status", "open"));
        var repo = Doc("t", "body", ("title", "T"), ("status", "open"), ("blocked-by", "c"));
        var ext = Doc("t", "body", ("title", "T"), ("status", "done"), ("blocked-by", "b")); // board added b

        var result = ReconcileEngine.Reconcile(b, repo, ext, fieldNormalizer: ResolveBlockedBySubset);

        Assert.Equal(ReconcileAction.WriteToRepo, result.Action);
        Assert.Equal("done", result.RepoWrite!.GetField("status"));       // the genuine board edit is honored
        Assert.Equal("b, c", result.RepoWrite.GetField("blocked-by"));    // board entry survives, repo pending kept
    }

    [Fact]
    public void WriteToRepo_BoardClearsWholeFieldInvisibleRelation_RepoPendingSurvives()
    {
        // Finding 2, clear variant. The board holds no resolvable entry (echoes blocked-by empty) while the repo
        // keeps its unresolvable, un-pushed c (the base never recorded it). A repo PENDING entry must ALWAYS
        // survive: the merged value keeps c.
        var b = Doc("t", "body", ("title", "T"), ("status", "open"));
        var repo = Doc("t", "body", ("title", "T"), ("status", "open"), ("blocked-by", "c"));
        var ext = Doc("t", "body", ("title", "T"), ("status", "done"), ("blocked-by", "")); // board relation empty

        var result = ReconcileEngine.Reconcile(b, repo, ext, fieldNormalizer: ResolveBlockedBySubset);

        Assert.Equal(ReconcileAction.WriteToRepo, result.Action);
        Assert.Equal("c", result.RepoWrite!.GetField("blocked-by")); // repo pending entry never lost
    }

    [Fact]
    public void MergeBoth_AllUnresolvableRelationEmptyEcho_NoPhantomConflict_PendingEntryPreserved()
    {
        // Finding 2 (MergeBoth path). The base never recorded blocked-by (its only target unresolvable at create),
        // real Notion echoes the relation back EMPTY, and BOTH sides edit the body. FieldMerge must treat the
        // external empty echo as absent (the base never recorded the key), so it does not collide with the repo's
        // pending entry z into a phantom conflict every tick; the pending entry survives and the body merges.
        var b = Doc("t", "l1\nl2\nl3", ("title", "T"));                          // base never recorded blocked-by
        var repo = Doc("t", "L1\nl2\nl3", ("title", "T"), ("blocked-by", "z"));  // z pending, repo edits line 1
        var ext = Doc("t", "l1\nl2\nL3", ("title", "T"), ("blocked-by", ""));    // empty echo, board edits line 3

        var result = ReconcileEngine.Reconcile(b, repo, ext, fieldNormalizer: ResolveBlockedBySubset);

        Assert.Equal(ReconcileAction.Merged, result.Action);
        Assert.False(result.Conflicted);                              // no phantom conflict from the empty echo
        Assert.Equal("L1\nl2\nL3", result.RepoWrite!.Body);           // both body edits merged
        Assert.Equal("z", result.RepoWrite.GetField("blocked-by"));   // pending entry preserved
    }

    [Fact]
    public void WriteToRepo_ExternalDroppedWholeInvisibleRelationEntirely_UnpushedEntryReappended()
    {
        // Finding 2/3 re-append path. The repo holds an all-unresolvable relation (blocked-by: z) the base never
        // recorded (pending), and the external DROPS the key entirely (not even an empty echo) while editing
        // status. The overlay's re-append must restore the repo's un-pushed entry z, never losing pending work.
        var b = Doc("t", "body", ("title", "T"), ("status", "open"));
        var repo = Doc("t", "body", ("title", "T"), ("status", "open"), ("blocked-by", "z"));
        var ext = Doc("t", "body", ("title", "T"), ("status", "done")); // no blocked-by key at all

        var result = ReconcileEngine.Reconcile(b, repo, ext, fieldNormalizer: ResolveBlockedBySubset);

        Assert.Equal(ReconcileAction.WriteToRepo, result.Action);
        Assert.Equal("done", result.RepoWrite!.GetField("status"));
        Assert.Equal("z", result.RepoWrite.GetField("blocked-by")); // pending entry re-appended, not lost
    }

    [Fact]
    public void WriteToRepo_ExternalDroppedWholeInvisibleRelationEntirely_RecordedRetiredEntryNotReappended()
    {
        // Finding 4 re-append path. Base RECORDED blocked-by: b (pushed while resolvable); b's target then retires,
        // so the key is whole-field invisible. The external drops the key entirely while editing status. The
        // re-append must NOT restore b — a recorded entry the board no longer shows was cleared/retired there.
        var b = Doc("t", "body", ("title", "T"), ("status", "open"), ("blocked-by", "b"));
        var repo = Doc("t", "body", ("title", "T"), ("status", "open"), ("blocked-by", "b"));
        var ext = Doc("t", "body", ("title", "T"), ("status", "done")); // no blocked-by key at all

        var result = ReconcileEngine.Reconcile(b, repo, ext, fieldNormalizer: ResolveNoneBlockedBy);

        Assert.Equal(ReconcileAction.WriteToRepo, result.Action);
        Assert.Equal("done", result.RepoWrite!.GetField("status"));
        Assert.Null(result.RepoWrite.GetField("blocked-by")); // retired recorded entry not resurrected
    }

    [Fact]
    public void WriteToRepo_BoardClearsRecordedRelation_TargetRetired_ClearApplies_NotResurrected()
    {
        // Finding 4. Base RECORDED blocked-by: b (pushed while b was resolvable). b's target then retires, so the
        // current maps drop blocked-by from both normalized sides and Equal(base, repo) holds — repoChanged false.
        // The board CLEARS the relation (empty echo). The clear must APPLY: a recorded entry the board no longer
        // shows was cleared there, not a pending local edit, so the overlay must not resurrect b. The empty-echo
        // logic keys off the base's RAW recorded state, so the clear is not swallowed as a phantom echo.
        var b = Doc("t", "body", ("title", "T"), ("blocked-by", "b"));   // base recorded b (was resolvable)
        var repo = Doc("t", "body", ("title", "T"), ("blocked-by", "b")); // repo untouched since push
        var ext = Doc("t", "body", ("title", "T"), ("blocked-by", ""));   // board cleared it

        var result = ReconcileEngine.Reconcile(b, repo, ext, fieldNormalizer: ResolveNoneBlockedBy);

        Assert.Equal(ReconcileAction.WriteToRepo, result.Action);
        Assert.Equal("", result.RepoWrite!.GetField("blocked-by")); // clear applied; b not resurrected
    }

    [Fact]
    public void DeletedInExternal_BaseRecordedEntryRetired_PendingEntryStillGuardsDelete()
    {
        // Finding 3 (subset-regressed-to-unresolvable). Base recorded `blocked-by: b` (pushed when b was
        // resolvable). The repo now holds `blocked-by: b, c` and BOTH targets are unresolvable (b's target was
        // retired), so the whole key drops from both normalized sides and Equal(base, repo) holds. The old
        // key-presence guard (base HAS "blocked-by") was defeated and the file was silently deleted, losing the
        // un-pushed c. Comparing raw repo entries against the base's recorded entries catches c → resurrect.
        var b = Doc("t", "body", ("title", "T"), ("blocked-by", "b"));
        b.ExternalId = "ext-1";
        var repo = Doc("t", "body", ("title", "T"), ("blocked-by", "b, c")); // c never pushed; both now unresolvable

        var result = ReconcileEngine.Reconcile(b, repo, null, fieldNormalizer: ResolveNoneBlockedBy);

        Assert.Equal(ReconcileAction.Conflict, result.Action);
        Assert.Null(result.RepoDelete);                                     // never a silent delete
        Assert.Equal("b, c", result.ExternalWrite!.GetField("blocked-by")); // resurrected from the repo
    }

    [Fact]
    public void WriteToRepo_PartialRelation_RawExternalIdEntry_PendingBranch_StrippedNotUnioned()
    {
        // Round-3 defect (finding 3, PENDING branch). The repo carries a partially-resolvable relation
        // `blocked-by: b, c` (b resolvable, c a not-yet-syncable pending entry the base never recorded). The board
        // references an archived/unmapped page that RENDERS as a raw Notion page id ("page-raw"), and edits status
        // the same tick. The overlay's pending branch must merge the pending entry onto the external's RESOLVABLE
        // subset (b), never its RAW value — so the raw page id is stripped and never enters frontmatter, while the
        // genuine pending entry c still survives.
        var b = Doc("t", "body", ("title", "T"), ("status", "open"), ("blocked-by", "b"));
        var repo = Doc("t", "body", ("title", "T"), ("status", "open"), ("blocked-by", "b, c"));
        var ext = Doc("t", "body", ("title", "T"), ("status", "done"), ("blocked-by", "b, page-raw"));

        var result = ReconcileEngine.Reconcile(b, repo, ext, fieldNormalizer: ResolveBlockedBySubset);

        Assert.Equal(ReconcileAction.WriteToRepo, result.Action);
        Assert.Equal("done", result.RepoWrite!.GetField("status"));      // the board edit landed
        Assert.Equal("b, c", result.RepoWrite.GetField("blocked-by"));   // resolvable subset + pending entry, no raw id
        Assert.DoesNotContain("page-raw", result.RepoWrite.GetField("blocked-by")!); // finding 3: raw id stripped
    }

    [Fact]
    public void WriteToRepo_PartialRelation_OneEntryRetired_BoardEdit_NoRawId_ThenArchivePropagates()
    {
        // Round-3 defect (finding 3, multi-value self-relation core case) composed with finding 4. Base recorded
        // `blocked-by: b, ret` (both pushed while resolvable). `ret`'s target RETIRES (its local id no longer
        // resolves) while the board still references its archived page, so the external RENDERS ret as a raw
        // Notion page id and edits another field the same tick. The overlay's pass-through branch must strip the
        // raw id and drop the retired recorded entry, leaving only the live b. With no raw-id (or retired) entry
        // left, a subsequent board archive propagates as a genuine delete, not an immortal conflict.
        var b = Doc("t", "body", ("title", "T"), ("status", "open"), ("blocked-by", "b, ret"));
        b.ExternalId = "ext-1";
        var repo = Doc("t", "body", ("title", "T"), ("status", "open"), ("blocked-by", "b, ret"));
        var ext = Doc("t", "body", ("title", "T"), ("status", "done"), ("blocked-by", "b, page-raw-ret")); // ret -> raw page id

        var write = ReconcileEngine.Reconcile(b, repo, ext, fieldNormalizer: ResolveBlockedBySubset);

        Assert.Equal(ReconcileAction.WriteToRepo, write.Action);
        Assert.Equal("done", write.RepoWrite!.GetField("status"));               // the board edit landed
        Assert.Equal("b", write.RepoWrite.GetField("blocked-by"));               // only the live entry; ret retired
        Assert.DoesNotContain("page-raw-ret", write.RepoWrite.GetField("blocked-by")!); // finding 3: no raw page id

        // Next tick: the file now holds `blocked-by: b`, the base advanced to the pushed subset, and the board
        // archives the doc. With no un-pushed raw-id/retired entry to guard it, the delete propagates.
        var settled = ReconcileEngine.Reconcile(write.NewBase, write.RepoWrite, null, fieldNormalizer: ResolveBlockedBySubset);

        Assert.Equal(ReconcileAction.Delete, settled.Action);
        Assert.Equal("tasks/t.md", settled.RepoDelete);
    }

    [Fact]
    public void WriteToRepo_ExternalAddsAllUnresolvableRelation_RepoLacksIt_CollapsesToEmpty_NoRawId()
    {
        // Overlay externalRelationKeys branch (finding 3, pass-through). The repo does NOT carry the relation (so
        // it is neither stale, invisible, nor pending) and the board ADDS a relation pointing solely at an
        // unmapped/archived page — a raw Notion page id — while editing status. The pass-through must reduce the
        // added relation to its (empty) resolvable subset, never write the raw page id into frontmatter. (When the
        // REPO holds the resolvable relation instead, this is the finding-1 STALE shape — repo wins and re-pushes,
        // covered below — not a collapse-to-empty.)
        var b = Doc("t", "body", ("title", "T"), ("status", "open"));
        var repo = Doc("t", "body", ("title", "T"), ("status", "open"));
        var ext = Doc("t", "body", ("title", "T"), ("status", "done"), ("blocked-by", "page-raw-only"));

        var result = ReconcileEngine.Reconcile(b, repo, ext, fieldNormalizer: ResolveBlockedBySubset);

        Assert.Equal(ReconcileAction.WriteToRepo, result.Action);
        Assert.Equal("done", result.RepoWrite!.GetField("status"));
        Assert.Equal("", result.RepoWrite.GetField("blocked-by")); // resolvable subset empty; raw id dropped
        Assert.DoesNotContain("page-raw-only", result.RepoWrite.GetField("blocked-by")!);
    }

    [Fact]
    public void WriteToRepo_PassThroughExternalOutOfSchemaKey_PassedThroughUntouched()
    {
        // Pass-through fallback. An external field the normalizer drops that is NOT a relation (an out-of-schema
        // key the repo does not itself carry, so it is not whole-field invisible) is neither sanitized to a
        // resolvable subset nor collapsed to empty — it is passed through verbatim, the safe default distinct from
        // the relation-only raw-id stripping.
        var b = Doc("t", "body", ("title", "T"), ("status", "open"));
        var repo = Doc("t", "body", ("title", "T"), ("status", "open"));
        var ext = Doc("t", "body", ("title", "T"), ("status", "done"), ("notes", "theirs")); // out-of-schema, dropped by adapter

        var result = ReconcileEngine.Reconcile(b, repo, ext, fieldNormalizer: DropOutOfSchemaNotes);

        Assert.Equal(ReconcileAction.WriteToRepo, result.Action);
        Assert.Equal("done", result.RepoWrite!.GetField("status"));
        Assert.Equal("theirs", result.RepoWrite.GetField("notes")); // non-relation dropped key passes through untouched
    }

    [Fact]
    public void DeletedInExternal_MixedLocalOnlyKeyAndUnresolvableRelation_GuardsDelete()
    {
        // Finding 3 (mixed shape). The repo carries a permanently-local key (notes) AND an all-unresolvable
        // relation (blocked-by: c) absent from base. The local-only key is not un-pushed work and must not block
        // the delete on its own; the relation entry c IS un-pushed and must — so the file is resurrected, proving
        // the relation is told apart from the local-only key.
        var b = Doc("t", "body", ("title", "T"));
        b.ExternalId = "ext-1";
        var repo = Doc("t", "body", ("title", "T"), ("notes", "mine"), ("blocked-by", "c"));

        var result = ReconcileEngine.Reconcile(b, repo, null, fieldNormalizer: DropNotesAndResolveBlockedBy);

        Assert.Equal(ReconcileAction.Conflict, result.Action);
        Assert.Null(result.RepoDelete);
        Assert.Equal("c", result.ExternalWrite!.GetField("blocked-by"));
    }

    [Fact]
    public void DeletedInExternal_EmptyRelationClear_NotUnpushedWork_StillDeletes()
    {
        // Finding 3 (empty-relation-clear delete shape). An EMPTY repo relation is not un-pushed work: base and
        // repo both hold an empty blocked-by (a pushed clear), so the archive propagates as a genuine delete.
        var b = Doc("t", "body", ("title", "T"), ("blocked-by", ""));
        b.ExternalId = "ext-1";
        var repo = Doc("t", "body", ("title", "T"), ("blocked-by", ""));

        var result = ReconcileEngine.Reconcile(b, repo, null, fieldNormalizer: ResolveBlockedBySubset);

        Assert.Equal(ReconcileAction.Delete, result.Action);
        Assert.Equal("tasks/t.md", result.RepoDelete);
    }

    [Fact]
    public void WriteToRepo_RepoDuplicateFrontmatterKey_DoesNotCrash_FirstWins()
    {
        // Finding 4. A repo doc carrying a DUPLICATE frontmatter key must not crash the overlay's dictionary
        // build (the wave-5 ToDictionary threw; the pre-wave FirstOrDefault did not). First-wins is kept,
        // matching SyncDoc.GetField. Here "notes" is out-of-schema (whole-field invisible) and appears twice.
        var b = Doc("t", "body", ("title", "T"), ("status", "open"), ("notes", "A"));
        var repo = new SyncDoc
        {
            LocalId = "t", Body = "body", SourcePath = "tasks/t.md",
            Fields =
            [
                new SyncField { Key = "title", Value = "T" },
                new SyncField { Key = "status", Value = "open" },
                new SyncField { Key = "notes", Value = "A" },
                new SyncField { Key = "notes", Value = "B" },
            ],
        };
        var ext = Doc("t", "body", ("title", "T"), ("status", "done")); // notes dropped by the adapter

        var result = ReconcileEngine.Reconcile(b, repo, ext, fieldNormalizer: DropOutOfSchemaNotes);

        Assert.Equal(ReconcileAction.WriteToRepo, result.Action);
        Assert.Equal("done", result.RepoWrite!.GetField("status"));
        Assert.Equal("A", result.RepoWrite.GetField("notes")); // first-wins, and no crash
    }

    [Fact]
    public void DeletedInExternal_RepoDuplicateKey_DoesNotCrash_DeleteGuardStillRuns()
    {
        // Finding 4, delete path. HasUnpushedRelation builds a first-wins dictionary from the normalized repo; a
        // duplicate key that SURVIVES normalization (here identity) must not crash it. Base and repo both carry
        // the duplicate so the delete-unchanged branch is reached (Equal holds), and the guard runs on the dup.
        SyncField[] fields =
        [
            new() { Key = "title", Value = "T" },
            new() { Key = "status", Value = "open" },
            new() { Key = "status", Value = "open" },
        ];
        var b = new SyncDoc { LocalId = "t", Body = "body", SourcePath = "tasks/t.md", ExternalId = "ext-1", Fields = fields.ToList() };
        var repo = new SyncDoc { LocalId = "t", Body = "body", SourcePath = "tasks/t.md", ExternalId = "ext-1", Fields = fields.ToList() };

        var result = ReconcileEngine.Reconcile(b, repo, null); // identity normalizer keeps the duplicate

        Assert.Equal(ReconcileAction.Delete, result.Action);
        Assert.Equal("tasks/t.md", result.RepoDelete);
    }

    [Fact]
    public void DeletedInExternal_RepoDuplicateRelationKey_LaterOccurrencePhantomEntry_FirstWins_StillDeletes()
    {
        // Review R2-2 (guard path). A duplicate relation key whose FIRST occurrence is fully pushed (recorded in
        // base) and a LATER occurrence carries an un-pushed entry (c). Every reader — visible/FirstWins,
        // FieldMerge, ToProperties, ParseFields, UpsertField, GetField — resolves the FIRST occurrence, so the later
        // one is a phantom no consumer will ever push or preserve. PendingRelationEntries / HasUnpushedRelation must
        // be first-wins and ignore it; a last-wins reading would count c as un-pushed work and block the delete
        // forever, resurrecting the archived page every tick on entries invisible to every other reader.
        var b = Doc("t", "body", ("title", "T"), ("blocked-by", "b"));
        b.ExternalId = "ext-1";
        var repo = new SyncDoc
        {
            LocalId = "t", Body = "body", SourcePath = "tasks/t.md",
            Fields =
            [
                new SyncField { Key = "title", Value = "T" },
                new SyncField { Key = "blocked-by", Value = "b" }, // first: fully pushed, recorded in base
                new SyncField { Key = "blocked-by", Value = "c" }, // later dup: c unresolvable/un-pushed (phantom)
            ],
        };

        var result = ReconcileEngine.Reconcile(b, repo, null, fieldNormalizer: ResolveBlockedBySubset);

        Assert.Equal(ReconcileAction.Delete, result.Action);
        Assert.Equal("tasks/t.md", result.RepoDelete);
    }

    [Fact]
    public void WriteToRepo_RepoDuplicateRelationKey_LaterOccurrencePhantom_NotInjectedIntoFrontmatter_FirstWins()
    {
        // Review R2-2 (overlay/pending path). On a WriteToRepo rewrite a duplicate relation key's LATER occurrence
        // must not seed a phantom pending entry. The first occurrence (b) is fully pushed and resolvable; the later
        // (c) is invisible to every reader. First-wins in PendingRelationEntries must ignore it, so the rewrite
        // never merges c back into the repo file (a last-wins reading would inject "b, c").
        var b = Doc("t", "body", ("title", "T"), ("status", "open"), ("blocked-by", "b"));
        var repo = new SyncDoc
        {
            LocalId = "t", Body = "body", SourcePath = "tasks/t.md",
            Fields =
            [
                new SyncField { Key = "title", Value = "T" },
                new SyncField { Key = "status", Value = "open" },
                new SyncField { Key = "blocked-by", Value = "b" },
                new SyncField { Key = "blocked-by", Value = "c" },
            ],
        };
        var ext = Doc("t", "body", ("title", "T"), ("status", "done"), ("blocked-by", "b")); // external edits a DIFFERENT field

        var result = ReconcileEngine.Reconcile(b, repo, ext, fieldNormalizer: ResolveBlockedBySubset);

        Assert.Equal(ReconcileAction.WriteToRepo, result.Action);
        Assert.Equal("done", result.RepoWrite!.GetField("status")); // the genuine external edit is honored
        Assert.Equal("b", result.RepoWrite.GetField("blocked-by"));  // phantom c never injected (first-wins)
    }

    [Fact]
    public void ExternalEmptyRelationEcho_BaseLacksKey_TreatedAsAbsent_NoChurn()
    {
        // Finding 6. Real Notion echoes every schema property, so an all-unresolvable relation reads back as an
        // empty string — but the normalized base never recorded that key. Without the fix the engine reads the
        // empty echo as an external "clear" and rewrites the repo every tick. It must be treated as absent (a
        // clear means something only when the base HELD the key), so an otherwise-unchanged doc is a no-op.
        var b = Doc("t", "body", ("title", "T"));                         // base never recorded blocked-by
        var repo = Doc("t", "body", ("title", "T"), ("blocked-by", "c")); // c unresolvable -> dropped
        var ext = Doc("t", "body", ("title", "T"), ("blocked-by", ""));   // real Notion empty-relation echo

        var result = ReconcileEngine.Reconcile(b, repo, ext, fieldNormalizer: ResolveBlockedBySubset);

        Assert.Equal(ReconcileAction.None, result.Action);
    }

    [Fact]
    public void ExternalEmptyRelationEcho_EntryResolves_PushesCleanly_NoSpuriousConflict()
    {
        // Finding 6, resolve tick. Once the entry becomes resolvable the repo change is a pure repo-side addition:
        // base still lacks the key, the external still echoes empty (never pushed). Treating the empty echo as
        // absent yields a clean PushToExternal — NOT the spurious MergeBoth conflict the phantom clear provoked.
        var b = Doc("t", "body", ("title", "T"));                         // base lacks blocked-by
        var repo = Doc("t", "body", ("title", "T"), ("blocked-by", "b")); // b now resolvable
        var ext = Doc("t", "body", ("title", "T"), ("blocked-by", ""));   // page still echoes empty (unpushed)

        var result = ReconcileEngine.Reconcile(b, repo, ext, fieldNormalizer: ResolveBlockedBySubset);

        Assert.Equal(ReconcileAction.PushToExternal, result.Action);
        Assert.Equal("b", result.ExternalWrite!.GetField("blocked-by"));
    }

    [Fact]
    public void ExternalEmptyRelation_BaseRecordedKey_StillAGenuineClear()
    {
        // Finding 6 guard: a clear is real when the base HELD the key. Base recorded `blocked-by: b`; the board
        // cleared it (empty echo). That must remain a genuine external change (WriteToRepo), not swallowed.
        var b = Doc("t", "body", ("title", "T"), ("blocked-by", "b"));
        var repo = Doc("t", "body", ("title", "T"), ("blocked-by", "b"));
        var ext = Doc("t", "body", ("title", "T"), ("blocked-by", "")); // board cleared a previously-set relation

        var result = ReconcileEngine.Reconcile(b, repo, ext, fieldNormalizer: ResolveBlockedBySubset);

        Assert.Equal(ReconcileAction.WriteToRepo, result.Action);
        Assert.Equal("", result.RepoWrite!.GetField("blocked-by"));
    }

    // A relation normalizer for the `sprint` field faithful to NotionSyncAdapter.ResolveRelationSubset: only the
    // re-provisioned parent's NEW local id ("sprint-7") resolves; an empty value stays "" (a valid clear); and any
    // other value — a stale OLD parent page id the board still points at, rendered as a raw Notion page id — drops
    // the key whole (its subset is empty). Models a child→parent relation across a parent re-provision (finding 1).
    private static readonly HashSet<string> ResolvableSprints = new(StringComparer.Ordinal) { "sprint-7" };
    private static SyncDoc ResolveSprintSubset(SyncDoc d) => new()
    {
        LocalId = d.LocalId, ExternalId = d.ExternalId, Body = d.Body, SourcePath = d.SourcePath,
        Fields = d.Fields.SelectMany(f =>
        {
            if (f.Key != "sprint")
                return new[] { f };
            var ids = f.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (ids.Length == 0)
                return new[] { new SyncField { Key = "sprint", Value = "" } };
            var kept = ids.Where(ResolvableSprints.Contains).ToList();
            return kept.Count == 0 ? [] : new[] { new SyncField { Key = "sprint", Value = string.Join(", ", kept) } };
        }).ToList(),
    };

    [Fact]
    public void ReprovisionedParentStaleRelationEcho_RepoWins_RePushes_NeverCleared()
    {
        // Finding 1 (HIGH, crux). A parent type re-provisioned: its pages were re-created with NEW ids, so the
        // child's relation on the board still points at the OLD parent page — a raw page id the normalizer drops,
        // leaving the external's resolvable subset EMPTY. The repo value still resolves (the write map holds the new
        // page) and equals base. This is a STALE echo, not a clear: repo wins, the relation is RE-PUSHED (resolving
        // to the new page on the write side), and it is NEVER collapsed to "" (the finding-1 silent data loss).
        var b = Doc("task-1", "body", ("title", "T"), ("sprint", "sprint-7"));
        b.ExternalId = "child-page";
        var repo = Doc("task-1", "body", ("title", "T"), ("sprint", "sprint-7"));
        var ext = Doc("task-1", "body", ("title", "T"), ("sprint", "old-parent-page")); // stale raw page id
        ext.ExternalId = "child-page";

        var result = ReconcileEngine.Reconcile(b, repo, ext, fieldNormalizer: ResolveSprintSubset);

        Assert.Equal(ReconcileAction.PushToExternal, result.Action);        // re-push, not WriteToRepo/None
        Assert.Null(result.RepoWrite);                                      // repo already correct, never rewritten to ""
        Assert.Equal("sprint-7", result.ExternalWrite!.GetField("sprint")); // re-pushed; the write map resolves it to the new page
        Assert.Equal("child-page", result.ExternalWrite.ExternalId);        // an UPDATE of the existing child page
        Assert.Equal("sprint-7", result.NewBase!.GetField("sprint"));       // base keeps the relation, never ""
        Assert.False(result.RepoChanged);                                   // a stale re-push, not a repo-side edit (no activity bump)
    }

    [Fact]
    public void ReprovisionedParentStaleRelation_ConcurrentBoardEdit_MergesAndRePushes_NeverCleared()
    {
        // Finding 1, concurrent-edit variant. The board edits a DIFFERENT field (status) the same tick the parent
        // re-mints. Without the fix the stale relation is read as a clear and MergeBoth pushes the empty relation to
        // the board (archiving the refs). With it: the board edit merges, the relation is preserved from the repo
        // AND re-pushed, and nothing is cleared.
        var b = Doc("task-1", "body", ("title", "T"), ("status", "open"), ("sprint", "sprint-7"));
        b.ExternalId = "child-page";
        var repo = Doc("task-1", "body", ("title", "T"), ("status", "open"), ("sprint", "sprint-7"));
        var ext = Doc("task-1", "body", ("title", "T"), ("status", "done"), ("sprint", "old-parent-page"));
        ext.ExternalId = "child-page";

        var result = ReconcileEngine.Reconcile(b, repo, ext, fieldNormalizer: ResolveSprintSubset);

        Assert.Equal(ReconcileAction.Merged, result.Action);
        Assert.Equal("done", result.RepoWrite!.GetField("status"));         // the board edit is honored
        Assert.Equal("sprint-7", result.RepoWrite.GetField("sprint"));      // relation preserved, not cleared
        Assert.Equal("sprint-7", result.ExternalWrite!.GetField("sprint")); // re-pushed resolvably to the new page
        Assert.Equal("sprint-7", result.NewBase!.GetField("sprint"));
    }

    [Fact]
    public void ReprovisionedParentStaleRelation_ConvergesInTwoTicks_NoChurn()
    {
        // Finding 1 + finding 4 fold-in. Tick 1 re-pushes the stale relation. Once the board points at the new page,
        // the external resolves to sprint-7 == base, so tick 2 (and every tick after) is a clean no-op — convergence
        // in <=2 ticks with zero WriteToRepo churn (the non-converging shape finding 4 named).
        var b = Doc("task-1", "body", ("title", "T"), ("sprint", "sprint-7"));
        b.ExternalId = "child-page";
        var repo = Doc("task-1", "body", ("title", "T"), ("sprint", "sprint-7"));
        var extStale = Doc("task-1", "body", ("title", "T"), ("sprint", "old-parent-page"));
        extStale.ExternalId = "child-page";

        var tick1 = ReconcileEngine.Reconcile(b, repo, extStale, fieldNormalizer: ResolveSprintSubset);
        Assert.Equal(ReconcileAction.PushToExternal, tick1.Action);

        // The re-push updated the board to the new page; the external now resolves the relation.
        var extHealed = Doc("task-1", "body", ("title", "T"), ("sprint", "sprint-7"));
        extHealed.ExternalId = "child-page";
        var tick2 = ReconcileEngine.Reconcile(tick1.NewBase, repo, extHealed, fieldNormalizer: ResolveSprintSubset);
        Assert.Equal(ReconcileAction.None, tick2.Action);
    }

    [Fact]
    public void ReprovisionedParentStaleRelation_RepoAlsoEdited_PushesRepo_NeverCleared()
    {
        // Finding 1, both-sides variant. The repo genuinely changed a field (status) AND the parent re-minted, so
        // the board relation is stale. The repo change drives a PushToExternal that already re-pushes the relation
        // resolvably; the stale echo must not be misrouted to a merge that reads it as a clear.
        var b = Doc("task-1", "body", ("title", "T"), ("status", "open"), ("sprint", "sprint-7"));
        b.ExternalId = "child-page";
        var repo = Doc("task-1", "body", ("title", "T"), ("status", "done"), ("sprint", "sprint-7")); // repo edited status
        var ext = Doc("task-1", "body", ("title", "T"), ("status", "open"), ("sprint", "old-parent-page"));
        ext.ExternalId = "child-page";

        var result = ReconcileEngine.Reconcile(b, repo, ext, fieldNormalizer: ResolveSprintSubset);

        Assert.Equal(ReconcileAction.PushToExternal, result.Action);
        Assert.Equal("done", result.ExternalWrite!.GetField("status"));
        Assert.Equal("sprint-7", result.ExternalWrite.GetField("sprint")); // relation re-pushed, never cleared
        Assert.Equal("sprint-7", result.NewBase!.GetField("sprint"));
    }

    [Fact]
    public void DeleteOneResurrect_UnmappedExternalRelation_SanitizedToResolvableSubset_NoRawId()
    {
        // Finding 3. Repo deleted the file while the board edited the page (so it resurrects); the page's relation
        // references an unmapped/archived page rendered as a raw Notion page id. The resurrect must sanitize the
        // relation to its resolvable subset — the raw page id must NEVER enter frontmatter or the base (the fourth
        // overlay-ingestion path the wave-7 resolvable-subset fix missed).
        var b = Doc("task-1", "body", ("title", "T"), ("sprint", "sprint-7"));
        b.ExternalId = "child-page";
        var ext = Doc("task-1", "body", ("title", "T2"), ("sprint", "raw-page-id")); // edited since base; relation unmapped
        ext.ExternalId = "child-page";

        var result = ReconcileEngine.Reconcile(b, null, ext, fieldNormalizer: ResolveSprintSubset);

        Assert.Equal(ReconcileAction.Conflict, result.Action);
        Assert.Equal("T2", result.RepoWrite!.GetField("title"));  // the board edit resurrected the file
        Assert.Null(result.RepoWrite.GetField("sprint"));         // raw page id NOT written to frontmatter
        Assert.Null(result.NewBase!.GetField("sprint"));          // nor recorded in the base
    }

    // ── ns-7 blocker: converter-migration shim ────────────────────────────────────────────────────

    // The board holds "degraded" (the old converter's projection of the canonical "canonical" body); the shim maps
    // that pair to true. Identity body normalizer keeps the two visibly distinct so the drift is real.
    private static bool StaleEcho(string external, string @base) => external == "degraded" && @base == "canonical";

    [Fact]
    public void StaleConverterEcho_ForcesRepoUpgradePush_NeverOverwritesTheCanonicalFile()
    {
        var b = Doc("t", "canonical", ("status", "open"));
        var repo = Doc("t", "canonical", ("status", "open"));
        var ext = Doc("t", "degraded", ("status", "open")); // board still on the old converter's rendering

        var result = ReconcileEngine.Reconcile(b, repo, ext, staleConverterEcho: StaleEcho);

        Assert.Equal(ReconcileAction.PushToExternal, result.Action); // repo wins → board re-rendered
        Assert.Equal("canonical", result.ExternalWrite!.Body);       // the canonical body, not the degraded echo
        Assert.Null(result.RepoWrite);                               // the file is NOT overwritten
        Assert.False(result.RepoChanged);                            // a migration, not a user edit — no activity bump
    }

    [Fact]
    public void GenuineBoardBodyEdit_NotClassifiedStale_WritesToRepoAsUsual()
    {
        var b = Doc("t", "canonical", ("status", "open"));
        var repo = Doc("t", "canonical", ("status", "open"));
        var ext = Doc("t", "a real human board edit", ("status", "open"));

        var result = ReconcileEngine.Reconcile(b, repo, ext, staleConverterEcho: StaleEcho);

        Assert.Equal(ReconcileAction.WriteToRepo, result.Action);
        Assert.Equal("a real human board edit", result.RepoWrite!.Body);
    }

    [Fact]
    public void AlreadyUpgradedBody_DoesNotChurn_EvenIfPredicateWouldMatch()
    {
        // The predicate is only consulted when the body genuinely drifts under the normalizer; an unchanged body
        // (board already on the new converter) stays a no-op, never a perpetual upgrade push.
        var b = Doc("t", "canonical", ("status", "open"));
        var result = ReconcileEngine.Reconcile(b, Doc("t", "canonical", ("status", "open")),
            Doc("t", "canonical", ("status", "open")), staleConverterEcho: static (_, _) => true);
        Assert.Equal(ReconcileAction.None, result.Action);
    }

    /// <summary>A docs-mirror normalizer: every field is adapter-invisible (a plain page carries no properties).</summary>
    private static SyncDoc DropAllFields(SyncDoc d) => new()
    {
        LocalId = d.LocalId, Fields = [], Body = d.Body, SourcePath = d.SourcePath,
    };

    private static SyncDoc DocX(string localId, string externalId, string body, params (string Key, string Value)[] fields) => new()
    {
        LocalId = localId, ExternalId = externalId,
        Fields = fields.Select(f => new SyncField { Key = f.Key, Value = f.Value }).ToList(),
        Body = body, SourcePath = $"tasks/{localId}.md",
    };

    private static readonly IReadOnlySet<string> StatusIsRepresentable = new HashSet<string> { "status" };

    [Fact]
    public void LocalScalarClearOnUpdate_SurfacesClearedKey_ForPush()
    {
        // Issue 0299 (F5): the base recorded a scalar the repo now blanks on an existing (externalId) object — the
        // push must carry it as an explicit clear so the board value is removed, not a silent revert.
        var b = DocX("t", "ext-1", "body", ("status", "open"));
        var repo = DocX("t", "ext-1", "body"); // status removed
        var ext = DocX("t", "ext-1", "body", ("status", "open")); // board still shows the old value

        var result = ReconcileEngine.Reconcile(b, repo, ext, representableScalarKeys: StatusIsRepresentable);

        Assert.Equal(ReconcileAction.PushToExternal, result.Action);
        Assert.Contains("status", result.ClearedKeys);
    }

    [Fact]
    public void ScalarClear_OnCreate_NoClearedKeys()
    {
        // A create (no externalId) omits blanks — "unset", not "clear" — so ClearedKeys stays empty even though the
        // synthetic base for a both-new merge carries no status.
        var repo = Doc("t", "body", ("status", "done"));
        var ext = Doc("t", "body", ("priority", "high")); // both new, different fields → merge with no externalId

        var result = ReconcileEngine.Reconcile(null, repo, ext, representableScalarKeys: StatusIsRepresentable);

        Assert.Empty(result.ClearedKeys);
    }

    [Fact]
    public void MergeBoth_ScalarResolvedToEmpty_SurfacesClearedKey()
    {
        // Two-sided edit whose merge drops a base-recorded scalar must also push the clear (F5).
        var b = DocX("t", "ext-1", "base body", ("status", "open"), ("priority", "low"));
        var repo = DocX("t", "ext-1", "base body", ("priority", "high")); // repo dropped status, changed priority
        var ext = DocX("t", "ext-1", "EXTERNAL body", ("status", "open"), ("priority", "low")); // board edited the body

        var result = ReconcileEngine.Reconcile(b, repo, ext, representableScalarKeys: new HashSet<string> { "status", "priority" });

        Assert.True(result.Action is ReconcileAction.Merged or ReconcileAction.Conflict);
        Assert.Contains("status", result.ClearedKeys);
    }

    [Fact]
    public void DocsMirrorOverlay_ExternalBodyEdit_PreservesAllFrontmatter_Unchanged()
    {
        // Issue 0299 (F1 pin): with no representable-scalar-keys set (the docs-mirror default), EVERY field is
        // adapter-invisible and must be restored from the repo when a board body edit writes back — so frontmatter
        // is byte-preserved. This pins that the F1 fix (representable scalars visible for the spine) did NOT change
        // the docs-mirror all-invisible behavior.
        var b = Doc("t", "body", ("area", "project"), ("type", "folder-meta"), ("needs-human", "false"));
        var repo = Doc("t", "body", ("area", "project"), ("type", "folder-meta"), ("needs-human", "false"));
        var ext = Doc("t", "edited body on the board"); // docs mirror: external carries no fields, only body

        var result = ReconcileEngine.Reconcile(b, repo, ext, fieldNormalizer: DropAllFields); // representable omitted ⇒ empty

        Assert.Equal(ReconcileAction.WriteToRepo, result.Action);
        Assert.Equal("edited body on the board", result.RepoWrite!.Body);
        Assert.Equal("project", result.RepoWrite.GetField("area"));       // frontmatter preserved
        Assert.Equal("folder-meta", result.RepoWrite.GetField("type"));
        Assert.Equal("false", result.RepoWrite.GetField("needs-human"));  // NOT clobbered/dropped in docs mirror
    }
}
