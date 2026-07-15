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

}
