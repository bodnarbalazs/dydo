// @test-tier: 2
namespace DynaDocs.Tests.Sync;

using DynaDocs.Models;
using DynaDocs.Sync;
using DynaDocs.Sync.Projection;

public sealed class DualProjectionSnapshotTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "dydo-dual-snapshot-" + Guid.NewGuid().ToString("N"));

    public DualProjectionSnapshotTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    [Fact]
    public void LegacyV1Json_LoadsWithoutRewriteUntilMutation()
    {
        var path = Path.Combine(_directory, "snapshot.json");
        var json = """{ "objects": [{ "localId": "old", "externalId": "page", "fields": [], "body": "legacy" }] }""";
        File.WriteAllText(path, json);

        var store = new BaseSnapshotStore(path);

        Assert.False(store.IsV2("old"));
        Assert.Null(store.GetDualBodyBase("old"));
        Assert.Null(store.GetResolutionCleanupReceipt("old"));
        Assert.Equal("legacy", store.Get("old")!.Body);
        store.Save();
        Assert.Equal(json, File.ReadAllText(path));
    }

    [Fact]
    public void MixedV1AndV2Json_SeparatesObjectLevelBodyBases()
    {
        var path = Path.Combine(_directory, "snapshot.json");
        File.WriteAllText(path, """
        {
          "objects": [
            { "localId": "legacy", "fields": [], "body": "one" },
            { "localId": "modern", "fields": [], "bodyVersion": 2, "localBody": "local", "externalBody": "external" }
          ]
        }
        """);

        var store = new BaseSnapshotStore(path);

        Assert.False(store.IsV2("legacy"));
        Assert.True(store.IsV2("modern"));
        Assert.Equal(new DualBodyBase("local", "external"), store.GetDualBodyBase("modern"));
    }

    [Fact]
    public void SetDualBodyBase_SourceGeneratedRoundTrip_PreservesDistinctBodies()
    {
        var path = Path.Combine(_directory, "snapshot.json");
        var store = new BaseSnapshotStore(path);
        store.SetDualBodyBase(Doc("modern", "page", "local"), new DualBodyBase("local\nbytes", "external\nbytes"));
        store.Save();

        var reloaded = new BaseSnapshotStore(path);
        var bodyBase = Assert.IsType<DualBodyBase>(reloaded.GetDualBodyBase("modern"));
        Assert.Equal("local\nbytes", bodyBase.LocalBody);
        Assert.Equal("external\nbytes", bodyBase.ExternalBody);
        Assert.Contains("\"externalBody\": \"external\\nbytes\"", File.ReadAllText(path));
    }

    [Fact]
    public void PartialV2Json_DoesNotInventAnExternalBase()
    {
        var path = Path.Combine(_directory, "snapshot.json");
        File.WriteAllText(path, """{ "objects": [{ "localId": "partial", "fields": [], "bodyVersion": 2, "localBody": "local" }] }""");

        var store = new BaseSnapshotStore(path);

        Assert.True(store.IsV2("partial"));
        Assert.Null(store.GetDualBodyBase("partial"));
        Assert.Null(store.GetPendingBodyWrite("partial"));
    }

    [Fact]
    public void MissingStateGettersAndPendingRemoval_AreNoOps()
    {
        var store = new BaseSnapshotStore(Path.Combine(_directory, "snapshot.json"));

        Assert.Null(store.GetDualBodyBase("missing"));
        Assert.Null(store.GetPendingBodyWrite("missing"));
        store.RemovePendingBodyWrite("missing");
        store.Save();

        Assert.False(File.Exists(Path.Combine(_directory, "snapshot.json")));
    }

    [Fact]
    public void SaveFailure_RemovesItsTemporaryFile()
    {
        var path = Path.Combine(_directory, "snapshot.json");
        var store = new BaseSnapshotStore(path);
        store.Set(Doc("task", "page", "body"));
        Directory.CreateDirectory(path);

        var error = Record.Exception(() => store.Save());
        Assert.True(error is IOException or UnauthorizedAccessException);
        Assert.Empty(Directory.GetFiles(_directory, "snapshot.json.tmp*"));
    }

    private static SyncDoc Doc(string localId, string? externalId, string body) => new()
    {
        LocalId = localId,
        ExternalId = externalId,
        Fields = [],
        Body = body,
        SourcePath = "",
    };
}
