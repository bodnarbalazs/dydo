namespace DynaDocs.Tests.Services;

using DynaDocs.Models;
using DynaDocs.Services;

[Collection("ProcessUtils")]
public class StaleDispatchDoubleClaimTests : IDisposable
{
    private readonly string _testDir;
    private readonly AgentRegistry _registry;

    public StaleDispatchDoubleClaimTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-stale-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _registry = new AgentRegistry(_testDir);
    }

    public void Dispose()
    {
        AgentRegistry.IsLauncherAliveOverride = null;
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void ReserveAgent_StaleButLauncherAlive_Fails()
    {
        WriteDispatchedState("Adele", DateTime.UtcNow.AddMinutes(-5));
        AgentRegistry.IsLauncherAliveOverride = _ => true;

        var result = _registry.ReserveAgent("Adele", out var error);

        Assert.False(result, "ReserveAgent should refuse to reclaim a stale dispatch whose launcher process is still alive");
        Assert.Contains("not free", error);

        var state = _registry.GetAgentState("Adele");
        Assert.NotNull(state);
        Assert.Equal(AgentStatus.Dispatched, state.Status);
    }

    [Fact]
    public void ReserveAgent_StaleAndNoLauncher_Succeeds()
    {
        var originalSince = DateTime.UtcNow.AddMinutes(-5);
        WriteDispatchedState("Adele", originalSince);
        AgentRegistry.IsLauncherAliveOverride = _ => false;

        var result = _registry.ReserveAgent("Adele", out var error);

        Assert.True(result, $"ReserveAgent should reclaim a stale dispatch when no launcher is alive (error: {error})");

        var state = _registry.GetAgentState("Adele");
        Assert.NotNull(state);
        Assert.Equal(AgentStatus.Dispatched, state.Status);
        Assert.NotNull(state.Since);
        Assert.True(state.Since.Value.ToUniversalTime() > originalSince,
            "Since should be bumped to the fresh reservation time");
    }

    [Fact]
    public void ReserveAgent_FreshDispatched_Fails()
    {
        WriteDispatchedState("Adele", DateTime.UtcNow);
        // Probe must not be consulted on the fresh path; fail loudly if it is.
        AgentRegistry.IsLauncherAliveOverride = _ => throw new InvalidOperationException(
            "Launcher probe should not be consulted for a fresh dispatch");

        var result = _registry.ReserveAgent("Adele", out var error);

        Assert.False(result);
        Assert.Contains("not free", error);
    }

    private void WriteDispatchedState(string agentName, DateTime since)
    {
        var workspace = Path.Combine(_testDir, "dydo", "agents", agentName);
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(workspace, "state.md"), $$"""
            ---
            agent: {{agentName}}
            role: null
            task: null
            status: dispatched
            assigned: null
            dispatched-by: null
            dispatched-by-role: null
            window-id: null
            auto-close: false
            started: {{since.ToUniversalTime():o}}
            writable-paths: []
            readonly-paths: []
            unread-must-reads: []
            unread-messages: []
            task-role-history: {}
            ---
            """);
    }
}
