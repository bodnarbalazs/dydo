namespace DynaDocs.Tests.Steps;

using Xunit.Sdk;

public class OptionalSummaryAssertionTests : IDisposable
{
    private readonly CliScenario _scenario = new();

    [Theory]
    [InlineData("")]
    [InlineData("Missing title")]
    public async Task MissingTitle_RequiresTheCompleteExpectedDiagnostic(string changed)
    {
        var steps = new OptionalSummarySteps(_scenario);
        steps.ValidTree();
        steps.MissingTitle();
        await steps.Check();
        steps.CheckFailure("Missing title (# heading)");
        Assert.ThrowsAny<XunitException>(() => steps.CheckFailure(changed));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Broken link:")]
    public async Task BrokenLink_RequiresTheCompleteExpectedDiagnostic(string changed)
    {
        var steps = new OptionalSummarySteps(_scenario);
        steps.ValidTree();
        steps.Section();
        steps.BrokenLink("./missing.md");
        await steps.Check();
        steps.CheckFailure("Broken link: ./missing.md");
        Assert.ThrowsAny<XunitException>(() => steps.CheckFailure(changed));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Add")]
    public async Task ManualRepair_RequiresTheCompleteExpectedAdvice(string changed)
    {
        var steps = new OptionalSummarySteps(_scenario);
        steps.ValidTree();
        steps.MissingFrontmatter();
        await steps.Fix();
        steps.ManualRepair("Add frontmatter");
        Assert.ThrowsAny<XunitException>(() => steps.ManualRepair(changed));
    }

    public void Dispose() => _scenario.Cleanup();
}
