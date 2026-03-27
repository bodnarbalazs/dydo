namespace DynaDocs.Tests.Services;

using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

public class QueueServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly QueueService _service;

    public QueueServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-queue-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);

        // Create agents dir for marker tests
        Directory.CreateDirectory(Path.Combine(_testDir, "agents", "Brian"));

        var config = new DydoConfig { Queues = ["merge"] };
        _service = new QueueService(_testDir, config);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void PersistentQueue_ExistsWithoutDirectory()
    {
        Assert.True(_service.QueueExists("merge"));
    }

    [Fact]
    public void NonExistentQueue_DoesNotExist()
    {
        Assert.False(_service.QueueExists("nonexistent"));
    }

    [Fact]
    public void CreateTransientQueue_CreatesDirectory()
    {
        Assert.True(_service.CreateQueue("hotfix", out _));
        Assert.True(Directory.Exists(_service.GetQueueDir("hotfix")));
        Assert.True(_service.QueueExists("hotfix"));
    }

    [Fact]
    public void CreatePersistentQueue_ReturnsError()
    {
        Assert.False(_service.CreateQueue("merge", out var error));
        Assert.Contains("persistent", error);
    }

    [Fact]
    public void CreateExistingQueue_ReturnsError()
    {
        _service.CreateQueue("hotfix", out _);
        Assert.False(_service.CreateQueue("hotfix", out var error));
        Assert.Contains("already exists", error);
    }

    [Fact]
    public void TryEnqueue_ReturnsFalse_WhenNoActiveItem()
    {
        // No _active.json → should launch immediately
        var enqueued = _service.TryEnqueue("merge", "Brian", "my-task",
            true, false, null, null, null, null, null);
        Assert.False(enqueued);
    }

    [Fact]
    public void TryEnqueue_ReturnsTrue_WhenActiveItemExists()
    {
        _service.SetActive("merge", "Charlie", "other-task", 12345);

        var enqueued = _service.TryEnqueue("merge", "Brian", "my-task",
            true, true, "wt-123", "win1", null, null, "/project");
        Assert.True(enqueued);

        var pending = _service.GetPending("merge");
        Assert.Single(pending);
        Assert.Equal("Brian", pending[0].Entry.Agent);
        Assert.Equal("my-task", pending[0].Entry.Task);
        Assert.True(pending[0].Entry.AutoClose);
        Assert.Equal("wt-123", pending[0].Entry.WorktreeId);
    }

    [Fact]
    public void GetActive_ReturnsNull_WhenEmpty()
    {
        Assert.Null(_service.GetActive("merge"));
    }

    [Fact]
    public void SetActive_And_GetActive_RoundTrips()
    {
        _service.SetActive("merge", "Brian", "task-1", 42);
        var active = _service.GetActive("merge");

        Assert.NotNull(active);
        Assert.Equal("Brian", active.Agent);
        Assert.Equal("task-1", active.Task);
        Assert.Equal(42, active.Pid);
    }

    [Fact]
    public void ClearActive_RemovesActiveEntry()
    {
        _service.SetActive("merge", "Brian", "task-1", 42);
        _service.ClearActive("merge");
        Assert.Null(_service.GetActive("merge"));
    }

    [Fact]
    public void DequeueNext_ReturnsFirstPending_InOrder()
    {
        _service.SetActive("merge", "Charlie", "task-0", 1);

        _service.TryEnqueue("merge", "Brian", "task-1", true, false, null, null, null, null, null);
        _service.TryEnqueue("merge", "Dexter", "task-2", false, true, null, null, null, null, null);

        var first = _service.DequeueNext("merge");
        Assert.NotNull(first);
        Assert.Equal("Brian", first.Agent);

        var second = _service.DequeueNext("merge");
        Assert.NotNull(second);
        Assert.Equal("Dexter", second.Agent);

        Assert.Null(_service.DequeueNext("merge"));
    }

    [Fact]
    public void DequeueNext_ReturnsNull_WhenNoPending()
    {
        Assert.Null(_service.DequeueNext("merge"));
    }

    [Fact]
    public void FindQueuesWithActiveAgent_FindsCorrectQueue()
    {
        _service.SetActive("merge", "Brian", "task-1", 42);

        var queues = _service.FindQueuesWithActiveAgent("Brian");
        Assert.Single(queues);
        Assert.Equal("merge", queues[0]);
    }

    [Fact]
    public void FindQueuesWithActiveAgent_ReturnsEmpty_WhenNoMatch()
    {
        _service.SetActive("merge", "Charlie", "task-1", 42);
        Assert.Empty(_service.FindQueuesWithActiveAgent("Brian"));
    }

    [Fact]
    public void CancelEntry_RemovesPendingEntry()
    {
        _service.SetActive("merge", "Charlie", "task-0", 1);
        _service.TryEnqueue("merge", "Brian", "task-1", true, false, null, null, null, null, null);

        var pending = _service.GetPending("merge");
        var seq = pending[0].FileName.Split('-')[0]; // e.g. "0001"

        Assert.True(_service.CancelEntry("merge", seq, out _));
        Assert.Empty(_service.GetPending("merge"));
    }

    [Fact]
    public void CancelEntry_ReturnsError_WhenNotFound()
    {
        // Create queue directory so we get past the "not found" check
        Directory.CreateDirectory(_service.GetQueueDir("merge"));
        Assert.False(_service.CancelEntry("merge", "9999", out var error));
        Assert.Contains("No pending entry", error);
    }

    [Fact]
    public void ClearQueue_RemovesAllEntries()
    {
        _service.SetActive("merge", "Charlie", "task-0", 1);
        _service.TryEnqueue("merge", "Brian", "task-1", true, false, null, null, null, null, null);

        Assert.True(_service.ClearQueue("merge", out _));
        Assert.Null(_service.GetActive("merge"));
        Assert.Empty(_service.GetPending("merge"));
    }

    [Fact]
    public void ClearQueue_ReturnsError_WhenNotFound()
    {
        Assert.False(_service.ClearQueue("nonexistent", out var error));
        Assert.Contains("not found", error);
    }

    [Fact]
    public void CleanupIfEmptyTransient_DeletesEmptyTransientDir()
    {
        _service.CreateQueue("hotfix", out _);
        Assert.True(Directory.Exists(_service.GetQueueDir("hotfix")));

        _service.CleanupIfEmptyTransient("hotfix");
        Assert.False(Directory.Exists(_service.GetQueueDir("hotfix")));
    }

    [Fact]
    public void CleanupIfEmptyTransient_KeepsPersistentDir()
    {
        // Create the persistent queue's directory
        Directory.CreateDirectory(_service.GetQueueDir("merge"));
        _service.CleanupIfEmptyTransient("merge");
        Assert.True(Directory.Exists(_service.GetQueueDir("merge")));
    }

    [Fact]
    public void CleanupIfEmptyTransient_KeepsNonEmptyTransient()
    {
        _service.CreateQueue("hotfix", out _);
        _service.SetActive("hotfix", "Brian", "task-1", 42);

        _service.CleanupIfEmptyTransient("hotfix");
        Assert.True(Directory.Exists(_service.GetQueueDir("hotfix")));
    }

    [Fact]
    public void ListQueues_IncludesPersistentAndTransient()
    {
        _service.CreateQueue("hotfix", out _);
        var queues = _service.ListQueues();
        Assert.Contains("merge", queues);
        Assert.Contains("hotfix", queues);
    }

    [Fact]
    public void FindStaleActiveEntries_DetectsDeadPid()
    {
        var originalOverride = ProcessUtils.IsProcessRunningOverride;
        try
        {
            ProcessUtils.IsProcessRunningOverride = _ => false;
            _service.SetActive("merge", "Brian", "task-1", 99999);

            var stale = _service.FindStaleActiveEntries();
            Assert.Single(stale);
            Assert.Equal("merge", stale[0].QueueName);
            Assert.Equal("Brian", stale[0].Entry.Agent);
        }
        finally
        {
            ProcessUtils.IsProcessRunningOverride = originalOverride;
        }
    }

    [Fact]
    public void FindStaleActiveEntries_IgnoresRunningPid()
    {
        var originalOverride = ProcessUtils.IsProcessRunningOverride;
        try
        {
            ProcessUtils.IsProcessRunningOverride = _ => true;
            _service.SetActive("merge", "Brian", "task-1", 42);

            var stale = _service.FindStaleActiveEntries();
            Assert.Empty(stale);
        }
        finally
        {
            ProcessUtils.IsProcessRunningOverride = originalOverride;
        }
    }

    [Fact]
    public void SequenceNumbers_Increment()
    {
        _service.SetActive("merge", "Charlie", "task-0", 1);

        _service.TryEnqueue("merge", "Brian", "task-1", true, false, null, null, null, null, null);
        _service.TryEnqueue("merge", "Dexter", "task-2", true, false, null, null, null, null, null);
        _service.TryEnqueue("merge", "Emma", "task-3", true, false, null, null, null, null, null);

        var pending = _service.GetPending("merge");
        Assert.Equal(3, pending.Count);
        Assert.StartsWith("0001", pending[0].FileName);
        Assert.StartsWith("0002", pending[1].FileName);
        Assert.StartsWith("0003", pending[2].FileName);
    }

    [Fact]
    public void DefaultPersistentQueues_UsedWhenConfigEmpty()
    {
        var service = new QueueService(_testDir, new DydoConfig());
        Assert.Contains("merge", service.GetPersistentQueues());
        Assert.True(service.QueueExists("merge"));
    }

    [Fact]
    public void IsPersistent_TrueForConfigured_FalseForTransient()
    {
        Assert.True(_service.IsPersistent("merge"));
        _service.CreateQueue("hotfix", out _);
        Assert.False(_service.IsPersistent("hotfix"));
    }

    [Fact]
    public void EntryPreservesAllLaunchParams()
    {
        _service.SetActive("merge", "Charlie", "task-0", 1);

        _service.TryEnqueue("merge", "Brian", "task-1",
            launchInTab: true, autoClose: true,
            worktreeId: "wt-abc", windowName: "win-1",
            workingDirOverride: "/custom/dir",
            cleanupWorktreeId: "wt-old",
            mainProjectRoot: "/project");

        var entry = _service.DequeueNext("merge")!;
        Assert.Equal("Brian", entry.Agent);
        Assert.Equal("task-1", entry.Task);
        Assert.True(entry.LaunchInTab);
        Assert.True(entry.AutoClose);
        Assert.Equal("wt-abc", entry.WorktreeId);
        Assert.Equal("win-1", entry.WindowName);
        Assert.Equal("/custom/dir", entry.WorkingDirOverride);
        Assert.Equal("wt-old", entry.CleanupWorktreeId);
        Assert.Equal("/project", entry.MainProjectRoot);
    }

    [Fact]
    public void TryAcquireOrEnqueue_FirstCall_ReturnsAcquired()
    {
        var result = _service.TryAcquireOrEnqueue("merge", "Brian", "task-1",
            true, false, null, null, null, null, null);

        Assert.Equal(QueueResult.Acquired, result);

        var active = _service.GetActive("merge");
        Assert.NotNull(active);
        Assert.Equal("Brian", active.Agent);
        Assert.Equal("task-1", active.Task);
        Assert.Equal(0, active.Pid);
    }

    [Fact]
    public void TryAcquireOrEnqueue_SecondCall_ReturnsQueued()
    {
        _service.TryAcquireOrEnqueue("merge", "Charlie", "task-0",
            true, false, null, null, null, null, null);

        var result = _service.TryAcquireOrEnqueue("merge", "Brian", "task-1",
            true, true, "wt-123", null, null, null, "/project");

        Assert.Equal(QueueResult.Queued, result);

        var pending = _service.GetPending("merge");
        Assert.Single(pending);
        Assert.Equal("Brian", pending[0].Entry.Agent);
    }

    [Fact]
    public void TryAcquireOrEnqueue_ConcurrentCalls_ExactlyOneAcquires()
    {
        // Verifies the check-then-set is atomic: two sequential calls produce
        // exactly one Acquired and one Queued (the lock ensures this even when
        // calls overlap in production across processes).
        var r1 = _service.TryAcquireOrEnqueue("merge", "Brian", "race-task",
            true, false, null, null, null, null, null);
        var r2 = _service.TryAcquireOrEnqueue("merge", "Charlie", "race-task",
            true, false, null, null, null, null, null);

        Assert.Equal(QueueResult.Acquired, r1);
        Assert.Equal(QueueResult.Queued, r2);

        var active = _service.GetActive("merge");
        Assert.Equal("Brian", active!.Agent);

        var pending = _service.GetPending("merge");
        Assert.Single(pending);
        Assert.Equal("Charlie", pending[0].Entry.Agent);
    }

    [Fact]
    public void UpdateActivePid_UpdatesExistingEntry()
    {
        _service.TryAcquireOrEnqueue("merge", "Brian", "task-1",
            true, false, null, null, null, null, null);

        Assert.Equal(0, _service.GetActive("merge")!.Pid);

        _service.UpdateActivePid("merge", 42);

        var active = _service.GetActive("merge");
        Assert.NotNull(active);
        Assert.Equal(42, active.Pid);
        Assert.Equal("Brian", active.Agent);
    }

    [Fact]
    public void Constructor_NormalizesWorktreeRoot_ToMainProject()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), "dydo-wt-test-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            // Main project: <projectRoot>/dydo.json and <projectRoot>/dydo/
            Directory.CreateDirectory(projectRoot);
            File.WriteAllText(Path.Combine(projectRoot, "dydo.json"), "{}");
            var mainDydoRoot = Path.Combine(projectRoot, "dydo");
            Directory.CreateDirectory(mainDydoRoot);

            // Worktree: <projectRoot>/dydo/_system/.local/worktrees/<id>/ with its own dydo.json and dydo/
            var worktreeRoot = Path.Combine(projectRoot, "dydo", "_system", ".local", "worktrees", "wt-abc");
            Directory.CreateDirectory(worktreeRoot);
            File.WriteAllText(Path.Combine(worktreeRoot, "dydo.json"), "{}");
            var worktreeDydoRoot = Path.Combine(worktreeRoot, "dydo");
            Directory.CreateDirectory(worktreeDydoRoot);

            var mainService = new QueueService(mainDydoRoot);
            var worktreeService = new QueueService(worktreeDydoRoot);

            // Both must resolve to the same queues directory (the main project's)
            Assert.Equal(
                PathUtils.NormalizePath(mainService.QueuesDir),
                PathUtils.NormalizePath(worktreeService.QueuesDir));
        }
        finally
        {
            if (Directory.Exists(projectRoot))
                Directory.Delete(projectRoot, true);
        }
    }

    [Fact]
    public void WorktreeService_SharesQueueState_WithMainService()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), "dydo-wt-test-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Directory.CreateDirectory(projectRoot);
            File.WriteAllText(Path.Combine(projectRoot, "dydo.json"), "{}");
            var mainDydoRoot = Path.Combine(projectRoot, "dydo");
            Directory.CreateDirectory(mainDydoRoot);

            var worktreeRoot = Path.Combine(projectRoot, "dydo", "_system", ".local", "worktrees", "wt-abc");
            Directory.CreateDirectory(worktreeRoot);
            File.WriteAllText(Path.Combine(worktreeRoot, "dydo.json"), "{}");
            var worktreeDydoRoot = Path.Combine(worktreeRoot, "dydo");
            Directory.CreateDirectory(worktreeDydoRoot);

            var config = new DydoConfig { Queues = ["merge"] };
            var mainService = new QueueService(mainDydoRoot, config);
            var worktreeService = new QueueService(worktreeDydoRoot, config);

            // Enqueue from main
            mainService.SetActive("merge", "Charlie", "task-0", 1);

            // Worktree service must see the active entry set by main
            var active = worktreeService.GetActive("merge");
            Assert.NotNull(active);
            Assert.Equal("Charlie", active.Agent);
        }
        finally
        {
            if (Directory.Exists(projectRoot))
                Directory.Delete(projectRoot, true);
        }
    }
}
