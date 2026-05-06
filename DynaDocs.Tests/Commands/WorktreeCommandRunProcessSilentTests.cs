namespace DynaDocs.Tests.Commands;

using System.Diagnostics;
using DynaDocs.Commands;

/// <summary>
/// Exercises <see cref="WorktreeCommand.RunProcessSilent"/> through the real (non-override)
/// path so the helper substitution under #0170 is verified end-to-end. The bulk of the
/// drain semantics are pinned by ProcessUtilsCaptureTests; these tests pin the override-layer
/// wiring plus the load-bearing env-var (GIT_TERMINAL_PROMPT) and stdin-EOF contract.
/// </summary>
public class WorktreeCommandRunProcessSilentTests : IDisposable
{
    private readonly string _testDir;
    private readonly Action<string, string>? _savedRunProcessOverride;
    private readonly Func<string, string, int>? _savedRunProcessWithExitCodeOverride;
    private readonly Func<string, string, int>? _savedRunProcessSilentOverride;

    public WorktreeCommandRunProcessSilentTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-rps-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);

        _savedRunProcessOverride = WorktreeCommand.RunProcessOverride;
        _savedRunProcessWithExitCodeOverride = WorktreeCommand.RunProcessWithExitCodeOverride;
        _savedRunProcessSilentOverride = WorktreeCommand.RunProcessSilentOverride;

        WorktreeCommand.RunProcessOverride = null;
        WorktreeCommand.RunProcessWithExitCodeOverride = null;
        WorktreeCommand.RunProcessSilentOverride = null;
    }

    public void Dispose()
    {
        WorktreeCommand.RunProcessOverride = _savedRunProcessOverride;
        WorktreeCommand.RunProcessWithExitCodeOverride = _savedRunProcessWithExitCodeOverride;
        WorktreeCommand.RunProcessSilentOverride = _savedRunProcessSilentOverride;

        try { Directory.Delete(_testDir, true); } catch { /* best-effort */ }
    }

    [Fact]
    public void RunProcessSilent_NoisyStderr_DoesNotDeadlock()
    {
        // Old sequential drain (StandardOutput.ReadToEnd then StandardError.ReadToEnd) hangs
        // when stderr fills before stdout reaches EOF. We simulate that here by piping ~3000
        // lines straight to stderr — easily clearing the ~64 KB Windows pipe buffer.
        if (!OperatingSystem.IsWindows()) return;

        var sw = Stopwatch.StartNew();
        var exit = WorktreeCommand.RunProcessSilent(
            "cmd",
            "/c \"for /L %i in (1,1,3000) do @echo line%i 1>&2\"");
        sw.Stop();

        Assert.Equal(0, exit);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(15),
            $"RunProcessSilent took {sw.Elapsed.TotalSeconds:F1}s on noisy-stderr — sequential-drain regression?");
    }

    [Fact]
    public void RunProcessSilent_PropagatesExitCode_OnSuccess()
    {
        InitMinimalRepo();

        var exit = WorktreeCommand.RunProcessSilent("git", $"-C \"{_testDir}\" rev-parse --is-inside-work-tree");

        Assert.Equal(0, exit);
    }

    [Fact]
    public void RunProcessSilent_PropagatesExitCode_OnFailure()
    {
        var exit = WorktreeCommand.RunProcessSilent("git", "nonexistent-subcommand-xyz");

        Assert.NotEqual(0, exit);
    }

    [Fact]
    public void RunProcessSilent_StartFailure_ReturnsOne()
    {
        // Preserves the documented contract that start-failure returns 1 (not the helper's
        // -1 sentinel). FinalizeMerge's best-effort cleanup branches on this.
        var exit = WorktreeCommand.RunProcessSilent("this-binary-definitely-does-not-exist-xyz", "");

        Assert.Equal(1, exit);
    }

    private void InitMinimalRepo()
    {
        Git("init", "--initial-branch=master");
        Git("config", "user.email", "test@example.com");
        Git("config", "user.name", "Test");
    }

    private void Git(params string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = _testDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var p = Process.Start(psi)
            ?? throw new InvalidOperationException("git failed to start");

        var stdoutTask = p.StandardOutput.ReadToEndAsync();
        var stderrTask = p.StandardError.ReadToEndAsync();

        if (!p.WaitForExit(10_000))
        {
            try { p.Kill(entireProcessTree: true); } catch { /* best-effort */ }
            throw new InvalidOperationException($"git {string.Join(' ', args)} timed out in {_testDir}");
        }

        if (p.ExitCode != 0)
        {
            var stderr = stderrTask.GetAwaiter().GetResult();
            throw new InvalidOperationException(
                $"git {string.Join(' ', args)} failed in {_testDir} (exit {p.ExitCode}): {stderr}");
        }
    }
}
