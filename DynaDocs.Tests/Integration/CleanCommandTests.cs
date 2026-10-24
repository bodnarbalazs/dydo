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
    public async Task SquashMerged_MultiCommitBranch_IsDeleted()
    {
        var tree = AddWorktree("feature");
        Write(tree, "one.txt", "first\n");
        Commit(tree, "one");
        Write(tree, "two.txt", "second\n");
        Commit(tree, "two");
        SquashIntoOriginMaster("feature");
        Write(_main, "later.txt", "landed after the squash\n");
        Commit(_main, "later");
        Git(_main, "push", "origin", "master");

        var (exit, stdout, _) = await Clean("feature");

        Assert.Equal(0, exit);
        Assert.Contains($"removed {tree}; branch feature deleted (no changes left to merge into origin/master)", stdout);
        Assert.False(HasBranch("feature"));
    }

    [Fact]
    public async Task SquashMergedThenAnUnmergedCommit_KeepsTheBranch()
    {
        var tree = AddWorktree("feature");
        Write(tree, "one.txt", "first\n");
        Commit(tree, "one");
        SquashIntoOriginMaster("feature");
        Write(tree, "after.txt", "not on origin\n");
        Commit(tree, "after the squash");
        var tip = Git(tree, "rev-parse", "HEAD").Text.Trim();

        var (exit, stdout, _) = await Clean("feature");

        Assert.Equal(0, exit);
        Assert.Contains("branch feature kept (not merged into origin/master)", stdout);
        Assert.Equal(tip, Git(_main, "rev-parse", "refs/heads/feature").Text.Trim());
    }

    [Fact]
    public async Task RebaseMerged_IsDeleted()
    {
        var tree = AddWorktree("feature");
        Write(tree, "one.txt", "first\n");
        Commit(tree, "one");
        Write(tree, "two.txt", "second\n");
        Commit(tree, "two");
        var fork = Git(_main, "rev-parse", "HEAD").Text.Trim();
        Write(_main, "other.txt", "moved master on\n");
        Commit(_main, "other");
        Git(_main, "cherry-pick", $"{fork}..feature");
        Git(_main, "push", "origin", "master");

        var (exit, stdout, _) = await Clean("feature");

        Assert.Equal(0, exit);
        Assert.Contains("branch feature deleted (no changes left to merge into origin/master)", stdout);
        Assert.False(HasBranch("feature"));
    }

    // A "keep mine" merge driver would resolve the branch's edit away and make the merge look empty.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConfiguredKeepMineMergeDriver_DoesNotHideAnUnmergedChange(bool committedAttributes)
    {
        Write(_main, "F", "1\n2\n3\n4\n5\n6\n7\n8\n");
        if (committedAttributes)
            Write(_main, ".gitattributes", "F merge=keep\n");
        else
            Write(_main, Path.Combine(".git", "info", "attributes"), "F merge=keep\n");
        Commit(_main, "eight lines");
        Git(_main, "push", "origin", "master");
        Git(_main, "config", "merge.keep.driver", "true");
        var tree = AddWorktree("feature");
        Write(tree, "F", "one\n2\n3\n4\n5\n6\n7\n8\n");
        Commit(tree, "line one");
        Write(_main, "F", "1\n2\n3\n4\n5\n6\n7\neight\n");
        Commit(_main, "line eight");
        Git(_main, "push", "origin", "master");

        var (exit, stdout, _) = await Clean("feature");

        Assert.Equal(0, exit);
        Assert.Contains("branch feature kept (not merged into origin/master)", stdout);
        Assert.True(HasBranch("feature"));
    }

    // Config dydo's own environment carries (GIT_CONFIG_COUNT or GIT_CONFIG_PARAMETERS) must not reach the squash check.
    [Theory]
    [InlineData("count", true)]
    [InlineData("count", false)]
    [InlineData("parameters", true)]
    [InlineData("parameters", false)]
    public async Task KeepMineMergeDriverFromTheEnvironment_DoesNotHideAnUnmergedChange(string source, bool dryRun)
    {
        Write(_main, "F", "1\n2\n3\n4\n5\n6\n7\n8\n");
        Commit(_main, "eight lines");
        Git(_main, "push", "origin", "master");
        var tree = AddWorktree("feature");
        Write(tree, "F", "one\n2\n3\n4\n5\n6\n7\n8\n");
        Commit(tree, "line one");
        Write(_main, "F", "1\n2\n3\n4\n5\n6\n7\neight\n");
        Commit(_main, "line eight");
        Git(_main, "push", "origin", "master");
        var keepMine = source == "count"
            ? new Dictionary<string, string?>
            {
                ["GIT_CONFIG_COUNT"] = "2",
                ["GIT_CONFIG_KEY_0"] = "merge.default",
                ["GIT_CONFIG_VALUE_0"] = "keep",
                ["GIT_CONFIG_KEY_1"] = "merge.keep.driver",
                ["GIT_CONFIG_VALUE_1"] = "true"
            }
            : new Dictionary<string, string?>
            {
                ["GIT_CONFIG_PARAMETERS"] = "'merge.default'='keep' 'merge.keep.driver'='true'"
            };

        var (exit, stdout, _) = await WithEnvironment(keepMine, () => dryRun ? Clean("feature", "--dry-run") : Clean("feature"));

        Assert.Equal(0, exit);
        Assert.Contains(dryRun ? "branch feature would be kept (not merged" : "branch feature kept (not merged", stdout);
        Assert.True(HasBranch("feature"));
    }

    // A driver a committed .gitattributes names but nothing defines falls back to git's own merge.
    [Fact]
    public async Task UndefinedMergeDriverInCommittedAttributes_StillJudgesBySquash()
    {
        Write(_main, ".gitattributes", "F merge=nowhere\n");
        Write(_main, "F", "1\n2\n");
        Commit(_main, "attributes");
        Git(_main, "push", "origin", "master");
        var tree = AddWorktree("feature");
        Write(tree, "F", "one\n2\n");
        Commit(tree, "line one");
        SquashIntoOriginMaster("feature");

        var (exit, stdout, _) = await Clean("feature");

        Assert.Equal(0, exit);
        Assert.Contains("branch feature deleted (no changes left to merge into origin/master)", stdout);
    }

    [Fact]
    public async Task SquashedThenRevertedOnOrigin_KeepsTheBranch()
    {
        var tree = AddWorktree("feature");
        Write(tree, "one.txt", "first\n");
        Commit(tree, "one");
        SquashIntoOriginMaster("feature");
        Git(_main, "revert", "--no-edit", "HEAD");
        Git(_main, "push", "origin", "master");

        var (exit, stdout, _) = await Clean("feature");

        Assert.Equal(0, exit);
        Assert.Contains("branch feature kept (not merged into origin/master)", stdout);
        Assert.True(HasBranch("feature"));
    }

    // Git splits GIT_ALTERNATE_OBJECT_DIRECTORIES on ';' on Windows, so the object store must not travel in it.
    [Fact]
    public async Task SquashMerged_InARepositoryWhosePathHoldsASemicolonAndASpace_IsDeleted()
    {
        var main = Path.Combine(_root, "odd; path", "main");
        Directory.CreateDirectory(Path.GetDirectoryName(main)!);
        Git(_root, "clone", Path.Combine(_root, "origin.git"), main);
        Git(main, "config", "user.name", "Test");
        Git(main, "config", "user.email", "test@example.com");
        Git(main, "config", "commit.gpgsign", "false");
        var tree = Path.Combine(main, ".claude", "worktrees", "feature");
        Git(main, "worktree", "add", "-b", "feature", tree);
        Write(tree, "one.txt", "first\n");
        Commit(tree, "one");
        Git(main, "merge", "--squash", "feature");
        Git(main, "commit", "-m", "squash feature");
        Git(main, "push", "origin", "master");
        Environment.CurrentDirectory = main;

        var (exit, stdout, _) = await Clean("feature");

        Assert.Equal(0, exit);
        Assert.Contains($"removed {tree}; branch feature deleted (no changes left to merge into origin/master)", stdout);
        Assert.False(GitRunner.Run(main, "rev-parse", "--verify", "--quiet", "refs/heads/feature").Ok);
    }

    // One branch merges to origin's tree, the other to a new tree: neither verdict may write an object.
    [Fact]
    public async Task DryRun_JudgingASquash_WritesNoObject_AndMovesNoRef()
    {
        var squashed = AddWorktree("squashed");
        var extended = AddWorktree("extended");
        Write(squashed, "one.txt", "first\n");
        Commit(squashed, "one");
        SquashIntoOriginMaster("squashed");
        Write(_main, "later.txt", "landed after the squash\n");
        Commit(_main, "later");
        Git(_main, "push", "origin", "master");
        Write(extended, "one.txt", "first\n");
        Commit(extended, "one");
        Write(extended, "two.txt", "only here\n");
        Commit(extended, "two");
        var objects = Path.Combine(_main, ".git", "objects");
        var filesBefore = Directory.GetFiles(objects, "*", SearchOption.AllDirectories).Length;
        var refsBefore = Git(_main, "for-each-ref").Text;

        var (exit, stdout, _) = await Clean("squashed", "extended", "--dry-run");

        Assert.Equal(0, exit);
        Assert.Contains("branch squashed would be deleted (no changes left to merge into origin/master as of the last fetch)", stdout);
        Assert.Contains("branch extended would be kept (not merged into origin/master as of the last fetch)", stdout);
        Assert.Equal(filesBefore, Directory.GetFiles(objects, "*", SearchOption.AllDirectories).Length);
        Assert.Equal(refsBefore, Git(_main, "for-each-ref").Text);
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

    // An agent that built or installed inside its workspace leaves bin, obj, node_modules or .vs there.
    [Fact]
    public async Task BuildOutputUnderAgentsEvidence_IsSkippedAndCounted_RealEvidenceIsArchived()
    {
        var tree = AddWorktree("feature");
        Write(tree, "dydo/agents/crew/Bin/Debug/app.dll", "binary\n");
        Write(tree, "dydo/agents/crew/obj/project.assets.json", "{}\n");
        Write(tree, "dydo/agents/crew/web/node_modules/pkg/index.js", "module\n");
        Write(tree, "dydo/agents/crew/.VS/state.bin", "ide\n");
        Write(tree, "dydo/agents/crew/report.md", "proof\n");
        Write(tree, "dydo/agents/crew/state.json", "{}\n");
        Write(tree, "dydo/agents/crew/run.log", "log\n");
        Write(tree, "dydo/agents/crew/notes.txt", "notes\n");

        var (exit, stdout, stderr) = await Clean("feature");

        Assert.Equal(0, exit);
        Assert.Contains("4 build-output file(s) under agents/ not archived (bin, obj, node_modules, .vs)", stdout);
        Assert.Contains("  4 build-output file(s) under agents/ not archived (bin, obj, node_modules, .vs)", stderr);
        foreach (var dropped in new[] { "app.dll", "project.assets.json", "index.js", "state.bin" })
            Assert.DoesNotContain(dropped, stderr);
        var entry = ArchiveEntries().Single();
        foreach (var kept in new[] { "report.md", "state.json", "run.log", "notes.txt" })
        {
            Assert.Contains($"evidence   dydo/agents/crew/{kept}", stderr);
            Assert.True(File.Exists(Path.Combine(entry, "evidence", "dydo", "agents", "crew", kept)));
        }

        Assert.False(Directory.Exists(Path.Combine(entry, "evidence", "dydo", "agents", "crew", "Bin")));
        Assert.Contains("4 file(s)", await ListOutput());
    }

    // dydo.json at HEAD names one root, the tree's edited copy another, and the default holds evidence
    // too: every root's ignored agents files are archived.
    [Fact]
    public async Task ChangedDydoRoot_ArchivesTheEvidenceOfTheOldRoot_TheNewAndTheDefault()
    {
        var tree = AddWorktree("feature");
        Write(tree, ".gitignore", ".claude/\nold/agents/\nnew/agents/\ndydo/agents/\n");
        Write(tree, "dydo.json", """{ "structure": { "root": "old" } }""");
        Commit(tree, "root old");
        Write(tree, "dydo.json", """{ "structure": { "root": "new/" } }""");
        Write(tree, "old/agents/captain/before.md", "old\n");
        Write(tree, "new/agents/captain/after.md", "new\n");
        Write(tree, "dydo/agents/captain/default.md", "default\n");

        var (exit, stdout, stderr) = await Clean("feature");

        Assert.True(exit == 0, stdout + stderr);
        var evidence = Path.Combine(ArchiveEntries().Single(), "evidence");
        Assert.Equal("old\n", File.ReadAllText(Path.Combine(evidence, "old", "agents", "captain", "before.md")));
        Assert.Equal("new\n", File.ReadAllText(Path.Combine(evidence, "new", "agents", "captain", "after.md")));
        Assert.Equal("default\n", File.ReadAllText(Path.Combine(evidence, "dydo", "agents", "captain", "default.md")));
        Assert.Contains("evidence   old/agents/captain/before.md", stderr);
        Assert.False(Directory.Exists(tree));
    }

    // A dydo.json that cannot be parsed, at HEAD or in the tree, leaves the evidence folders unknown:
    // the tree is skipped, dry run included, and nothing is archived.
    [Theory]
    [InlineData("HEAD", false)]
    [InlineData("HEAD", true)]
    [InlineData("the working tree's", false)]
    [InlineData("the working tree's", true)]
    public async Task MalformedDydoJson_KeepsTheTree(string where, bool dryRun)
    {
        var tree = AddWorktree("feature");
        Write(tree, "dydo.json", "{ \"structure\": ");
        if (where == "HEAD")
        {
            Commit(tree, "broken config");
            Write(tree, "dydo.json", "{}");
        }

        Write(tree, "dydo/agents/captain/evidence.md", "proof\n");
        var source = where == "HEAD" ? "dydo.json at HEAD" : "the working tree's dydo.json";

        var (exit, stdout, _) = await Clean(dryRun ? ["feature", "--dry-run"] : ["feature"]);

        Assert.Equal(2, exit);
        Assert.Contains($"skipped {tree}: {source} cannot be parsed (", stdout);
        Assert.Contains("so the evidence folders to archive are unknown; branch feature kept (worktree not removed)", stdout);
        Assert.Equal("proof\n", File.ReadAllText(Path.Combine(tree, "dydo", "agents", "captain", "evidence.md")));
        Assert.Empty(ArchiveEntries());
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
        Assert.Contains($"Move-Item -LiteralPath '{probe}' -Destination '{stranded}', then git worktree unlock '{stranded}'; "
            + "branch stranded kept (worktree not removed)", stdout);
        Assert.Contains($"removed {other}", stdout);
        Assert.False(Directory.Exists(other));
        var list = Git(_main, "worktree", "list", "--porcelain").Text;
        Assert.Contains("branch refs/heads/stranded\nlocked", list.ReplaceLineEndings("\n"));
        Assert.True(File.Exists(Path.Combine(probe, "readme.md")));
    }

    [Theory]
    [InlineData("untracked")]
    [InlineData("modified")]
    [InlineData("unpushed")]
    public async Task SubmoduleHoldingWork_KeepsTheTree_AndNamesTheSubmodule(string work)
    {
        var tree = AddWorktree("feature");
        var submodule = AddSubmodule(tree);
        if (work == "untracked")
            Write(submodule, "draft.txt", "only here\n");
        else
            File.WriteAllText(Path.Combine(submodule, "lib.txt"), "changed\n");
        if (work == "unpushed")
            CommitAs(submodule, "local only");
        var problem = work == "unpushed" ? "has commits on no remote ref" : "has uncommitted or untracked changes";

        var (exit, stdout, _) = await Clean("feature");

        Assert.Equal(2, exit);
        Assert.Contains($"skipped {tree}: submodule lib {problem}; commit and push it, or clean it, first; "
            + "branch feature kept (worktree not removed)", stdout);
        Assert.True(Directory.Exists(submodule));
        Assert.True(HasBranch("feature"));
        Assert.Empty(ArchiveEntries());
    }

    [Fact]
    public async Task CleanPushedSubmodule_GoesWithTheTree()
    {
        var tree = AddWorktree("feature");
        AddSubmodule(tree);

        var (exit, stdout, stderr) = await Clean("feature");

        Assert.True(exit == 0, stdout + stderr);
        Assert.Contains($"removed {tree}", stdout);
        Assert.False(Directory.Exists(tree));
    }

    // HEAD is clean and pushed; the unpushed commit sits on a ref HEAD does not reach.
    [Theory]
    [InlineData("branch")]
    [InlineData("tag")]
    [InlineData("stash")]
    public async Task SubmoduleWithUnpushedWorkOffHead_KeepsTheTree(string where)
    {
        var tree = AddWorktree("feature");
        var submodule = AddSubmodule(tree);
        File.WriteAllText(Path.Combine(submodule, "lib.txt"), "side work\n");
        if (where == "stash")
            Git(submodule, "-c", "user.name=Test", "-c", "user.email=test@example.com", "stash");
        else
        {
            Git(submodule, "checkout", "-b", "side");
            CommitAs(submodule, "side work");
            if (where == "tag")
            {
                Git(submodule, "tag", "side-tag");
                Git(submodule, "checkout", "master");
                Git(submodule, "branch", "-D", "side");
            }
            else
                Git(submodule, "checkout", "master");
        }

        var (exit, stdout, _) = await Clean("feature");

        Assert.Equal(2, exit);
        Assert.Contains($"skipped {tree}: submodule lib has commits on no remote ref", stdout);
        Assert.True(Directory.Exists(submodule));
    }

    // The stat cache is stale (a tracked file touched, content unchanged) in the tree and its submodule,
    // so a git call that refreshes the index would rewrite it.
    [Fact]
    public async Task DryRun_LeavesTheIndexOfTheTreeAndItsSubmoduleUnchanged()
    {
        var tree = AddWorktree("feature");
        var submodule = AddSubmodule(tree);
        var indexes = new[] { tree, submodule }
            .Select(repository => Git(repository, "rev-parse", "--path-format=absolute", "--git-path", "index").Text.Trim())
            .ToList();
        var stale = DateTime.UtcNow.AddHours(-1);
        foreach (var index in indexes)
            File.SetLastWriteTimeUtc(index, stale.AddMinutes(-10));
        File.SetLastWriteTimeUtc(Path.Combine(tree, "readme.md"), stale);
        File.SetLastWriteTimeUtc(Path.Combine(submodule, "lib.txt"), stale);
        var before = indexes.Select(index => (File.ReadAllBytes(index), File.GetLastWriteTimeUtc(index))).ToList();

        var (exit, stdout, stderr) = await Clean("feature", "--dry-run");

        Assert.True(exit == 0, stdout + stderr);
        Assert.Contains($"would remove {tree}", stdout);
        Assert.DoesNotContain("WARNING", stderr);
        for (var i = 0; i < indexes.Count; i++)
        {
            Assert.Equal(before[i].Item1, File.ReadAllBytes(indexes[i]));
            Assert.Equal(before[i].Item2, File.GetLastWriteTimeUtc(indexes[i]));
        }
    }

    // The archive root, or the folder above it inside the git directory, links outside: nothing behind
    // the link is pruned, and no work is archived there.
    [Theory]
    [InlineData("clean-archive")]
    [InlineData("dydo")]
    public async Task ArchiveRootBehindALink_IsNeitherPrunedNorWritten(string linked)
    {
        var tree = AddWorktree("feature");
        Write(tree, "notes.txt", "draft\n");
        var outside = Path.Combine(_root, "outside");
        var outsideArchive = linked == "dydo" ? Path.Combine(outside, "clean-archive") : outside;
        var oldEntry = Path.Combine(outsideArchive, "20200101T000000Z-old");
        Write(oldEntry, "tracked.patch", "keep me\n");
        var gitDydo = Path.Combine(_main, ".git", "dydo");
        DirectoryLinks.Create(linked == "dydo" ? gitDydo : Path.Combine(gitDydo, "clean-archive"), outside);

        var (exit, stdout, _) = await Clean("feature");

        Assert.Equal(2, exit);
        Assert.Contains($"failed {tree}: archiving its work failed, so nothing was removed: ", stdout);
        Assert.Contains("is a link", stdout);
        Assert.Contains("archive not pruned: ", stdout);
        Assert.Equal("keep me\n", File.ReadAllText(Path.Combine(oldEntry, "tracked.patch")));
        Assert.Equal([oldEntry], Directory.GetDirectories(outsideArchive));
        Assert.Equal("draft\n", File.ReadAllText(Path.Combine(tree, "notes.txt")));
    }

    [Fact]
    public async Task SkippedTrees_SayTheirBranchIsKept()
    {
        var locked = AddWorktree("locked");
        Git(_main, "worktree", "lock", locked);
        var detached = Path.Combine(_main, ".claude", "worktrees", "detached");
        Git(_main, "worktree", "add", "--detach", detached);
        Git(_main, "worktree", "lock", detached);

        var (exit, stdout, _) = await Clean(locked, detached);

        Assert.Equal(2, exit);
        Assert.Contains($"skipped {locked}: it is locked; branch locked kept (worktree not removed)", stdout);
        Assert.Contains($"skipped {detached}: it is locked; no branch (detached HEAD)", stdout);
    }

    [Fact]
    public async Task ArchiveThatCannotBeWritten_FailsClosed_AndRemovesNothing()
    {
        var tree = AddWorktree("feature");
        Write(tree, "notes.txt", "draft\n");
        var outside = Path.Combine(_root, "outside");
        Write(outside, "keep.txt", "keep me\n");
        var link = Path.Combine(tree, ".claude", "skills");
        DirectoryLinks.Create(link, outside);
        var gone = AddWorktree("gone");
        LinkSafeDelete.DeleteTree(gone); // a prunable admin entry: still listed only if nothing ran git worktree prune
        // A file where the archive folder belongs: creating the entry fails.
        Directory.CreateDirectory(Path.GetDirectoryName(ArchiveRoot)!);
        File.WriteAllText(ArchiveRoot, "not a folder");

        var (exit, stdout, _) = await Clean("feature");

        Assert.Equal(2, exit);
        Assert.Contains($"failed {tree}: archiving its work failed, so nothing was removed: ", stdout);
        Assert.Contains("; branch feature kept (worktree not removed)", stdout);
        Assert.Equal("draft\n", File.ReadAllText(Path.Combine(tree, "notes.txt")));
        Assert.NotNull(new DirectoryInfo(link).LinkTarget);
        Assert.Equal("keep me\n", File.ReadAllText(Path.Combine(outside, "keep.txt")));
        Assert.True(HasBranch("feature"));
        var list = Git(_main, "worktree", "list", "--porcelain").Text;
        Assert.Contains("branch refs/heads/feature", list);
        Assert.Contains("branch refs/heads/gone", list);
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

    // Runs action with variables set in this process, restoring them after; test parallelization is off assembly-wide.
    private static async Task<T> WithEnvironment<T>(IReadOnlyDictionary<string, string?> variables, Func<Task<T>> action)
    {
        var saved = variables.Keys.ToDictionary(name => name, Environment.GetEnvironmentVariable);
        try
        {
            foreach (var (name, value) in variables)
                Environment.SetEnvironmentVariable(name, value);
            return await action();
        }
        finally
        {
            foreach (var (name, value) in saved)
                Environment.SetEnvironmentVariable(name, value);
        }
    }

    private async Task<string> ListOutput()=> (await Clean("--archive")).Stdout;

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

    // A submodule at tree/lib, cloned from a repository under the temp root and committed in the tree.
    private string AddSubmodule(string tree)
    {
        var library = Path.Combine(_root, "lib-origin");
        if (!Directory.Exists(library))
        {
            Directory.CreateDirectory(library);
            Git(library, "init", "-b", "master");
            File.WriteAllText(Path.Combine(library, "lib.txt"), "library\n");
            CommitAs(library, "library");
        }

        Git(tree, "-c", "protocol.file.allow=always", "submodule", "add", library, "lib");
        Commit(tree, "add the library");
        return Path.Combine(tree, "lib");
    }

    // A commit in a repository that has no identity of its own configured.
    private static void CommitAs(string directory, string message)
    {
        Git(directory, "add", "-A");
        Git(directory, "-c", "user.name=Test", "-c", "user.email=test@example.com", "-c", "commit.gpgsign=false",
            "commit", "-m", message);
    }

    private static void Commit(string directory, string message)
    {
        Git(directory, "add", "-A");
        Git(directory, "commit", "-m", message);
    }

    // Lands branch's changes on origin's master as one squash commit from the main checkout.
    private void SquashIntoOriginMaster(string branch)
    {
        Git(_main, "merge", "--squash", branch);
        Git(_main, "commit", "-m", $"squash {branch}");
        Git(_main, "push", "origin", "master");
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
