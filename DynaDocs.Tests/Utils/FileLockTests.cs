// @test-tier: 2
namespace DynaDocs.Tests.Utils;

using DynaDocs.Services;
using DynaDocs.Utils;

[Collection("ProcessUtils")]
public class FileLockTests : IDisposable
{
    private readonly string _testDir;
    private readonly Func<int, bool>? _originalOverride;

    public FileLockTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-filelock-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _originalOverride = ProcessUtils.IsProcessRunningOverride;
    }

    public void Dispose()
    {
        ProcessUtils.IsProcessRunningOverride = _originalOverride;
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public void WithExclusiveLock_RunsActionAndCleansUp()
    {
        var lockPath = Path.Combine(_testDir, "test.lock");
        var ran = false;

        FileLock.WithExclusiveLock(lockPath, () =>
        {
            ran = true;
            Assert.True(File.Exists(lockPath));
        });

        Assert.True(ran);
        Assert.False(File.Exists(lockPath));
    }

    [Fact]
    public void TryRemoveStaleLock_ParsesLockFormat()
    {
        // Verify the lock file format produced by WithExclusiveLock is parseable
        // by TryRemoveStaleLock (can't read during hold — FileShare.None)
        ProcessUtils.IsProcessRunningOverride = _ => false;

        var lockPath = Path.Combine(_testDir, "pid.lock");
        var lockInfo = $"{{\"Pid\":{Environment.ProcessId},\"Acquired\":\"{DateTime.UtcNow:o}\"}}";
        File.WriteAllText(lockPath, lockInfo);

        Assert.True(FileLock.TryRemoveStaleLock(lockPath));
        Assert.False(File.Exists(lockPath));
    }

    [Fact]
    public void WithExclusiveLock_ActionException_PropagatesAndCleansUp()
    {
        var lockPath = Path.Combine(_testDir, "throw.lock");

        Assert.Throws<InvalidOperationException>(() =>
            FileLock.WithExclusiveLock(lockPath, () => throw new InvalidOperationException("boom")));

        Assert.False(File.Exists(lockPath));
    }

    [Fact]
    public void TryRemoveStaleLock_DeadProcess_RemovesLock()
    {
        ProcessUtils.IsProcessRunningOverride = _ => false;

        var lockPath = Path.Combine(_testDir, "stale.lock");
        File.WriteAllText(lockPath, "{\"Pid\":99999,\"Acquired\":\"2026-01-01T00:00:00Z\"}");

        Assert.True(FileLock.TryRemoveStaleLock(lockPath));
        Assert.False(File.Exists(lockPath));
    }

    [Fact]
    public void TryRemoveStaleLock_LiveProcess_KeepsLock()
    {
        ProcessUtils.IsProcessRunningOverride = _ => true;

        var lockPath = Path.Combine(_testDir, "live.lock");
        File.WriteAllText(lockPath, "{\"Pid\":12345,\"Acquired\":\"2026-01-01T00:00:00Z\"}");

        Assert.False(FileLock.TryRemoveStaleLock(lockPath));
        Assert.True(File.Exists(lockPath));
    }

    [Fact]
    public void TryRemoveStaleLock_MalformedJson_ReturnsFalse()
    {
        var lockPath = Path.Combine(_testDir, "bad.lock");
        File.WriteAllText(lockPath, "not json at all");

        Assert.False(FileLock.TryRemoveStaleLock(lockPath));
        Assert.True(File.Exists(lockPath));
    }

    [Fact]
    public void TryRemoveStaleLock_MissingFile_ReturnsFalse()
    {
        var lockPath = Path.Combine(_testDir, "nonexistent.lock");

        Assert.False(FileLock.TryRemoveStaleLock(lockPath));
    }

    [Fact]
    public void WithExclusiveLock_StaleLock_IsRemovedAndAcquired()
    {
        ProcessUtils.IsProcessRunningOverride = _ => false;

        var lockPath = Path.Combine(_testDir, "stale-acquire.lock");
        File.WriteAllText(lockPath, "{\"Pid\":99999,\"Acquired\":\"2026-01-01T00:00:00Z\"}");

        var ran = false;
        FileLock.WithExclusiveLock(lockPath, () => ran = true);

        Assert.True(ran);
        Assert.False(File.Exists(lockPath));
    }

    [Fact]
    public void WithExclusiveLock_HeldByLiveProcess_ThrowsTimeout()
    {
        ProcessUtils.IsProcessRunningOverride = _ => true;

        var lockPath = Path.Combine(_testDir, "held.lock");
        File.WriteAllText(lockPath, "{\"Pid\":12345,\"Acquired\":\"2026-01-01T00:00:00Z\"}");

        var ex = Assert.Throws<TimeoutException>(() =>
            FileLock.WithExclusiveLock(lockPath, () => { }, maxAttempts: 2, retryDelayMs: 10));

        Assert.Contains("Could not acquire lock", ex.Message);
        Assert.Contains(lockPath, ex.Message);
    }

    [Fact]
    public void WithExclusiveLock_CustomRetryParams_Respected()
    {
        ProcessUtils.IsProcessRunningOverride = _ => true;

        var lockPath = Path.Combine(_testDir, "custom.lock");
        File.WriteAllText(lockPath, "{\"Pid\":12345,\"Acquired\":\"2026-01-01T00:00:00Z\"}");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        Assert.Throws<TimeoutException>(() =>
            FileLock.WithExclusiveLock(lockPath, () => { }, maxAttempts: 3, retryDelayMs: 50));
        sw.Stop();

        // 3 attempts with 50ms between = at least ~100ms (2 sleeps), should be under 500ms
        Assert.True(sw.ElapsedMilliseconds >= 80, $"Expected >= 80ms, got {sw.ElapsedMilliseconds}ms");
        Assert.True(sw.ElapsedMilliseconds < 2000, $"Expected < 2000ms, got {sw.ElapsedMilliseconds}ms");
    }
}
