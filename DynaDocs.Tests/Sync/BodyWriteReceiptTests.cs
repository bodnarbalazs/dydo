// @test-tier: 2
namespace DynaDocs.Tests.Sync;

using System.Text.Json;
using DynaDocs.Models;
using DynaDocs.Serialization;
using DynaDocs.Sync;
using DynaDocs.Sync.Projection;

public sealed class BodyWriteReceiptTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "dydo-body-receipt-" + Guid.NewGuid().ToString("N"));

    public BodyWriteReceiptTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    [Fact]
    public void PendingIntent_SurvivesFreshStoreInstance()
    {
        var path = Path.Combine(_directory, "snapshot.json");
        var intent = Intent(BodyWriteOperationKind.Update, "page");
        var store = new BaseSnapshotStore(path);
        store.SetDualBodyBase(Doc("task", "page"), new DualBodyBase("before", "echo-before"));
        store.WritePendingBodyWrite(intent);
        store.Save();

        var reloaded = new BaseSnapshotStore(path);
        var pending = Assert.IsType<BodyWriteIntent>(reloaded.GetPendingBodyWrite("task"));
        Assert.Equal(intent.OperationId, pending.OperationId);
        Assert.Equal("echo-before", pending.PriorExternalBody);
    }

    [Fact]
    public void FreshCreateIntent_PersistsAnExplicitV2DualBase()
    {
        var path = Path.Combine(_directory, "snapshot.json");
        var store = new BaseSnapshotStore(path);
        var intent = Intent(BodyWriteOperationKind.Create, null, "new-task");

        store.WritePendingBodyWrite(intent);
        store.Save();

        var reloaded = new BaseSnapshotStore(path);
        Assert.True(reloaded.IsV2("new-task"));
        Assert.Equal(new DualBodyBase("before", "echo-before"), reloaded.GetDualBodyBase("new-task"));
        Assert.Null(reloaded.GetPendingBodyWrite("new-task")!.ExternalId);
        Assert.Contains("\"body\": null", File.ReadAllText(path));
    }

    [Fact]
    public void IntentJsonRoundTrip_PreservesEveryOperationKind_AndCreateWithoutExternalId()
    {
        var path = Path.Combine(_directory, "snapshot.json");
        var store = new BaseSnapshotStore(path);
        foreach (var (localId, kind, externalId) in new[]
        {
            ("create", BodyWriteOperationKind.Create, (string?)null),
            ("update", BodyWriteOperationKind.Update, "page-update"),
            ("resolution", BodyWriteOperationKind.Resolution, "page-resolution"),
        })
        {
            store.SetDualBodyBase(Doc(localId, externalId), new DualBodyBase("prior-local", "prior-external"));
            store.WritePendingBodyWrite(Intent(kind, externalId, localId));
        }
        store.Save();

        var reloaded = new BaseSnapshotStore(path);
        Assert.Null(reloaded.GetPendingBodyWrite("create")!.ExternalId);
        Assert.Equal(BodyWriteOperationKind.Update, reloaded.GetPendingBodyWrite("update")!.Kind);
        Assert.Equal(BodyWriteOperationKind.Resolution, reloaded.GetPendingBodyWrite("resolution")!.Kind);
        Assert.Contains("\"intendedLocalBody\"", File.ReadAllText(path));
    }

    [Fact]
    public void ReceiptCommit_ClearsIntentOnlyInTheAtomicallySavedSnapshot()
    {
        var path = Path.Combine(_directory, "snapshot.json");
        var store = new BaseSnapshotStore(path);
        store.SetDualBodyBase(Doc("task", "page"), new DualBodyBase("before", "echo-before"));
        store.WritePendingBodyWrite(Intent(BodyWriteOperationKind.Update, "page"));
        store.Save();

        store.SetDualBodyBase(Doc("task", "page"), new DualBodyBase("after", "observed-after"));
        store.RemovePendingBodyWrite("task");

        Assert.NotNull(new BaseSnapshotStore(path).GetPendingBodyWrite("task"));
        store.Save();

        var reloaded = new BaseSnapshotStore(path);
        Assert.Null(reloaded.GetPendingBodyWrite("task"));
        Assert.Equal(new DualBodyBase("after", "observed-after"), reloaded.GetDualBodyBase("task"));
    }

    [Fact]
    public void ResolutionCleanupReceipt_RoundTripsAndLegacySnapshotsRemainCompatible()
    {
        var path = Path.Combine(_directory, "snapshot.json");
        var store = new BaseSnapshotStore(path);
        store.SetDualBodyBase(Doc("task", "page"), new DualBodyBase("body", "echo"));
        store.SetResolutionCleanupReceipt(new ResolutionCleanupReceipt
        {
            LocalId = "task", OperationId = "receipt-operation", ResolvedBody = "body",
        });
        store.Save();

        var reloaded = new BaseSnapshotStore(path);
        var cleanup = Assert.IsType<ResolutionCleanupReceipt>(reloaded.GetResolutionCleanupReceipt("task"));
        Assert.Equal("receipt-operation", cleanup.OperationId);
        Assert.Equal("body", cleanup.ResolvedBody);
        Assert.Contains("resolutionCleanupReceipt", File.ReadAllText(path));
    }

    [Fact]
    public void IdentityAdapterApplyWithReceipts_ReportsOnlyObservedBodyUpserts()
    {
        ISyncAdapter adapter = new FakeSyncAdapter();
        var changes = new SyncChangeSet();
        changes.Upserts.Add(new SyncUpsert
        {
            LocalId = "body",
            Fields = [],
            Body = "exact body",
            OperationId = Guid.NewGuid().ToString(),
        });
        changes.Upserts.Add(new SyncUpsert
        {
            LocalId = "properties-only",
            ExternalId = "page-properties",
            Fields = [],
            Body = "must not become a receipt",
            WriteBody = false,
        });
        var assigned = new Dictionary<string, string>();
        var result = adapter.ApplyWithReceipts(changes, assigned, new HashSet<string>(), new HashSet<string>());

        var receipt = Assert.Single(result.BodyWriteReceipts);
        Assert.Equal("exact body", receipt.ObservedExternalBody);
        Assert.Equal(assigned["body"], receipt.ExternalId);
    }

    [Fact]
    public void NonIdentityAdapterApplyWithReceipts_DoesNotFabricateAReceipt()
    {
        ISyncAdapter adapter = new FakeSyncAdapter { HasIdentityBodyProjection = false };
        var changes = new SyncChangeSet();
        changes.Upserts.Add(new SyncUpsert
        {
            LocalId = "projected",
            Fields = [],
            Body = "submitted but unobserved",
            OperationId = Guid.NewGuid().ToString(),
        });

        var result = adapter.ApplyWithReceipts(changes, new Dictionary<string, string>(), new HashSet<string>(), new HashSet<string>());

        Assert.Empty(result.BodyWriteReceipts);
    }

    [Fact]
    public void RecordBodyReadStatus_DefaultsComplete_AndKeepsTruncatedExplicit()
    {
        var complete = new SyncRecord { ExternalId = "one", Fields = [], Body = "body" };
        var truncated = new SyncRecord
        {
            ExternalId = "two",
            Fields = [],
            Body = "partial diagnostic",
            BodyReadStatus = SyncBodyReadStatus.Truncated,
        };

        Assert.Equal(SyncBodyReadStatus.Complete, complete.BodyReadStatus);
        Assert.Equal(SyncBodyReadStatus.Truncated, truncated.BodyReadStatus);
        Assert.Equal("partial diagnostic", truncated.Body);
    }

    [Fact]
    public void ApplyResult_SourceGeneratedJson_UsesStableReceiptPropertyName()
    {
        var json = JsonSerializer.Serialize(new SyncApplyResult(), SyncSnapshotJsonContext.Default.SyncApplyResult);

        Assert.Contains("\"bodyWriteReceipts\"", json);
    }

    private static BodyWriteIntent Intent(BodyWriteOperationKind kind, string? externalId, string localId = "task") => new()
    {
        OperationId = Guid.NewGuid().ToString(),
        Kind = kind,
        LocalId = localId,
        ExternalId = externalId,
        PriorLocalBody = "before",
        PriorExternalBody = "echo-before",
        IntendedLocalBody = "after",
    };

    private static SyncDoc Doc(string localId, string? externalId) => new()
    {
        LocalId = localId,
        ExternalId = externalId,
        Fields = new List<SyncField>(),
        Body = "",
        SourcePath = "",
    };
}
