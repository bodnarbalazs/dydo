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
    public void LastActivity_SetSaveLoad_RoundTrips_AndIsClearedOnRemove()
    {
        var path = Path.Combine(_dir, "snapshot.json");
        var store = new BaseSnapshotStore(path);
        store.Set(Doc("t1", "ext-1", "body"));
        store.SetLastActivity("t1", "2026-06-20");
        store.Save();

        var reloaded = new BaseSnapshotStore(path);
        Assert.Equal("2026-06-20", reloaded.GetLastActivity("t1"));
        Assert.Null(reloaded.GetLastActivity("nope"));

        reloaded.Remove("t1");
        reloaded.Save();
        Assert.Null(new BaseSnapshotStore(path).GetLastActivity("t1"));
    }

    [Fact]
    public void LastActivity_MissingFromOlderFile_LoadsAsEmpty()
    {
        // A pre-DR-030 snapshot file has no LastActivity map; it must still load, with last-activity absent.
        var path = Path.Combine(_dir, "snapshot.json");
        File.WriteAllText(path, """{ "objects": [ { "localId": "t1", "fields": [], "body": "" } ] }""");

        var store = new BaseSnapshotStore(path);
        Assert.NotNull(store.Get("t1"));
        Assert.Null(store.GetLastActivity("t1"));
    }

    [Fact]
    public void PruneOrphanLastActivity_DropsEntriesWithNoBaseObject_KeepsBackedOnes()
    {
        // Finding 7: a last-activity with no surviving base object is an orphan (a crashed create's seed) that
        // Retire can never reach; the sweep drops exactly those, leaving a base-backed entry intact.
        var path = Path.Combine(_dir, "snapshot.json");
        var store = new BaseSnapshotStore(path);
        store.Set(Doc("live", "ext-1", "body"));
        store.SetLastActivity("live", "2026-06-20");
        store.SetLastActivity("orphan", "2026-01-01"); // no base object

        store.PruneOrphanLastActivity();

        Assert.Equal("2026-06-20", store.GetLastActivity("live")); // base-backed entry kept
        Assert.Null(store.GetLastActivity("orphan"));              // orphan swept
    }

    [Fact]
    public void LocalIds_ListsAllObjects()
    {
        var store = new BaseSnapshotStore(Path.Combine(_dir, "snapshot.json"));
        store.Set(Doc("a", null, "x"));
        store.Set(Doc("b", null, "y"));
        Assert.Equal(["a", "b"], store.LocalIds.OrderBy(x => x));
    }

    [Fact]
    public void DeleteSnapshot_ExistingFile_Removes_MissingFile_NoOp()
    {
        var path = Path.Combine(_dir, "snapshot.json");
        var store = new BaseSnapshotStore(path);
        store.Set(Doc("t", null, "body"));
        store.Save();
        Assert.True(File.Exists(path));

        BaseSnapshotStore.DeleteSnapshot(path);
        Assert.False(File.Exists(path));

        BaseSnapshotStore.DeleteSnapshot(path); // already gone -> no throw
    }

    [Fact]
    public void DeleteSnapshot_LockedFile_ThrowsIOException_WithClearMessage()
    {
        // Finding 2: the reset runs BEFORE the re-provision mint, so a delete failure (a share-lock from AV /
        // OneDrive / another process, a read-only attribute) must SURFACE and abort before any database exists —
        // never silently proceed and leave a stale snapshot to mass-delete the repo on the next run.
        var path = Path.Combine(_dir, "snapshot.json");
        var store = new BaseSnapshotStore(path);
        store.Set(Doc("t", null, "body"));
        store.Save();

        // Hold an exclusive handle so File.Delete cannot remove it.
        using var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
        var ex = Assert.Throws<IOException>(() => BaseSnapshotStore.DeleteSnapshot(path));
        Assert.Contains("failed to reset base snapshot", ex.Message);
        Assert.Contains(path, ex.Message);
    }
}
