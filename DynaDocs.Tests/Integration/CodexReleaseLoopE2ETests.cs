namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;
using DynaDocs.Services;

/// <summary>
/// c1-7 / issue 0254: the full codex release loop as one end-to-end regression — the "can release"
/// acceptance criterion. A codex-shaped claim → role → durable wait registration → an arriving
/// message → host-agnostic `dydo read` (display-equals-ack) → `inbox clear --all` → release that
/// clears the durable wait marker. Each C1 seam (c1-1 read verb, c1-2 durable wait + release
/// cleanup, c1-6 codex host capture) is exercised together, on a codex host, in the order a codex
/// operator runs them.
/// </summary>
[Collection("Integration")]
public class CodexReleaseLoopE2ETests : IntegrationTestBase
{
    [Fact]
    public async Task CodexHost_FullReleaseLoop_ReadUnwedgesInboxClear_ReleaseClearsDurableWait()
    {
        await InitProjectAsync("none", "testuser", 3);

        // 1. Claim through a codex-shaped hook payload (session_id, .codex transcript path, model,
        //    NO agent_id) so the live session is genuinely codex-hosted.
        await ClaimCodexViaHookAsync("Adele", "gpt-5-codex");
        var claim = await RunAsync(AgentCommand.Create(), "claim", "Adele");
        claim.AssertSuccess();
        StoreSessionContext();
        Assert.Equal("codex", new AgentRegistry(TestDir).GetSession("Adele")?.Host);

        // 2. Take a role (no auto general-wait — the loop registers its own durable one next).
        var role = await SetRoleAsync("co-thinker", registerGeneralWait: false);
        role.AssertSuccess();

        // 3. Register a DURABLE general wait (the only wait a codex host can hold — its runtime
        //    kills a foreground `dydo wait` at the tool timeout).
        StoreSessionContext();
        var register = await RunAsync(WaitCommand.Create(), "--register");
        register.AssertSuccess();
        register.AssertStdoutContains("Durable general wait registered");
        Assert.True(new AgentRegistry(TestDir).GetWaitMarkers("Adele").Single(m => m.Task == "_general-wait").Durable);

        // 4. A message arrives via the production delivery path.
        var messageId = MessageService.DeliverInboxMessage(
            new AgentRegistry(TestDir), "Brian", "Adele", "please review the auth slice", "review-ready");
        Assert.Contains(messageId, new AgentRegistry(TestDir).GetAgentState("Adele")!.UnreadMessages);

        // 5. `dydo read` PRINTS the message and registers the read in one step (display-equals-ack).
        StoreSessionContext();
        var read = await RunAsync(ReadCommand.Create(), messageId);
        read.AssertSuccess();
        read.AssertStdoutContains("please review the auth slice");
        Assert.Empty(new AgentRegistry(TestDir).GetAgentState("Adele")!.UnreadMessages);

        // 6. With the message read-acked, `inbox clear --all` succeeds — the wedge the read verb closes.
        StoreSessionContext();
        var clear = await RunAsync(InboxCommand.Create(), "clear", "--all");
        clear.AssertSuccess();

        // 7. Release clears the durable wait marker along with the rest of the agent's wait state.
        var release = await ReleaseAgentAsync();
        release.AssertSuccess();
        Assert.Empty(new AgentRegistry(TestDir).GetWaitMarkers("Adele"));
    }

    // Publishes a codex-shaped claim hook (Noah's probe shape) so the pending session is stamped
    // codex + model before the promote-to-claimed runs.
    private async Task ClaimCodexViaHookAsync(string agent, string model)
    {
        var dir = Path.Combine(TestDir, ".codex", "sessions");
        Directory.CreateDirectory(dir);
        var transcript = Path.Combine(dir, "rollout.jsonl").Replace("\\", "/");
        File.WriteAllText(transcript, "{\"type\":\"user\",\"message\":{\"role\":\"user\"}}\n");

        var json = "{\"session_id\":\"" + TestSessionId + "\",\"transcript_path\":\"" + transcript
            + "\",\"model\":\"" + model
            + "\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"dydo agent claim " + agent + "\"}}";
        await GuardWithStdinAsync(json);
    }
}
