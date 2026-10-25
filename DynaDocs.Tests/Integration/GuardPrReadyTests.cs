namespace DynaDocs.Tests.Integration;

using System.Text.Json;
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

    [Fact]
    public async Task CodexPayloadWithoutCwd_ChecksTheProjectRoot()
    {
        await InitPushedProject();
        WriteFile("wip.txt", "w\n");

        var result = await GuardWithStdinAsync(Payload(PrCreate, withCwd: false, codex: true));

        var reason = result.AssertPreToolUseDenied();
        Assert.Contains($"Checked: {TestDir} (the project root; the host sent no cwd)", reason);
    }

    [Fact]
    public async Task OutsideGit_IsAllowed()
    {
        await InitProjectAsync("none");

        var result = await GuardWithStdinAsync(Payload(PrCreate));

        result.AssertPreToolUseAllowed();
    }
}
