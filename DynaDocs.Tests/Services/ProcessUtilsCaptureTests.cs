namespace DynaDocs.Tests.Services;

using System.Diagnostics;
using DynaDocs.Services;

public class ProcessUtilsCaptureTests : IDisposable
{
    private readonly string _testDir;

    public ProcessUtilsCaptureTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-procutils-cap-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { /* best-effort */ }
    }

    [Fact]
    public void RunProcessCapture_LargeOutput_DoesNotDeadlock()
    {
        // Reproduces the pipe-buffer deadlock that the production callers used to risk.
        // A 256 KB diff blows past the ~64 KB Windows pipe buffer; without concurrent
        // draining the call hangs until the WaitForExit timeout.
        InitRepoWithBigCommit();

        var sw = Stopwatch.StartNew();
        var (exitCode, stdout, _) = ProcessUtils.RunProcessCapture(
            "git", "log -p", _testDir, timeoutMs: 30_000);
        sw.Stop();

        Assert.Equal(0, exitCode);
        Assert.True(stdout.Length > 64 * 1024, "Expected large stdout to confirm we cleared the buffer threshold.");
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(15),
            $"git log -p took {sw.Elapsed.TotalSeconds:F1}s — pipe-buffer deadlock regression?");
    }

    [Fact]
    public void RunProcessCapture_NonZeroExit_ReturnsCapturedStderr()
    {
        var (exitCode, _, stderr) = ProcessUtils.RunProcessCapture(
            "git", "nonexistent-subcommand-xyz", workingDir: null, timeoutMs: 5000);

        Assert.NotEqual(0, exitCode);
        Assert.NotEmpty(stderr);
    }

    [Fact]
    public void RunProcessCapture_Timeout_KillsProcessAndReturnsSentinel()
    {
        var (cmd, args) = OperatingSystem.IsWindows()
            ? ("cmd", "/c ping -n 30 127.0.0.1 > nul")
            : ("sh", "-c \"sleep 30\"");

        var sw = Stopwatch.StartNew();
        var (exitCode, _, _) = ProcessUtils.RunProcessCapture(cmd, args, workingDir: null, timeoutMs: 500);
        sw.Stop();

        Assert.Equal(-1, exitCode);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5),
            $"Helper waited {sw.Elapsed.TotalSeconds:F1}s after kill — kill-tree path regressed?");
    }

    [Fact]
    public void RunProcessCapture_WorkingDirRespected()
    {
        InitMinimalRepo();

        var (exitCode, stdout, _) = ProcessUtils.RunProcessCapture(
            "git", "rev-parse --show-toplevel", _testDir, timeoutMs: 5000);

        Assert.Equal(0, exitCode);
        Assert.Contains(Path.GetFileName(_testDir), stdout);
    }

    [Fact]
    public void RunProcessCapture_EnvironmentInjected_PreservesParentEnv()
    {
        var (cmd, args) = OperatingSystem.IsWindows()
            ? ("cmd", "/c echo %DYDO_TEST_VAR%-%PATH:~0,1%")
            : ("sh", "-c \"echo $DYDO_TEST_VAR-${PATH:0:1}\"");

        var (exitCode, stdout, _) = ProcessUtils.RunProcessCapture(
            cmd, args,
            workingDir: null,
            timeoutMs: 5000,
            environment: new Dictionary<string, string?> { ["DYDO_TEST_VAR"] = "hello" });

        Assert.Equal(0, exitCode);
        Assert.StartsWith("hello-", stdout.Trim());
        // The trailing char is the first char of PATH inherited from the parent — proves
        // we set entries on psi.Environment rather than overwriting it.
        Assert.True(stdout.Trim().Length > "hello-".Length,
            "Expected parent PATH to be inherited and visible to the child.");
    }

    [Fact]
    public void RunProcessCapture_StartFailure_ReturnsSentinel()
    {
        var (exitCode, stdout, stderr) = ProcessUtils.RunProcessCapture(
            "this-binary-definitely-does-not-exist-xyz", "", workingDir: null, timeoutMs: 1000);

        Assert.Equal(-1, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Equal(string.Empty, stderr);
    }

    [Fact]
    public void RunProcessCapture_RedirectStdin_DoesNotHangOnStdinReaders()
    {
        // git hash-object --stdin reads bytes from stdin until EOF and prints the SHA-1.
        // If the helper forgets to close stdin, the child blocks forever waiting for input
        // and the call hits the timeout (exit -1). With redirectStdin=true the helper must
        // EOF stdin immediately so empty input yields the well-known empty-blob hash.
        const string EmptyBlobSha1 = "e69de29bb2d1d6434b8b29ae775ad8c2e48c5391";
        InitMinimalRepo();

        var sw = Stopwatch.StartNew();
        var (exitCode, stdout, _) = ProcessUtils.RunProcessCapture(
            "git", "hash-object --stdin",
            _testDir,
            timeoutMs: 5000,
            redirectStdin: true);
        sw.Stop();

        Assert.Equal(0, exitCode);
        Assert.Equal(EmptyBlobSha1, stdout.Trim());
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(3),
            $"Expected immediate EOF on stdin; took {sw.Elapsed.TotalSeconds:F1}s.");
    }

    private void InitMinimalRepo()
    {
        Git("init", "--initial-branch=master");
        Git("config", "user.email", "test@example.com");
        Git("config", "user.name", "Test");
        File.WriteAllText(Path.Combine(_testDir, "seed.txt"), "seed");
        Git("add", "seed.txt");
        Git("commit", "-m", "seed");
    }

    private void InitRepoWithBigCommit()
    {
        InitMinimalRepo();
        var bigContent = new string('a', 256 * 1024);
        File.WriteAllText(Path.Combine(_testDir, "big.txt"), bigContent);
        Git("add", "big.txt");
        Git("commit", "-m", "big");
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
            throw new InvalidOperationException(
                $"git {string.Join(' ', args)} timed out in {_testDir}");
        }

        if (p.ExitCode != 0)
        {
            var stderr = stderrTask.GetAwaiter().GetResult();
            throw new InvalidOperationException(
                $"git {string.Join(' ', args)} failed in {_testDir} (exit {p.ExitCode}): {stderr}");
        }
    }
}
