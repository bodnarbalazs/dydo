namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

// Hypothesis (Brian, inquisitor — task identity-hijack-repro-role-set):
//   `dydo agent role` writes to the WRONG agent's state.md when DYDO_AGENT env var
//   points at a different agent than the calling process's actual claim.
//
// These tests stand up two claimed agents (Charlie, Zelda), point DYDO_AGENT at
// Charlie while the "actual" process holds Zelda's session, and check which
// state.md is mutated by the role-set code path.
public class IdentityHijackRoleSetTests : IDisposable
{
    private readonly string _testDir;
    // Synthetic PID for the "claude ancestor" the in-process test pretends to live under.
    // Zelda is set up as the truthful owner: her .session.ClaimedPid matches this value,
    // and FindAncestorProcessOverride returns it. Charlie's ClaimedPid is a different
    // value, so IsOwnedByCaller(Charlie) returns false and the env-path fall-through fires.
    private const int ZeldaClaudePid = 424242;
    private const int CharlieClaudePid = 131313;

    public IdentityHijackRoleSetTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-hijack-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("DYDO_AGENT", null);
        Environment.SetEnvironmentVariable("DYDO_HUMAN", null);
        ProcessUtils.FindAncestorProcessOverride = null;
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, true); } catch { }
        }
    }

    private void SetupConfig(string[] agents, string human)
    {
        var pool = string.Join(", ", agents.Select(a => $"\"{a}\""));
        var assigned = string.Join(", ", agents.Select(a => $"\"{a}\""));
        var config = $$"""
            {
              "version": 1,
              "agents": {
                "pool": [{{pool}}],
                "assignments": {
                  "{{human}}": [{{assigned}}]
                }
              }
            }
            """;
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), config);
    }

    private void CreateClaimedAgent(string agentName, string sessionId, string human, int claimedPid)
    {
        var workspace = Path.Combine(_testDir, "dydo", "agents", agentName);
        Directory.CreateDirectory(workspace);

        var sessionJson = $$"""{"Agent":"{{agentName}}","SessionId":"{{sessionId}}","Claimed":"{{DateTime.UtcNow:o}}","ClaimedPid":{{claimedPid}}}""";
        File.WriteAllText(Path.Combine(workspace, ".session"), sessionJson);

        File.WriteAllText(Path.Combine(workspace, "state.md"), $$"""
            ---
            agent: {{agentName}}
            role: null
            task: null
            status: working
            assigned: {{human}}
            ---

            # {{agentName}} — Session State
            """);
    }

    [Fact]
    public void SetRole_DydoAgentMismatchesPassedSession_WritesToActualSessionOwner()
    {
        // Process truly holds Zelda (sid_zelda). DYDO_AGENT inherited "Charlie" from a parent shell.
        // SetRole is called with the truthful session id (sid_zelda). The role MUST land on Zelda.
        SetupConfig(new[] { "Charlie", "Zelda" }, "testuser");
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        CreateClaimedAgent("Charlie", "sid_charlie", "testuser", CharlieClaudePid);
        CreateClaimedAgent("Zelda", "sid_zelda", "testuser", ZeldaClaudePid);
        Environment.SetEnvironmentVariable("DYDO_AGENT", "Charlie");
        ProcessUtils.FindAncestorProcessOverride = (_, _) => ZeldaClaudePid;

        var registry = new AgentRegistry(_testDir);

        var ok = registry.SetRole("sid_zelda", "co-thinker", "test-task", out var error);
        Assert.True(ok, $"SetRole failed: {error}");

        var zeldaState = File.ReadAllText(Path.Combine(_testDir, "dydo", "agents", "Zelda", "state.md"));
        var charlieState = File.ReadAllText(Path.Combine(_testDir, "dydo", "agents", "Charlie", "state.md"));

        Assert.Contains("role: co-thinker", zeldaState);
        Assert.DoesNotContain("role: co-thinker", charlieState);
    }

    [Fact]
    public void ExecuteRoleFlow_DydoAgentMismatchesActualClaim_HijacksRoleToEnvAgent()
    {
        // Mirrors AgentLifecycleHandlers.ExecuteRole's sequence verbatim:
        //   var sessionId = registry.GetSessionContext();
        //   var current   = registry.GetCurrentAgent(sessionId);
        //   registry.SetRole(sessionId, role, task, out _);
        //
        // The shared session-context file says Zelda is the current claim (sid_zelda + agent=Zelda).
        // DYDO_AGENT is "Charlie" (inherited from parent shell). Post-F1, the env fast-path's
        // ownership check rejects Charlie (his ClaimedPid doesn't match the overridden claude
        // ancestor PID), GetSessionContext falls through to the verified file and returns
        // sid_zelda, and the write lands on Zelda's state.md.
        SetupConfig(new[] { "Charlie", "Zelda" }, "testuser");
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        CreateClaimedAgent("Charlie", "sid_charlie", "testuser", CharlieClaudePid);
        CreateClaimedAgent("Zelda", "sid_zelda", "testuser", ZeldaClaudePid);
        ProcessUtils.FindAncestorProcessOverride = (_, _) => ZeldaClaudePid;

        var registry = new AgentRegistry(_testDir);
        // The guard writes the current claim's session id (with agent name) to the shared file
        // every time the agent calls any dydo command. Here we simulate Zelda having just done so.
        registry.StoreSessionContext("sid_zelda", "Zelda");

        Environment.SetEnvironmentVariable("DYDO_AGENT", "Charlie");

        var sessionId = registry.GetSessionContext();
        var current = registry.GetCurrentAgent(sessionId);
        Assert.NotNull(current);
        var ok = registry.SetRole(sessionId, "co-thinker", "test-task", out var error);
        Assert.True(ok, $"SetRole failed: {error}");

        var zeldaState = File.ReadAllText(Path.Combine(_testDir, "dydo", "agents", "Zelda", "state.md"));
        var charlieState = File.ReadAllText(Path.Combine(_testDir, "dydo", "agents", "Charlie", "state.md"));

        Assert.Contains("role: co-thinker", zeldaState);
        Assert.DoesNotContain("role: co-thinker", charlieState);
    }
}
