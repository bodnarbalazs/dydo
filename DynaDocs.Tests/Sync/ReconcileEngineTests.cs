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
    private static readonly HashSet<string> ResolvableBlockers = new(StringComparer.Ordinal) { "b" };
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
        // invisible. A colleague adds a resolvable blocker (b) on the board, and in the same tick edits a
        // different field (status). The overlay must UNION the board's entry with the repo's pending one, not
        // REPLACE the board edit with the raw repo value — else b is discarded here and clobbered on push.
        var b = Doc("t", "body", ("title", "T"), ("status", "open"), ("blocked-by", "c"));
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
        // keeps its unresolvable c. A repo pending entry must ALWAYS survive: the merged value keeps c.
        var b = Doc("t", "body", ("title", "T"), ("status", "open"), ("blocked-by", "c"));
        var repo = Doc("t", "body", ("title", "T"), ("status", "open"), ("blocked-by", "c"));
        var ext = Doc("t", "body", ("title", "T"), ("status", "done"), ("blocked-by", "")); // board relation empty

        var result = ReconcileEngine.Reconcile(b, repo, ext, fieldNormalizer: ResolveBlockedBySubset);

        Assert.Equal(ReconcileAction.WriteToRepo, result.Action);
        Assert.Equal("c", result.RepoWrite!.GetField("blocked-by")); // repo pending entry never lost
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
}
