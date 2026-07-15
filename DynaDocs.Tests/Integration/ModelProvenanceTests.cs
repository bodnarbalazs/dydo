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
    // The headline success criterion, proven through the real claim flow (ParseInput → the guard's
    // claim storage), not by calling InferModel directly: a Claude session with no explicit model
    // in its payload still lands a concrete model on session state, and an issue it creates carries
    // the DISPLAY name — with zero edits to IssueCreateHandler.
    [Fact]
    public async Task ClaimThroughGuard_CapturesModelFromTranscript_IssueRendersDisplayName()
    {
        await InitProjectAsync("none", "balazs", 3);

        var transcript = WriteClaudeTranscript("claude-opus-4-8");
        var claimJson = BuildClaimHookJson("Adele", transcript);

        // Claim through the guard hook: the model is captured from the transcript and persisted
        // on the pending session by HandleClaimSessionStorage.
        await GuardWithStdinAsync(claimJson);

        // Promote the pending claim to a live session, as `dydo agent claim` does.
        var claim = await RunAsync(AgentCommand.Create(), "claim", "Adele");
        claim.AssertSuccess();
        StoreSessionContext();

        // The concrete model reached persisted session state (not thrown away).
        var registry = new AgentRegistry(TestDir);
        var session = registry.GetSession("Adele");
        Assert.NotNull(session);
        Assert.Equal("claude-opus-4-8", session!.Model);
        Assert.Equal("claude", session.Host);

        // Provenance renders the display name at the source.
        var content = await CreateIssueAndRead("Runtime provenance issue");
        Assert.Contains("found-by-agent: Adele", content);
        Assert.Contains("found-by-vendor: claude", content);
        Assert.Contains("found-by-model: Opus 4.8", content);
    }

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

    private string WriteClaudeTranscript(string modelId)
    {
        // A ".claude" path so host inference resolves to claude, mirroring a real Claude transcript.
        var dir = Path.Combine(TestDir, ".claude");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "session.jsonl");
        File.WriteAllText(path,
            "{\"type\":\"user\",\"message\":{\"role\":\"user\"}}\n" +
            "{\"type\":\"assistant\",\"message\":{\"model\":\"" + modelId + "\"}}\n");
        return path;
    }

    private static string BuildClaimHookJson(string agent, string transcriptPath)
    {
        var tp = transcriptPath.Replace("\\", "/");
        return "{\"session_id\":\"" + TestSessionId + "\",\"transcript_path\":\"" + tp
            + "\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"dydo agent claim " + agent + "\"}}";
    }
}
