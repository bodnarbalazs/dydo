namespace DynaDocs.Tests.Services;

using System.Text.Json;
using DynaDocs.Models;
using DynaDocs.Serialization;
using DynaDocs.Services;

// Closes #0195 (F11). With F1's env-path ownership gate in place, an attacker who can set
// DYDO_AGENT in a non-claude shell still has one remaining DoS primitive: the .session-context
// file holds the last legitimately-claiming agent, so the fall-through resolves to a real
// claimed agent. The wait-marker callsite then registers under that agent's identity and the
// attacker holds the general-wait slot. F11 closes that by requiring the caller to actually
// own the resolved agent before any wait-marker write.
public class IdentityHijackWaitDoSTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _originalDir;

    public IdentityHijackWaitDoSTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-waitdos-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _originalDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(_testDir);

        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), """
            {
              "version": 1,
              "agents": {
                "pool": ["Adele", "Zelda"],
                "assignments": {
                  "testuser": ["Adele", "Zelda"]
                }
              }
            }
            """);
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDir);
        Environment.SetEnvironmentVariable("DYDO_AGENT", null);
        Environment.SetEnvironmentVariable("DYDO_HUMAN", null);
        ProcessUtils.FindAncestorProcessOverride = null;
        try { Directory.Delete(_testDir, true); } catch { }
    }

    private static void WriteSessionWithPid(string agentsDir, string agentName, string sessionId, int claimedPid)
    {
        var workspace = Path.Combine(agentsDir, agentName);
        Directory.CreateDirectory(workspace);
        var session = new AgentSession
        {
            Agent = agentName,
            SessionId = sessionId,
            Claimed = DateTime.UtcNow,
            ClaimedPid = claimedPid
        };
        File.WriteAllText(Path.Combine(workspace, ".session"),
            JsonSerializer.Serialize(session, DydoDefaultJsonContext.Default.AgentSession));
        File.WriteAllText(Path.Combine(workspace, "state.md"), $"""
            ---
            agent: {agentName}
            role: code-writer
            task: t
            status: working
            ---
            """);
    }

    [Fact]
    public void VerifyCallerOwnsAgent_AttackerWithMismatchingDydoAgent_ReturnsFalse()
    {
        // Setup: Adele is the legit claim under one claude tab; the "attacker" process lives
        // under a different claude (or a plain shell) and exports DYDO_AGENT=Adele.
        const int adeleClaudePid = 121212;
        const int attackerPid = 393939;

        var agentsDir = Path.Combine(_testDir, "dydo", "agents");
        WriteSessionWithPid(agentsDir, "Adele", "sid-adele", adeleClaudePid);

        ProcessUtils.FindAncestorProcessOverride = (_, _) => attackerPid;
        Environment.SetEnvironmentVariable("DYDO_AGENT", "Adele");

        var registry = new AgentRegistry(_testDir);

        // Pre-F11: WaitCommand would still call CreateListeningWaitMarker after resolving
        // Adele via the .session-context fall-through. Post-F11 the gate at WaitCommand.Execute
        // refuses on this exact predicate.
        Assert.False(registry.VerifyCallerOwnsAgent("Adele"));
    }

    [Fact]
    public void VerifyCallerOwnsAgent_LegitimateOwner_ReturnsTrue()
    {
        const int adeleClaudePid = 121212;

        var agentsDir = Path.Combine(_testDir, "dydo", "agents");
        WriteSessionWithPid(agentsDir, "Adele", "sid-adele", adeleClaudePid);

        ProcessUtils.FindAncestorProcessOverride = (_, _) => adeleClaudePid;

        var registry = new AgentRegistry(_testDir);

        Assert.True(registry.VerifyCallerOwnsAgent("Adele"));
    }

    [Fact]
    public void VerifyCallerOwnsAgent_AgentNotClaimed_ReturnsFalse()
    {
        ProcessUtils.FindAncestorProcessOverride = (_, _) => 0;

        var registry = new AgentRegistry(_testDir);

        Assert.False(registry.VerifyCallerOwnsAgent("Adele"));
    }
}
