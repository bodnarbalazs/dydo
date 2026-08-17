// @test-tier: 2
namespace DynaDocs.Tests.Sync;

using DynaDocs.Models;
using DynaDocs.Sync;
using DynaDocs.Sync.Projection;

public sealed class ProjectedReconcileTests
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "dydo-projected-runner-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void ProjectedReconcile_NoOp_LeavesBothRepresentationsUntouched()
    {
        var result = Reconcile("one", "one", "one", "one");

        Assert.Equal(ReconcileAction.None, result.Action);
        Assert.False(result.PatchBody);
        Assert.False(result.WriteBody);
    }

    [Fact]
    public void ProjectedReconcile_RepoBodyEdit_PushesOnlyBody()
    {
        var result = Reconcile("one", "one", "local", "one");

        Assert.Equal(ReconcileAction.PushToExternal, result.Action);
        Assert.True(result.WriteBody);
        Assert.False(result.PatchFields);
        Assert.Equal("local", result.ExternalWrite!.Body);
    }

    [Fact]
    public void ProjectedReconcile_ExternalBodyEdit_PatchesOnlyBody()
    {
        var result = Reconcile("one\n\ntwo", "one\n\ntwo", "one\n\ntwo", "one\n\nthree");

        Assert.Equal(ReconcileAction.WriteToRepo, result.Action);
        Assert.True(result.PatchBody);
        Assert.False(result.WriteBody);
        Assert.Contains("three", result.RepoWrite!.Body);
    }

    [Fact]
    public void ProjectedReconcile_FieldOnlyEdit_DoesNotWriteBody()
    {
        var result = Reconcile("body", "body", "body", "body", "open", "done");

        Assert.Equal(ReconcileAction.WriteToRepo, result.Action);
        Assert.True(result.PatchFields);
        Assert.False(result.PatchBody);
        Assert.False(result.WriteBody);
    }

    [Fact]
    public void ProjectedReconcile_CombinedChanges_ComposesFieldAndBody()
    {
        var result = Reconcile("one\n\ntwo", "one\n\ntwo", "external\n\ntwo", "one\n\nlocal", "open", "done");

        Assert.Equal(ReconcileAction.Merged, result.Action);
        Assert.True(result.PatchFields);
        Assert.True(result.WriteBody);
        Assert.Equal("done", result.RepoWrite!.GetField("status"));
    }

    [Fact]
    public void ProjectedReconcile_Overlap_IsStructuredConflictWithCompleteCandidates()
    {
        var result = Reconcile("one", "one", "local", "external");

        Assert.Equal(ReconcileAction.Conflict, result.Action);
        Assert.NotNull(result.StructuredConflictReason);
        Assert.Contains("<<<<<<< repo", result.RepoWrite!.Body);
        Assert.Contains("local", result.RepoWrite.Body);
        Assert.Contains("external", result.RepoWrite.Body);
        Assert.Null(result.ExternalWrite);
    }

    [Fact]
    public void ProjectedReconcile_NewRepoDoc_RequestsCreateReceipt()
    {
        var repo = Doc("new", "body", "open");
        var result = ReconcileEngine.ReconcileProjected(null, null, repo, null);

        Assert.Equal(ReconcileAction.Create, result.Action);
        Assert.True(result.WriteBody);
        Assert.Equal(BodyWriteOperationKind.Create, result.BodyWriteKind);
    }

    [Fact]
    public void ProjectedReconcile_LocalDeleteWithUnchangedExternalProjection_DeletesExternal()
    {
        var baseDoc = Doc("t", "local base", "open");
        baseDoc.ExternalId = "page";
        var external = Doc("t", "external base", "open");
        external.ExternalId = "page";

        var result = ReconcileEngine.ReconcileProjected(baseDoc, new DualBodyBase("local base", "external base"), null, external);

        Assert.Equal(ReconcileAction.Delete, result.Action);
        Assert.Equal("page", result.ExternalDelete);
    }

    [Fact]
    public void ProjectedReconcile_LocalDeleteWithChangedExternalProjection_IsStructuredConflict()
    {
        var baseDoc = Doc("t", "local base", "open");
        baseDoc.ExternalId = "page";
        var external = Doc("t", "external change", "open");
        external.ExternalId = "page";

        var result = ReconcileEngine.ReconcileProjected(baseDoc, new DualBodyBase("local base", "external base"), null, external);

        Assert.Equal(ReconcileAction.Conflict, result.Action);
        Assert.Contains("canonical file deleted", result.RepoWrite!.Body);
        Assert.Contains("external change", result.RepoWrite.Body);
        Assert.NotNull(result.StructuredConflictReason);
    }

    [Fact]
    public void ProjectedReconcile_LocalDeleteWithExternalFieldEdit_ResurrectsCanonicalUsingGenericFieldConflict()
    {
        var baseDoc = Doc("t", "local base", "open");
        baseDoc.ExternalId = "page";
        var external = Doc("t", "external base", "done");
        external.ExternalId = "page";

        var result = ReconcileEngine.ReconcileProjected(baseDoc, new DualBodyBase("local base", "external base"), null, external);

        Assert.Equal(ReconcileAction.Conflict, result.Action);
        Assert.Null(result.StructuredConflictReason);
        Assert.Equal("done", result.RepoWrite!.GetField("status"));
        Assert.Equal("local base", result.RepoWrite.Body);
        Assert.Equal("local base", result.NewBase!.Body);
        Assert.Equal(new DualBodyBase("local base", "external base"), result.NewBodyBase);
    }

    [Fact]
    public void ProjectedReconcile_ExternalDeleteWithUnchangedLocalProjection_DeletesCanonical()
    {
        var baseDoc = Doc("t", "local base", "open");
        baseDoc.ExternalId = "page";
        var repo = new SyncDoc
        {
            LocalId = "t",
            ExternalId = "page",
            Fields = [new SyncField { Key = "status", Value = "open" }],
            Body = "local base",
            SourcePath = "t.md",
        };

        var result = ReconcileEngine.ReconcileProjected(baseDoc, new DualBodyBase("local base", "external base"), repo, null);

        Assert.Equal(ReconcileAction.Delete, result.Action);
        Assert.Equal("t.md", result.RepoDelete);
    }

    [Fact]
    public void ProjectedReconcile_ExternalDeleteWithChangedLocalProjection_IsStructuredConflict()
    {
        var baseDoc = Doc("t", "local base", "open");
        baseDoc.ExternalId = "page";
        var repo = Doc("t", "local change", "open");
        repo.ExternalId = "page";

        var result = ReconcileEngine.ReconcileProjected(baseDoc, new DualBodyBase("local base", "external base"), repo, null);

        Assert.Equal(ReconcileAction.Conflict, result.Action);
        Assert.Null(result.ExternalWrite);
        Assert.Contains("external record deleted", result.RepoWrite!.Body);
        Assert.NotNull(result.StructuredConflictReason);
    }

    [Fact]
    public void ProjectedReconcile_ExternalDeleteWithLocalFieldEdit_ResurrectsExternalUsingGenericFieldConflict()
    {
        var baseDoc = Doc("t", "local base", "open");
        baseDoc.ExternalId = "page";
        var repo = Doc("t", "local base", "done");
        repo.ExternalId = "page";

        var result = ReconcileEngine.ReconcileProjected(baseDoc, new DualBodyBase("local base", "external base"), repo, null);

        Assert.Equal(ReconcileAction.Conflict, result.Action);
        Assert.Null(result.StructuredConflictReason);
        Assert.Equal("done", result.ExternalWrite!.GetField("status"));
        Assert.Equal("local base", result.ExternalWrite.Body);
        Assert.Equal("local base", result.NewBase!.Body);
        Assert.True(result.WriteBody);
        Assert.Equal(BodyWriteOperationKind.Create, result.BodyWriteKind);
        Assert.Equal(new DualBodyBase("local base", "external base"), result.NewBodyBase);
    }

    [Fact]
    public void ProjectedReconcile_BothDeleted_RetiresTheSnapshot()
    {
        var baseDoc = Doc("t", "local base", "open");
        baseDoc.ExternalId = "page";

        var result = ReconcileEngine.ReconcileProjected(baseDoc, new DualBodyBase("local base", "external base"), null, null);

        Assert.Equal(ReconcileAction.Retire, result.Action);
    }

    [Fact]
    public void ProjectedRunner_LocalMissingFieldEdit_RestoresAuthoredBodyWithoutCollapsingBases()
    {
        Directory.CreateDirectory(_directory);
        var baseDoc = Doc("t", "__authored__", "open");
        baseDoc.ExternalId = "page";
        var external = Doc("t", "**external**", "done");
        external.ExternalId = "page";
        var store = Store(baseDoc, "__authored__", "**external**");
        var path = Path.Combine(_directory, "t.md");

        Runner(new ReceiptAdapter(new SyncRecord { ExternalId = "page", Fields = external.Fields, Body = external.Body }),
            store, _ => path, _ => Path.Combine(_directory, "shadow.md")).Run([]);

        var restored = SyncDocFile.Read(path, "t", path);
        Assert.Equal("__authored__", restored.Body);
        Assert.Equal("done", restored.GetField("status"));
        Assert.Equal(new DualBodyBase("__authored__", "**external**"), store.GetDualBodyBase("t"));
        Assert.Equal("done", store.Get("t")!.GetField("status"));
    }

    [Fact]
    public void ProjectedRunner_ExternalMissingFieldEdit_JournalsCreateAndUsesLossyReceiptAsExternalBase()
    {
        Directory.CreateDirectory(_directory);
        var baseDoc = Doc("t", "__authored__", "open");
        baseDoc.ExternalId = "page";
        var repo = Doc("t", "__authored__", "done");
        repo.ExternalId = "page";
        var store = Store(baseDoc, "__authored__", "**external-base**");
        var adapter = new ReceiptAdapter
        {
            LossyBody = body => body.Replace("__", "**", StringComparison.Ordinal),
            BeforeApply = () =>
            {
                var intent = Assert.IsType<BodyWriteIntent>(store.GetPendingBodyWrite("t"));
                Assert.Equal(BodyWriteOperationKind.Create, intent.Kind);
                Assert.NotEmpty(intent.OperationId);
                Assert.Equal("__authored__", intent.IntendedLocalBody);
            },
        };

        Runner(adapter, store, id => Path.Combine(_directory, id + ".md"), _ => Path.Combine(_directory, "shadow.md")).Run([repo]);

        Assert.Equal(1, adapter.UpsertCount);
        Assert.NotNull(adapter.BodyOperationIds.Single());
        Assert.Null(store.GetPendingBodyWrite("t"));
        Assert.Equal(new DualBodyBase("__authored__", "**authored**"), store.GetDualBodyBase("t"));
        Assert.Equal("created", store.Get("t")!.ExternalId);
        Assert.Equal("done", store.Get("t")!.GetField("status"));
    }

    [Fact]
    public void ProjectedRunner_ExternalMissingFieldEdit_CrashAfterCreateKeepsIntentAndPriorDualBase()
    {
        Directory.CreateDirectory(_directory);
        var baseDoc = Doc("t", "__authored__", "open");
        baseDoc.ExternalId = "page";
        var repo = Doc("t", "__authored__", "done");
        repo.ExternalId = "page";
        var store = Store(baseDoc, "__authored__", "**external-base**");
        var adapter = new ReceiptAdapter { ThrowAfterApplyCount = 1 };

        Assert.Throws<InvalidOperationException>(() =>
            Runner(adapter, store, id => Path.Combine(_directory, id + ".md"), _ => Path.Combine(_directory, "shadow.md")).Run([repo]));

        var intent = Assert.IsType<BodyWriteIntent>(store.GetPendingBodyWrite("t"));
        Assert.Equal(BodyWriteOperationKind.Create, intent.Kind);
        Assert.NotEmpty(intent.OperationId);
        Assert.Equal(new DualBodyBase("__authored__", "**external-base**"), store.GetDualBodyBase("t"));
        Assert.Equal("open", store.Get("t")!.GetField("status"));
    }

    [Fact]
    public void ProjectedRunner_MappedTruncatedRecord_WritesShadowAndLeavesCanonicalAndBaseUntouched()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "t.md");
        var repo = Doc("t", "canonical", "open");
        repo.ExternalId = "page";
        SyncDocFile.Write(path, repo);
        repo = SyncDocFile.Read(path, "t", path);
        repo.ExternalId = "page";
        var store = Store(repo, "base", "external-base");
        var adapter = new ReceiptAdapter(new SyncRecord
        {
            ExternalId = "page", Fields = repo.Fields, Body = "partial", BodyReadStatus = SyncBodyReadStatus.Truncated,
        });
        var shadow = Path.Combine(_directory, "shadow", "t.md");

        var result = Runner(adapter, store, id => id == "t" ? path : Path.Combine(_directory, id + ".md"), id => shadow)
            .Run([repo]);

        Assert.Equal(["t"], result.ShadowedLocalIds);
        Assert.Equal(SyncDocFile.Render(repo), File.ReadAllText(path));
        Assert.Contains("external body unavailable: truncated export", File.ReadAllText(shadow));
        Assert.Equal(new DualBodyBase("base", "external-base"), store.GetDualBodyBase("t"));
        Assert.Equal(0, adapter.UpsertCount);
    }

    [Fact]
    public void ProjectedRunner_UnmappedTruncatedRecord_CreatesOnlyShadow()
    {
        Directory.CreateDirectory(_directory);
        var store = new BaseSnapshotStore(Path.Combine(_directory, "snapshot.json"));
        var adapter = new ReceiptAdapter(new SyncRecord
        {
            ExternalId = "page", Fields = [new SyncField { Key = SyncRunner.LocalIdField, Value = "new" }],
            Body = "partial", BodyReadStatus = SyncBodyReadStatus.Truncated,
        });
        var canonical = Path.Combine(_directory, "new.md");
        var shadow = Path.Combine(_directory, "shadow", "new.md");

        var result = Runner(adapter, store, _ => canonical, _ => shadow).Run([]);

        Assert.Equal(["new"], result.ShadowedLocalIds);
        Assert.False(File.Exists(canonical));
        Assert.Contains("<<<<<<< repo", File.ReadAllText(shadow));
        Assert.Contains("external body unavailable: truncated export", File.ReadAllText(shadow));
        Assert.Equal(0, adapter.UpsertCount);
    }

    [Fact]
    public void ProjectedRunner_PendingCreate_IsFencedWithoutAnOperationIdentityMatch()
    {
        Directory.CreateDirectory(_directory);
        var repo = Doc("t", "body", "open");
        var store = new BaseSnapshotStore(Path.Combine(_directory, "snapshot.json"));
        store.WritePendingBodyWrite(Intent(BodyWriteOperationKind.Create, null, "t"));
        var adapter = new ReceiptAdapter(new SyncRecord
        {
            ExternalId = "page", Fields = [new SyncField { Key = SyncRunner.LocalIdField, Value = "t" }], Body = "body",
        });

        Runner(adapter, store, id => Path.Combine(_directory, id + ".md"), _ => Path.Combine(_directory, "shadow.md")).Run([repo]);

        Assert.Equal(0, adapter.UpsertCount);
        Assert.NotNull(store.GetPendingBodyWrite("t"));
        Assert.Null(store.Get("t")!.ExternalId);
    }

    [Fact]
    public void ProjectedRunner_PendingUpdate_WithMovedExternal_RemainsJournaled()
    {
        PendingMovedExternalRemainsJournaled(BodyWriteOperationKind.Update);
    }

    [Fact]
    public void ProjectedRunner_PendingResolution_WithMovedExternal_RemainsJournaled()
    {
        PendingMovedExternalRemainsJournaled(BodyWriteOperationKind.Resolution);
    }

    private void PendingMovedExternalRemainsJournaled(BodyWriteOperationKind kind)
    {
        Directory.CreateDirectory(_directory);
        var repo = Doc("t", "after", "open");
        repo.ExternalId = "page";
        var store = Store(repo, "before", "before-external");
        store.WritePendingBodyWrite(Intent(kind, "page", "t"));
        var adapter = new ReceiptAdapter(new SyncRecord { ExternalId = "page", Fields = repo.Fields, Body = "after" });

        Runner(adapter, store, id => Path.Combine(_directory, id + ".md"), _ => Path.Combine(_directory, "shadow.md")).Run([repo]);

        Assert.Equal(0, adapter.UpsertCount);
        Assert.NotNull(store.GetPendingBodyWrite("t"));
        Assert.Equal(new DualBodyBase("before", "before-external"), store.GetDualBodyBase("t"));
    }

    [Fact]
    public void ProjectedRunner_PersistsIntentBeforeApply_ThenCommitsOnlyReceipt()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "t.md");
        var repo = Doc("t", "after", "open");
        repo.ExternalId = "page";
        SyncDocFile.Write(path, repo);
        repo = SyncDocFile.Read(path, "t", path);
        var baseDoc = Doc("t", "before", "open");
        baseDoc.ExternalId = "page";
        var store = Store(baseDoc, "before", "before");
        var adapter = new ReceiptAdapter(new SyncRecord { ExternalId = "page", Fields = repo.Fields, Body = "before" })
        {
            BeforeApply = () => Assert.NotNull(store.GetPendingBodyWrite("t")),
        };

        Runner(adapter, store, _ => path, _ => Path.Combine(_directory, "shadow.md")).Run([repo]);

        Assert.Null(store.GetPendingBodyWrite("t"));
        Assert.Equal(new DualBodyBase("after", "after"), store.GetDualBodyBase("t"));
    }

    [Fact]
    public void ProjectedRunner_ApplyThrowBeforeMutation_RetriesTheJournaledOperation()
    {
        Directory.CreateDirectory(_directory);
        var baseDoc = Doc("t", "**before**", "open");
        baseDoc.ExternalId = "page";
        var repo = Doc("t", "**after**", "open");
        repo.ExternalId = "page";
        var store = Store(baseDoc, "**before**", "__before__");
        var adapter = new ReceiptAdapter(new SyncRecord { ExternalId = "page", Fields = repo.Fields, Body = "__before__" })
        {
            ThrowBeforeApplyCount = 1,
        };
        var runner = Runner(adapter, store, id => Path.Combine(_directory, id + ".md"), _ => Path.Combine(_directory, "shadow.md"));

        Assert.Throws<InvalidOperationException>(() => runner.Run([repo]));
        var operationId = Assert.IsType<BodyWriteIntent>(store.GetPendingBodyWrite("t")).OperationId;
        Assert.Equal(new DualBodyBase("**before**", "__before__"), store.GetDualBodyBase("t"));

        runner.Run([repo]);

        Assert.Equal([operationId, operationId], adapter.BodyOperationIds);
        Assert.Null(store.GetPendingBodyWrite("t"));
        Assert.Equal(new DualBodyBase("**after**", "**after**"), store.GetDualBodyBase("t"));
    }

    [Fact]
    public void ProjectedRunner_LossyWriteWithLostReadback_RecoversOnlyAfterProjectionProvesIntent()
    {
        Directory.CreateDirectory(_directory);
        var baseDoc = Doc("t", "__before__", "open");
        baseDoc.ExternalId = "page";
        var repo = Doc("t", "__after__", "open");
        repo.ExternalId = "page";
        var store = Store(baseDoc, "__before__", "**before**");
        var adapter = new ReceiptAdapter(new SyncRecord { ExternalId = "page", Fields = repo.Fields, Body = "**before**" })
        {
            LossyBody = body => body.Replace("__", "**", StringComparison.Ordinal),
            ThrowAfterApplyCount = 1,
        };
        var runner = Runner(adapter, store, id => Path.Combine(_directory, id + ".md"), _ => Path.Combine(_directory, "shadow.md"));

        Assert.Throws<InvalidOperationException>(() => runner.Run([repo]));
        Assert.NotNull(store.GetPendingBodyWrite("t"));

        runner.Run([repo]);

        Assert.Equal(1, adapter.UpsertCount);
        Assert.Null(store.GetPendingBodyWrite("t"));
        Assert.Equal(new DualBodyBase("__after__", "**after**"), store.GetDualBodyBase("t"));
    }

    [Fact]
    public void ProjectedRunner_ConcurrentExternalChange_ShadowKeepsIntentAndPriorBases()
    {
        Directory.CreateDirectory(_directory);
        var repo = Doc("t", "local", "open");
        repo.ExternalId = "page";
        var baseDoc = Doc("t", "before", "open");
        baseDoc.ExternalId = "page";
        var store = Store(baseDoc, "before", "before");
        var intent = new BodyWriteIntent
        {
            OperationId = Guid.NewGuid().ToString(),
            Kind = BodyWriteOperationKind.Update,
            LocalId = "t",
            ExternalId = "page",
            PriorLocalBody = "before",
            PriorExternalBody = "before",
            IntendedLocalBody = "local",
        };
        store.WritePendingBodyWrite(intent);
        var adapter = new ReceiptAdapter(new SyncRecord { ExternalId = "page", Fields = repo.Fields, Body = "external" });
        var shadow = Path.Combine(_directory, "shadow.md");

        Runner(adapter, store, id => Path.Combine(_directory, id + ".md"), _ => shadow).Run([repo]);

        var shadowBody = File.ReadAllText(shadow);
        Assert.Contains("<<<<<<< repo\nlocal\n=======\nexternal\n>>>>>>> external", shadowBody);
        Assert.Equal(intent, store.GetPendingBodyWrite("t"));
        Assert.Equal(new DualBodyBase("before", "before"), store.GetDualBodyBase("t"));
        Assert.Equal(0, adapter.UpsertCount);
    }

    [Fact]
    public void ProjectedRunner_PendingProjectionWithoutShadowSink_StaysFenced()
    {
        Directory.CreateDirectory(_directory);
        var repo = Doc("t", "local", "open");
        repo.ExternalId = "page";
        var baseDoc = Doc("t", "before", "open");
        baseDoc.ExternalId = "page";
        var store = Store(baseDoc, "before", "before");
        var intent = Intent(BodyWriteOperationKind.Update, "page", "t");
        store.WritePendingBodyWrite(intent);
        var adapter = new ReceiptAdapter(new SyncRecord { ExternalId = "page", Fields = repo.Fields, Body = "external" });
        var runner = new SyncRunner(adapter, store, (id, _, _) => Path.Combine(_directory, id + ".md"),
            conflictShadowPathFor: null, useProjectedBodies: true);

        var run = runner.Run([repo]);

        Assert.Empty(run.ShadowedLocalIds);
        Assert.Equal(intent, store.GetPendingBodyWrite("t"));
        Assert.Equal(0, adapter.UpsertCount);
    }

    [Fact]
    public void SyncRunner_SanitizeLocalId_StripsDrivePrefixWithoutSeparator()
    {
        Assert.Equal("page", SyncRunner.SanitizeLocalId("C:page"));
    }

    [Fact]
    public void ProjectedRunner_MarkerBearingResolutionBeforeMutation_StaysFencedAndShadowed()
    {
        Directory.CreateDirectory(_directory);
        var repo = Doc("t", "<<<<<<< repo\nlocal\n=======\nexternal\n>>>>>>> external", "open");
        repo.ExternalId = "page";
        var baseDoc = Doc("t", "before", "open");
        baseDoc.ExternalId = "page";
        var store = Store(baseDoc, "before", "before");
        var intent = new BodyWriteIntent
        {
            OperationId = Guid.NewGuid().ToString(),
            Kind = BodyWriteOperationKind.Resolution,
            LocalId = "t",
            ExternalId = "page",
            PriorLocalBody = "before",
            PriorExternalBody = "before",
            IntendedLocalBody = repo.Body,
        };
        store.WritePendingBodyWrite(intent);
        // The remote body still equals the intent's prior external base. A retry would ordinarily be safe,
        // but a marker-bearing resolution is not a resolved candidate and must never reach Apply.
        var adapter = new ReceiptAdapter(new SyncRecord { ExternalId = "page", Fields = repo.Fields, Body = "before" });
        var shadow = Path.Combine(_directory, "shadow.md");

        Runner(adapter, store, id => Path.Combine(_directory, id + ".md"), _ => shadow).Run([repo]);

        Assert.Equal(intent, store.GetPendingBodyWrite("t"));
        Assert.Equal(new DualBodyBase("before", "before"), store.GetDualBodyBase("t"));
        Assert.Contains("<<<<<<< repo", File.ReadAllText(shadow));
        Assert.Equal(0, adapter.UpsertCount);
    }

    [Fact]
    public void ProjectedRunner_ReceiptlessBodyWrite_LeavesIntentAndBaseForRecovery()
    {
        Directory.CreateDirectory(_directory);
        var baseDoc = Doc("t", "before", "open");
        baseDoc.ExternalId = "page";
        var repo = Doc("t", "after", "open");
        repo.ExternalId = "page";
        var store = Store(baseDoc, "before", "before");
        var adapter = new ReceiptAdapter(new SyncRecord { ExternalId = "page", Fields = repo.Fields, Body = "before" })
        {
            SuppressReceipts = true,
        };

        Runner(adapter, store, id => Path.Combine(_directory, id + ".md"), _ => Path.Combine(_directory, "shadow.md")).Run([repo]);

        Assert.NotNull(store.GetPendingBodyWrite("t"));
        Assert.Equal(new DualBodyBase("before", "before"), store.GetDualBodyBase("t"));
    }

    [Fact]
    public void ProjectedRunner_PlanTruncatedRecord_IsExplicitlyUnhandledWithoutAdvancingBase()
    {
        Directory.CreateDirectory(_directory);
        var repo = Doc("t", "canonical", "open");
        repo.ExternalId = "page";
        var store = Store(repo, "base", "external-base");
        var adapter = new ReceiptAdapter(new SyncRecord
        {
            ExternalId = "page", Fields = repo.Fields, Body = "partial", BodyReadStatus = SyncBodyReadStatus.Truncated,
        });

        var result = Assert.Single(Runner(adapter, store, id => Path.Combine(_directory, id + ".md"), _ => Path.Combine(_directory, "shadow.md")).Plan([repo]));

        Assert.True(result.UnhandledProjection);
        Assert.Equal(ReconcileAction.Conflict, result.Action);
        Assert.Equal(new DualBodyBase("base", "external-base"), store.GetDualBodyBase("t"));
    }

    [Fact]
    public void ProjectedRunner_RunDeltaTruncatedRecord_RemainsUnhandledAndLeavesBaseUntouched()
    {
        Directory.CreateDirectory(_directory);
        var repo = Doc("t", "canonical", "open");
        repo.ExternalId = "page";
        var store = Store(repo, "base", "external-base");
        var record = new SyncRecord
        {
            ExternalId = "page", Fields = repo.Fields, Body = "partial", BodyReadStatus = SyncBodyReadStatus.Truncated,
        };

        var run = Runner(new ReceiptAdapter(record), store, id => Path.Combine(_directory, id + ".md"),
            _ => Path.Combine(_directory, "shadow.md")).RunDelta([repo], [record], new HashSet<string> { "t" });

        Assert.True(Assert.Single(run.Results).UnhandledProjection);
        Assert.Equal(new DualBodyBase("base", "external-base"), store.GetDualBodyBase("t"));
    }

    private static ReconcileResult Reconcile(string localBase, string externalBase, string local, string external,
        string localStatus = "open", string externalStatus = "open")
    {
        var baseDoc = Doc("t", localBase, "open");
        baseDoc.ExternalId = "page";
        var repo = Doc("t", local, localStatus);
        repo.ExternalId = "page";
        var externalDoc = Doc("t", external, externalStatus);
        externalDoc.ExternalId = "page";
        return ReconcileEngine.ReconcileProjected(baseDoc, new DualBodyBase(localBase, externalBase), repo, externalDoc);
    }

    private static SyncDoc Doc(string id, string body, string status) => new()
    {
        LocalId = id,
        Fields = [new SyncField { Key = "status", Value = status }],
        Body = body,
        SourcePath = "",
    };

    private BaseSnapshotStore Store(SyncDoc doc, string local, string external)
    {
        var store = new BaseSnapshotStore(Path.Combine(_directory, "snapshot.json"));
        store.SetDualBodyBase(doc, new DualBodyBase(local, external));
        return store;
    }

    private static BodyWriteIntent Intent(BodyWriteOperationKind kind, string? externalId, string localId) => new()
    {
        OperationId = Guid.NewGuid().ToString(), Kind = kind, LocalId = localId, ExternalId = externalId,
        PriorLocalBody = "before", PriorExternalBody = "before-external", IntendedLocalBody = "after",
    };

    private static SyncRunner Runner(ReceiptAdapter adapter, BaseSnapshotStore store,
        Func<string, string> path, Func<string, string> shadow) =>
        new(adapter, (BaseSnapshotStore)store, (id, _, _) => path(id), shadow, useProjectedBodies: true);

    private sealed class ReceiptAdapter(params SyncRecord[] records) : ISyncAdapter
    {
        private readonly Dictionary<string, SyncRecord> _records = records.ToDictionary(record => record.ExternalId);
        public int ApplyCalls { get; private set; }
        public int UpsertCount { get; private set; }
        public List<string> BodyOperationIds { get; } = [];
        public Action? BeforeApply { get; init; }
        public int ThrowBeforeApplyCount { get; set; }
        public int ThrowAfterApplyCount { get; set; }
        public bool SuppressReceipts { get; init; }
        public Func<string, string>? LossyBody { get; init; }
        public IReadOnlyList<SyncRecord> ReadExternalState() => _records.Values.ToList();
        public void Apply(SyncChangeSet changes, IDictionary<string, string> assigned, ICollection<string> deleted,
            ICollection<string> emptyBodied)
        {
            ApplyCalls++;
            UpsertCount += changes.Upserts.Count;
            BeforeApply?.Invoke();
            BodyOperationIds.AddRange(changes.Upserts.Where(upsert => upsert.WriteBody && upsert.OperationId != null)
                .Select(upsert => upsert.OperationId!));
            if (ThrowBeforeApplyCount-- > 0)
                throw new InvalidOperationException("before mutation");
            foreach (var upsert in changes.Upserts)
            {
                var id = upsert.ExternalId ?? "created";
                if (upsert.ExternalId == null) assigned[upsert.LocalId] = id;
                var previous = _records.TryGetValue(id, out var record) ? record : null;
                _records[id] = new SyncRecord { ExternalId = id, Fields = upsert.Fields,
                    Body = upsert.WriteBody ? LossyBody?.Invoke(upsert.Body) ?? upsert.Body : previous?.Body ?? "" };
            }
            if (ThrowAfterApplyCount-- > 0)
                throw new InvalidOperationException("after mutation");
        }
        public SyncApplyResult ApplyWithReceipts(SyncChangeSet changes, IDictionary<string, string> assigned,
            ICollection<string> deleted, ICollection<string> emptyBodied)
        {
            Apply(changes, assigned, deleted, emptyBodied);
            if (SuppressReceipts)
                return new SyncApplyResult();
            return new SyncApplyResult
            {
                BodyWriteReceipts = changes.Upserts.Where(upsert => upsert.WriteBody && upsert.OperationId != null)
                .Select(upsert => new BodyWriteReceipt
                    {
                        OperationId = upsert.OperationId!, LocalId = upsert.LocalId,
                        ExternalId = upsert.ExternalId ?? assigned[upsert.LocalId],
                        ObservedExternalBody = _records[upsert.ExternalId ?? assigned[upsert.LocalId]].Body,
                    }).ToList(),
            };
        }
    }
}
