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
    public void GoneFromBothSides_ReturnsNone()
    {
        var b = Doc("t", "body", ("status", "open"));
        var result = ReconcileEngine.Reconcile(b, null, null);
        Assert.Equal(ReconcileAction.None, result.Action);
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
}
