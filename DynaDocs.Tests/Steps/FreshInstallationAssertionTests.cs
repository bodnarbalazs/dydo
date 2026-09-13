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

    [Theory]
    [InlineData("sync", "about.md", false)]
    [InlineData("sync", "about.md", true)]
    [InlineData("sync", "architecture.md", false)]
    [InlineData("sync", "architecture.md", true)]
    [InlineData("update", "about.md", false)]
    [InlineData("update", "about.md", true)]
    [InlineData("update", "architecture.md", false)]
    [InlineData("update", "architecture.md", true)]
    public async Task FoundationPreservation_RejectsChangeOrDeletionAfterEveryOperation(string operation, string name, bool delete)
    {
        var fresh = await Initialize();
        fresh.CustomizeFoundationDocuments();
        await fresh.FirstSync();
        fresh.CommandSucceeds();
        await fresh.SecondSync();
        fresh.CommandSucceeds();
        if (operation == "update")
        {
            await fresh.Update();
            fresh.CommandSucceeds();
        }
        fresh.PreservedFoundationDocuments();
        var original = File.ReadAllBytes(Document(name));
        Corrupt(Document(name), delete);
        Assert.ThrowsAny<XunitException>(fresh.PreservedFoundationDocuments);
        File.WriteAllBytes(Document(name), original);
        fresh.PreservedFoundationDocuments();
    }

    [Theory]
    [InlineData("none", false)]
    [InlineData("none", true)]
    [InlineData("claude", false)]
    [InlineData("claude", true)]
    [InlineData("codex", false)]
    [InlineData("codex", true)]
    [InlineData("all", false)]
    [InlineData("all", true)]
    public async Task NativeSnapshot_RejectsChangedOrMissingArtifactForEverySelection(string integration, bool delete)
    {
        var fresh = await Initialize(integration);
        await fresh.FirstSync();
        fresh.CommandSucceeds();
        await fresh.SecondSync();
        fresh.CommandSucceeds();
        fresh.IdenticalArtifacts();
        var folder = integration == "codex" ? ".codex/agents" : ".claude/agents";
        var path = Directory.EnumerateFiles(Path.Combine(_scenario.DirectoryPath, folder)).First();
        var original = File.ReadAllBytes(path);
        Corrupt(path, delete);
        Assert.ThrowsAny<XunitException>(fresh.IdenticalArtifacts);
        File.WriteAllBytes(path, original);
        fresh.IdenticalArtifacts();
    }

    [Fact]
    public void EmptySnapshots_CannotProvePreservation()
    {
        Assert.ThrowsAny<XunitException>(Fresh.IdenticalArtifacts);
        Assert.ThrowsAny<XunitException>(Fresh.PreservedFoundationDocuments);
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

    private static void Corrupt(string path, bool delete)
    {
        if (delete)
            File.Delete(path);
        else
            File.AppendAllText(path, "\nchanged bytes\n");
    }

    public void Dispose() => _scenario.Cleanup();
}
