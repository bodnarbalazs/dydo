namespace DynaDocs.Tests.Commands;

using System.Diagnostics;
using System.Reflection;

[Collection("ConsoleOutput")]
public class WorktreeMergeSafetyGitHelperTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _repoDir;

    public WorktreeMergeSafetyGitHelperTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-githelper-test-" + Guid.NewGuid().ToString("N")[..8]);
        _repoDir = Path.Combine(_testDir, "repo");
        Directory.CreateDirectory(_repoDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public void Git_NoisyOutput_DoesNotDeadlock()
    {
        // Reproduces the pipe-buffer deadlock that the Git() helper used to hit.
        // Commit ~256 KB of text and run `git log -p`, which prints the full diff
        // to stdout. Without concurrent stream draining the helper hangs for 30 s.
        InvokeGit(_repoDir, "init", "--initial-branch=master");
        InvokeGit(_repoDir, "config", "user.email", "test@example.com");
        InvokeGit(_repoDir, "config", "user.name", "Test");

        var bigContent = new string('a', 256 * 1024);
        File.WriteAllText(Path.Combine(_repoDir, "big.txt"), bigContent);
        InvokeGit(_repoDir, "add", "big.txt");
        InvokeGit(_repoDir, "commit", "-m", "big");

        var sw = Stopwatch.StartNew();
        InvokeGit(_repoDir, "log", "-p");
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10),
            $"Git() took {sw.Elapsed.TotalSeconds:F1}s — pipe-buffer deadlock regression?");
    }

    private static void InvokeGit(string cwd, params string[] args)
    {
        var method = typeof(WorktreeMergeSafetyIntegrationTests)
            .GetMethod("Git", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Git helper not found");
        try
        {
            method.Invoke(null, new object[] { cwd, args });
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }
}
