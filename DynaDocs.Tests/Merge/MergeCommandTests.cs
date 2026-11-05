namespace DynaDocs.Tests.Merge;

using DynaDocs.Commands;
using DynaDocs.Services;
using static FakeGitHub;
using static MergeScenario;

/// <summary>The <c>dydo merge</c> command's parsing and its listing in completion and help.</summary>
[Collection("Integration")]
public sealed class MergeCommandTests : IDisposable
{
    private readonly MergeScenario _s = new();
    private readonly string _originalDirectory = Environment.CurrentDirectory;

    public void Dispose()
    {
        Environment.CurrentDirectory = _originalDirectory;
        _s.Dispose();
    }

    [Fact]
    public void TheCommand_MergesThePrItParsesFromTheCurrentDirectory()
    {
        Environment.CurrentDirectory = _s.Root;
        var command = MergeCommand.Create(_ => _s.GitHub);

        var (exit, stdout, stderr) = ConsoleCapture.All(() =>
            command.Parse(["7", "--match-head-commit", Sha, "--match-base-commit", Tip, "--dry-run"]).Invoke());

        Assert.True(exit == 0, stderr);
        Assert.Contains("would merge 0123456789ab into feature/x at 111111111111 (PR #7)", stdout);
        Assert.Empty(_s.GitHub.Writes);
    }

    // Proof (e): the base pin is validated like the head pin, before any GitHub call.
    [Theory]
    [InlineData(null)]
    [InlineData("1111111")]
    [InlineData("111111111111111111111111111111111111111")]
    [InlineData("11111111111111111111111111111111111111111")]
    [InlineData("g111111111111111111111111111111111111111")]
    [InlineData("feature/x")]
    public void AnythingButAFullBasePin_IsRefusedBeforeAnyCall(string? pin)
    {
        var (exit, _, stderr) = Invoke(["7", "--match-head-commit", Sha, .. pin == null ? Array.Empty<string>() : ["--match-base-commit", pin]]);

        Assert.Equal(1, exit);
        Assert.Equal("refused: --match-base-commit takes the full 40-character SHA of the target branch tip you pinned", stderr.TrimEnd());
        Assert.Empty(_s.GitHub.Calls);
    }

    [Fact]
    public void WithNeitherPin_TheHeadPinIsRefusedFirst()
    {
        var (exit, _, stderr) = Invoke(["7"]);

        Assert.Equal(1, exit);
        Assert.Equal("refused: --match-head-commit takes the full 40-character SHA the reviewer passed", stderr.TrimEnd());
        Assert.Empty(_s.GitHub.Calls);
    }

    // As with the head pin, upper-case hex names the same commit.
    [Fact]
    public void AnUpperCaseBasePin_IsTheSameTip()
    {
        const string tip = "abcdef1111111111111111111111111111111111";
        _s.GitHub.On("GET", Branch, Reply(200, BranchAt(tip)));
        _s.GitHub.On("GET", Compare(ReviewBase, tip), Reply(200, """{ "status": "ahead" }"""));

        var (exit, stdout, stderr) = Invoke(["7", "--match-head-commit", Sha, "--match-base-commit", tip.ToUpperInvariant(), "--dry-run"]);

        Assert.True(exit == 0, stderr);
        Assert.Contains("would merge 0123456789ab into feature/x at abcdef111111 (PR #7)", stdout);
    }

    private (int Exit, string Stdout, string Stderr) Invoke(string[] args)
    {
        Environment.CurrentDirectory = _s.Root;
        return ConsoleCapture.All(() => MergeCommand.Create(_ => _s.GitHub).Parse(args).Invoke());
    }

    [Fact]
    public void WithoutMatchHeadCommit_TheReleasedCommandRefusesBeforeAnyCall()
    {
        var (exit, _, stderr) = ConsoleCapture.All(() => MergeCommand.Create().Parse(["7"]).Invoke());

        Assert.Equal(1, exit);
        Assert.StartsWith("refused: --match-head-commit takes the full 40-character SHA the reviewer passed", stderr);
    }

    [Fact]
    public void Completion_ListsMerge()
    {
        Assert.Contains("merge", CompletionProvider.GetCompletions(1, ["dydo"]));
    }

    [Fact]
    public void Help_ListsMerge()
    {
        var help = ConsoleCapture.Stdout(HelpCommand.PrintHelp);

        Assert.Contains("  merge <pr> --match-head-commit <sha> --match-base-commit <sha>", help);
    }
}
