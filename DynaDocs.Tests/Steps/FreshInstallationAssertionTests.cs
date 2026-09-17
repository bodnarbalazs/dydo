namespace DynaDocs.Tests.Steps;

using Xunit.Sdk;

public class FreshInstallationAssertionTests : IDisposable
{
    private readonly CliScenario _scenario = new();
    private FreshInstallationSteps Fresh => new(_scenario);
    private string Document(string name) => Path.Combine(_scenario.DirectoryPath, "dydo", "understand", name);

    [Theory]
    [InlineData("about.md")]
    [InlineData("architecture.md")]
    public async Task InitialCheck_RejectsEitherMissingOnboardingWarning(string customized)
    {
        var fresh = await Initialize();
        var other = customized == "about.md" ? "architecture.md" : "about.md";
        var shipped = File.ReadAllBytes(Document(other));
        fresh.CustomizeFoundationDocuments();
        File.WriteAllBytes(Document(other), shipped);
        await _scenario.RunAsync("check");
        _scenario.Result.AssertSuccess();
        Assert.ThrowsAny<XunitException>(() => fresh.OnboardingWarnings(CheckAssertionTests.ExpectedWarnings()));
    }

    [Fact]
    public async Task InitialCheck_RejectsRealBrokenLinkWithBothOnboardingWarnings()
    {
        var fresh = await Initialize();
        File.AppendAllText(Document("about.md"), "\n[Broken](./absent-hardening-doc.md)\n");
        await _scenario.RunAsync("check");
        Assert.Equal(1, _scenario.Result.ExitCode);
        Assert.Contains("Broken link: ./absent-hardening-doc.md", _scenario.Result.Stdout);
        Assert.Contains("2 warnings", _scenario.Result.Stdout);
        Assert.ThrowsAny<XunitException>(() => fresh.OnboardingWarnings(CheckAssertionTests.ExpectedWarnings()));
    }

    [Theory]
    [InlineData("about.md")]
    [InlineData("architecture.md")]
    public async Task CleanCheck_RejectsEitherUncustomizedFoundation(string name)
    {
        var fresh = await Initialize();
        var shipped = File.ReadAllBytes(Document(name));
        fresh.CustomizeFoundationDocuments();
        await AssertClean();
        File.WriteAllBytes(Document(name), shipped);
        await _scenario.RunAsync("check");
        Assert.ThrowsAny<XunitException>(() => new OptionalSummarySteps(_scenario).ValidDocumentation());
    }

    [Fact]
    public async Task BothCheckAssertions_RejectRemovedDocumentationTree()
    {
        var fresh = await Initialize();
        Directory.Delete(Path.Combine(_scenario.DirectoryPath, "dydo"), recursive: true);
        await _scenario.RunAsync("check");
        Assert.ThrowsAny<XunitException>(() => fresh.OnboardingWarnings(CheckAssertionTests.ExpectedWarnings()));
        Assert.ThrowsAny<XunitException>(() => new OptionalSummarySteps(_scenario).ValidDocumentation());
    }

    [Theory]
    [InlineData("about.md")]
    [InlineData("architecture.md")]
    public async Task Customization_RequiresBothInitializedFilesBeforeWritingEither(string missing)
    {
        var fresh = await Initialize();
        var other = missing == "about.md" ? "architecture.md" : "about.md";
        var shipped = File.ReadAllBytes(Document(other));
        File.Delete(Document(missing));
        Assert.ThrowsAny<XunitException>(fresh.CustomizeFoundationDocuments);
        Assert.Equal(shipped, File.ReadAllBytes(Document(other)));
        Assert.False(File.Exists(Document(missing)));
    }

    private async Task<FreshInstallationSteps> Initialize(string integration = "none")
    {
        var fresh = Fresh;
        await fresh.Initialize(integration);
        fresh.CommandSucceeds();
        return fresh;
    }

    private async Task AssertClean()
    {
        await _scenario.RunAsync("check");
        new OptionalSummarySteps(_scenario).ValidDocumentation();
    }

    public void Dispose() => _scenario.Cleanup();
}
