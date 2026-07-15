namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;
using DynaDocs.Services;

/// <summary>
/// c1-7 / issue 0233: end-to-end regression cover for the codex-first-class CLAIM seams that green
/// tests previously only touched indirectly. Each test drives a real codex-shaped path — a hook
/// payload with NO agent_id/agent_type (codex delivers none), a codex-owned claim, a legacy codex
/// .session — through the production code, not a helper shortcut.
/// </summary>
[Collection("Integration")]
public class CodexClaimE2ETests : IntegrationTestBase
{
    // (0233 ask 1) A codex-shaped guard-stdin claim carries its host and model all the way into the
    // claimed session. The payload mirrors Noah's probe findings: session_id, a transcript_path into
    // the vendor sessions dir (host inference), an explicit model field, and — crucially — NO
    // agent_id/agent_type. The host/model captured at hook time must survive the promote-to-claimed.
    [Fact]
    public async Task CodexShapedGuardClaim_HostAndModelSurviveIntoClaimedSession()
    {
        await InitProjectAsync("none", "balazs", 3);

        var transcript = WriteCodexTranscript();
        var claimJson = BuildCodexClaimHookJson("Adele", transcript, "gpt-5-codex");

        // Claim through the guard hook: HandleClaimSessionStorage captures host (from the .codex
        // transcript path) and the explicit model onto the pending session — with no agent_id present.
        await GuardWithStdinAsync(claimJson);

        // Promote the pending claim to a live session, as `dydo agent claim` does.
        var claim = await RunAsync(AgentCommand.Create(), "claim", "Adele");
        claim.AssertSuccess();
        StoreSessionContext();

        var session = new AgentRegistry(TestDir).GetSession("Adele");
        Assert.NotNull(session);
        Assert.Equal("codex", session!.Host);
        Assert.Equal("gpt-5-codex", session.Model);
    }

    // (0233 ask 2) Claiming a codex-owned session registers a watchdog anchor keyed to the codex
    // host ancestor — not the claude one. The anchor is what keeps the watchdog alive for a codex
    // dispatch; without codex ancestry threading it would never register.
    [Fact]
    public async Task CodexOwnedClaim_RegistersWatchdogAnchor_FromCodexAncestor()
    {
        await InitProjectAsync("none", "testuser", 3);

        const int codexHostPid = 606161;
        var prev = ProcessUtils.FindAncestorProcessOverride;
        // Only the codex-ancestor lookup resolves a PID; a claude lookup finds nothing. So an anchor
        // appears ONLY if the claim used codex ancestry (FindAgentHostAncestor("codex")).
        ProcessUtils.FindAncestorProcessOverride =
            (name, _) => name == "codex" ? codexHostPid : (int?)null;
        try
        {
            await ClaimAgentWithRuntimeAsync("Adele", "codex", "gpt-5-codex");

            var anchors = Directory.GetFiles(TestDir, $"{codexHostPid}.anchor", SearchOption.AllDirectories);
            Assert.True(anchors.Length > 0,
                "claiming a codex-owned session must register a watchdog anchor from the codex host ancestor");
        }
        finally
        {
            ProcessUtils.FindAncestorProcessOverride = prev;
        }
    }

    // (0233 ask 3) A legacy codex .session predates ClaimedPid (null). WaitCommand host-liveness must
    // then fall back to the ancestry walk keyed to the session's OWN host — codex — not the historical
    // claude default. Resolving to the claude ancestor would anchor liveness to the wrong process.
    [Fact]
    public async Task LegacyCodexSession_NullClaimedPid_ResolvesHostLivenessViaCodexAncestry()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        // Overwrite with a legacy-shaped codex session: Host=codex, no ClaimedPid.
        var sessionPath = Path.Combine(TestDir, "dydo", "agents", "Adele", ".session");
        File.WriteAllText(sessionPath,
            $$"""{"Agent":"Adele","SessionId":"sess-legacy-codex","Host":"codex","Claimed":"{{DateTime.UtcNow:o}}"}""");

        var registry = new AgentRegistry(TestDir);
        Assert.Equal("codex", registry.GetSession("Adele")?.Host);
        Assert.Null(registry.GetSession("Adele")?.ClaimedPid);

        const int codexAncestor = 7777;
        const int claudeAncestor = 8888;
        var prev = ProcessUtils.FindAncestorProcessOverride;
        ProcessUtils.FindAncestorProcessOverride =
            (name, _) => name == "codex" ? codexAncestor : name == "claude" ? claudeAncestor : (int?)null;
        try
        {
            // Must pick the codex ancestor (7777), never the claude one (8888).
            Assert.Equal(codexAncestor, WaitCommand.ResolveHostLivenessPid(registry, "Adele"));
        }
        finally
        {
            ProcessUtils.FindAncestorProcessOverride = prev;
        }
    }

    private string WriteCodexTranscript()
    {
        // A ".codex/sessions" path so host inference resolves to codex, mirroring a real codex
        // rollout transcript location (Noah's probe: transcript_path points into the vendor sessions dir).
        var dir = Path.Combine(TestDir, ".codex", "sessions");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "rollout.jsonl");
        File.WriteAllText(path, "{\"type\":\"user\",\"message\":{\"role\":\"user\"}}\n");
        return path;
    }

    // The codex hook shape (Noah's probe): session_id, transcript_path into the vendor sessions dir,
    // an explicit model field, and NO agent_id / agent_type. The claim command rides in tool_input.
    private static string BuildCodexClaimHookJson(string agent, string transcriptPath, string model)
    {
        var tp = transcriptPath.Replace("\\", "/");
        return "{\"session_id\":\"" + TestSessionId + "\",\"transcript_path\":\"" + tp
            + "\",\"model\":\"" + model
            + "\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"dydo agent claim " + agent + "\"}}";
    }
}
