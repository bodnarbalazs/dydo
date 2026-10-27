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
        string? cwd = null, string? toolCwd = null, string? workdir = null, string toolName = "Bash")
    {
        var toolInput = new Dictionary<string, object?> { ["command"] = command };
        if (toolCwd != null) toolInput["cwd"] = toolCwd;
        if (workdir != null) toolInput["workdir"] = workdir;
        var payload = new Dictionary<string, object?>
        {
            ["session_id"] = TestSessionId,
            ["tool_name"] = toolName,
            ["tool_input"] = toolInput,
        };
        if (withCwd) payload["cwd"] = cwd ?? TestDir;
        if (codex) payload["turn_id"] = "turn-1";
        return JsonSerializer.Serialize(payload);
    }

    // Runs each command once and returns every one whose decision is not the expected one, so a
    // replayed table reports all its misses at once.
    private async Task<List<string>> Misses(bool denied, IEnumerable<string> commands, string? reasonPart = null,
        string toolName = "Bash")
    {
        List<string> misses = [];
        foreach (var command in commands)
        {
            var result = await GuardWithStdinAsync(Payload(command, toolName: toolName));
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

    // ---- DYD-391 round 1 FAIL ----

    // C1, B2: the PowerShell tool's command is read as PowerShell reads it.
    [Theory]
    [InlineData(@"C:\tools\gh.exe pr create --fill")]
    [InlineData(@"& C:\tools\gh.exe pr create --fill")]
    [InlineData(@"& 'C:\Program Files\GitHub CLI\gh.exe' pr ready 12")]
    public async Task PowerShellToolPrCall_FromADirtyTree_IsDenied(string command)
    {
        await InitPushedProject();
        WriteFile("wip.txt", "w\n");

        var result = await GuardWithStdinAsync(Payload(command, toolName: "PowerShell"));

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("untracked files: wip.txt");
    }

    // B2: a backtick carries a PowerShell call onto the next line, so --head still names the branch,
    // whether the tool says PowerShell or the text uses PowerShell syntax.
    [Theory]
    [InlineData("PowerShell", "gh pr create `\n  --head topic `\n  --fill")]
    [InlineData("Bash", "gh pr create `\n  --head topic `\n  --fill")]
    [InlineData("Bash", "gh pr create `\r\n  --head topic `\r\n  --fill")]
    [InlineData("Bash", @"& C:\Tools\gh.exe pr create --head topic --fill")]
    public async Task PowerShellContinuation_NamingADirtyWorktree_IsDenied(string toolName, string command)
    {
        await InitPushedProject(withWorktree: true);
        File.WriteAllText(Path.Combine(Worktree, "draft.txt"), "d\n");
        SilenceReviewBlockWarn();

        var reason = (await GuardWithStdinAsync(Payload(command, toolName: toolName))).AssertPreToolUseDenied();

        Assert.Contains("untracked files: draft.txt", reason);
    }

    // C3: a directory outside git is not this rule's business, wrapped or encoded.
    [Theory]
    [InlineData("pwsh -EncodedCommand ZwBpAHQAIABzAHQAYQB0AHUAcwA=")]
    [InlineData("bash -c 'gh pr create --fill'")]
    [InlineData("cmd /c gh pr ready 12")]
    public async Task WrappedOrEncoded_OutsideGit_IsAllowed(string command)
    {
        await InitProjectAsync("none");

        var result = await GuardWithStdinAsync(Payload(command));

        result.AssertPreToolUseAllowed();
    }

    // A1: a relative tool cwd or workdir is the session's subdirectory, not the hook process's.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RelativeWorkdir_ResolvesAgainstTheSessionCwd(bool asToolCwd)
    {
        await InitDirtyMainWithCleanWorktree();

        var result = await GuardWithStdinAsync(asToolCwd
            ? Payload(PrCreate, cwd: Worktree, toolCwd: "..")
            : Payload(PrCreate, cwd: Worktree, codex: true, workdir: ".."));

        var reason = result.AssertPreToolUseDenied();
        Assert.Contains("untracked files: wip.txt", reason);
    }

    [Fact]
    public async Task RelativeWorkdir_WithoutASessionCwd_ResolvesAgainstTheProcessDirectory()
    {
        await InitDirtyMainWithCleanWorktree();

        var result = await GuardWithStdinAsync(Payload(PrCreate, withCwd: false, codex: true, workdir: "wt"));

        result.AssertPreToolUseAllowed();
    }

    // A2: the host is part of the repository.
    [Theory]
    [InlineData("gh pr create -R ghe.example.com/acme/lc --fill")]
    [InlineData("gh pr create --repo https://ghe.example.com/acme/lc.git --fill")]
    [InlineData("GH_REPO=ghe.example.com/acme/lc gh pr create --fill")]
    [InlineData("gh api https://ghe.example.com/api/v3/repos/acme/lc/pulls -f head=topic")]
    [InlineData("gh api --hostname ghe.example.com repos/acme/lc/pulls -f head=topic")]
    public async Task SameOwnerAndName_OnAnotherHost_IsRefused(string command)
    {
        await InitPushedProject(withWorktree: true);

        var reason = (await GuardWithStdinAsync(Payload(command))).AssertPreToolUseDenied();

        Assert.Contains("names another repository", reason);
        Assert.Contains("run it from that repo's checkout", reason);
    }

    [Theory]
    [InlineData("gh pr create -R GitHub.com/ACME/lc.git --fill")]
    [InlineData("gh pr create -R ssh://git@GITHUB.COM/acme/LC.git --fill")]
    [InlineData("gh api https://api.github.com/repos/Acme/LC/pulls -f head=feature -f title=t")]
    [InlineData("gh api --hostname github.com repos/acme/lc/pulls -f head=feature -f title=t")]
    public async Task SameHostInAnotherCase_IsThisRepository(string command)
    {
        await InitPushedProject();
        SilenceReviewBlockWarn();

        var result = await GuardWithStdinAsync(Payload(command));

        result.AssertPreToolUseAllowed();
    }

    // A3: gh pr ready given a PR URL readies that URL's repository's PR.
    [Theory]
    [InlineData("gh pr ready https://github.com/other/x/pull/12")]
    [InlineData("gh pr ready https://ghe.example.com/acme/lc/pull/12")]
    public async Task PrReadyOfAnotherRepositorysUrl_FromACleanTree_IsRefused(string command)
    {
        await InitPushedProject();

        var reason = (await GuardWithStdinAsync(Payload(command))).AssertPreToolUseDenied();

        Assert.Contains("names another repository", reason);
        Assert.Contains("run it from that repo's checkout", reason);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PrReadyOfThisRepositorysUrl_ChecksTheTree(bool dirty)
    {
        await InitPushedProject();
        if (dirty) WriteFile("wip.txt", "w\n");

        var result = await GuardWithStdinAsync(Payload("gh pr ready https://GitHub.com/acme/LC/pull/12"));

        if (dirty)
            Assert.Contains("untracked files: wip.txt", result.AssertPreToolUseDenied());
        else
            result.AssertPreToolUseAllowed();
    }

    // A4: a worktree git still lists for the branch but whose directory is gone cannot be checked.
    [Fact]
    public async Task BranchHeldOnlyByAPrunableWorktree_IsRefused()
    {
        await InitPushedProject(withWorktree: true);
        var listed = WorktreeAsGitListsIt;
        Directory.Delete(Worktree, recursive: true);

        var reason = (await GuardWithStdinAsync(Payload("gh pr ready topic"))).AssertPreToolUseDenied();

        Assert.Contains($"{listed} holds branch topic but is not an available work tree", reason);
    }

    // A5: the wrapper check reads the wrapped script as a command line: a PR call there is refused
    // even across a quoted separator, and a read is not a PR call.
    [Fact]
    public async Task WrappedPrCallAcrossAQuotedSeparator_IsRefused()
    {
        await InitPushedProject();
        SilenceReviewBlockWarn();
        string[] commands =
        [
            "bash -c 'gh api -X POST -H \"Content-Type: application/json; charset=utf-8\" repos/acme/lc/pulls --input body.json'",
            "pwsh -Command \"gh pr create --title 'a; b' --fill\"",
            "bash -c 'echo \"x | y\" && gh pr ready 12'",
        ];

        Assert.Empty(await Misses(denied: true, commands, "run gh directly so the PR check can see it"));
    }

    [Fact]
    public async Task WrappedRead_FromADirtyTree_PassesUntouched()
    {
        await InitPushedProject();
        WriteFile("wip.txt", "w\n");
        string[] commands =
        [
            "bash -c 'gh api repos/acme/lc/pulls'", "bash -c 'gh pr list --search ready'",
            "sh -c 'gh pr view 12 --json title'", "pwsh -Command \"gh api repos/acme/lc/pulls?state=open\"",
            "bash -c 'gh api repos/acme/lc/pulls/12/comments -f body=x'", "cmd /c gh pr list --search create",
            "eval \"gh pr status\"", "bash -c 'echo gh pr create'",
        ];

        Assert.Empty(await Misses(denied: false, commands));
    }

    // Admiral ruling 2026-10-27: GitHub's REST API cannot take a PR out of draft, so a PATCH is no
    // PR-readying call and refusing it would be a false block.
    [Fact]
    public async Task RestPatchDraftFalse_FromADirtyTree_PassesUntouched()
    {
        await InitPushedProject();
        WriteFile("wip.txt", "w\n");

        var result = await GuardWithStdinAsync(Payload("gh api -X PATCH repos/acme/lc/pulls/12 -f draft=false"));

        result.AssertPreToolUseAllowed();
    }

    // A6: two worktrees may hold one branch (git worktree add --force); each is checked and named.
    [Fact]
    public async Task BranchHeldByTwoWorktrees_ChecksAndNamesBoth()
    {
        await InitPushedProject(withWorktree: true);
        var second = Path.Combine(TestDir, "wt2");
        File.AppendAllText(Path.Combine(TestDir, ".gitignore"), "/wt2/\n");
        Git(TestDir, "worktree", "add", "--force", second, "topic");
        File.WriteAllText(Path.Combine(Worktree, "one.txt"), "1\n");
        File.WriteAllText(Path.Combine(second, "two.txt"), "2\n");

        var reason = (await GuardWithStdinAsync(Payload("gh pr ready topic"))).AssertPreToolUseDenied();

        Assert.Contains("untracked files: one.txt", reason);
        Assert.Contains("untracked files: two.txt", reason);
    }

    // A6 on a case-sensitive filesystem: holders whose paths differ only in case are two trees.
    [SkippableFact]
    public async Task BranchHeldByWorktreesDifferingInCase_ChecksAndNamesBoth()
    {
        await InitPushedProject(withWorktree: true);
        var upper = Path.Combine(TestDir, "WT");
        Skip.If(Directory.Exists(upper), "this filesystem ignores case");
        File.AppendAllText(Path.Combine(TestDir, ".gitignore"), "/WT/\n");
        Git(TestDir, "worktree", "add", "--force", upper, "topic");
        File.WriteAllText(Path.Combine(Worktree, "one.txt"), "1\n");
        File.WriteAllText(Path.Combine(upper, "two.txt"), "2\n");

        var reason = (await GuardWithStdinAsync(Payload("gh pr ready topic"))).AssertPreToolUseDenied();

        Assert.Contains("untracked files: one.txt", reason);
        Assert.Contains("untracked files: two.txt", reason);
    }

    // A7: a refusal names every tree it checked, the clean ones marked clean.
    [Fact]
    public async Task MixedCleanAndDirtyTrees_RefusalNamesEveryCheckedTree()
    {
        await InitDirtyMainWithCleanWorktree();

        var reason = (await GuardWithStdinAsync(Payload("gh pr ready topic && gh pr ready feature")))
            .AssertPreToolUseDenied();

        Assert.Contains("untracked files: wip.txt", reason);
        Assert.Contains($"Checked: {TestDir}", reason);
        Assert.Contains($"Checked: {WorktreeAsGitListsIt} (clean, HEAD matches its upstream)", reason);
    }

    // B1 (admiral ruling): Codex's shell envelope, a shell and its script flag, is read as its
    // script; LC check-pr-ready.test.mjs:1695-1705. A pwsh envelope's script is PowerShell.
    [Theory]
    [InlineData(new object[] { new[] { "pwsh", "-Command", @"& C:\tools\gh.exe pr create --fill" } })]
    [InlineData(new object[] { new[] { @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe", "-NoProfile", "-Command", "gh pr create `\n  --fill" } })]
    [InlineData(new object[] { new[] { "/bin/zsh", "-c", "gh pr ready 12" } })]
    [InlineData(new object[] { new[] { "sh", "-c", "gh pr ready 12" } })]
    public async Task ShellEnvelope_FromADirtyTree_IsDenied(string[] argv)
    {
        await InitPushedProject();
        WriteFile("wip.txt", "w\n");

        var result = await GuardWithStdinAsync(Payload(argv, codex: true));

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("untracked files: wip.txt");
    }

    // A wrapper inside the envelope's script, or an argv array of another shape, fails closed.
    [Theory]
    [InlineData(new object[] { new[] { "bash", "-lc", "bash -c 'gh pr create --fill'" } })]
    [InlineData(new object[] { new[] { "pwsh", "-Command", "cmd /c gh pr ready 12" } })]
    [InlineData(new object[] { new[] { "cmd", "/c", "gh pr create --fill" } })]
    [InlineData(new object[] { new[] { "cmd.exe", "/c", "gh pr ready 12" } })]
    [InlineData(new object[] { new[] { "eval", "gh pr ready 12" } })]
    public async Task WrapperInsideOrBesideTheEnvelope_FromACleanTree_IsRefused(string[] argv)
    {
        await InitPushedProject();
        SilenceReviewBlockWarn();

        var result = await GuardWithStdinAsync(Payload(argv, codex: true));

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("run gh directly so the PR check can see it");
    }

    // B3: the effective repository is the last -R/--repo, else an inline GH_REPO, else the inherited one.
    [Theory]
    [InlineData(null, "GH_REPO=other/x gh pr create -R acme/lc --fill", false)]
    [InlineData(null, "GH_REPO=other/x gh -R acme/lc pr create --fill", false)]
    [InlineData("other/x", "gh pr create --fill", true)]
    [InlineData("other/x", "gh pr ready 12", true)]
    [InlineData("other/x", "gh pr create -R acme/lc --fill", false)]
    [InlineData("other/x", "GH_REPO=acme/lc gh pr create --fill", false)]
    [InlineData("acme/lc", "GH_REPO=other/x gh pr create --fill", true)]
    public async Task EffectiveRepository_DecidesTheForeignRefusal(string? inherited, string command, bool refused)
    {
        await InitPushedProject();
        SilenceReviewBlockWarn();
        var before = Environment.GetEnvironmentVariable("GH_REPO");
        Environment.SetEnvironmentVariable("GH_REPO", inherited);
        try
        {
            var result = await GuardWithStdinAsync(Payload(command));

            if (refused)
                Assert.Contains("names another repository", result.AssertPreToolUseDenied());
            else
                result.AssertPreToolUseAllowed();
        }
        finally
        {
            Environment.SetEnvironmentVariable("GH_REPO", before);
        }
    }

    // C2: command position is the reading the dydo scanner uses, so these prefixes reach gh too.
    [Theory]
    [InlineData("nohup gh pr create --fill")]
    [InlineData("command gh pr create --fill")]
    [InlineData("exec gh pr ready 12")]
    [InlineData("env -- gh pr create --fill")]
    [InlineData("sudo -u me gh pr create --fill")]
    public async Task RunnerPrefixedPrCall_FromADirtyTree_IsDenied(string command)
    {
        await InitPushedProject();
        WriteFile("wip.txt", "w\n");

        var result = await GuardWithStdinAsync(Payload(command));

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("untracked files: wip.txt");
    }

    // B1: the envelope's script is the command every other guard rule reads too.
    [Fact]
    public async Task ShellEnvelope_OtherGuardRulesSeeTheScript()
    {
        await InitPushedProject();

        var result = await GuardWithStdinAsync(Payload(new[] { "bash", "-lc", "git push --force origin main" }, codex: true));

        result.AssertPreToolUseDenied();
        Assert.DoesNotContain("PR check", result.Stderr);
    }

    // ---- DYD-391 round 2 FAIL ----

    private async Task<CommandResult> GuardWithInheritedGhRepo(string? inherited, string payload)
    {
        var before = Environment.GetEnvironmentVariable("GH_REPO");
        Environment.SetEnvironmentVariable("GH_REPO", inherited);
        try
        {
            return await GuardWithStdinAsync(payload);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GH_REPO", before);
        }
    }

    // R2-1: the bash reading of a backtick continuation loses --head; it must not add a call on the dirty main tree.
    [Theory]
    [InlineData("gh pr create `\n  --head topic `\n  --title t --body '## Independent review'")]
    [InlineData("gh pr create `\r\n  --head topic `\r\n  --title t --body '## Independent review'")]
    public async Task BashLabelledContinuationNamingACleanWorktree_FromADirtyMain_IsAllowed(string command)
    {
        await InitDirtyMainWithCleanWorktree();
        SilenceReviewBlockWarn();

        var result = await GuardWithStdinAsync(Payload(command));

        result.AssertPreToolUseAllowed();
    }

    // R2-2: env's unset, ignore-environment and an empty GH_REPO drop the inherited GH_REPO, in order.
    [Theory]
    [InlineData("env -u GH_REPO gh pr ready 12", false)]
    [InlineData("env --unset=GH_REPO gh pr ready 12", false)]
    [InlineData("env --unset GH_REPO gh pr ready 12", false)]
    [InlineData("env -i gh pr ready 12", false)]
    [InlineData("GH_REPO= gh pr ready 12", false)]
    [InlineData("env -u GH_REPO GH_REPO=other/y gh pr ready 12", true)]
    [InlineData("env -i GH_REPO=other/y gh pr ready 12", true)]
    [InlineData("GH_REPO=acme/lc env -u OTHER gh pr ready 12", false)]
    public async Task EnvDroppingTheInheritedGhRepo_UsesTheCheckout(string command, bool refused)
    {
        await InitPushedProject();

        var result = await GuardWithInheritedGhRepo("other/x", Payload(command));

        if (refused)
            Assert.Contains("names another repository", result.AssertPreToolUseDenied());
        else
            result.AssertPreToolUseAllowed();
    }

    // R2-3: a literal owner or name in a placeholder api path is compared, each placeholder resolved on its own.
    [Theory]
    [InlineData("gh api repos/{owner}/other/pulls -X POST", true)]
    [InlineData("gh api repos/other/{repo}/pulls -X POST", true)]
    [InlineData("gh api repos/{owner}/{repo}/pulls -X POST -f title=t", false)]
    [InlineData("gh api repos/{owner}/LC/pulls -X POST -f title=t", false)]
    [InlineData("gh api repos/Acme/{repo}/pulls -X POST -f title=t", false)]
    public async Task ApiPathWithPlaceholders_ComparesItsLiteralSegments(string command, bool refused)
    {
        await InitPushedProject();
        SilenceReviewBlockWarn();

        var result = await GuardWithStdinAsync(Payload(command));

        if (refused)
            Assert.Contains("names another repository", result.AssertPreToolUseDenied());
        else
            result.AssertPreToolUseAllowed();
    }

    [Theory]
    [InlineData("acme/zz", "gh api repos/{owner}/lc/pulls -X POST -f title=t", false)]
    [InlineData("other/x", "gh api repos/{owner}/lc/pulls -X POST -f title=t", true)]
    [InlineData("other/lc", "gh api repos/acme/{repo}/pulls -X POST -f title=t", false)]
    public async Task ApiPathPlaceholder_ResolvesAgainstGhRepo(string inherited, string command, bool refused)
    {
        await InitPushedProject();
        SilenceReviewBlockWarn();

        var result = await GuardWithInheritedGhRepo(inherited, Payload(command));

        if (refused)
            Assert.Contains("names another repository", result.AssertPreToolUseDenied());
        else
            result.AssertPreToolUseAllowed();
    }

    // R2-4: env -S and --split-string are a string wrapper; a PR-shaped string is refused, a read-only one is not.
    [Theory]
    [InlineData("env -S 'gh pr ready 12'")]
    [InlineData("env --split-string='gh pr ready 12'")]
    [InlineData("env --split-string 'gh pr ready 12'")]
    public async Task EnvSplitStringPrCall_IsRefusedAsAWrapper(string command)
    {
        await InitPushedProject();
        WriteFile("wip.txt", "w\n");

        var result = await GuardWithStdinAsync(Payload(command));

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("run gh directly so the PR check can see it");
    }

    [Theory]
    [InlineData("env -S 'gh pr view 12'")]
    [InlineData("env --split-string='gh pr list --search ready'")]
    public async Task EnvSplitStringReadOnlyCall_FromADirtyTree_IsAllowed(string command)
    {
        await InitPushedProject();
        WriteFile("wip.txt", "w\n");

        var result = await GuardWithStdinAsync(Payload(command));

        result.AssertPreToolUseAllowed();
    }
}
