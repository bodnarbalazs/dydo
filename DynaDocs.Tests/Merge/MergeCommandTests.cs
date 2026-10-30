namespace DynaDocs.Tests.Merge;

using DynaDocs.Commands;
using DynaDocs.Services;
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
            command.Parse(["7", "--match-head-commit", Sha, "--dry-run"]).Invoke());

        Assert.True(exit == 0, stderr);
        Assert.Contains("would merge 0123456789ab into feature/x (PR #7)", stdout);
        Assert.Empty(_s.GitHub.Writes);
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

        Assert.Contains("  merge <pr> --match-head-commit <sha>", help);
    }
}
