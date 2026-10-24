namespace DynaDocs.Tests.Integration;

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using DynaDocs.Commands;
using DynaDocs.Services;

/// <summary>
/// <c>dydo clean</c> on real temp repositories: a bare <c>origin</c>, a main checkout and linked worktrees
/// under <c>main/.claude/worktrees/</c>, the layout agents use.
/// </summary>
[Collection("Integration")]
public sealed class CleanCommandTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dydo-clean-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string _originalDirectory = Environment.CurrentDirectory;
    private readonly string _main;
    private readonly string _t3Root;

    public CleanCommandTests()
    {
        _main = Path.Combine(_root, "main");
        _t3Root = Path.Combine(_root, "t3-worktrees");
        Directory.CreateDirectory(_main);
        Git(_root, "init", "--bare", "-b", "master", "origin.git");
        Git(_main, "init", "-b", "master");
        Git(_main, "config", "user.name", "Test");
        Git(_main, "config", "user.email", "test@example.com");
        Git(_main, "config", "commit.gpgsign", "false");
        File.WriteAllText(Path.Combine(_main, ".gitignore"), ".claude/worktrees/\n.claude/skills/\nnode_modules/\nbin/\ndydo/agents/\n");
        File.WriteAllText(Path.Combine(_main, "readme.md"), "hello\n");
        File.WriteAllBytes(Path.Combine(_main, "logo.bin"), [0, 1, 2, 3, 0, 255]);
        Commit(_main, "seed");
        Git(_main, "remote", "add", "origin", Path.Combine(_root, "origin.git"));
        Git(_main, "push", "-u", "origin", "master");
        Git(_main, "remote", "set-head", "origin", "master");
        Environment.CurrentDirectory = _main;
    }

    public void Dispose()
    {
        Environment.CurrentDirectory = _originalDirectory;
        LinkSafeDelete.DeleteTree(_root);
    }

    [Fact]
    public async Task Merged_RemovesTheTreeAndDeletesTheBranch()
    {
        var tree = AddWorktree("feature");
        File.WriteAllText(Path.Combine(tree, "feature.txt"), "done\n");
        Commit(tree, "feature");
        // Merged on origin only: the main checkout's master is stale, so git branch -d alone refuses.
        Git(tree, "push", "origin", "feature:master");

        var (exit, stdout, stderr) = await Clean("feature");

        Assert.Equal(0, exit);
        Assert.Contains($"removed {tree}; branch feature deleted (merged into origin/master)", stdout);
        Assert.DoesNotContain("WARNING", stderr);
        Assert.False(Directory.Exists(tree));
        Assert.False(HasBranch("feature"));
        Assert.DoesNotContain("feature", Git(_main, "worktree", "list").Text);
    }

    [Fact]
    public async Task PushedButUnmerged_KeepsTheBranchAndTheRemoteBranch()
    {
        var tree = AddWorktree("feature");
        File.WriteAllText(Path.Combine(tree, "feature.txt"), "wip\n");
        Commit(tree, "wip");
        Git(tree, "push", "-u", "origin", "feature");

        var (exit, stdout, _) = await Clean(tree);

        Assert.Equal(0, exit);
        Assert.Contains($"removed {tree}; branch feature kept (not merged into origin/master)", stdout);
        Assert.False(Directory.Exists(tree));
        Assert.True(HasBranch("feature"));
        Assert.Contains("refs/heads/feature", Git(_main, "ls-remote", "origin").Text);
    }

    [Fact]
    public async Task NoOriginRemote_KeepsTheBranch()
    {
        Git(_main, "remote", "remove", "origin");
        var tree = AddWorktree("feature");

        var (exit, stdout, _) = await Clean("feature");

        Assert.Equal(0, exit);
        Assert.Contains($"removed {tree}; branch feature kept (no origin remote)", stdout);
        Assert.True(HasBranch("feature"));
    }

    [Fact]
    public async Task FailedFetch_KeepsTheBranch()
    {
        Git(_main, "remote", "set-url", "origin", Path.Combine(_root, "gone.git"));
        AddWorktree("feature");

        var (exit, stdout, _) = await Clean("feature");

        Assert.Equal(0, exit);
        Assert.Contains("branch feature kept (git fetch --prune origin failed:", stdout);
        Assert.True(HasBranch("feature"));
    }

    [Fact]
    public async Task WithoutOriginHead_FallsBackToOriginMaster()
    {
        Git(_main, "remote", "set-head", "origin", "--delete");
        Git(_main, "config", "remote.origin.followRemoteHEAD", "never"); // git 2.48+ would restore origin/HEAD on fetch
        AddWorktree("feature");

        var (exit, stdout, _) = await Clean("feature");

        Assert.Equal(0, exit);
        Assert.Contains("branch feature deleted (merged into origin/master)", stdout);
    }

    [Fact]
    public async Task OriginWithoutADefaultBranch_KeepsTheBranch()
    {
        Git(_main, "remote", "set-head", "origin", "--delete");
        Git(_main, "config", "remote.origin.followRemoteHEAD", "never"); // git 2.48+ would restore origin/HEAD on fetch
        Git(_main, "branch", "-m", "master", "trunk");
        Git(_main, "push", "origin", "trunk");
        // origin's HEAD names a branch that does not exist, so origin advertises no default.
        Git(Path.Combine(_root, "origin.git"), "symbolic-ref", "HEAD", "refs/heads/nothing");
        Git(_main, "push", "origin", "--delete", "master");
        AddWorktree("feature");

        var (exit, stdout, _) = await Clean("feature");

        Assert.Equal(0, exit);
        Assert.Contains("branch feature kept (origin has no default branch", stdout);
    }

    [Fact]
    public async Task DefaultBranchChangedOnOrigin_IsJudgedAgainstTheNewDefault_NotAStaleOriginHead()
    {
        Git(_main, "config", "remote.origin.followRemoteHEAD", "never"); // keep the local origin/HEAD stale
        var tree = AddWorktree("feature");
        File.WriteAllText(Path.Combine(tree, "feature.txt"), "done\n");
        Commit(tree, "feature");
        Git(tree, "push", "origin", "feature:master");
        Git(_main, "push", "origin", "HEAD:refs/heads/main"); // main lacks the feature commit
        Git(Path.Combine(_root, "origin.git"), "symbolic-ref", "HEAD", "refs/heads/main");

        var (exit, stdout, _) = await Clean("feature");

        Assert.Equal(0, exit);
        Assert.Contains("branch feature kept (not merged into origin/main)", stdout);
        Assert.True(HasBranch("feature"));
        Assert.Equal("origin/master", Git(_main, "symbolic-ref", "--short", "refs/remotes/origin/HEAD").Text.Trim());
    }

    [Fact]
    public void BranchFallback_DeletesOnlyTheVerifiedTip_AMovedRefIsKept()
    {
        var tree = AddWorktree("feature");
        File.WriteAllText(Path.Combine(tree, "feature.txt"), "done\n");
        Commit(tree, "feature");
        var verified = Git(tree, "rev-parse", "HEAD").Text.Trim();
        Git(tree, "push", "origin", "feature:master"); // merged on origin; the main checkout's master is stale
        File.WriteAllText(Path.Combine(tree, "late.txt"), "after the verdict\n");
        Commit(tree, "late");
        var moved = Git(tree, "rev-parse", "HEAD").Text.Trim();
        Git(_main, "worktree", "remove", tree);

        var deleted = WorktreeCleaner.DeleteMergedBranch(_main, "feature", verified);

        Assert.False(deleted);
        Assert.Equal(moved, Git(_main, "rev-parse", "refs/heads/feature").Text.Trim());
    }

    [Fact]
    public async Task UnpushedCommits_KeepTheBranchAtItsTip()
    {
        var tree = AddWorktree("feature");
        File.WriteAllText(Path.Combine(tree, "local.txt"), "only here\n");
        Commit(tree, "local only");
        var tip = Git(tree, "rev-parse", "HEAD").Text.Trim();

        var (exit, stdout, _) = await Clean("feature");

        Assert.Equal(0, exit);
        Assert.Contains("branch feature kept (not merged into origin/master)", stdout);
        Assert.Equal(tip, Git(_main, "rev-parse", "refs/heads/feature").Text.Trim());
    }

    [Fact]
    public async Task DirtyTrackedChanges_AreArchivedAsAPatchThatReapplies()
    {
        var tree = AddWorktree("feature");
        File.WriteAllText(Path.Combine(tree, "readme.md"), "hello\nstaged line\n");
        Git(tree, "add", "readme.md");
        File.WriteAllBytes(Path.Combine(tree, "logo.bin"), [9, 0, 8, 0, 7]);
        var head = Git(tree, "rev-parse", "HEAD").Text.Trim();

        var (exit, stdout, stderr) = await Clean("feature");

        Assert.Equal(0, exit);
        var entry = ArchiveEntries().Single();
        Assert.Contains($"archived and removed {tree} -> {entry}", stdout);
        Assert.Contains("WARNING:", stderr);
        Assert.Contains("tracked    readme.md", stderr);
        Assert.Contains("tracked    logo.bin", stderr);
        Assert.Contains($"Archive: {entry}", stderr);
        Assert.False(Directory.Exists(tree));

        var fresh = Path.Combine(_root, "fresh");
        Git(_main, "worktree", "add", "--detach", fresh, head);
        Git(fresh, "apply", "--whitespace=nowarn", Path.Combine(entry, "tracked.patch"));
        Assert.Equal("hello\nstaged line\n", File.ReadAllText(Path.Combine(fresh, "readme.md")).Replace("\r\n", "\n"));
        Assert.Equal([9, 0, 8, 0, 7], File.ReadAllBytes(Path.Combine(fresh, "logo.bin")));

        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(entry, "manifest.json")));
        Assert.Equal(head, manifest.RootElement.GetProperty("head").GetString());
        Assert.Equal("feature", manifest.RootElement.GetProperty("branch").GetString());
        Assert.Equal(2, manifest.RootElement.GetProperty("files").GetArrayLength());
    }

    [Fact]
    public async Task UntrackedFilesAndEvidence_AreCopied_IgnoredBuildOutputIsDropped()
    {
        var tree = AddWorktree("feature");
        Write(tree, "notes/todo.txt", "remember\n");
        Write(tree, "dydo/agents/captain/evidence.md", "proof\n");
        Write(tree, "bin/Debug/app.dll", "binary\n");

        var (exit, _, stderr) = await Clean("feature");

        Assert.Equal(0, exit);
        var entry = ArchiveEntries().Single();
        Assert.Equal("remember\n", File.ReadAllText(Path.Combine(entry, "untracked", "notes", "todo.txt")));
        Assert.Equal("proof\n", File.ReadAllText(Path.Combine(entry, "evidence", "dydo", "agents", "captain", "evidence.md")));
        Assert.False(File.Exists(Path.Combine(entry, "tracked.patch")));
        Assert.DoesNotContain("app.dll", stderr);
        Assert.Contains("untracked  notes/todo.txt", stderr);
        Assert.Contains("evidence   dydo/agents/captain/evidence.md", stderr);
        Assert.Contains("2 file(s)", await ListOutput());
    }

    [Fact]
    public async Task Archive_ListsEntriesNewestFirst()
    {
        var older = MakeArchiveEntry(DateTime.UtcNow.AddDays(-2), "old", 10);
        var newer = MakeArchiveEntry(DateTime.UtcNow.AddDays(-1), "new", 10);

        var list = await ListOutput();

        Assert.True(list.IndexOf(Path.GetFileName(newer), StringComparison.Ordinal)
            < list.IndexOf(Path.GetFileName(older), StringComparison.Ordinal), list);
        Assert.Contains("git apply", list);
    }

    [Fact]
    public async Task Retention_PrunesEntriesOlderThanNinetyDays()
    {
        var old = MakeArchiveEntry(DateTime.UtcNow.AddDays(-100), "old", 10);
        var recent = MakeArchiveEntry(DateTime.UtcNow.AddDays(-10), "recent", 10);

        var (exit, stdout, _) = await Clean("--all");

        Assert.Equal(0, exit);
        Assert.False(Directory.Exists(old));
        Assert.True(Directory.Exists(recent));
        Assert.Contains($"pruned archive entry {Path.GetFileName(old)} (older than 90 days)", stdout);
    }

    [Fact]
    public async Task Retention_PrunesTheOldestEntriesPastTheSizeCap()
    {
        File.WriteAllText(Path.Combine(_main, "dydo.json"), """{ "clean": { "archiveSizeCapMb": 1 } }""");
        var oldest = MakeArchiveEntry(DateTime.UtcNow.AddDays(-3), "a", 400_000);
        var middle = MakeArchiveEntry(DateTime.UtcNow.AddDays(-2), "b", 400_000);
        var newest = MakeArchiveEntry(DateTime.UtcNow.AddDays(-1), "c", 400_000);

        var (exit, stdout, _) = await Clean("--all");

        Assert.Equal(0, exit);
        Assert.False(Directory.Exists(oldest));
        Assert.True(Directory.Exists(middle));
        Assert.True(Directory.Exists(newest));
        Assert.Contains("archive over its 1 MB cap", stdout);
    }

    [Fact]
    public async Task InvalidSizeCap_IsRefused()
    {
        File.WriteAllText(Path.Combine(_main, "dydo.json"), """{ "clean": { "archiveSizeCapMb": 0 } }""");
        var tree = AddWorktree("feature");

        var (exit, _, stderr) = await Clean("--all");

        Assert.Equal(2, exit);
        Assert.Contains("clean.archiveSizeCapMb", stderr);
        Assert.True(Directory.Exists(tree));
    }

    [Fact]
    public async Task LinkInsideTheTree_IsUnlinked_ItsTargetSurvives()
    {
        var tree = AddWorktree("feature");
        var outside = Path.Combine(_root, "outside");
        Write(outside, "keep.txt", "keep me\n");
        DirectoryLinks.Create(Path.Combine(tree, ".claude", "skills"), outside);

        var (exit, stdout, _) = await Clean("feature");

        Assert.Equal(0, exit);
        Assert.Contains($"removed {tree}", stdout);
        Assert.False(Directory.Exists(tree));
        Assert.Equal("keep me\n", File.ReadAllText(Path.Combine(outside, "keep.txt")));
    }

    [Fact]
    public async Task UntrackedLink_IsListedButNotFollowed()
    {
        var tree = AddWorktree("feature");
        var outside = Path.Combine(_root, "outside");
        Write(outside, "secret.txt", "outside the tree");
        DirectoryLinks.Create(Path.Combine(tree, "linked"), outside);

        var (exit, _, stderr) = await Clean("feature");

        Assert.Equal(0, exit);
        var entry = ArchiveEntries().Single();
        Assert.Contains("link       linked", stderr);
        Assert.Empty(Directory.GetFiles(entry, "secret.txt", SearchOption.AllDirectories));
        Assert.Equal("outside the tree", File.ReadAllText(Path.Combine(outside, "secret.txt")));
    }

    [Fact]
    public async Task UntrackedLink_IsRecordedWithItsTarget_SoItCanBeRecreated()
    {
        var tree = AddWorktree("feature");
        var outside = Path.Combine(_root, "outside");
        Write(outside, "secret.txt", "outside the tree");
        DirectoryLinks.Create(Path.Combine(tree, "linked"), outside);

        var (exit, _, stderr) = await Clean("feature");

        Assert.Equal(0, exit);
        var entry = ArchiveEntries().Single();
        Assert.Contains($"link       linked -> {outside}", stderr);
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(entry, "manifest.json")));
        var link = manifest.RootElement.GetProperty("links").EnumerateArray().Single();
        Assert.Equal("linked", link.GetProperty("path").GetString());
        Assert.Equal("directory", link.GetProperty("linksTo").GetString());
        Assert.Equal(Path.TrimEndingDirectorySeparator(outside), Path.TrimEndingDirectorySeparator(link.GetProperty("target").GetString()!));
        var list = await ListOutput();
        Assert.Contains("1 link(s)", list);
        Assert.Contains("recreate each link", list);
    }

    [Fact]
    public async Task PathLongerThan260Characters_IsRemoved()
    {
        var tree = AddWorktree("feature");
        var deep = Path.Combine(tree, "node_modules", string.Join(Path.DirectorySeparatorChar, Enumerable.Repeat("segment-of-forty-characters-long-xxxxxx", 8)));
        Assert.True(deep.Length > 260);
        Directory.CreateDirectory(LinkSafeDelete.Extended(deep));
        File.WriteAllText(LinkSafeDelete.Extended(Path.Combine(deep, "file.js")), "x");

        var (exit, stdout, stderr) = await Clean("feature");

        Assert.True(exit == 0, stdout + stderr);
        Assert.False(Directory.Exists(LinkSafeDelete.Extended(tree)));
        Assert.DoesNotContain("feature", Git(_main, "worktree", "list").Text);
    }

    [Fact]
    public async Task MainCheckout_IsRefused()
    {
        var (exit, stdout, _) = await Clean(_main);

        Assert.Equal(2, exit);
        Assert.Contains("the main checkout is never removed", stdout);
        Assert.True(File.Exists(Path.Combine(_main, "readme.md")));
    }

    [Fact]
    public async Task TreeHoldingTheCurrentDirectory_IsRefused()
    {
        var tree = AddWorktree("feature");
        var inside = Path.Combine(tree, "sub");
        Directory.CreateDirectory(inside);
        Environment.CurrentDirectory = inside;

        var (exit, stdout, _) = await Clean(tree);

        Environment.CurrentDirectory = _main;
        Assert.Equal(2, exit);
        Assert.Contains($"skipped {tree}: the current directory is inside it", stdout);
        Assert.True(Directory.Exists(tree));
    }

    [Fact]
    public async Task All_SkipsTheMainCheckout_LockedTrees_AndT3Trees()
    {
        var locked = AddWorktree("locked");
        Git(_main, "worktree", "lock", locked);
        Directory.CreateDirectory(_t3Root);
        var t3 = Path.Combine(_t3Root, "thread");
        Git(_main, "worktree", "add", "-b", "thread", t3);
        var plain = AddWorktree("plain");

        var (exit, stdout, _) = await Clean("--all");

        Assert.Equal(0, exit);
        Assert.Contains($"skipped {_main}: the main checkout is never removed", stdout);
        Assert.Contains($"skipped {locked}: it is locked", stdout);
        Assert.Contains($"skipped {t3}: T3 owns worktrees under {_t3Root}", stdout);
        Assert.Contains($"removed {plain}", stdout);
        Assert.True(Directory.Exists(locked));
        Assert.True(Directory.Exists(t3));
        Assert.False(Directory.Exists(plain));
    }

    [Fact]
    public async Task RunningProcessInside_IsSkipped()
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
            return; // documented: macOS has no busy-worktree detection
        var tree = AddWorktree("feature");
        var start = new ProcessStartInfo("git", "hash-object --stdin")
        {
            WorkingDirectory = tree,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        using var process = Process.Start(start)!;
        try
        {
            var (exit, stdout, _) = await Clean("feature");

            Assert.Equal(2, exit);
            Assert.Contains($"skipped {tree}: a running process", stdout);
            Assert.True(Directory.Exists(tree));
        }
        finally
        {
            process.Kill();
            process.WaitForExit();
        }
    }

    [Fact]
    public async Task DryRun_PrintsTheSameDecisions_AndChangesNothing()
    {
        var tree = AddWorktree("feature");
        Write(tree, "notes.txt", "draft\n");
        var old = MakeArchiveEntry(DateTime.UtcNow.AddDays(-100), "old", 10);

        var (exit, stdout, stderr) = await Clean("--all", "--dry-run");

        Assert.Equal(0, exit);
        Assert.Contains($"would archive and remove {tree} -> ", stdout);
        Assert.Contains("branch feature would be deleted (merged into origin/master as of the last fetch)", stdout);
        Assert.Contains($"would prune archive entry {Path.GetFileName(old)}", stdout);
        Assert.Contains("would archive", stderr);
        Assert.Contains("untracked  notes.txt", stderr);
        Assert.True(File.Exists(Path.Combine(tree, "notes.txt")));
        Assert.True(HasBranch("feature"));
        Assert.Equal([old], ArchiveEntries());
    }

    [Fact]
    public async Task DryRun_RunsNoBusyCheck_AndSaysSo()
    {
        var tree = AddWorktree("feature");
        var probed = new List<string>();
        var cleaner = new WorktreeCleaner(_main, _t3Root, path => { probed.Add(path); return null; });

        var (exit, stdout, _) = await ConsoleCapture.AllAsync(() => Task.FromResult(cleaner.Clean(["feature"], all: false, dryRun: true)));

        Assert.Equal(0, exit);
        Assert.Empty(probed);
        Assert.Contains($"would remove {tree}", stdout);
        Assert.Contains("busy check not run in a dry run", stdout);
        Assert.True(Directory.Exists(tree));
    }

    [Fact]
    public async Task BusyProbeThatCannotMoveTheTreeBack_FailsThatTree_LocksIt_AndTheRunGoesOn()
    {
        var stranded = AddWorktree("stranded");
        var other = AddWorktree("other");
        void MoveBackFails(string from, string to)
        {
            if (to == stranded)
                throw new IOException("the folder is held");
            Directory.Move(from, to);
        }

        var cleaner = new WorktreeCleaner(_main, _t3Root,
            path => path == stranded ? WorktreeProcessProbe.RenameProbe(path, MoveBackFails, TimeSpan.Zero) : null);

        var (exit, stdout, _) = await ConsoleCapture.AllAsync(() => Task.FromResult(cleaner.Clean(["stranded", "other"], all: false, dryRun: false)));

        Assert.Equal(2, exit);
        var probe = Directory.GetDirectories(Path.GetDirectoryName(stranded)!, "stranded.dydo-clean-probe-*").Single();
        Assert.Contains($"failed {stranded}: the busy check renamed it to {probe} and could not move it back (the folder is held)", stdout);
        Assert.Contains($"Move-Item -LiteralPath '{probe}' -Destination '{stranded}', then git worktree unlock '{stranded}'", stdout);
        Assert.Contains($"removed {other}", stdout);
        Assert.False(Directory.Exists(other));
        var list = Git(_main, "worktree", "list", "--porcelain").Text;
        Assert.Contains("branch refs/heads/stranded\nlocked", list.ReplaceLineEndings("\n"));
        Assert.True(File.Exists(Path.Combine(probe, "readme.md")));
    }

    [Theory]
    [InlineData]
    [InlineData("--all", "feature")]
    public async Task NeitherOrBothTargetForms_IsAUsageError(params string[] args)
    {
        var (exit, _, stderr) = await Clean(args);

        Assert.Equal(2, exit);
        Assert.Contains("name the worktree to clean", stderr);
        Assert.Contains("dydo clean --dry-run <worktree>, then dydo clean <worktree>", stderr);
        Assert.DoesNotContain("or pass --all", stderr);
    }

    [Fact]
    public async Task UnknownTarget_IsRefused()
    {
        var (exit, stdout, _) = await Clean("no-such-tree");

        Assert.Equal(2, exit);
        Assert.Contains("skipped no-such-tree: not a worktree of this repository", stdout);
    }

    private async Task<(int Exit, string Stdout, string Stderr)> Clean(params string[] args) =>
        await ConsoleCapture.AllAsync(() => CleanCommand.Create(_t3Root).Parse(args).InvokeAsync());

    private async Task<string> ListOutput() => (await Clean("--archive")).Stdout;

    private string AddWorktree(string branch)
    {
        var path = Path.Combine(_main, ".claude", "worktrees", branch);
        Git(_main, "worktree", "add", "-b", branch, path);
        return path;
    }

    private string ArchiveRoot => Path.Combine(_main, ".git", "dydo", "clean-archive");

    private List<string> ArchiveEntries() =>
        Directory.Exists(ArchiveRoot) ? [.. Directory.GetDirectories(ArchiveRoot).Order(StringComparer.Ordinal)] : [];

    private string MakeArchiveEntry(DateTime utc, string label, int bytes)
    {
        var path = Path.Combine(ArchiveRoot, utc.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture) + "-" + label);
        Directory.CreateDirectory(path);
        File.WriteAllBytes(Path.Combine(path, "tracked.patch"), new byte[bytes]);
        return path;
    }

    private static void Write(string root, string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static void Commit(string directory, string message)
    {
        Git(directory, "add", "-A");
        Git(directory, "commit", "-m", message);
    }

    private bool HasBranch(string branch) =>
        GitRunner.Run(_main, "rev-parse", "--verify", "--quiet", $"refs/heads/{branch}").Ok;

    private static GitResult Git(string directory, params string[] args)
    {
        var result = GitRunner.Run(directory, args);
        Assert.True(result.Ok, $"git {string.Join(' ', args)} failed: {result.Error}");
        return result;
    }
}
