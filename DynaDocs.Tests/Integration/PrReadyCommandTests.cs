namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;
using DynaDocs.Services;
using static DynaDocs.Tests.Integration.PushedRepository;

/// <summary>
/// <c>dydo pr-ready</c> on a real temp repository pushed to a bare origin (DYD-374).
/// </summary>
[Collection("Integration")]
public sealed class PrReadyCommandTests : IDisposable
{
    private const string GoodBody = """
        Closes DYD-374.

        ## Independent review

        ```text
        PASS - rubric/type: code; contract DYD-374 description; candidate abc; base def; reviewer/model: r. Checks: ok.
        ```
        """;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "dydo-pr-ready-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string _tree;

    public PrReadyCommandTests()
    {
        _tree = Path.Combine(_root, "tree");
        Create(_tree, Path.Combine(_root, "origin.git"));
    }

    public void Dispose() => LinkSafeDelete.DeleteTree(_root);

    [Fact]
    public async Task CleanAndPushed_IsReady()
    {
        var (exit, stdout, stderr) = await PrReady(_tree);

        Assert.Equal(0, exit);
        Assert.Contains("ready: ", stdout);
        Assert.Empty(stderr);
    }

    [Fact]
    public async Task CleanAndPushedWithAGoodBody_IsReady()
    {
        var (exit, stdout, _) = await PrReady(_tree, "--body", BodyFile(GoodBody));

        Assert.Equal(0, exit);
        Assert.Contains("PASS review", stdout);
    }

    // DYD-391: LC's PR bodies (bodnarbalazs/LC#473) write the review line with an em dash.
    [Theory]
    [InlineData("PASS — rubric/type: code; contract LC-1111 description as of 2026-10-26T20:51:09.455Z; candidate 2148f550; base f65e5647; reviewer/model: reviewer/claude-opus-5-5 (requested identity, requested effort high; fresh round-3 code review); applicable tests 83/83.")]
    [InlineData("PASS – rubric/type: code; contract DYD-374 description; candidate abc; base def; reviewer/model: r.")]
    [InlineData("PASS—rubric/type: code; contract DYD-374 description; candidate abc; base def; reviewer/model: r.")]
    public async Task ReviewLineWithAnEnOrEmDash_IsReady(string passLine)
    {
        var body = $"Closes LC-1111.\n\n## Independent review\n\n{passLine}\n\nChecks: I read the whole candidate.\n";

        var (exit, stdout, stderr) = await PrReady(_tree, "--body", BodyFile(body));

        Assert.True(exit == 0, stderr);
        Assert.Contains("PASS review", stdout);
    }

    [Fact]
    public async Task DirtyTree_ListsStagedUnstagedAndUntrackedEachOnItsOwnLine()
    {
        File.WriteAllText(Path.Combine(_tree, "staged.txt"), "s\n");
        Git(_tree, "add", "staged.txt");
        File.WriteAllText(Path.Combine(_tree, "readme.md"), "changed\n");
        File.WriteAllText(Path.Combine(_tree, "new.txt"), "n\n");

        var (exit, _, stderr) = await PrReady(_tree);

        Assert.Equal(1, exit);
        var lines = stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Contains("not ready: staged changes: staged.txt", lines);
        Assert.Contains("not ready: unstaged changes: readme.md", lines);
        Assert.Contains("not ready: untracked files: new.txt", lines);
    }

    [Fact]
    public async Task StagedRename_NamesOnlyTheNewPath()
    {
        Git(_tree, "mv", "readme.md", "renamed.md");

        var (exit, _, stderr) = await PrReady(_tree);

        Assert.Equal(1, exit);
        var lines = stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Equal(["not ready: staged changes: renamed.md"], lines);
        Assert.DoesNotContain("dme.md", stderr.Replace("renamed.md", ""));
    }

    [Fact]
    public async Task IgnoredFile_DoesNotCount()
    {
        Directory.CreateDirectory(Path.Combine(_tree, "dydo", "agents"));
        File.WriteAllText(Path.Combine(_tree, "dydo", "agents", "evidence.md"), "e\n");

        var (exit, _, _) = await PrReady(_tree);

        Assert.Equal(0, exit);
    }

    [Fact]
    public async Task UnpushedCommit_NamesTheUpstream()
    {
        File.WriteAllText(Path.Combine(_tree, "more.txt"), "m\n");
        Commit(_tree, "more");

        var (exit, _, stderr) = await PrReady(_tree);

        Assert.Equal(1, exit);
        Assert.Contains("not ready: HEAD differs from its upstream origin/feature (1 ahead, 0 behind)", stderr);
    }

    [Fact]
    public async Task NoUpstream_IsNotReady()
    {
        Git(_tree, "switch", "-c", "local-only");

        var (exit, _, stderr) = await PrReady(_tree);

        Assert.Equal(1, exit);
        Assert.Contains("not ready: HEAD has no upstream", stderr);
    }

    [Fact]
    public async Task NotAGitTree_IsNotReady()
    {
        var plain = Path.Combine(_root, "plain");
        Directory.CreateDirectory(plain);

        var (exit, _, stderr) = await PrReady(plain);

        Assert.Equal(1, exit);
        Assert.Contains($"not ready: {plain} is not inside a git work tree", stderr);
    }

    [Fact]
    public async Task BodyWithoutTicketKey_IsNotReady()
    {
        var (exit, _, stderr) = await PrReady(_tree, "--body", BodyFile(GoodBody.Replace("DYD-374", "the ticket")));

        Assert.Equal(1, exit);
        Assert.Contains("not ready: the body names no ticket key", stderr);
        Assert.DoesNotContain("Independent review", stderr);
    }

    [Fact]
    public async Task BodyWithoutReviewSection_IsNotReady()
    {
        var (exit, _, stderr) = await PrReady(_tree, "--body", BodyFile("Closes DYD-374.\n\nPASS - rubric/type: code\n"));

        Assert.Equal(1, exit);
        Assert.Contains("not ready: the body has no '## Independent review' section", stderr);
    }

    [Fact]
    public async Task ReviewSectionWithoutPassBlock_IsNotReady()
    {
        var body = "Closes DYD-374.\n\n## Independent review\n\nlooks fine\n\n## Notes\n\nPASS - rubric/type: code\n";

        var (exit, _, stderr) = await PrReady(_tree, "--body", BodyFile(body));

        Assert.Equal(1, exit);
        Assert.Contains("not ready: the '## Independent review' section holds no reviewer 'PASS - rubric/type: ...' block", stderr);
    }

    [Fact]
    public async Task EveryProblemIsListed()
    {
        File.WriteAllText(Path.Combine(_tree, "new.txt"), "n\n");

        var (exit, _, stderr) = await PrReady(_tree, "--body", BodyFile("nothing here\n"));

        Assert.Equal(1, exit);
        Assert.Contains("untracked files: new.txt", stderr);
        Assert.Contains("no ticket key", stderr);
        Assert.Contains("no '## Independent review' section", stderr);
    }

    private string BodyFile(string body)
    {
        var path = Path.Combine(_root, "body.md");
        File.WriteAllText(path, body);
        return path;
    }

    private static async Task<(int Exit, string Stdout, string Stderr)> PrReady(params string[] args) =>
        await ConsoleCapture.AllAsync(() => PrReadyCommand.Create().Parse(args).InvokeAsync());
}
