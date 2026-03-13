namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

public class MarkerStoreTests : IDisposable
{
    private readonly string _testDir;
    private readonly MarkerStore _store;

    public MarkerStoreTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-marker-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _store = new MarkerStore(agent => Path.Combine(_testDir, agent));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    #region Wait Markers

    [Fact]
    public void CreateWaitMarker_CreatesFileOnDisk()
    {
        _store.CreateWaitMarker("Alice", "my-task", "Bob");

        var markers = _store.GetWaitMarkers("Alice");
        Assert.Single(markers);
        Assert.Equal("Bob", markers[0].Target);
        Assert.Equal("my-task", markers[0].Task);
    }

    [Fact]
    public void GetWaitMarkers_NoDir_ReturnsEmpty()
    {
        var markers = _store.GetWaitMarkers("NonExistent");
        Assert.Empty(markers);
    }

    [Fact]
    public void GetWaitMarkers_InvalidJson_SkipsCorruptFile()
    {
        var waitDir = Path.Combine(_testDir, "Alice", ".waiting");
        Directory.CreateDirectory(waitDir);
        File.WriteAllText(Path.Combine(waitDir, "bad.json"), "not json");

        var markers = _store.GetWaitMarkers("Alice");
        Assert.Empty(markers);
    }

    [Fact]
    public void RemoveWaitMarker_ExistingMarker_ReturnsTrue()
    {
        _store.CreateWaitMarker("Alice", "my-task", "Bob");

        var removed = _store.RemoveWaitMarker("Alice", "my-task");

        Assert.True(removed);
        Assert.Empty(_store.GetWaitMarkers("Alice"));
    }

    [Fact]
    public void RemoveWaitMarker_NoDir_ReturnsFalse()
    {
        Assert.False(_store.RemoveWaitMarker("NonExistent", "task"));
    }

    [Fact]
    public void RemoveWaitMarker_NoFile_ReturnsFalse()
    {
        var waitDir = Path.Combine(_testDir, "Alice", ".waiting");
        Directory.CreateDirectory(waitDir);

        Assert.False(_store.RemoveWaitMarker("Alice", "nonexistent-task"));
    }

    [Fact]
    public void ClearAllWaitMarkers_RemovesDirectory()
    {
        _store.CreateWaitMarker("Alice", "task1", "Bob");
        _store.CreateWaitMarker("Alice", "task2", "Carol");

        _store.ClearAllWaitMarkers("Alice");

        Assert.Empty(_store.GetWaitMarkers("Alice"));
    }

    [Fact]
    public void ClearAllWaitMarkers_NoDirNoError()
    {
        _store.ClearAllWaitMarkers("NonExistent"); // should not throw
    }

    [Fact]
    public void UpdateWaitMarkerListening_SetsListeningAndPid()
    {
        _store.CreateWaitMarker("Alice", "my-task", "Bob");

        var result = _store.UpdateWaitMarkerListening("Alice", "my-task", 12345);

        Assert.True(result);
        var markers = _store.GetWaitMarkers("Alice");
        Assert.True(markers[0].Listening);
        Assert.Equal(12345, markers[0].Pid);
    }

    [Fact]
    public void UpdateWaitMarkerListening_NoFile_ReturnsFalse()
    {
        Assert.False(_store.UpdateWaitMarkerListening("Alice", "no-task", 1));
    }

    [Fact]
    public void UpdateWaitMarkerListening_CorruptFile_ReturnsFalse()
    {
        var waitDir = Path.Combine(_testDir, "Alice", ".waiting");
        Directory.CreateDirectory(waitDir);
        File.WriteAllText(Path.Combine(waitDir, "bad.json"), "corrupt");

        Assert.False(_store.UpdateWaitMarkerListening("Alice", "bad", 1));
    }

    [Fact]
    public void ResetWaitMarkerListening_ClearsListeningAndPid()
    {
        _store.CreateWaitMarker("Alice", "my-task", "Bob");
        _store.UpdateWaitMarkerListening("Alice", "my-task", 999);

        _store.ResetWaitMarkerListening("Alice", "my-task");

        var markers = _store.GetWaitMarkers("Alice");
        Assert.False(markers[0].Listening);
        Assert.Null(markers[0].Pid);
    }

    [Fact]
    public void ResetWaitMarkerListening_NoFile_DoesNotThrow()
    {
        _store.ResetWaitMarkerListening("NonExistent", "no-task"); // should not throw
    }

    [Fact]
    public void GetNonListeningWaitMarkers_FiltersCorrectly()
    {
        _store.CreateWaitMarker("Alice", "task1", "Bob");
        _store.CreateWaitMarker("Alice", "task2", "Carol");
        _store.UpdateWaitMarkerListening("Alice", "task1", 123);

        var nonListening = _store.GetNonListeningWaitMarkers("Alice");

        Assert.Single(nonListening);
        Assert.Equal("task2", nonListening[0].Task);
    }

    #endregion

    #region Reply-Pending Markers

    [Fact]
    public void CreateReplyPendingMarker_CreatesFileOnDisk()
    {
        _store.CreateReplyPendingMarker("Alice", "my-task", "Bob");

        var markers = _store.GetReplyPendingMarkers("Alice");
        Assert.Single(markers);
        Assert.Equal("Bob", markers[0].To);
        Assert.Equal("my-task", markers[0].Task);
    }

    [Fact]
    public void GetReplyPendingMarkers_NoDir_ReturnsEmpty()
    {
        Assert.Empty(_store.GetReplyPendingMarkers("NonExistent"));
    }

    [Fact]
    public void GetReplyPendingMarkers_CorruptFile_SkipsIt()
    {
        var dir = Path.Combine(_testDir, "Alice", ".reply-pending");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "bad.json"), "not json");

        Assert.Empty(_store.GetReplyPendingMarkers("Alice"));
    }

    [Fact]
    public void RemoveReplyPendingMarker_ExistingMarker_ReturnsTrue()
    {
        _store.CreateReplyPendingMarker("Alice", "my-task", "Bob");

        Assert.True(_store.RemoveReplyPendingMarker("Alice", "my-task"));
        Assert.Empty(_store.GetReplyPendingMarkers("Alice"));
    }

    [Fact]
    public void RemoveReplyPendingMarker_NoDir_ReturnsFalse()
    {
        Assert.False(_store.RemoveReplyPendingMarker("NonExistent", "task"));
    }

    [Fact]
    public void RemoveReplyPendingMarker_NoFile_ReturnsFalse()
    {
        var dir = Path.Combine(_testDir, "Alice", ".reply-pending");
        Directory.CreateDirectory(dir);

        Assert.False(_store.RemoveReplyPendingMarker("Alice", "nonexistent"));
    }

    [Fact]
    public void ClearAllReplyPendingMarkers_RemovesDirectory()
    {
        _store.CreateReplyPendingMarker("Alice", "task1", "Bob");

        _store.ClearAllReplyPendingMarkers("Alice");

        Assert.Empty(_store.GetReplyPendingMarkers("Alice"));
    }

    [Fact]
    public void ClearAllReplyPendingMarkers_NoDirNoError()
    {
        _store.ClearAllReplyPendingMarkers("NonExistent"); // should not throw
    }

    #endregion
}
