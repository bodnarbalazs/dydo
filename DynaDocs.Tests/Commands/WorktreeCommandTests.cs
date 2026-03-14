namespace DynaDocs.Tests.Commands;

using DynaDocs.Commands;
using DynaDocs.Services;

[Collection("ConsoleOutput")]
public class WorktreeCommandTests : IDisposable
{
    private readonly string _testDir;
    private readonly AgentRegistry _registry;

    public WorktreeCommandTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-wt-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _registry = new AgentRegistry(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public void Cleanup_RemovesOwnMarkers()
    {
        var workspace = _registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(workspace);

        var worktreeId = "Adele-20260313120000";
        File.WriteAllText(Path.Combine(workspace, ".worktree"), worktreeId);
        File.WriteAllText(Path.Combine(workspace, ".worktree-path"), "/some/path");

        WorktreeCommand.ExecuteCleanup(worktreeId, "Adele", _registry);

        Assert.False(File.Exists(Path.Combine(workspace, ".worktree")));
        Assert.False(File.Exists(Path.Combine(workspace, ".worktree-path")));
    }

    [Fact]
    public void Cleanup_SkipsRemoval_WhenOtherAgentsReference()
    {
        var worktreeId = "Adele-20260313120000";

        var adeleWs = _registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(adeleWs);
        File.WriteAllText(Path.Combine(adeleWs, ".worktree"), worktreeId);

        var brianWs = _registry.GetAgentWorkspace("Brian");
        Directory.CreateDirectory(brianWs);
        File.WriteAllText(Path.Combine(brianWs, ".worktree"), worktreeId);

        var stdout = CaptureStdout(() => WorktreeCommand.ExecuteCleanup(worktreeId, "Adele", _registry));

        Assert.Contains("still referencing", stdout);
        Assert.False(File.Exists(Path.Combine(adeleWs, ".worktree")));
        Assert.True(File.Exists(Path.Combine(brianWs, ".worktree")));
    }

    [Fact]
    public void Cleanup_LastAgent_AttemptsRemoval()
    {
        var worktreeId = "Adele-20260313120000";

        var adeleWs = _registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(adeleWs);
        File.WriteAllText(Path.Combine(adeleWs, ".worktree"), worktreeId);
        File.WriteAllText(Path.Combine(adeleWs, ".worktree-path"), Path.Combine(_testDir, "dydo/_system/.local/worktrees", worktreeId));

        var stdout = CaptureStdout(() => WorktreeCommand.ExecuteCleanup(worktreeId, "Adele", _registry));

        Assert.False(File.Exists(Path.Combine(adeleWs, ".worktree")));
        Assert.DoesNotContain("still referencing", stdout);
    }

    [Fact]
    public void Cleanup_ReturnsSuccess()
    {
        var workspace = _registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(workspace);

        var exitCode = WorktreeCommand.ExecuteCleanup("nonexistent-id", "Adele", _registry);
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void Cleanup_NoMarkersToRemove_StillSucceeds()
    {
        // Agent workspace exists but has no worktree markers
        var workspace = _registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(workspace);

        var exitCode = WorktreeCommand.ExecuteCleanup("some-id", "Adele", _registry);
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void Cleanup_DoesNotMatchPartialWorktreeId()
    {
        var worktreeId = "abc";
        var similarId = "xabc";

        var adeleWs = _registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(adeleWs);
        File.WriteAllText(Path.Combine(adeleWs, ".worktree-path"), Path.Combine(_testDir, "worktrees", similarId));

        var stdout = CaptureStdout(() => WorktreeCommand.ExecuteCleanup(worktreeId, "Adele", _registry));

        // Should NOT resolve the path since the directory name "xabc" != "abc"
        Assert.Contains("no path found", stdout);
    }

    private static string CaptureStdout(Action action)
    {
        var original = Console.Out;
        var sw = new StringWriter();
        Console.SetOut(sw);
        try
        {
            action();
            return sw.ToString();
        }
        finally
        {
            Console.SetOut(original);
        }
    }
}
