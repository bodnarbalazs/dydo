namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

[Collection("ConsoleOutput")]
public class WorktreeCreationLockTests : IDisposable
{
    private readonly string _testDir;

    public WorktreeCreationLockTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-wt-lock-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        DispatchService.CreateGitWorktreeOverride = null;
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public void CreateGitWorktree_CallsWorktreePruneAndAdd()
    {
        var calls = new List<(string WorkDir, string Cmd, string Args)>();
        DispatchService.CreateGitWorktreeOverride = (wd, cmd, args) =>
        {
            calls.Add((wd, cmd, args));
            return 0;
        };

        var projectRoot = Path.Combine(_testDir, "project");
        var wtPath = Path.Combine(projectRoot, "dydo", "_system", ".local", "worktrees", "my-task");
        Directory.CreateDirectory(Path.Combine(projectRoot, "dydo", "_system", ".local", "worktrees"));

        DispatchService.CreateGitWorktree(projectRoot, wtPath, "worktree/my-task");

        Assert.Contains(calls, c => c.Args.Contains("worktree prune"));
        Assert.Contains(calls, c => c.Args.Contains("worktree add -b worktree/my-task --"));
    }

    [Fact]
    public void CreateGitWorktree_ThrowsOnNonZeroExitCode()
    {
        DispatchService.CreateGitWorktreeOverride = (wd, cmd, args) =>
            args.Contains("worktree add") ? 1 : 0;

        var projectRoot = Path.Combine(_testDir, "project");
        var wtPath = Path.Combine(projectRoot, "dydo", "_system", ".local", "worktrees", "fail-task");
        Directory.CreateDirectory(Path.Combine(projectRoot, "dydo", "_system", ".local", "worktrees"));

        Assert.Throws<InvalidOperationException>(() =>
            DispatchService.CreateGitWorktree(projectRoot, wtPath, "worktree/fail-task"));
    }

    [Fact]
    public void CreateGitWorktree_CreatesAndDeletesLockFile()
    {
        DispatchService.CreateGitWorktreeOverride = (_, _, _) => 0;

        var projectRoot = Path.Combine(_testDir, "project");
        var worktreesDir = Path.Combine(projectRoot, "dydo", "_system", ".local", "worktrees");
        Directory.CreateDirectory(worktreesDir);
        var wtPath = Path.Combine(worktreesDir, "lock-test");

        DispatchService.CreateGitWorktree(projectRoot, wtPath, "worktree/lock-test");

        var lockPath = Path.Combine(worktreesDir, ".lock");
        Assert.False(File.Exists(lockPath), "Lock file should be deleted after operation");
    }

    [Fact]
    public void CreateGitWorktree_RemovesStaleDirectoryBeforeAdd()
    {
        DispatchService.CreateGitWorktreeOverride = (_, _, _) => 0;

        var projectRoot = Path.Combine(_testDir, "project");
        var worktreesDir = Path.Combine(projectRoot, "dydo", "_system", ".local", "worktrees");
        var wtPath = Path.Combine(worktreesDir, "stale-task");

        // Create stale directory
        Directory.CreateDirectory(wtPath);
        File.WriteAllText(Path.Combine(wtPath, "leftover.txt"), "stale");

        DispatchService.CreateGitWorktree(projectRoot, wtPath, "worktree/stale-task");

        // Stale directory should have been deleted before git worktree add
        // (the override doesn't recreate it, so it shouldn't exist)
        Assert.False(Directory.Exists(wtPath));
    }

    [Fact]
    public async Task CreateGitWorktree_SerializesWithLock()
    {
        // Verify that concurrent calls serialize via the lock file
        var callOrder = new List<int>();
        var barrier = new ManualResetEventSlim(false);
        DispatchService.CreateGitWorktreeOverride = (_, _, args) =>
        {
            if (args.Contains("worktree add"))
            {
                var id = args.Contains("task-1") ? 1 : 2;
                lock (callOrder) callOrder.Add(id);
                Thread.Sleep(50);
            }
            return 0;
        };

        var projectRoot = Path.Combine(_testDir, "project");
        var worktreesDir = Path.Combine(projectRoot, "dydo", "_system", ".local", "worktrees");
        Directory.CreateDirectory(worktreesDir);

        var t1 = Task.Run(() =>
        {
            barrier.Wait();
            DispatchService.CreateGitWorktree(projectRoot,
                Path.Combine(worktreesDir, "task-1"), "worktree/task-1");
        });

        var t2 = Task.Run(() =>
        {
            barrier.Wait();
            DispatchService.CreateGitWorktree(projectRoot,
                Path.Combine(worktreesDir, "task-2"), "worktree/task-2");
        });

        barrier.Set();
        await Task.WhenAll(t1, t2);

        // Both should complete — the lock serializes them
        Assert.Equal(2, callOrder.Count);
    }

    [Fact]
    public void CreateGitWorktree_LockFileDeletedEvenOnFailure()
    {
        DispatchService.CreateGitWorktreeOverride = (_, _, args) =>
            args.Contains("worktree add") ? 128 : 0;

        var projectRoot = Path.Combine(_testDir, "project");
        var worktreesDir = Path.Combine(projectRoot, "dydo", "_system", ".local", "worktrees");
        Directory.CreateDirectory(worktreesDir);

        try
        {
            DispatchService.CreateGitWorktree(projectRoot,
                Path.Combine(worktreesDir, "fail-task"), "worktree/fail-task");
        }
        catch (InvalidOperationException) { }

        Assert.False(File.Exists(Path.Combine(worktreesDir, ".lock")));
    }
}
