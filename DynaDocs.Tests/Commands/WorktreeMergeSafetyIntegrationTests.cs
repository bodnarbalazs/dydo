namespace DynaDocs.Tests.Commands;

using System.Diagnostics;
using DynaDocs.Commands;
using DynaDocs.Services;

/// <summary>
/// End-to-end safety check tests that invoke ExecuteMerge against a real git repo WITHOUT
/// mocking RunProcessCapture. Unit tests (WorktreeCommandTests) mock the git command strings
/// and therefore cannot catch flag-shape bugs like a missing/extra '--' separator.
///
/// Fix for: silent-data-loss incident 2026-04-14, plus Brian's review of dispatch-commit-gap-fix
/// which uncovered a malformed `rev-list --count -- base..source` that made the guard never
/// produce a count (git exit 129) — silently blocking every non-force merge.
/// </summary>
[Collection("ConsoleOutput")]
public class WorktreeMergeSafetyIntegrationTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _repoDir;
    private readonly AgentRegistry _registry;

    public WorktreeMergeSafetyIntegrationTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-wtms-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);

        _repoDir = Path.Combine(_testDir, "repo");
        Directory.CreateDirectory(_repoDir);

        _registry = new AgentRegistry(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public void CheckMergeSafety_BranchAdvanced_CleanTree_ReturnsNull()
    {
        InitRepoWithAdvancedBranch(out var worktreePath);

        var result = WorktreeCommand.CheckMergeSafety(_repoDir, "master", "feature/advanced", worktreePath);

        Assert.Null(result);
    }

    [Fact]
    public void CheckMergeSafety_BranchNotAdvanced_ReturnsError()
    {
        InitRepoWithUnadvancedBranch(out var worktreePath);

        var result = WorktreeCommand.CheckMergeSafety(_repoDir, "master", "feature/same", worktreePath);

        Assert.NotNull(result);
        Assert.Contains("0 commits ahead", result);
    }

    [Fact]
    public void CheckMergeSafety_DirtyWorktree_ReturnsError()
    {
        InitRepoWithAdvancedBranch(out var worktreePath);
        File.WriteAllText(Path.Combine(worktreePath, "dirty.txt"), "uncommitted");

        var result = WorktreeCommand.CheckMergeSafety(_repoDir, "master", "feature/advanced", worktreePath);

        Assert.NotNull(result);
        Assert.Contains("uncommitted", result);
        Assert.Contains("dirty.txt", result);
    }

    [Fact]
    public void ExecuteMerge_BranchAdvanced_CleanTree_ProceedsToGitMerge()
    {
        // End-to-end: real rev-list + real status --porcelain, mocked merge/teardown.
        // Regression guard for the rev-list "--" bug that silently blocked every merge.
        InitRepoWithAdvancedBranch(out var worktreePath);
        SetupMergeAgent("Adele", "master", "feature/advanced", worktreePath);

        var calls = new List<(string FileName, string Arguments)>();
        WorktreeCommand.RunProcessOverride = (f, a) => calls.Add((f, a));
        WorktreeCommand.RunProcessWithExitCodeOverride = (f, a) =>
        {
            calls.Add((f, a));
            return 0;
        };
        try
        {
            var exitCode = WorktreeCommand.ExecuteMerge(finalize: false, _registry);

            Assert.Equal(0, exitCode);
            Assert.Contains(calls, c => c.FileName == "git" && c.Arguments.Contains("merge --no-edit -- feature/advanced"));
        }
        finally
        {
            WorktreeCommand.RunProcessOverride = null;
            WorktreeCommand.RunProcessWithExitCodeOverride = null;
        }
    }

    [Fact]
    public void ExecuteMerge_BranchNotAdvanced_Blocks_BeforeGitMerge()
    {
        InitRepoWithUnadvancedBranch(out var worktreePath);
        SetupMergeAgent("Adele", "master", "feature/same", worktreePath);

        var calls = new List<(string FileName, string Arguments)>();
        WorktreeCommand.RunProcessOverride = (f, a) => calls.Add((f, a));
        WorktreeCommand.RunProcessWithExitCodeOverride = (f, a) =>
        {
            calls.Add((f, a));
            return 0;
        };
        try
        {
            var (exitCode, _, stderr) = CaptureAll(() => WorktreeCommand.ExecuteMerge(finalize: false, _registry));

            Assert.NotEqual(0, exitCode);
            Assert.DoesNotContain(calls, c => c.FileName == "git" && c.Arguments.Contains("merge --no-edit"));
            Assert.Contains("0 commits ahead", stderr);
        }
        finally
        {
            WorktreeCommand.RunProcessOverride = null;
            WorktreeCommand.RunProcessWithExitCodeOverride = null;
        }
    }

    private void InitRepoWithAdvancedBranch(out string worktreePath)
    {
        Git(_repoDir, "init", "--initial-branch=master");
        Git(_repoDir, "config", "user.email", "test@example.com");
        Git(_repoDir, "config", "user.name", "Test");
        File.WriteAllText(Path.Combine(_repoDir, "seed.txt"), "seed");
        Git(_repoDir, "add", "seed.txt");
        Git(_repoDir, "commit", "-m", "seed");

        Git(_repoDir, "branch", "feature/advanced");
        Git(_repoDir, "checkout", "feature/advanced");
        File.WriteAllText(Path.Combine(_repoDir, "feature.txt"), "feature work");
        Git(_repoDir, "add", "feature.txt");
        Git(_repoDir, "commit", "-m", "feature commit");
        Git(_repoDir, "checkout", "master");

        worktreePath = Path.Combine(_testDir, "wt-advanced");
        Git(_repoDir, "worktree", "add", worktreePath, "feature/advanced");
    }

    private void InitRepoWithUnadvancedBranch(out string worktreePath)
    {
        Git(_repoDir, "init", "--initial-branch=master");
        Git(_repoDir, "config", "user.email", "test@example.com");
        Git(_repoDir, "config", "user.name", "Test");
        File.WriteAllText(Path.Combine(_repoDir, "seed.txt"), "seed");
        Git(_repoDir, "add", "seed.txt");
        Git(_repoDir, "commit", "-m", "seed");

        Git(_repoDir, "branch", "feature/same");
        worktreePath = Path.Combine(_testDir, "wt-same");
        Git(_repoDir, "worktree", "add", worktreePath, "feature/same");
    }

    private static void Git(string cwd, params string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = cwd,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        var p = Process.Start(psi) ?? throw new InvalidOperationException("git failed to start");
        p.WaitForExit(30_000);
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"git {string.Join(' ', args)} failed in {cwd}: {p.StandardError.ReadToEnd()}");
    }

    private void SetupMergeAgent(string agentName, string worktreeBase, string mergeSource, string worktreePath)
    {
        var workspace = _registry.GetAgentWorkspace(agentName);
        Directory.CreateDirectory(workspace);

        File.WriteAllText(Path.Combine(workspace, "state.md"), $"""
            ---
            status: working
            role: code-writer
            task: merge-task
            ---
            """);

        File.WriteAllText(Path.Combine(workspace, ".session"),
            $"{{\"Agent\":\"{agentName}\",\"SessionId\":\"test-session\"}}");
        var agentsDir = Path.Combine(_testDir, "dydo", "agents");
        Directory.CreateDirectory(agentsDir);
        File.WriteAllText(Path.Combine(agentsDir, ".session-context"), "test-session");

        File.WriteAllText(Path.Combine(workspace, ".worktree-base"), worktreeBase);
        File.WriteAllText(Path.Combine(workspace, ".merge-source"), mergeSource);
        File.WriteAllText(Path.Combine(workspace, ".worktree-root"), _repoDir);
        File.WriteAllText(Path.Combine(workspace, ".worktree-path"), worktreePath);
    }

    private static (int exitCode, string stdout, string stderr) CaptureAll(Func<int> action)
    {
        var outWriter = new StringWriter();
        var errWriter = new StringWriter();
        var origOut = Console.Out;
        var origErr = Console.Error;
        try
        {
            Console.SetOut(outWriter);
            Console.SetError(errWriter);
            var code = action();
            return (code, outWriter.ToString(), errWriter.ToString());
        }
        finally
        {
            Console.SetOut(origOut);
            Console.SetError(origErr);
        }
    }
}
