namespace DynaDocs.Tests.Commands;

using DynaDocs.Commands;
using DynaDocs.Services;
using DynaDocs.Utils;

// Closes #0193 (F8): claim refuses when DYDO_AGENT inherited from an upstream shell points
// at a different agent than the one being claimed. UX-only after F1 closed the underlying
// hijack, but the early refusal saves the operator a downstream surprise.
[Collection("ConsoleOutput")]
public class AgentLifecycleHandlersTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _originalDir;

    public AgentLifecycleHandlersTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-lifecycle-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), """
            {
              "version": 1,
              "agents": {
                "pool": ["Charlie", "Zelda"],
                "assignments": {
                  "testuser": ["Charlie", "Zelda"]
                }
              }
            }
            """);
        _originalDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(_testDir);
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDir);
        Environment.SetEnvironmentVariable("DYDO_AGENT", null);
        Environment.SetEnvironmentVariable("DYDO_HUMAN", null);
        try { Directory.Delete(_testDir, true); } catch { }
    }

    private void CreateClaimedAgent(string agentName, string sessionId)
    {
        var workspace = Path.Combine(_testDir, "dydo", "agents", agentName);
        Directory.CreateDirectory(workspace);
        // #0250: ClaimedPid = this process so the caller owns the session (the .session-context
        // fallback now enforces caller ownership before resolving ambient identity).
        File.WriteAllText(Path.Combine(workspace, ".session"),
            $$"""{"Agent":"{{agentName}}","SessionId":"{{sessionId}}","Claimed":"{{DateTime.UtcNow:o}}","ClaimedPid":{{Environment.ProcessId}}}""");
        File.WriteAllText(Path.Combine(workspace, "state.md"), $"""
            ---
            agent: {agentName}
            role: null
            task: null
            status: working
            assigned: testuser
            ---
            """);
    }

    [Fact]
    public void ExecuteClaim_WithoutHookPlumbedSession_ExplainsHowToClaim()
    {
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");

        int exit = 0;
        var stderr = ConsoleCapture.Stderr(() =>
        {
            exit = AgentLifecycleHandlers.ExecuteClaim("Zelda");
        });

        Assert.Equal(ExitCodes.ToolError, exit);
        Assert.Contains("must be run via the Bash tool", stderr);
        Assert.Contains("dydo agent claim Zelda", stderr);
        Assert.DoesNotContain("No session ID available", stderr);
    }

    [Fact]
    public void ExecuteClaim_StaleEnvVarMismatch_RefusedWithActionableError()
    {
        Environment.SetEnvironmentVariable("DYDO_AGENT", "Charlie");
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");

        int exit = 0;
        var stderr = ConsoleCapture.Stderr(() =>
        {
            exit = AgentLifecycleHandlers.ExecuteClaim("Zelda");
        });

        Assert.Equal(ExitCodes.ToolError, exit);
        Assert.Contains("DYDO_AGENT is set to 'Charlie'", stderr);
        Assert.Contains("DYDO_AGENT=$null", stderr);
        Assert.Contains("unset DYDO_AGENT", stderr);
        // No claim should have been made — Zelda's workspace remains absent.
        Assert.False(Directory.Exists(Path.Combine(_testDir, "dydo", "agents", "Zelda")));
    }

    [Fact]
    public void ExecuteClaim_StaleEnvVarMatchesTarget_Allowed()
    {
        // DYDO_AGENT matches the agent being claimed — the standard re-claim shape; not refused.
        Environment.SetEnvironmentVariable("DYDO_AGENT", "Zelda");
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        new AgentRegistry(_testDir).StorePendingSessionId("Zelda", "test-session-zelda");

        int exit = 0;
        ConsoleCapture.Stdout(() =>
        {
            exit = AgentLifecycleHandlers.ExecuteClaim("Zelda");
        });

        Assert.Equal(ExitCodes.Success, exit);
    }

    [Fact]
    public void ExecuteClaim_WithHookPlumbedSession_Succeeds()
    {
        Environment.SetEnvironmentVariable("DYDO_AGENT", null);
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        new AgentRegistry(_testDir).StorePendingSessionId("Zelda", "test-session-zelda");

        int exit = 0;
        ConsoleCapture.Stdout(() =>
        {
            exit = AgentLifecycleHandlers.ExecuteClaim("Zelda");
        });

        Assert.Equal(ExitCodes.Success, exit);
    }

    [Fact]
    public void ExecuteClaim_Auto_StaleEnvVarMismatch_RefusedWithoutClaiming()
    {
        // Pool order is [Charlie, Zelda], so `claim auto` would pick Charlie (first free).
        // DYDO_AGENT names Zelda — a mismatch. The refusal must happen BEFORE ClaimAuto runs,
        // so no agent is left half-claimed. Regression test for the review BLOCKER where the
        // auto branch claimed first and checked the stale env afterwards.
        Environment.SetEnvironmentVariable("DYDO_AGENT", "Zelda");
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");

        int exit = 0;
        var stderr = ConsoleCapture.Stderr(() =>
        {
            exit = AgentLifecycleHandlers.ExecuteClaim("auto");
        });

        Assert.Equal(ExitCodes.ToolError, exit);
        Assert.Contains("DYDO_AGENT is set to 'Zelda'", stderr);
        // No claim leaked — neither agent has a .session file.
        Assert.False(File.Exists(Path.Combine(_testDir, "dydo", "agents", "Charlie", ".session")));
        Assert.False(File.Exists(Path.Combine(_testDir, "dydo", "agents", "Zelda", ".session")));
    }

    [Fact]
    public void ExecuteClaim_Auto_StaleEnvVarMatchesAutoTarget_Allowed()
    {
        // DYDO_AGENT names Charlie, which is exactly the agent `claim auto` would pick
        // (first free in pool order) — the re-claim shape; allowed.
        Environment.SetEnvironmentVariable("DYDO_AGENT", "Charlie");
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        new AgentRegistry(_testDir).StorePendingSessionId("Charlie", "test-session-charlie");

        int exit = 0;
        ConsoleCapture.Stdout(() =>
        {
            exit = AgentLifecycleHandlers.ExecuteClaim("auto");
        });

        Assert.Equal(ExitCodes.Success, exit);
        Assert.True(File.Exists(Path.Combine(_testDir, "dydo", "agents", "Charlie", ".session")));
    }

    [Fact]
    public void ExecuteRole_TraversalTaskName_ExitsValidationErrors_WritesNothingOutsideTasksTree()
    {
        // The command surface validates the task name BEFORE any registry call (AgentLifecycleHandlers.ExecuteRole
        // ~196-202): a traversal or rooted name — which would become a file path in the tasks tree and the
        // needs-human mirror — must exit ValidationErrors, no filesystem touch. Previously only the registry's
        // defence-in-depth path (SetRole) was covered; this pins the outer gate.
        CreateClaimedAgent("Zelda", "sess-role");
        new AgentRegistry(_testDir).StoreSessionContext("sess-role", "Zelda");

        int exit = 0;
        var stderr = ConsoleCapture.Stderr(() =>
        {
            exit = AgentLifecycleHandlers.ExecuteRole("code-writer", "../../evil");
        });

        Assert.Equal(ExitCodes.ValidationErrors, exit);
        Assert.Contains("Invalid task name", stderr);
        Assert.False(File.Exists(Path.Combine(_testDir, "evil.md")));
        Assert.False(File.Exists(Path.Combine(_testDir, "dydo", "project", "tasks", "evil.md")));
    }
}
