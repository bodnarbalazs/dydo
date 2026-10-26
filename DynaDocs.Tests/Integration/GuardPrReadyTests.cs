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

    private async Task InitPushedProject()
    {
        await InitProjectAsync("none");
        File.AppendAllText(Path.Combine(TestDir, ".gitignore"), "\n/origin.git/\n");
        Create(TestDir, Path.Combine(TestDir, "origin.git"));
    }

    private string Payload(string command, bool withCwd = true, bool codex = false)
    {
        var payload = new Dictionary<string, object?>
        {
            ["session_id"] = TestSessionId,
            ["tool_name"] = "Bash",
            ["tool_input"] = new { command },
        };
        if (withCwd) payload["cwd"] = TestDir;
        if (codex) payload["turn_id"] = "turn-1";
        return JsonSerializer.Serialize(payload);
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
}
