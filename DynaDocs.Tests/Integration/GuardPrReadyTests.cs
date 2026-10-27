namespace DynaDocs.Tests.Integration;

using System.Text.Json;
using System.Text.Json.Nodes;
using DynaDocs.Commands;
using DynaDocs.Services;
using static DynaDocs.Tests.Integration.PushedRepository;

/// <summary>
/// The guard's PR-ready rule through the real hook entry point (DYD-374): the project is a git
/// tree pushed to a bare origin, and a PR-opening gh call is refused while the tree is dirty.
/// </summary>
[Collection("Integration")]
public class GuardPrReadyTests : IntegrationTestBase
{
    // "Independent review" in the text keeps the shipped review-block nudge quiet.
    private const string PrCreate = "gh pr create --title t --body '## Independent review'";

    private const string Origin = "https://github.com/Acme/LC.git";

    private string Worktree => Path.Combine(TestDir, "wt");

    // A temp path may reach git through a symlink or a short name; the refusal names git's path.
    private string WorktreeAsGitListsIt => Path.GetFullPath(Git(Worktree, "rev-parse", "--show-toplevel").Text.Trim());

    // The origin is a bare repository on disk; after the pushes it is renamed to LC's URL so a
    // call naming acme/lc names this tree's repository. Upstream refs stay local, so nothing fetches.
    private async Task InitPushedProject(bool withWorktree = false)
    {
        await InitProjectAsync("none");
        File.AppendAllText(Path.Combine(TestDir, ".gitignore"), "\n/origin.git/\n/wt/\n");
        Create(TestDir, Path.Combine(TestDir, "origin.git"));
        if (withWorktree)
        {
            Git(TestDir, "worktree", "add", "-b", "topic", Worktree);
            Git(Worktree, "push", "-u", "origin", "topic");
        }
        Git(TestDir, "remote", "set-url", "origin", Origin);
    }

    // Dirty main checkout on feature, clean pushed worktree on topic.
    private async Task InitDirtyMainWithCleanWorktree()
    {
        await InitPushedProject(withWorktree: true);
        WriteFile("wip.txt", "w\n");
    }

    private void SilenceReviewBlockWarn()
    {
        var reviewBlockWarn = ConfigFactory.DefaultNudges.Single(n => n.Message.StartsWith("This PR carries no review block"));
        var marker = Path.Combine(GuardCommand.GuardEnv.Load(TestDir).MarkerDir,
            $".nudge-{GuardCommand.ComputeNudgeHash(reviewBlockWarn.Pattern)}");
        Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
        File.WriteAllText(marker, "spent");
    }

    private string Payload(object command, bool withCwd = true, bool codex = false,
        string? cwd = null, string? toolCwd = null, string? workdir = null)
    {
        var toolInput = new Dictionary<string, object?> { ["command"] = command };
        if (toolCwd != null) toolInput["cwd"] = toolCwd;
        if (workdir != null) toolInput["workdir"] = workdir;
        var payload = new Dictionary<string, object?>
        {
            ["session_id"] = TestSessionId,
            ["tool_name"] = "Bash",
            ["tool_input"] = toolInput,
        };
        if (withCwd) payload["cwd"] = cwd ?? TestDir;
        if (codex) payload["turn_id"] = "turn-1";
        return JsonSerializer.Serialize(payload);
    }

    // Runs each command once and returns every one whose decision is not the expected one, so a
    // replayed table reports all its misses at once.
    private async Task<List<string>> Misses(bool denied, IEnumerable<string> commands, string? reasonPart = null)
    {
        List<string> misses = [];
        foreach (var command in commands)
        {
            var result = await GuardWithStdinAsync(Payload(command));
            var wasDenied = !result.IsPreToolUseAllowed;
            if (wasDenied != denied || (denied && reasonPart != null && !result.Stderr.Contains(reasonPart)))
                misses.Add($"{(wasDenied ? "denied" : "allowed")}: {command}\n    {result.Stderr.Trim().Replace("\n", "\n    ")}");
        }
        return misses;
    }

    [Fact]
    public async Task PrCreate_FromADirtyTree_IsDenied()
    {
        await InitPushedProject();
        WriteFile("wip.txt", "w\n");

        var result = await GuardWithStdinAsync(Payload(PrCreate));

        var reason = result.AssertPreToolUseDenied();
        Assert.Contains("untracked files: wip.txt", reason);
        Assert.Contains($"Checked: {TestDir}", reason);
        Assert.DoesNotContain("the host sent no cwd", reason);
        Assert.Contains("Run dydo pr-ready there", reason);
    }

    [Theory]
    [InlineData(PrCreate)]
    [InlineData("gh pr ready 12")]
    [InlineData("gh pr new --fill")]
    public async Task PrOpeningCall_FromACleanPushedTree_IsAllowed(string command)
    {
        await InitPushedProject();

        var result = await GuardWithStdinAsync(Payload(command));

        result.AssertPreToolUseAllowed();
    }

    [Theory]
    [InlineData("git status && gh pr ready")]
    [InlineData("git status; gh pr new --fill")]
    [InlineData(@"printf '%s\n' 'C:\'; gh pr ready 12")]
    [InlineData("echo a#b; gh pr ready")]
    [InlineData("gh pr create --title \"fix #12\" --body '## Independent review'")]
    public async Task ChainedPrCall_FromADirtyTree_IsDenied(string command)
    {
        await InitPushedProject();
        WriteFile("readme.md", "changed\n");

        var result = await GuardWithStdinAsync(Payload(command));

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("unstaged changes: readme.md");
    }

    [Fact]
    public async Task UnpushedCommit_IsDenied()
    {
        await InitPushedProject();
        WriteFile("more.txt", "m\n");
        Commit(TestDir, "more");

        var result = await GuardWithStdinAsync(Payload("gh pr ready"));

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("HEAD differs from its upstream origin/feature");
    }

    [Theory]
    [InlineData("gh pr view 12")]
    [InlineData("git status")]
    [InlineData("echo gh pr create")]
    [InlineData("git status # reminder; gh pr ready 12")]
    public async Task UnrelatedCommand_FromADirtyTree_IsUntouched(string command)
    {
        await InitPushedProject();
        WriteFile("wip.txt", "w\n");

        var result = await GuardWithStdinAsync(Payload(command));

        result.AssertPreToolUseAllowed();
    }

    [Theory]
    [InlineData("git commit -m 'Document workflow; gh pr ready follows review'")]
    [InlineData("git commit -m \"Document workflow && gh pr new follows review\"")]
    public async Task PrCallInsideQuotedText_FromAStagedTree_IsAllowed(string command)
    {
        await InitPushedProject();
        WriteFile("docs.md", "d\n");
        Git(TestDir, "add", "docs.md");

        var result = await GuardWithStdinAsync(Payload(command));

        result.AssertPreToolUseAllowed();
    }

    // A heredoc or PowerShell here-string body is text the command reads, not a command line.
    [Theory]
    [InlineData("cat <<'EOF'\ngh pr ready 12\nEOF")]
    [InlineData("cat <<\"EOF\"\ngh pr ready 12\nEOF")]
    [InlineData("cat <<EOF\ngh pr ready 12\nEOF")]
    [InlineData("cat <<-EOF\n\tgh pr ready 12\n\tEOF")]
    [InlineData("Write-Output @'\nIt's ready.\ngh pr ready 12\n'@")]
    [InlineData("Write-Output @\"\nFits a 13\" screen.\ngh pr new --fill\n\"@")]
    public async Task PrCallInsideALiteralBody_FromADirtyTree_IsAllowed(string command)
    {
        await InitPushedProject();
        WriteFile("wip.txt", "w\n");

        var result = await GuardWithStdinAsync(Payload(command));

        result.AssertPreToolUseAllowed();
    }

    // The guard's file-write analysis refuses a multi-line write on its own; this rule must not.
    [Fact]
    public async Task PrCallInsideAPrBodyHeredoc_FromADirtyTree_IsNotThisRulesBlock()
    {
        await InitPushedProject();
        WriteFile("wip.txt", "w\n");

        var result = await GuardWithStdinAsync(
            Payload("cat > /tmp/b.md <<'EOF'\n## Steps\ngh pr create --title t --fill\nEOF"));

        Assert.DoesNotContain("This PR would not match", result.Stderr);
    }

    [Theory]
    [InlineData("cat <<'EOF'\nnotes\nEOF\n" + PrCreate)]
    [InlineData("cat <<-EOF\n\tnotes\n\tEOF\n" + PrCreate)]
    [InlineData("Write-Output @'\nIt's ready.\n'@\n" + PrCreate)]
    public async Task PrCallAfterALiteralBody_FromADirtyTree_IsDenied(string command)
    {
        await InitPushedProject();
        WriteFile("wip.txt", "w\n");

        var result = await GuardWithStdinAsync(Payload(command));

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("untracked files: wip.txt");
    }

    [Fact]
    public async Task CodexPayloadWithoutCwd_ChecksTheProjectRoot()
    {
        await InitPushedProject();
        WriteFile("wip.txt", "w\n");

        var result = await GuardWithStdinAsync(Payload(PrCreate, withCwd: false, codex: true));

        var reason = result.AssertPreToolUseDenied();
        Assert.Contains($"Checked: {TestDir} (the project root; the host sent no cwd)", reason);
    }

    // DYD-386: the review-block warn must not arm or spend its once-only marker while the tree
    // check refuses the call anyway, or the two messages alternate and "run again" is false.
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task PrCreateWithoutReviewBlock_FromATreeNotReady_RefusesEveryTryAndLeavesTheWarnMarker(
        bool unpushed, bool markerBefore)
    {
        await InitPushedProject();
        WriteFile("wip.txt", "w\n");
        if (unpushed) Commit(TestDir, "wip");
        var reviewBlockWarn = ConfigFactory.DefaultNudges
            .Single(n => n.Message.StartsWith("This PR carries no review block"));
        var marker = Path.Combine(GuardCommand.GuardEnv.Load(TestDir).MarkerDir,
            $".nudge-{GuardCommand.ComputeNudgeHash(reviewBlockWarn.Pattern)}");
        if (markerBefore)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
            File.WriteAllText(marker, "armed before the first try");
        }

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var result = await GuardWithStdinAsync(Payload("gh pr create --fill"));

            var reason = result.AssertPreToolUseDenied();
            Assert.True(reason.StartsWith("BLOCKED: This PR would not match the branch in your tree."),
                $"try {attempt}: {reason}");
            Assert.True(File.Exists(marker) == markerBefore, $"try {attempt} changed the warn marker");
        }
    }

    // A block nudge on a PR-opening call wins over the tree check, every try.
    [Fact]
    public async Task BlockNudgeOnPrCreate_FromADirtyTree_RefusesWithTheNudgeEveryTry()
    {
        await InitPushedProject();
        var config = JsonNode.Parse(ReadFile("dydo.json"))!.AsObject();
        var nudges = config["nudges"] as JsonArray ?? [];
        nudges.Add(new JsonObject { ["pattern"] = @"gh\s+pr\s+create", ["message"] = "Custom PR block.", ["severity"] = "block" });
        config["nudges"] = nudges;
        WriteFile("dydo.json", config.ToJsonString());
        WriteFile("wip.txt", "w\n");

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var reason = (await GuardWithStdinAsync(Payload(PrCreate))).AssertPreToolUseDenied();

            Assert.True(reason.StartsWith("BLOCKED: Custom PR block."), $"try {attempt}: {reason}");
        }
    }

    [Fact]
    public async Task PrCreateWithoutReviewBlock_FromACleanPushedTree_WarnsOnceThenPasses()
    {
        await InitPushedProject();

        var first = await GuardWithStdinAsync(Payload("gh pr create --fill"));
        var second = await GuardWithStdinAsync(Payload("gh pr create --fill"));

        Assert.Contains("carries no review block", first.AssertPreToolUseDenied());
        second.AssertPreToolUseAllowed();
    }

    [Fact]
    public async Task OutsideGit_IsAllowed()
    {
        await InitProjectAsync("none");

        var result = await GuardWithStdinAsync(Payload(PrCreate));

        result.AssertPreToolUseAllowed();
    }

    // DYD-391: a PR naming its branch is checked where that branch is checked out, not where the
    // call runs. LC check-pr-ready.test.mjs:454-464, 1661-1668; pr-ready-guard.test.mjs:149.
    [Theory]
    [InlineData("gh pr create --head topic --fill")]
    [InlineData("gh pr create -H topic --fill")]
    [InlineData("gh pr create -Htopic --fill")]
    [InlineData("gh pr create --head=topic --fill")]
    [InlineData("gh pr create -dH \"topic\" --title x")]
    [InlineData("gh pr create --head other --fill --head me:topic")]
    [InlineData("gh -R acme/lc pr create --repo acme/lc --head 'topic'")]
    [InlineData("gh pr new --head topic --fill")]
    [InlineData("gh pr ready topic")]
    [InlineData("gh pr ready -R acme/lc topic")]
    [InlineData("gh pr ready --undo me:topic")]
    [InlineData("gh api repos/acme/lc/pulls -f head=topic -f base=master -f title=x")]
    [InlineData("gh api -X POST /repos/acme/lc/pulls -f head=me:topic")]
    [InlineData("gh api 'repos/{owner}/{repo}/pulls' -F head=topic")]
    [InlineData("gh pr create --head topic --fill && gh pr ready topic")]
    public async Task BranchNamedCall_FromADirtyMain_ChecksTheCleanBranchWorktree(string command)
    {
        await InitDirtyMainWithCleanWorktree();
        SilenceReviewBlockWarn();

        var result = await GuardWithStdinAsync(Payload(command));

        result.AssertPreToolUseAllowed();
    }

    [Theory]
    [InlineData("gh pr create --head topic --fill")]
    [InlineData("gh pr ready topic")]
    [InlineData("gh api repos/acme/lc/pulls -f head=topic")]
    public async Task BranchNamedCall_OfADirtyWorktree_IsDeniedNamingThatTree(string command)
    {
        await InitPushedProject(withWorktree: true);
        File.WriteAllText(Path.Combine(Worktree, "draft.txt"), "d\n");

        var reason = (await GuardWithStdinAsync(Payload(command))).AssertPreToolUseDenied();

        Assert.Contains("untracked files: draft.txt", reason);
        Assert.Contains($"Checked: {WorktreeAsGitListsIt}", reason);
    }

    [Fact]
    public async Task BranchNamedCall_OfAnUnpushedWorktree_IsDenied()
    {
        await InitPushedProject(withWorktree: true);
        File.WriteAllText(Path.Combine(Worktree, "more.txt"), "m\n");
        Commit(Worktree, "more");

        var reason = (await GuardWithStdinAsync(Payload("gh pr ready topic"))).AssertPreToolUseDenied();

        Assert.Contains("HEAD differs from its upstream origin/topic", reason);
    }

    [Theory]
    [InlineData("gh pr create --head nope --fill", "--head nope")]
    [InlineData("gh pr ready nope", "gh pr ready nope")]
    [InlineData("gh api repos/acme/lc/pulls -f head=nope", "head=nope")]
    public async Task BranchNoWorktreeHas_IsDeniedNamingTheBranchForm(string command, string form)
    {
        await InitPushedProject(withWorktree: true);

        var reason = (await GuardWithStdinAsync(Payload(command))).AssertPreToolUseDenied();

        Assert.Contains($"{form} names branch nope, which no worktree of this repository has checked out", reason);
    }

    // Every call in one command is checked against its own tree, and each tree is named.
    [Fact]
    public async Task TwoCalls_OfTwoDirtyTrees_NameBothTrees()
    {
        await InitDirtyMainWithCleanWorktree();
        File.WriteAllText(Path.Combine(Worktree, "draft.txt"), "d\n");

        var reason = (await GuardWithStdinAsync(Payload("gh pr create --head topic --fill && gh pr create --fill")))
            .AssertPreToolUseDenied();

        Assert.Contains("untracked files: draft.txt", reason);
        Assert.Contains($"Checked: {WorktreeAsGitListsIt}", reason);
        Assert.Contains("untracked files: wip.txt", reason);
        Assert.Contains($"Checked: {TestDir}", reason);
    }

    // A value that looks like --head is not the head. LC check-pr-ready.test.mjs:492-497, 1514, 1549-1555.
    [Theory]
    [InlineData("gh --head topic pr create --fill")]
    [InlineData("gh -H topic pr create --fill")]
    [InlineData("gh pr create --title -H topic --fill")]
    [InlineData("gh pr create --title=x --body --head topic")]
    [InlineData("gh pr ready 12")]
    [InlineData("gh pr ready https://github.com/acme/lc/pull/12")]
    public async Task CallNamingNoBranch_FromADirtyMain_ChecksTheDirectory(string command)
    {
        await InitDirtyMainWithCleanWorktree();

        var reason = (await GuardWithStdinAsync(Payload(command))).AssertPreToolUseDenied();

        Assert.Contains("untracked files: wip.txt", reason);
    }

    // LC's plain forms (the PR body's table names each source line), each from a dirty tree.
    [Fact]
    public async Task PlainForms_FromADirtyTree_AreDenied()
    {
        await InitPushedProject();
        WriteFile("wip.txt", "w\n");
        string[] commands =
        [
            "gh pr create --fill", "gh pr new --fill", "gh pr ready 12",
            "git status && gh -R acme/lc pr create --fill", "gh.exe pr create --fill",
            "GH_TOKEN=x gh pr create --fill", "env gh pr create --fill", "time gh pr create --fill",
            "{ gh pr create --fill; }", "if true; then gh pr create --fill; fi",
            "for i in 1; do gh pr create --fill; done", "if false; then :; else gh pr create --fill; fi",
            "env -u GH_TOKEN gh pr create --fill", "env GH_TOKEN=x gh pr create --fill",
            "timeout 60 gh pr create --fill", "nice gh pr create --fill", "nice -n 5 gh pr create --fill",
            "GH pr create --fill", "Gh.exe pr create --fill", "C:/bin/GH.EXE pr ready 12",
            "\"C:/Program Files/GitHub CLI/gh.exe\" pr create --fill", "(gh pr create --fill)",
            "gh -R acme/lc pr new --fill", "gh --repo acme/lc pr create --fill", "gh pr -R acme/lc create --fill",
            "gh pr create -R acme/lc --fill", "gh pr create --repo=Acme/LC.git --fill", "gh pr create -Racme/lc --fill",
            "GH_REPO=acme/lc gh pr create --fill", "  gh  pr create   --fill  ",
            "gh pr create --fill > out.txt", "git status\ngh pr create --fill",
            "cat <<EOF\njust notes\nEOF\ngh pr create --fill", "cat <<EOF; gh pr create --fill\njust notes\nEOF",
            "gh api repos/acme/lc/pulls -f title=t -f base=master", "gh api -X POST repos/acme/lc/pulls",
            "gh api --method=post repos/acme/lc/pulls -ftitle=x", "gh api -XPOST repos/acme/lc/pulls --field=title=x",
            "gh api https://api.github.com/repos/acme/lc/pulls -f title=t", "gh api REPOS/ACME/LC/PULLS -f title=t",
            "env GH_TOKEN=x gh api repos/acme/lc/pulls -f title=t", "gh api repos/{owner}/{repo}/pulls -f title=t",
            "gh api graphql -f query='mutation{markPullRequestReadyForReview(input:{pullRequestId:\"P\"}){clientMutationId}}'",
            "gh api graphql -f query='mutation{createPullRequest(input:{repositoryId:\"R\",baseRefName:\"master\",headRefName:\"h\",title:\"t\"}){clientMutationId}}'",
            "gh api https://ghe.example.com/api/graphql -f query='mutation{createPullRequest(input:{headRefName:\"h\"}){clientMutationId}}'",
        ];

        Assert.Empty(await Misses(denied: true, commands, "untracked files: wip.txt"));
    }

    // LC's commands that open no PR, from a dirty tree: the rule stays out of the way.
    [Fact]
    public async Task CommandsOpeningNoPr_FromADirtyTree_PassUntouched()
    {
        await InitPushedProject();
        WriteFile("wip.txt", "w\n");
        SilenceReviewBlockWarn();
        string[] commands =
        [
            "gh pr view 12", "ls", "git commit -m \"gh pr create later\"", "grep -rn \"gh pr create\" dydo",
            "gh pr view $n", "gh pr create-notes", "echo gh pr create --fill", "gh pr view 12 --json title",
            "gh api graphql -F query=@viewer.graphql", "gh api graphql -f query='{viewer{login}}'",
            "gh api repos/acme/lc/pulls", "gh api repos/acme/lc/pulls/12/comments -f body=x",
            "git commit -m \"$(cat <<'EOF'\nfix the hook\n\ngh pr create now checks the tree\nEOF\n)\"",
            "cat <<'EOF'\ngh pr create --fill\nEOF", "bash -c \"gh pr view 12\"",
            "pwsh -NoProfile -Command \"git status\"", "cmd /c dir", "eval \"$(ssh-agent -s)\"",
        ];

        Assert.Empty(await Misses(denied: false, commands));
    }

    // -R, --repo, GH_REPO or a REST endpoint naming another repository cannot be checked here.
    [Theory]
    [InlineData("gh pr create -R other/x --fill")]
    [InlineData("gh -R other/x pr create --head topic")]
    [InlineData("gh pr create --repo=github.com/other/x --head topic")]
    [InlineData("gh pr create -R https://github.com/Other/X.git --fill")]
    [InlineData("GH_REPO=other/x gh pr create --fill")]
    [InlineData("env GH_REPO=other/x gh pr create --fill")]
    [InlineData("gh -R acme/lc pr create --repo other/x --fill")]
    [InlineData("gh pr ready topic -R github.com/other/x")]
    [InlineData("gh pr create -R acme/lc-docs --fill")]
    [InlineData("gh api repos/other/x/pulls -f head=topic")]
    public async Task CallNamingAnotherRepository_FromACleanTree_IsRefused(string command)
    {
        await InitPushedProject(withWorktree: true);

        var reason = (await GuardWithStdinAsync(Payload(command))).AssertPreToolUseDenied();

        Assert.Contains("names another repository", reason);
        Assert.Contains("run it from that repo's checkout", reason);
    }

    [Theory]
    [InlineData("gh pr create -R Acme/LC --fill")]
    [InlineData("gh pr create --repo https://github.com/acme/lc.git --fill")]
    [InlineData("gh pr create -R git@github.com:Acme/LC.git --fill")]
    [InlineData("GH_REPO=github.com/acme/lc gh pr create --fill")]
    public async Task CallNamingThisRepository_FromACleanTree_Proceeds(string command)
    {
        await InitPushedProject();
        SilenceReviewBlockWarn();

        var result = await GuardWithStdinAsync(Payload(command));

        result.AssertPreToolUseAllowed();
    }

    // Balazs's 2026-10-27 ruling: a PR call inside a shell wrapper fails closed, with no decoding,
    // whatever the tree's state. LC check-pr-ready.test.mjs:328-347, 2454-2455, 2506, 2530.
    [Fact]
    public async Task WrappedPrCall_FromACleanTree_IsRefused()
    {
        await InitPushedProject();
        SilenceReviewBlockWarn();
        string[] commands =
        [
            "bash -c \"gh pr create --fill\"", "sh -c 'git status; gh pr ready 12'",
            "pwsh -NoProfile -Command \"gh pr create --fill\"", "powershell -c \"gh pr create --fill\"",
            "eval \"gh pr create --fill\"", "eval gh pr create --fill", "cmd /c gh pr create --fill",
            "cmd.exe /c \"gh pr create --fill\"", "cmd /k gh pr create --fill", "cmd //c gh pr create --fill",
            "cmd.exe /s /C \"git status && gh pr ready 12\"", "bash -lc 'gh -R acme/lc pr create --fill'",
            "bash -c 'gh api repos/acme/lc/pulls -f head=feature'",
            "pwsh -NoProfile -Command \"gh api repos/acme/lc/pulls -f head=feature\"",
            "bash -c \"gh api -X POST repos/acme/lc/pulls -F head=feature -f base=main\"",
            "bash -c 'pwsh -NoProfile -Command \"gh pr create --head feature --fill\"'",
            "pwsh -EncodedCommand ZwBoACAAcAByACAAYwByAGUAYQB0AGUAIAAtAC0AZgBpAGwAbAA=",
            "powershell.exe -ec ZwBoACAAcAByACAAYwByAGUAYQB0AGUAIAAtAC0AZgBpAGwAbAA=",
        ];

        Assert.Empty(await Misses(denied: true, commands, "run gh directly so the PR check can see it"));
    }

    // Codex runs a call in tool_input.workdir, which may not be the session's directory.
    [Fact]
    public async Task CodexDirtyWorkdir_FromACleanSession_IsDenied()
    {
        await InitDirtyMainWithCleanWorktree();

        var result = await GuardWithStdinAsync(Payload(PrCreate, cwd: Worktree, codex: true, workdir: TestDir));

        var reason = result.AssertPreToolUseDenied();
        Assert.Contains("untracked files: wip.txt", reason);
        Assert.Contains($"Checked: {TestDir}", reason);
    }

    [Fact]
    public async Task CleanWorkdir_FromADirtySession_IsAllowed()
    {
        await InitDirtyMainWithCleanWorktree();

        var result = await GuardWithStdinAsync(Payload(PrCreate, codex: true, workdir: Worktree));

        result.AssertPreToolUseAllowed();
    }

    // LC check-pr-ready.test.mjs:1683: tool_input.cwd is read before tool_input.workdir.
    [Fact]
    public async Task ToolInputCwd_IsReadBeforeWorkdir()
    {
        await InitDirtyMainWithCleanWorktree();

        var result = await GuardWithStdinAsync(Payload(PrCreate, cwd: Worktree, toolCwd: TestDir, workdir: Worktree));

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("untracked files: wip.txt");
    }

    // Codex may send the command as an argv array. LC check-pr-ready.test.mjs:1695-1707, 2782.
    [Theory]
    [InlineData(true, new[] { "gh", "pr", "new", "--fill" })]
    [InlineData(true, new[] { "bash", "-lc", "gh pr create --fill" })]
    [InlineData(true, new[] { @"C:\Program Files\PowerShell\7\pwsh.exe", "-NoProfile", "-Command", "gh pr ready" })]
    [InlineData(false, new[] { "bash", "-lc", "gh pr view 12" })]
    [InlineData(false, new[] { "gh", "api", "repos/acme/lc/pulls/12/comments", "-f", "body=x" })]
    public async Task ArgvCommand_FromADirtyTree_IsReadAsItsScript(bool opensAPr, string[] argv)
    {
        await InitPushedProject();
        WriteFile("wip.txt", "w\n");

        var result = await GuardWithStdinAsync(Payload(argv, codex: true));

        if (opensAPr)
        {
            result.AssertPreToolUseDenied();
            result.AssertStderrContains("untracked files: wip.txt");
        }
        else
            result.AssertPreToolUseAllowed();
    }

    [Fact]
    public async Task ArgvCommand_FromACleanTree_IsAllowed()
    {
        await InitPushedProject();
        SilenceReviewBlockWarn();

        var result = await GuardWithStdinAsync(Payload(new[] { "bash", "-lc", "gh pr create --fill" }, codex: true));

        result.AssertPreToolUseAllowed();
    }

    // LC check-pr-ready.test.mjs:2920: a PR call split across pwsh's arguments is still a wrapped call.
    [Fact]
    public async Task ArgvWrapperSplittingThePrCall_IsRefused()
    {
        await InitPushedProject();

        var result = await GuardWithStdinAsync(Payload(new[] { "pwsh", "-Command", "gh", "pr", "create", "--fill" }, codex: true));

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("run gh directly so the PR check can see it");
    }
}
