namespace DynaDocs.Tests.Services;

using DynaDocs.Services;
using DynaDocs.Utils;

[Collection("ProcessUtils")]
public class PathUtilsWorktreeIsolationTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _originalDir;

    public PathUtilsWorktreeIsolationTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-wtiso-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _originalDir = Environment.CurrentDirectory;
    }

    public void Dispose()
    {
        Environment.CurrentDirectory = _originalDir;
        if (Directory.Exists(_testDir))
        {
            for (var i = 0; i < 3; i++)
            {
                try { Directory.Delete(_testDir, true); return; }
                catch (IOException) when (i < 2) { Thread.Sleep(50 * (i + 1)); }
            }
        }
    }

    private (string mainRoot, string worktreeRoot) BuildMainAndWorktree()
    {
        var mainRoot = Path.Combine(_testDir, "main-project");
        var worktreeRoot = Path.Combine(mainRoot, "dydo", "_system", ".local", "worktrees", "my-wt");
        Directory.CreateDirectory(Path.Combine(mainRoot, "dydo"));
        Directory.CreateDirectory(Path.Combine(worktreeRoot, "dydo"));
        File.WriteAllText(Path.Combine(mainRoot, "dydo.json"), """{"name":"main"}""");
        File.WriteAllText(Path.Combine(worktreeRoot, "dydo.json"), """{"name":"main"}""");
        return (mainRoot, worktreeRoot);
    }

    [Fact]
    public void FindDydoRoot_FromInsideWorktree_ResolvesToWorktreeNotMainProject()
    {
        var (mainRoot, worktreeRoot) = BuildMainAndWorktree();
        Environment.CurrentDirectory = worktreeRoot;

        var resolved = PathUtils.FindDydoRoot();

        Assert.NotNull(resolved);
        var normalizedResolved = PathUtils.NormalizePath(Path.GetFullPath(resolved));
        var normalizedWorktreeDydo = PathUtils.NormalizePath(Path.GetFullPath(Path.Combine(worktreeRoot, "dydo")));
        var normalizedMainDydo = PathUtils.NormalizePath(Path.GetFullPath(Path.Combine(mainRoot, "dydo")));
        Assert.Equal(normalizedWorktreeDydo, normalizedResolved);
        Assert.NotEqual(normalizedMainDydo, normalizedResolved);

        var pidFile = PathUtils.NormalizePath(WatchdogService.GetPidFilePath(resolved));
        Assert.Contains("_system/.local/worktrees/my-wt", pidFile);
    }

    [Fact]
    public void FindMainProjectRoot_FromInsideWorktree_ResolvesToMainNotWorktree()
    {
        var (mainRoot, worktreeRoot) = BuildMainAndWorktree();
        Environment.CurrentDirectory = worktreeRoot;

        var resolved = PathUtils.FindMainProjectRoot();

        Assert.NotNull(resolved);
        var normalizedResolved = PathUtils.NormalizePath(Path.GetFullPath(resolved));
        var normalizedMainRoot = PathUtils.NormalizePath(Path.GetFullPath(mainRoot));
        var normalizedWorktreeRoot = PathUtils.NormalizePath(Path.GetFullPath(worktreeRoot));
        Assert.Equal(normalizedMainRoot, normalizedResolved);
        Assert.NotEqual(normalizedWorktreeRoot, normalizedResolved);
    }

    [Fact]
    public void FindMainProjectRoot_FromOutsideWorktree_FallsBackToFindProjectRoot()
    {
        var (mainRoot, _) = BuildMainAndWorktree();
        var subDir = Path.Combine(mainRoot, "dydo", "project");
        Directory.CreateDirectory(subDir);
        Environment.CurrentDirectory = subDir;

        var resolved = PathUtils.FindMainProjectRoot();

        Assert.NotNull(resolved);
        Assert.Equal(
            PathUtils.NormalizePath(Path.GetFullPath(mainRoot)),
            PathUtils.NormalizePath(Path.GetFullPath(resolved)));
    }

    [Fact]
    public void FindMainProjectRoot_NotInProject_ReturnsNull()
    {
        Environment.CurrentDirectory = _testDir;

        var resolved = PathUtils.FindMainProjectRoot();

        Assert.Null(resolved);
    }

    // #0174 regression: claim-time anchor write must land in the MAIN dydo root
    // whether the claimer's CWD is the main project or a worktree. Pre-fix the
    // claim path resolved its own root via _configService.GetDydoRoot(_basePath),
    // which returns the worktree's own dydo/ when the basepath is inside one —
    // so the anchor never reached the watchdog.
    [Fact]
    public void RegisterMainAnchor_FromInsideWorktree_WritesToMainAnchorsDir()
    {
        var (mainRoot, worktreeRoot) = BuildMainAndWorktree();
        Environment.CurrentDirectory = worktreeRoot;

        var anchorPid = Environment.ProcessId;
        WatchdogService.RegisterMainAnchor(anchorPid);

        var mainAnchorsDir = WatchdogService.GetAnchorsDirPath(Path.Combine(mainRoot, "dydo"));
        var worktreeAnchorsDir = WatchdogService.GetAnchorsDirPath(Path.Combine(worktreeRoot, "dydo"));
        var mainAnchorFile = Path.Combine(mainAnchorsDir, $"{anchorPid}.anchor");
        var worktreeAnchorFile = Path.Combine(worktreeAnchorsDir, $"{anchorPid}.anchor");

        Assert.True(File.Exists(mainAnchorFile),
            $"Anchor must land in main anchors dir; expected at {mainAnchorFile}");
        Assert.False(File.Exists(worktreeAnchorFile),
            "Anchor must NOT land in the worktree's anchors dir — the watchdog only reads main");
    }

    [Fact]
    public void RegisterMainAnchor_FromMainProject_WritesToMainAnchorsDir()
    {
        var (mainRoot, _) = BuildMainAndWorktree();
        Environment.CurrentDirectory = mainRoot;

        var anchorPid = Environment.ProcessId;
        WatchdogService.RegisterMainAnchor(anchorPid);

        var mainAnchorsDir = WatchdogService.GetAnchorsDirPath(Path.Combine(mainRoot, "dydo"));
        Assert.True(File.Exists(Path.Combine(mainAnchorsDir, $"{anchorPid}.anchor")));
    }
}
