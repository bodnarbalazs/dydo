// @test-tier: 2
namespace DynaDocs.Tests.Sync;

using DynaDocs.Models;
using DynaDocs.Sync;

public class BaseSnapshotStoreTests : IDisposable
{
    private readonly string _dir;

    public BaseSnapshotStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "dydo-snap-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private static SyncDoc Doc(string localId, string? externalId, string body, params (string, string)[] fields) => new()
    {
        LocalId = localId,
        ExternalId = externalId,
        Fields = fields.Select(f => new SyncField { Key = f.Item1, Value = f.Item2 }).ToList(),
        Body = body,
        SourcePath = $"tasks/{localId}.md",
    };

    [Fact]
    public void PathFor_LandsUnderGitignoredLocalTree()
    {
        var path = BaseSnapshotStore.PathFor("/proj/dydo", "notion").Replace('\\', '/');
        Assert.Contains("dydo/_system/.local/sync/notion/snapshot.json", path);
    }

    [Fact]
    public void Get_Missing_ReturnsNull()
    {
        var store = new BaseSnapshotStore(Path.Combine(_dir, "snapshot.json"));
        Assert.Null(store.Get("nope"));
    }

    [Fact]
    public void SetSaveLoad_RoundTrips()
    {
        var path = Path.Combine(_dir, "snapshot.json");
        var store = new BaseSnapshotStore(path);
        store.Set(Doc("t1", "ext-1", "# T1\n\nbody", ("status", "open"), ("priority", "low")));
        store.Save();

        var reloaded = new BaseSnapshotStore(path);
        var got = reloaded.Get("t1");

        Assert.NotNull(got);
        Assert.Equal("ext-1", got!.ExternalId);
        Assert.Equal("# T1\n\nbody", got.Body);
        Assert.Equal(["status", "priority"], got.Fields.Select(f => f.Key));
        Assert.Equal("open", got.GetField("status"));
    }

    [Fact]
    public void Remove_DropsObject()
    {
        var path = Path.Combine(_dir, "snapshot.json");
        var store = new BaseSnapshotStore(path);
        store.Set(Doc("t1", "ext-1", "body"));
        store.Remove("t1");
        store.Save();

        var reloaded = new BaseSnapshotStore(path);
        Assert.Null(reloaded.Get("t1"));
        Assert.Empty(reloaded.LocalIds);
    }

    [Fact]
    public void Save_CreatesNestedDirectories()
    {
        var path = Path.Combine(_dir, "a", "b", "snapshot.json");
        var store = new BaseSnapshotStore(path);
        store.Set(Doc("t", null, "body"));
        store.Save();
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void LocalIds_ListsAllObjects()
    {
        var store = new BaseSnapshotStore(Path.Combine(_dir, "snapshot.json"));
        store.Set(Doc("a", null, "x"));
        store.Set(Doc("b", null, "y"));
        Assert.Equal(["a", "b"], store.LocalIds.OrderBy(x => x));
    }
}
