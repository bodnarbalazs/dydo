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

    [Fact]
    public void CreateGitWorktree_StaleDirectoryWithJunctions_DoesNotDeleteJunctionTargetContents()
    {
        // Reproduce: stale worktree dir has junctions pointing to "main repo" dirs.
        // Directory.Delete(recursive: true) follows junctions and destroys target contents.
        // The fix: remove junctions before the recursive delete.
        if (!OperatingSystem.IsWindows()) return; // Junctions are a Windows concept

        DispatchService.CreateGitWorktreeOverride = (_, _, _) => 0;

        var projectRoot = Path.Combine(_testDir, "project");
        var worktreesDir = Path.Combine(projectRoot, "dydo", "_system", ".local", "worktrees");
        var wtPath = Path.Combine(worktreesDir, "stale-junctions");

        // Simulate main repo dirs with files that must survive
        var mainAgents = Path.Combine(_testDir, "main-repo", "dydo", "agents");
        var mainRoles = Path.Combine(_testDir, "main-repo", "dydo", "_system", "roles");
        var mainIssues = Path.Combine(_testDir, "main-repo", "dydo", "project", "issues");
        var mainInquisitions = Path.Combine(_testDir, "main-repo", "dydo", "project", "inquisitions");
        Directory.CreateDirectory(mainAgents);
        Directory.CreateDirectory(mainRoles);
        Directory.CreateDirectory(mainIssues);
        Directory.CreateDirectory(mainInquisitions);
        File.WriteAllText(Path.Combine(mainAgents, "Adele.json"), "agent-data");
        File.WriteAllText(Path.Combine(mainRoles, "code-writer.role.json"), "role-data");
        File.WriteAllText(Path.Combine(mainIssues, "issue-1.md"), "issue-data");
        File.WriteAllText(Path.Combine(mainInquisitions, "inq-1.md"), "inq-data");

        // Create stale worktree dir with junctions pointing to main repo dirs
        Directory.CreateDirectory(Path.Combine(wtPath, "dydo", "project"));
        Directory.CreateDirectory(Path.Combine(wtPath, "dydo", "_system"));
        CreateJunction(Path.Combine(wtPath, "dydo", "agents"), mainAgents);
        CreateJunction(Path.Combine(wtPath, "dydo", "_system", "roles"), mainRoles);
        CreateJunction(Path.Combine(wtPath, "dydo", "project", "issues"), mainIssues);
        CreateJunction(Path.Combine(wtPath, "dydo", "project", "inquisitions"), mainInquisitions);

        DispatchService.CreateGitWorktree(projectRoot, wtPath, "worktree/stale-junctions");

        // Main repo files must survive — if Directory.Delete followed junctions, these are gone
        Assert.True(File.Exists(Path.Combine(mainAgents, "Adele.json")),
            "Main repo agents dir was destroyed by stale worktree cleanup");
        Assert.True(File.Exists(Path.Combine(mainRoles, "code-writer.role.json")),
            "Main repo roles dir was destroyed by stale worktree cleanup");
        Assert.True(File.Exists(Path.Combine(mainIssues, "issue-1.md")),
            "Main repo issues dir was destroyed by stale worktree cleanup");
        Assert.True(File.Exists(Path.Combine(mainInquisitions, "inq-1.md")),
            "Main repo inquisitions dir was destroyed by stale worktree cleanup");
    }

    [Fact]
    public void CreateGitWorktree_StaleDirectoryWithUnknownJunction_DoesNotDeleteJunctionTarget()
    {
        // Regression: a junction NOT in the known JunctionSubpaths list must still be
        // handled safely. The junction-safe delete should detect it via ReparsePoint flag
        // rather than relying on a hardcoded list.
        if (!OperatingSystem.IsWindows()) return;

        DispatchService.CreateGitWorktreeOverride = (_, _, _) => 0;

        var projectRoot = Path.Combine(_testDir, "project");
        var worktreesDir = Path.Combine(projectRoot, "dydo", "_system", ".local", "worktrees");
        var wtPath = Path.Combine(worktreesDir, "unknown-junction");

        // Simulate a target directory outside the worktree that must survive
        var externalTarget = Path.Combine(_testDir, "external-data");
        Directory.CreateDirectory(externalTarget);
        File.WriteAllText(Path.Combine(externalTarget, "precious.txt"), "must survive");

        // Create stale worktree dir with a junction NOT in JunctionSubpaths
        var unknownJunctionPath = Path.Combine(wtPath, "some", "unexpected", "junction");
        Directory.CreateDirectory(Path.Combine(wtPath, "some", "unexpected"));
        CreateJunction(unknownJunctionPath, externalTarget);

        DispatchService.CreateGitWorktree(projectRoot, wtPath, "worktree/unknown-junction");

        Assert.True(File.Exists(Path.Combine(externalTarget, "precious.txt")),
            "External directory was destroyed by stale worktree cleanup — junction not detected");
    }

    private static void CreateJunction(string junctionPath, string targetPath)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "cmd",
            Arguments = $"/c mklink /J \"{junctionPath}\" \"{targetPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        var proc = System.Diagnostics.Process.Start(psi)!;
        proc.WaitForExit(5000);
        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"mklink /J failed: {proc.StandardError.ReadToEnd()}");
    }
}
