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
    public void ExecuteClaim_NoEnvVar_Allowed()
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
}
