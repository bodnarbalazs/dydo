namespace DynaDocs.Tests.Merge;

using DynaDocs.Commands;
using DynaDocs.Services;
using DynaDocs.Tests.Integration;
using Reqnroll;
using static MergeScenario;

[Binding]
public sealed class ReviewPassGrammarSteps
{
    private string _line = "";
    private int _readyExit;
    private int _mergeExit;

    [Given("an independent review line with candidate separator {string} and base separator {string}")]
    public void Fields(string candidate, string reviewBase) =>
        _line = PassLine().Replace("candidate ", candidate == "colon" ? "candidate: " : "candidate ")
            .Replace("base ", reviewBase == "colon" ? "base: " : "base ");

    [Given("the independent review line {string}")]
    public void Line(string line) => _line = line;

    [Given("an independent review line without a parsed base {string}")]
    public void WithoutBase(string suffix) => _line = $"PASS - rubric/type: code; candidate {Sha}{suffix}";

    [When("I check that review with pr-ready and a pinned merge dry run")]
    public async Task Check()
    {
        using var merge = new MergeScenario();
        var body = Body(_line);
        merge.PullJson["body"] = body;
        (_mergeExit, _, _) = merge.Merge(dryRun: true);
        var tree = Path.Combine(merge.Root, "ready-tree");
        PushedRepository.Create(tree, Path.Combine(merge.Root, "origin.git"));
        var bodyFile = Path.Combine(merge.Root, "body.md");
        File.WriteAllText(bodyFile, body);
        (_readyExit, _, _) = await ConsoleCapture.AllAsync(() =>
            PrReadyCommand.Create().Parse([tree, "--body", bodyFile]).InvokeAsync());
    }

    [Then("both commands accept the review")]
    public void Accept()
    {
        Assert.Equal(0, _readyExit);
        Assert.Equal(0, _mergeExit);
    }

    [Then("both commands refuse the review")]
    public void Refuse()
    {
        Assert.Equal(1, _readyExit);
        Assert.Equal(1, _mergeExit);
    }
}
