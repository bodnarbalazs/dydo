namespace DynaDocs.Tests.Integration;

using System.Text.Json;
using System.Text.Json.Nodes;
using DynaDocs.Commands;
using DynaDocs.Services;
using DynaDocs.Tests.Services;
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

    // Arms the review-block warn's marker for each command a test sends, so the warn passes and
    // the tree check decides. The marker is keyed by the command the guard reads (DYD-503).
    private void SilenceReviewBlockWarn(params string[] commands)
    {
        var markerDir = GuardCommand.GuardEnv.Load(TestDir).MarkerDir;
        Directory.CreateDirectory(markerDir);
        foreach (var command in commands)
            File.WriteAllText(Path.Combine(markerDir, GuardCommand.CommandWarnMarkerName(ConfigFactory.ReviewBlockPattern, command)), "spent");
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
        string toolName = "Bash", string? toolCwd = null)
    {
        List<string> misses = [];
        foreach (var command in commands)
        {
            var result = await GuardWithStdinAsync(Payload(command, toolName: toolName, toolCwd: toolCwd));
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

    // GS-06: help is no PR. The first line is the retro's original Codex batch. The review-block warn
    // is a configured nudge on the gh pr create text, not this rule, so it is spent first.
    [Theory]
    [InlineData("gh version; gh pr create --help; gh pr ready --help", "exec")]
    [InlineData("gh pr new -h", "Bash")]
    public async Task HelpForAPrVerb_FromADirtyTree_IsAllowed(string command, string toolName)
    {
        await InitPushedProject();
        WriteFile("wip.txt", "w\n");
        SilenceReviewBlockWarn(command);

        var result = await GuardWithStdinAsync(Payload(command, codex: toolName == "exec", toolName: toolName));

        result.AssertPreToolUseAllowed();
    }

    [Fact]
    public async Task HelpThenARealPrCall_FromADirtyTree_IsDenied()
    {
        await InitPushedProject();
        WriteFile("wip.txt", "w\n");

        var result = await GuardWithStdinAsync(Payload("gh pr create --help; " + PrCreate));

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("untracked files: wip.txt");
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

    // A PR body heredoc quoting a PR call is no call for this rule.
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

    [Theory]
    [InlineData("", null, null)]
    [InlineData("  ", null, null)]
    [InlineData("", "", null)]
    [InlineData("", null, "")]
    public async Task PayloadWithBlankCwd_ChecksTheProjectRoot(string cwd, string? toolCwd, string? workdir)
    {
        await InitPushedProject();
        WriteFile("wip.txt", "w\n");

        var result = await GuardWithStdinAsync(Payload("gh pr create --fill", cwd: cwd, toolCwd: toolCwd, workdir: workdir));

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
            GuardCommand.CommandWarnMarkerName(reviewBlockWarn.Pattern, "gh pr create --fill"));
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

    // DYD-639: a gh pr create taking its body from a file is checked as an inline body is when the
    // guard may read the file, a regular file in the call directory's work tree; else it warns.
    private const string ReviewedBody = "DYD-1\n\n## Independent review\n\nPASS - rubric/type: code\n";

    private const string ReviewBlockWarning = "carries no review block";

    [Theory]
    [InlineData("gh pr create --title t --body-file dydo/agents/body.md")]
    [InlineData("gh pr create --title t --body-file=dydo/agents/body.md")]
    public async Task PrCreate_InTreeBodyFileWithTheReviewBlock_DoesNotWarn(string command)
    {
        await InitPushedProject();
        WriteFile("dydo/agents/body.md", ReviewedBody);

        (await GuardWithStdinAsync(Payload(command))).AssertPreToolUseAllowed();
    }

    [Theory]
    [InlineData("gh pr create --title t -F dydo/agents/body.md")]
    [InlineData("gh pr create --title t -Fdydo/agents/body.md")]
    public async Task PrCreate_ShortFormBodyFileWithTheReviewBlock_DoesNotWarn(string command)
    {
        await InitPushedProject();
        WriteFile("dydo/agents/body.md", ReviewedBody);

        (await GuardWithStdinAsync(Payload(command))).AssertPreToolUseAllowed();
    }

    [Fact]
    public async Task PrCreate_BodyFileNamedFromTheCallDirectory_DoesNotWarn()
    {
        await InitPushedProject();
        WriteFile("dydo/agents/body.md", ReviewedBody);

        (await GuardWithStdinAsync(Payload("gh pr create -F agents/body.md", toolCwd: DydoDir))).AssertPreToolUseAllowed();
    }

    // An absolute body file is read as a relative one is, for a lone call only. A Windows path needs
    // the PowerShell reading, which a Bash call there shares with bash's, which keeps no backslash.
    [SkippableFact]
    public async Task PrCreate_AbsoluteBodyFile_DoesNotWarn()
    {
        Skip.If(OperatingSystem.IsWindows(), "A Bash call on Windows is also read as bash, which takes no backslash.");
        await InitPushedProject();
        WriteFile("dydo/agents/body.md", ReviewedBody);
        var absolute = Path.Combine(TestDir, "dydo", "agents", "body.md");

        Assert.Empty(await Misses(denied: false, [$"gh pr create -F '{absolute}'", $"gh pr create -F {absolute}"]));
    }

    // The plain spellings stay read: quoted with a space, and attached.
    [Fact]
    public async Task PrCreate_PlainlyWrittenBodyFile_DoesNotWarn()
    {
        await InitPushedProject();
        WriteFile("dydo/agents/pr body.md", ReviewedBody);

        Assert.Empty(await Misses(denied: false,
        [
            "gh pr create -F \"dydo/agents/pr body.md\"",
            "gh pr create --body-file='dydo/agents/pr body.md'",
        ]));
    }

    [Fact]
    public async Task PrCreate_CodexArgvWithAnInTreeBodyFile_DoesNotWarn()
    {
        await InitPushedProject();
        WriteFile("dydo/agents/body.md", ReviewedBody);

        (await GuardWithStdinAsync(Payload(new[] { "gh", "pr", "create", "-F", "dydo/agents/body.md" }, codex: true)))
            .AssertPreToolUseAllowed();
    }

    // A user's dydo.json copy of the review-block nudge keeps its pattern, so its body file is read
    // too; a custom nudge on gh pr create reads the command alone.
    [Theory]
    [InlineData(@"(?:^|[;&|]\s*)gh\s+pr\s+create\b(?![\s\S]*Independent review)", false)]
    [InlineData(@"gh\s+pr\s+create\b(?![\s\S]*Independent review)", true)]
    public async Task BlockNudgeOnPrCreate_WithAnInTreeBodyFile_ReadsItOnlyForTheReviewBlockPattern(string pattern, bool denied)
    {
        await InitPushedProject();
        var config = JsonNode.Parse(ReadFile("dydo.json"))!.AsObject();
        config["nudges"] = new JsonArray(new JsonObject { ["pattern"] = pattern, ["message"] = "Custom PR block.", ["severity"] = "block" });
        WriteFile("dydo.json", config.ToJsonString());
        // The tree check reads the local remote-tracking ref, so moving it stands in for a push.
        Commit(TestDir, "nudge");
        Git(TestDir, "update-ref", "refs/remotes/origin/feature", "HEAD");
        WriteFile("dydo/agents/body.md", ReviewedBody);

        var result = await GuardWithStdinAsync(Payload("gh pr create -F dydo/agents/body.md"));

        if (denied) Assert.StartsWith("BLOCKED: Custom PR block.", result.AssertPreToolUseDenied());
        else result.AssertPreToolUseAllowed();
    }

    [Fact]
    public async Task PrCreate_InTreeBodyFileWithoutTheReviewBlock_Warns()
    {
        await InitPushedProject();
        WriteFile("dydo/agents/body.md", "DYD-1\n\n## Summary\n");

        Assert.Empty(await Misses(denied: true, ["gh pr create --body-file dydo/agents/body.md"], ReviewBlockWarning));
    }

    [Fact]
    public async Task PrCreate_BodyFileOutsideTheWorkTree_Warns()
    {
        await InitPushedProject();
        using var outside = new OutsideDirectory();

        Assert.Empty(await Misses(denied: true,
            [$"gh pr create -F '{outside.Body}'", $"gh pr create -F ../{outside.Name}/body.md"], ReviewBlockWarning));
    }

    [SkippableTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PrCreate_InTreeLinkToAnOutsideBodyFile_Warns(bool linkedDirectory)
    {
        Skip.If(OperatingSystem.IsWindows() && !linkedDirectory, "Creating file symbolic links needs Windows developer mode.");
        await InitPushedProject();
        using var outside = new OutsideDirectory();
        Directory.CreateDirectory(Path.Combine(DydoDir, "agents"));
        string path;
        if (linkedDirectory)
        {
            DirectoryLinks.Create(Path.Combine(DydoDir, "agents", "linked"), outside.Path);
            path = "dydo/agents/linked/body.md";
        }
        else
        {
            File.CreateSymbolicLink(Path.Combine(DydoDir, "agents", "body.md"), outside.Body);
            path = "dydo/agents/body.md";
        }

        Assert.Empty(await Misses(denied: true, [$"gh pr create -F {path}"], ReviewBlockWarning));
    }

    [Fact]
    public async Task PrCreate_BodyFileTheGuardMayNotRead_Warns()
    {
        await InitPushedProject();
        WriteFile("dydo/_system/.local/body.md", ReviewedBody);

        Assert.Empty(await Misses(denied: true,
        [
            "gh pr create -F dydo/agents/missing.md",
            "gh pr create -F dydo",
            "gh pr create -F -",
            "gh pr create -F dydo/_system/.local/body.md",
        ], ReviewBlockWarning));
    }

    [Fact]
    public async Task PrCreate_BodyFileOverTheCap_Warns()
    {
        await InitPushedProject();
        WriteFile("dydo/agents/body.md", ReviewedBody + new string('x', 256 * 1024));

        Assert.Empty(await Misses(denied: true, ["gh pr create -F dydo/agents/body.md"], ReviewBlockWarning));
    }

    [Fact]
    public async Task PrCreate_BodyFileOutsideGit_Warns()
    {
        await InitProjectAsync("none");
        WriteFile("dydo/agents/body.md", ReviewedBody);

        Assert.Empty(await Misses(denied: true, ["gh pr create -F dydo/agents/body.md"], ReviewBlockWarning));
    }

    // GIT_DIR and GIT_WORK_TREE exported to the session name one tree for every directory; a file
    // outside the call directory's tree still warns, and an in-tree one is still read.
    [Fact]
    public async Task PrCreate_BodyFileOutsideTheWorkTree_WithGitDirAndWorkTreeExported_Warns()
    {
        await InitPushedProject();
        using var outside = new OutsideDirectory();
        WriteFile("dydo/agents/body.md", ReviewedBody);
        var saved = new[] { "GIT_DIR", "GIT_WORK_TREE" }.ToDictionary(name => name, Environment.GetEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable("GIT_DIR", Path.Combine(TestDir, ".git"));
            Environment.SetEnvironmentVariable("GIT_WORK_TREE", TestDir);

            Assert.Empty(await Misses(denied: true, [$"gh pr create -F '{outside.Body}'"], ReviewBlockWarning));
            (await GuardWithStdinAsync(Payload("gh pr create -F dydo/agents/body.md"))).AssertPreToolUseAllowed();
        }
        finally
        {
            foreach (var (name, value) in saved)
                Environment.SetEnvironmentVariable(name, value);
        }
    }

    // Each gh pr create is cleared only by its own body, inline or from its own file, so one
    // call's reviewed file leaves another call's warning standing. A body file is read only for a
    // lone call (ruling 7), so no file on a line of two calls clears either; the command text
    // still clears every call it shows the block after.
    [Fact]
    public async Task PrCreates_EachClearedOnlyByItsOwnBody()
    {
        await InitPushedProject();
        WriteFile("dydo/agents/body.md", ReviewedBody);
        WriteFile("dydo/agents/other.md", ReviewedBody);
        WriteFile("dydo/agents/plain.md", "DYD-1\n\n## Summary\n");

        Assert.Empty(await Misses(denied: true,
        [
            "gh pr create -F dydo/agents/body.md; gh pr create -F dydo/agents/missing.md",
            "gh pr create -F dydo/agents/body.md; gh pr create -F dydo/agents/plain.md",
            "gh pr create -F dydo/agents/body.md; gh pr create --title other",
            "gh pr create -F dydo/agents/plain.md; gh pr create -F dydo/agents/body.md",
            "gh pr create --title other; gh pr create -F dydo/agents/body.md",
            "gh pr create -F dydo/agents/body.md; gh pr create -F dydo/agents/other.md",
            "gh pr create --body '## Independent review'; gh pr create -F dydo/agents/body.md",
        ], ReviewBlockWarning));
        Assert.Empty(await Misses(denied: false,
        [
            "gh pr create -F dydo/agents/body.md && gh pr create --body '## Independent review'",
        ]));
    }

    // R2-B1: bash hands gh the expanded operand, so an operand the guard cannot resolve to a literal
    // path keeps the warning, even when its literal part names an in-tree reviewed file.
    [Fact]
    public async Task PrCreate_BodyFileOperandWithAnExpansion_Warns()
    {
        await InitPushedProject();
        WriteFile("dydo/agents/reviewed.md", ReviewedBody);
        WriteFile("dydo/agents/reviewed.md.plain", "DYD-1\n\n## Summary\n");
        // A file literally named as the unexpanded operand, which bash never opens.
        WriteFile("dydo/agents/reviewed.md$SUFFIX", ReviewedBody);

        Assert.Empty(await Misses(denied: true,
        [
            "gh pr create -F \"dydo/agents/reviewed.md$(printf .plain)\"",
            "gh pr create -F \"dydo/agents/reviewed.md`printf .plain`\"",
            "SUFFIX=.plain; gh pr create -F \"dydo/agents/reviewed.md$SUFFIX\"",
        ], ReviewBlockWarning));
        (await GuardWithStdinAsync(Payload("gh pr create -F \"dydo/agents/reviewed.md\""))).AssertPreToolUseAllowed();
    }

    // R3-B1: bash opens a relative body file from wherever the line has moved, so a relative operand
    // is read only after commands that cannot move the shell's directory; here the reviewed body.md
    // sits in the call directory and the unreviewed one in sub.
    [Fact]
    public async Task PrCreate_RelativeBodyFileAfterACommandThatMayMoveTheDirectory_Warns()
    {
        await InitPushedProject();
        WriteFile("dydo/agents/body.md", ReviewedBody);
        WriteFile("dydo/agents/sub/body.md", "DYD-1\n\n## Summary\n");
        WriteFile("dydo/agents/env.sh", "cd sub\n");
        var directory = Path.Combine(DydoDir, "agents");

        Assert.Empty(await Misses(denied: true,
        [
            "git status --short && cd sub && gh pr create -F body.md",
            "git status --short && pushd sub && gh pr create -F body.md",
            "git status --short; source env.sh; gh pr create -F body.md",
            "git status --short; . ./env.sh; gh pr create -F body.md",
            "echo ${ cd sub; } && gh pr create -F body.md",
            "(git status) && gh pr create -F body.md",
            "{ git status; } && gh pr create -F body.md",
        ], ReviewBlockWarning, toolCwd: directory));
        Assert.Empty(await Misses(denied: true, ["git status; Set-Location sub; gh pr create -F body.md"],
            ReviewBlockWarning, toolName: "PowerShell", toolCwd: directory));
    }

    // R3-B2: the guard reads only a path of plain names. It emulates no . or .. and takes no empty
    // name or trailing separator, which the OS opens differently or not at all.
    [Fact]
    public async Task PrCreate_BodyFileOperandWithADotNameOrATrailingSeparator_Warns()
    {
        await InitPushedProject();
        WriteFile("dydo/agents/body.md", ReviewedBody);
        WriteFile("dydo/agents/plain.md", "DYD-1\n\n## Summary\n");

        Assert.Empty(await Misses(denied: true,
        [
            "gh pr create -F dydo/agents/missing/../body.md",
            "gh pr create -F dydo/agents/plain.md/../body.md",
            "gh pr create -F dydo/agents/./body.md",
            "gh pr create -F dydo/agents//body.md",
            "gh pr create -F dydo/agents/body.md/",
            "gh pr create -F dydo/agents/body.md/.",
        ], ReviewBlockWarning));
    }

    // R3-B3: bash keeps a trailing backslash, an escaped carriage return and a bare one in the word,
    // so an operand not written plainly keeps the warning.
    [Fact]
    public async Task PrCreate_BodyFileOperandBashReadsDifferently_Warns()
    {
        await InitPushedProject();
        WriteFile("dydo/agents/body.md", ReviewedBody);

        Assert.Empty(await Misses(denied: true,
        [
            "gh pr create -F dydo/agents/body.md\\",
            "gh pr create -F dydo/agents/body\\\r\n.md",
            "gh pr create -F dydo/agents/body.md\r\n",
        ], ReviewBlockWarning));
    }

    // R3-S1: the operand is checked as written, before gh's {repo} placeholder is spelt :repo.
    [SkippableFact]
    public async Task PrCreate_BodyFileOperandWithAGhPlaceholder_Warns()
    {
        Skip.If(OperatingSystem.IsWindows(), "A Windows file name takes no colon.");
        await InitPushedProject();
        WriteFile("dydo/agents/reviewed.md:repo", ReviewedBody);
        WriteFile("dydo/agents/reviewed.md{repo}", "DYD-1\n\n## Summary\n");

        Assert.Empty(await Misses(denied: true, ["gh pr create -F 'dydo/agents/reviewed.md{repo}'"], ReviewBlockWarning));
    }

    // Addendum 5: gh takes the last body file it is given. With EXTRA='-F dydo/agents/plain.md'
    // exported, or a file named -F beside the call, bash hands gh the unreviewed plain.md last, so a
    // word of the call that may expand into more arguments keeps the warning. So do PowerShell's
    // array variable and splat.
    [Fact]
    public async Task PrCreate_WordThatMayExpandIntoMoreArguments_Warns()
    {
        await InitPushedProject();
        WriteFile("dydo/agents/reviewed.md", ReviewedBody);
        WriteFile("dydo/agents/plain.md", "DYD-1\n\n## Summary\n");

        Assert.Empty(await Misses(denied: true,
        [
            "gh pr create -F dydo/agents/reviewed.md $EXTRA",
            "gh pr create -F dydo/agents/reviewed.md `printf -- '-F dydo/agents/plain.md'`",
            "gh pr create -F dydo/agents/reviewed.md \"$@\"",
            "gh pr create -F dydo/agents/reviewed.md \"${EXTRA[@]}\"",
            "gh pr create -F dydo/agents/reviewed.md -? dydo/agents/plain.md",
            "gh pr create -F dydo/agents/reviewed.md {-F,dydo/agents/plain.md}",
        ], ReviewBlockWarning));
        Assert.Empty(await Misses(denied: true,
            ["gh pr create -F dydo/agents/reviewed.md $extra", "gh pr create -F dydo/agents/reviewed.md @extra"],
            ReviewBlockWarning, toolName: "PowerShell"));
    }

    // Quoted words that cannot split leave a reviewed body file read: literal titles in either shell.
    [Fact]
    public async Task PrCreate_LiteralWordsBesideTheBodyFile_DoNotWarn()
    {
        await InitPushedProject();
        WriteFile("dydo/agents/reviewed.md", ReviewedBody);

        Assert.Empty(await Misses(denied: false,
            ["gh pr create --title 'Fix $HOME * {a,b}' -F dydo/agents/reviewed.md", "gh pr create --title \"Why? [WIP]\" -F dydo/agents/reviewed.md"]));
        Assert.Empty(await Misses(denied: false,
            ["gh pr create --title 'Fix $x @y' -F dydo/agents/reviewed.md"], toolName: "PowerShell"));
    }

    // Ruling 7: no expansion anywhere in the line, so a double-quoted scalar keeps the warning too.
    [Fact]
    public async Task PrCreate_DoubleQuotedScalarBesideTheBodyFile_Warns()
    {
        await InitPushedProject();
        WriteFile("dydo/agents/reviewed.md", ReviewedBody);

        Assert.Empty(await Misses(denied: true, ["gh pr create --title \"$TITLE\" -F dydo/agents/reviewed.md"], ReviewBlockWarning));
    }

    // R4-B1: the shell opens the call's redirections before gh runs, so a file target such as the
    // body file itself is emptied before gh reads it. A file target on the call keeps the warning.
    [Fact]
    public async Task PrCreate_CallRedirectingToAFile_Warns()
    {
        await InitPushedProject();
        WriteFile("dydo/agents/body.md", ReviewedBody);

        Assert.Empty(await Misses(denied: true,
        [
            "gh pr create -F dydo/agents/body.md > dydo/agents/body.md",
            "gh pr create -F dydo/agents/body.md >> dydo/agents/body.md",
            "gh pr create -F dydo/agents/body.md &> dydo/agents/body.md",
            "gh pr create -F dydo/agents/body.md 2>dydo/agents/body.md",
            "gh pr create -F dydo/agents/body.md >| dydo/agents/body.md",
        ], ReviewBlockWarning));
    }

    [Fact]
    public async Task PrCreate_PowerShellCallRedirectingToAFile_Warns()
    {
        await InitPushedProject();
        WriteFile("dydo/agents/body.md", ReviewedBody);

        Assert.Empty(await Misses(denied: true,
            ["gh pr create -F dydo/agents/body.md > dydo/agents/body.md", "gh pr create -F dydo/agents/body.md *> dydo/agents/body.md"],
            ReviewBlockWarning, toolName: "PowerShell"));
    }

    // R4-B2: a command before gh runs before gh opens the body file, so a git switch, checkout or
    // stash may replace the reviewed file. Only read-only commands may come first (ruling 6).
    [Fact]
    public async Task PrCreate_BodyFileAfterACommandThatMayChangeIt_Warns()
    {
        await InitPushedProject();
        WriteFile("dydo/agents/pr-body.md", ReviewedBody);

        Assert.Empty(await Misses(denied: true,
        [
            "git switch plain && gh pr create -F pr-body.md",
            "git checkout x && gh pr create -F pr-body.md",
            "git -C x status && gh pr create -F pr-body.md",
            "git stash pop && gh pr create -F pr-body.md",
        ], ReviewBlockWarning, toolCwd: Path.Combine(DydoDir, "agents")));
    }

    // Ruling 7: an absolute body file after any earlier command keeps the warning, as a relative one does.
    [SkippableFact]
    public async Task PrCreate_AbsoluteBodyFileAfterAnEarlierCommand_Warns()
    {
        Skip.If(OperatingSystem.IsWindows(), "A Bash call on Windows is also read as bash, which takes no backslash.");
        await InitPushedProject();
        WriteFile("dydo/agents/body.md", ReviewedBody);
        var absolute = Path.Combine(TestDir, "dydo", "agents", "body.md");

        Assert.Empty(await Misses(denied: true,
        [
            $"printf 'Summary only' > {absolute} && gh pr create -F {absolute}",
            $"git switch plain && gh pr create -F {absolute}",
            $"git status --short && cd dydo && gh pr create -F {absolute}",
            $"git status --short && gh pr create -F {absolute}",
        ], ReviewBlockWarning));
    }

    // Nothing may run alongside gh, neither the line's next command once a lone & puts the call in
    // the background, nor any other pipeline member.
    [Fact]
    public async Task PrCreate_BodyFileBesideACommandRunningAlongside_Warns()
    {
        await InitPushedProject();
        WriteFile("dydo/agents/body.md", ReviewedBody);

        Assert.Empty(await Misses(denied: true,
        [
            "gh pr create -F dydo/agents/body.md & git status",
            "gh pr create -F dydo/agents/body.md | tee dydo/agents/body.md",
        ], ReviewBlockWarning));
    }

    // Ruling 7: the guard reads the body file only for a lone gh pr create, so any other command on
    // the line, a redirection or an expansion keeps the warning, though the file holds the block.
    // The chained dydo pr-ready form warns too: that cost is accepted. With
    // DIFF_OPTIONS=--output=dydo/agents/body.md exported, an earlier git diff writes the file.
    [Fact]
    public async Task PrCreate_BodyFileOnALineHoldingMoreThanTheCall_Warns()
    {
        await InitPushedProject();
        WriteFile("dydo/agents/body.md", ReviewedBody);

        Assert.Empty(await Misses(denied: true,
        [
            "dydo pr-ready --body dydo/agents/body.md && gh pr create --base feature/x --title \"DYD-1 fix: thing\" --body-file dydo/agents/body.md",
            "dydo pr-ready --body dydo/agents/body.md && gh pr create --base feature/x --title \"DYD-1 fix: thing\" -F dydo/agents/body.md",
            "dydo pr-ready --body dydo/agents/body.md && gh pr create --title t -F dydo/agents/body.md",
            "git status && gh pr create -F dydo/agents/body.md",
            "echo x; gh pr create -F dydo/agents/body.md",
            "gh pr create -F dydo/agents/body.md 2>&1 | cat",
            "gh pr create -F dydo/agents/body.md < /dev/null",
            "gh pr create -F dydo/agents/body.md && echo done",
            "gh pr create -F dydo/agents/body.md; true",
            "gh pr create --title \"$TITLE\" -F dydo/agents/body.md",
            "echo \"$(git diff $DIFF_OPTIONS)\" && gh pr create --title \"DYD-1 fix: thing\" -F dydo/agents/body.md",
            "git diff \"$DIFF_OPTION\" && gh pr create --title t -F dydo/agents/body.md",
        ], ReviewBlockWarning));
        Assert.Empty(await Misses(denied: true,
            ["git status; gh pr create -F dydo/agents/body.md", "git status --short; gh pr create -F dydo/agents/body.md 2>&1 | cat"],
            ReviewBlockWarning, toolName: "PowerShell"));
    }

    // A directory beside the test's tree, outside it, holding a body with the review block.
    private sealed class OutsideDirectory : IDisposable
    {
        public string Name { get; } = "dydo-outside-" + Guid.NewGuid().ToString("N")[..8];

        public string Path => System.IO.Path.Combine(System.IO.Path.GetTempPath(), Name);

        public string Body => System.IO.Path.Combine(Path, "body.md");

        public OutsideDirectory()
        {
            Directory.CreateDirectory(Path);
            File.WriteAllText(Body, ReviewedBody);
        }

        public void Dispose() => Directory.Delete(Path, recursive: true);
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
        SilenceReviewBlockWarn(command);

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
        SilenceReviewBlockWarn(commands);

        Assert.Empty(await Misses(denied: false, commands));
    }

    // LC CPR:283: run from a subdirectory, the check reads the repository that directory is in.
    [Fact]
    public async Task PrCreate_FromASubdirectoryOfADirtyRepo_IsDenied()
    {
        await InitPushedProject();
        WriteFile("wip.txt", "w\n");
        var subdirectory = Path.Combine(TestDir, "src", "app");
        Directory.CreateDirectory(subdirectory);

        var result = await GuardWithStdinAsync(Payload("gh pr create --fill", cwd: subdirectory));

        Assert.Contains("untracked files: wip.txt", result.AssertPreToolUseDenied());
    }

    // LC CPR:328-349: a substitution's command runs, so its PR call is checked.
    [Fact]
    public async Task PrCallInACommandSubstitution_FromADirtyTree_IsDenied()
    {
        await InitPushedProject();
        WriteFile("wip.txt", "w\n");
        string[] commands = ["`gh pr create --fill`", "echo $(gh pr create --fill)", "git commit -m \"$(gh pr create --fill)\""];

        Assert.Empty(await Misses(denied: true, commands, "untracked files: wip.txt"));
    }

    // A bash backtick substitution mid-line or inside double quotes runs its command too.
    [Fact]
    public async Task PrCallInABacktickSubstitution_FromADirtyTree_IsDenied()
    {
        await InitPushedProject();
        WriteFile("wip.txt", "w\n");
        string[] commands = ["echo `gh pr create --fill`", "git commit -m \"`gh pr create --fill`\""];

        Assert.Empty(await Misses(denied: true, commands, "untracked files: wip.txt"));
    }

    // Single quotes substitute nothing in bash, and a PowerShell backtick is an escape, not a substitution.
    [Theory]
    [InlineData("Bash", "git commit -m '`gh pr create --fill`'")]
    [InlineData("PowerShell", "Write-Host `\"hi`\"")]
    [InlineData("PowerShell", "Write-Host \"`gh pr create --fill`\"")]
    public async Task BacktickThatSubstitutesNothing_FromADirtyTree_IsAllowed(string toolName, string command)
    {
        await InitPushedProject();
        WriteFile("wip.txt", "w\n");

        var result = await GuardWithStdinAsync(Payload(command, toolName: toolName));

        result.AssertPreToolUseAllowed();
    }

    // LC CPR:251-281: a staged-only change and a branch with no upstream are each not ready.
    [Fact]
    public async Task PrCreate_FromAStagedOnlyTree_IsDenied()
    {
        await InitPushedProject();
        WriteFile("wip.txt", "w\n");
        Git(TestDir, "add", "wip.txt");

        var reason = (await GuardWithStdinAsync(Payload("gh pr create --fill"))).AssertPreToolUseDenied();

        Assert.Contains("staged changes: wip.txt", reason);
        Assert.DoesNotContain("untracked files", reason);
    }

    [Fact]
    public async Task PrCreate_FromABranchWithNoUpstream_IsDenied()
    {
        await InitPushedProject();
        Git(TestDir, "checkout", "-b", "local-only");

        var reason = (await GuardWithStdinAsync(Payload("gh pr create --fill"))).AssertPreToolUseDenied();

        Assert.Contains("HEAD has no upstream", reason);
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
        SilenceReviewBlockWarn(command);

        var result = await GuardWithStdinAsync(Payload(command));

        result.AssertPreToolUseAllowed();
    }

    // Balazs's 2026-10-27 ruling: a PR call inside a shell wrapper fails closed, with no decoding,
    // whatever the tree's state. LC check-pr-ready.test.mjs:328-347, 2454-2455, 2506, 2530.
    [Fact]
    public async Task WrappedPrCall_FromACleanTree_IsRefused()
    {
        await InitPushedProject();
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
        SilenceReviewBlockWarn(commands);

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
        SilenceReviewBlockWarn("gh pr create --fill");

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
        SilenceReviewBlockWarn(command);

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
        SilenceReviewBlockWarn(command);

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
        string[] commands =
        [
            "bash -c 'gh api -X POST -H \"Content-Type: application/json; charset=utf-8\" repos/acme/lc/pulls --input body.json'",
            "pwsh -Command \"gh pr create --title 'a; b' --fill\"",
            "bash -c 'echo \"x | y\" && gh pr ready 12'",
        ];
        SilenceReviewBlockWarn(commands);

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
        SilenceReviewBlockWarn(command);
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
        SilenceReviewBlockWarn(command);

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
        SilenceReviewBlockWarn(command);

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
        SilenceReviewBlockWarn(command);

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

    [Theory]
    [InlineData("x=\"$(case a in a) echo y;; esac)\"; gh pr create --title t --body '## Independent review'")]
    [InlineData("cat <<EOF\n$(case a in a) echo y;; esac)\nEOF\ngh pr create --title t --body '## Independent review'")]
    [InlineData("echo \"$(case a in a) echo y;; esac)\" && gh pr ready")]
    [InlineData("arch=\"$(case \"$(uname -m)\" in\n  x86_64) echo amd64;;\n  *) echo other;;\nesac)\"\ngh pr create --fill")]
    [InlineData("x=\"$(echo y)\"; gh pr create --title t --body '## Independent review'")]
    public async Task FailedReading_LaterPrCall_FromADirtyTree_IsDeniedOnFirstAndRetry(string command)
    {
        await InitPushedProject();
        WriteFile("readme.md", "changed\n");
        var canary = ReadFile("dydo/files-off-limits.md");

        var first = await GuardWithStdinAsync(Payload(command));
        var retry = await GuardWithStdinAsync(Payload(command));

        foreach (var result in new[] { first, retry })
        {
            Assert.False(result.IsPreToolUseAllowed);
            var reason = result.AssertPreToolUseDenied();
            Assert.True(reason.Contains("PR check") || reason.Contains("unstaged changes: readme.md"), reason);
        }
        Assert.Equal(canary, ReadFile("dydo/files-off-limits.md"));
    }

    [Theory]
    [InlineData("gh pr create --title 'Independent review' --help; gh pr ready -h")]
    [InlineData("cat <<'EOF'\nx=\"$(case a in a) echo y;; esac)\"; gh pr ready\nEOF")]
    [InlineData("echo '$(case a in a) gh pr ready;; esac)'")]
    public async Task FailedReading_HelpAndLiteralControls_FromADirtyTree_StayAllowed(string command)
    {
        await InitPushedProject();
        WriteFile("readme.md", "changed\n");
        SilenceReviewBlockWarn(command);

        (await GuardWithStdinAsync(Payload(command))).AssertPreToolUseAllowed();
        (await GuardWithStdinAsync(Payload(command))).AssertPreToolUseAllowed();
    }

    [Theory]
    [InlineData("gh pr create --title \"$(case a in a) echo t;; esac)\" --head missing")]
    [InlineData("gh pr create --title \"$(case a in a) echo t;; esac)\" --repo other/repo")]
    public async Task FailedReading_TruncatedTarget_FromACleanTree_IsRefusedUnread(string command)
    {
        await InitPushedProject();

        foreach (var result in new[] { await GuardWithStdinAsync(Payload(command)),
                     await GuardWithStdinAsync(Payload(command)) })
        {
            Assert.False(result.IsPreToolUseAllowed);
            Assert.Contains("PR check", result.AssertPreToolUseDenied());
        }
    }

    // DYD-515: "cli" is dydo guard --command, which reads the line both ways (Unknown); an argv
    // array is a Codex call. Inert input strings, never shell programs.
    private async Task<CommandResult> Attempt(string tool, object command) => tool == "cli"
        ? await RunAsync(GuardCommand.Create(), "--command", (string)command)
        : await GuardWithStdinAsync(Payload(command, codex: command is string[], toolName: tool));

    private static bool Allowed(CommandResult result) => result.ExitCode == 0 && result.Stdout.Length == 0;

    public static TheoryData<string, object> FailedReadingsWithoutGh => new()
    {
        { "Bash", PrCallScannerTests.ArchIdiom },
        { "Bash", PrCallScannerTests.EchoCase },
        { "PowerShell", PrCallScannerTests.EscapedApostrophe },
        { "cli", PrCallScannerTests.EscapedApostrophe },
        { "cli", PrCallScannerTests.ArchIdiom },
        { "Bash", "bash -c '" + PrCallScannerTests.EchoCase + "'" },
        { "PowerShell", "pwsh -Command 'v=$(date); echo it\\''s'" },
        { "Bash", new[] { "bash", "-lc", PrCallScannerTests.ArchIdiom } },
        { "Bash", new[] { "pwsh", "-Command", PrCallScannerTests.EscapedApostrophe } },
        { "Bash", new[] { "echo", PrCallScannerTests.EchoCase } },
    };

    [Theory]
    [MemberData(nameof(FailedReadingsWithoutGh))]
    public async Task FailedReading_WithoutAGhWord_FromACleanOrDirtyTree_IsAllowedOnFirstAndRetry(string tool, object command)
    {
        await InitPushedProject();
        List<CommandResult> results = [await Attempt(tool, command), await Attempt(tool, command)];
        WriteFile("readme.md", "changed\n");
        results.AddRange([await Attempt(tool, command), await Attempt(tool, command)]);

        Assert.All(results, result => Assert.True(Allowed(result), $"exit {result.ExitCode}\n{result.Stdout}\n{result.Stderr}"));
    }

    public static TheoryData<string, object> FailedReadingsWithALaterGhWord => new()
    {
        { "Bash", PrCallScannerTests.ArchIdiom + "\n/usr/bin/gh pr create --fill" },
        { "Bash", PrCallScannerTests.EchoCase + " && GH.EXE pr ready" },
        { "Bash", PrCallScannerTests.EchoCase + " \"$(gh pr ready)\"" },
        { "PowerShell", PrCallScannerTests.EscapedApostrophe + "; C:\\tools\\gh.exe pr ready" },
        { "cli", PrCallScannerTests.FailsBothReadings + "; gh pr ready" },
        { "Bash", "bash -c '" + PrCallScannerTests.EchoCase + "; gh pr ready'" },
        { "Bash", new[] { "bash", "-lc", PrCallScannerTests.EchoCase + "; gh pr ready" } },
        { "Bash", new[] { "pwsh", "-Command", PrCallScannerTests.EscapedApostrophe + "; gh pr ready" } },
        { "Bash", new[] { "echo", PrCallScannerTests.FailsBothReadings + "; gh pr ready" } },
    };

    // A clean tree would let a call the reading found through, so only the unread refusal denies these.
    [Theory]
    [MemberData(nameof(FailedReadingsWithALaterGhWord))]
    public Task FailedReading_LaterGhWord_FromACleanTree_IsRefusedUnreadOnFirstAndRetry(string tool, object command) =>
        AssertRefusedUnreadFromACleanTree(tool, command);

    // An encoded command shows no gh word; pwsh or powershell reads it.
    public static TheoryData<string, object> FailedReadingsWithALaterEncodedCommand => new()
    {
        { "Bash", PrCallScannerTests.EchoCase + "; pwsh -EncodedCommand " + PrCallScannerTests.EncodedGhPrReady },
        { "PowerShell", PrCallScannerTests.EscapedApostrophe + "; powershell -ec " + PrCallScannerTests.EncodedGhPrReady },
        { "Bash", "bash -c '" + PrCallScannerTests.EchoCase + "; pwsh -enc " + PrCallScannerTests.EncodedGhPrReady + "'" },
        { "Bash", new[] { "bash", "-lc", PrCallScannerTests.EchoCase + "; pwsh -EncodedCommand " + PrCallScannerTests.EncodedGhPrReady } },
    };

    [Theory]
    [MemberData(nameof(FailedReadingsWithALaterEncodedCommand))]
    public Task FailedReading_LaterEncodedCommand_FromACleanTree_IsRefusedUnreadOnFirstAndRetry(string tool, object command) =>
        AssertRefusedUnreadFromACleanTree(tool, command);

    private async Task AssertRefusedUnreadFromACleanTree(string tool, object command)
    {
        await InitPushedProject();

        foreach (var result in new[] { await Attempt(tool, command), await Attempt(tool, command) })
        {
            Assert.False(Allowed(result), $"allowed: {command}");
            Assert.Contains("PR check", result.Stdout + result.Stderr);
        }
    }
}
