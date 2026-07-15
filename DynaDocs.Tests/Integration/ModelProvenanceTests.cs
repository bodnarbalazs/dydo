namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;
using DynaDocs.Services;

/// <summary>
/// c1-6 exact-model provenance: the guard captures a concrete Claude runtime model from the
/// session transcript at claim time, it persists onto session state, and every provenance
/// surface renders the human display name (resolved once, at the source).
/// </summary>
[Collection("Integration")]
public class ModelProvenanceTests : IntegrationTestBase
{
    [Fact]
    public async Task Provenance_UnknownModel_FallsBackToVendor()
    {
        await InitProjectAsync("none", "balazs", 3);
        // Known vendor, but no runtime model captured.
        await ClaimAgentWithRuntimeAsync("Adele", "codex", AgentSessionUnknownModel);

        var content = await CreateIssueAndRead("Vendor fallback issue");
        Assert.Contains("found-by-vendor: codex", content);
        // Vendor is the only fallback when the model is unknown.
        Assert.Contains("found-by-model: codex", content);
    }

    [Fact]
    public async Task Provenance_UnknownIdNotInMap_PassesThroughVerbatim()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentWithRuntimeAsync("Adele", "codex", "gpt-5-experimental-42");

        var content = await CreateIssueAndRead("Verbatim id issue");
        Assert.Contains("found-by-model: gpt-5-experimental-42", content);
    }

    private const string AgentSessionUnknownModel = "unknown";

    private async Task<string> CreateIssueAndRead(string title)
    {
        var result = await RunAsync(IssueCommand.Create(),
            "create", "--title", title, "--area", "general", "--severity", "low");
        result.AssertSuccess();
        var slug = title.ToLowerInvariant().Replace(" ", "-");
        return ReadFile($"dydo/project/issues/0001-{slug}.md");
    }
}
