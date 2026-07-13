// @test-tier: 2
namespace DynaDocs.Tests.Utils;

using DynaDocs.Utils;

public class FileReadRetryTests : IDisposable
{
    private readonly string _testDir;

    public FileReadRetryTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-readretry-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public void Read_ExistingFile_ReturnsContent()
    {
        var path = Path.Combine(_testDir, "hello.txt");
        File.WriteAllText(path, "hello world");

        Assert.Equal("hello world", FileReadRetry.Read(path));
    }

    [Fact]
    public void Read_NonexistentFile_ReturnsNull()
    {
        var path = Path.Combine(_testDir, "nope.txt");

        Assert.Null(FileReadRetry.Read(path));
    }

    [Fact]
    public void Read_EmptyFile_ReturnsEmptyString()
    {
        var path = Path.Combine(_testDir, "empty.txt");
        File.WriteAllText(path, "");

        Assert.Equal("", FileReadRetry.Read(path));
    }

    [Fact]
    public void Read_FileWithConcurrentWriter_SucceedsViaSharedAccess()
    {
        var path = Path.Combine(_testDir, "shared.txt");
        File.WriteAllText(path, "initial");

        // Hold the file open for writing with ReadWrite sharing
        using var writer = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);

        // Should still be able to read via FileShare.ReadWrite
        var content = FileReadRetry.Read(path);
        Assert.Equal("initial", content);
    }

    [Fact]
    public void Read_RespectsMaxRetries()
    {
        var path = Path.Combine(_testDir, "nope.txt");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = FileReadRetry.Read(path, maxRetries: 1);
        sw.Stop();

        Assert.Null(result);
        // With maxRetries=1, no sleep should occur (single attempt, no retry)
        Assert.True(sw.ElapsedMilliseconds < 200, $"Expected < 200ms, got {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void Read_MultilineContent_PreservedExactly()
    {
        var path = Path.Combine(_testDir, "multi.txt");
        const string content = "---\narea: backend\ntype: issue\n---\n\n# Title";
        File.WriteAllText(path, content);

        Assert.Equal(content, FileReadRetry.Read(path));
    }

    [Fact]
    public void Read_ExclusivelyLockedFile_RetriesAndSucceeds()
    {
        // FileShare.None mandatory locking is Windows-only; Linux flock() is advisory
        // and cross-thread release timing is unreliable on CI runners
        if (!OperatingSystem.IsWindows()) return;

        var path = Path.Combine(_testDir, "locked.txt");
        File.WriteAllText(path, "locked-content");

        // Hold file exclusively — causes IOException on first attempt
        var locker = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var delays = new List<int>();
        FileReadRetry.DelayOverride = delay =>
        {
            delays.Add(delay);
            locker.Dispose();
        };

        string? result;
        try
        {
            // The first retry releases the lock, so the second read succeeds.
            result = FileReadRetry.Read(path);
        }
        finally
        {
            FileReadRetry.DelayOverride = null;
        }

        Assert.Equal("locked-content", result);
        Assert.Equal([50], delays);
    }

    [Fact]
    public void Read_DefaultBackoffUsesRealSchedule()
    {
        var path = Path.Combine(_testDir, "nope.txt");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = FileReadRetry.Read(path);
        stopwatch.Stop();

        Assert.Null(result);
        Assert.True(stopwatch.ElapsedMilliseconds >= 180,
            $"Expected the default 50ms and 150ms backoff, got {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void Read_PermanentlyLockedFile_ReturnsNull()
    {
        var path = Path.Combine(_testDir, "perm-locked.txt");
        File.WriteAllText(path, "content");

        // Hold file exclusively for the duration
        using var locker = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var delays = new List<int>();
        FileReadRetry.DelayOverride = delays.Add;

        string? result;
        try
        {
            // Should exhaust retries and return null.
            result = FileReadRetry.Read(path, maxRetries: 2);
        }
        finally
        {
            FileReadRetry.DelayOverride = null;
        }

        Assert.Null(result);
        Assert.Equal([50], delays);
    }
}
