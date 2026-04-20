namespace DynaDocs.Tests.Services;

using System.Text.Json;
using DynaDocs.Models;
using DynaDocs.Serialization;
using DynaDocs.Services;

[Collection("ProcessUtils")]
public class StaleWorkingReclaimTests : IDisposable
{
    private readonly string _testDir;
    private readonly AgentRegistry _registry;

    public StaleWorkingReclaimTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-staleworking-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _registry = new AgentRegistry(_testDir);
    }

    public void Dispose()
    {
        AgentRegistry.IsSessionPidAliveOverride = null;
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void ReserveAgent_StaleWorkingWithDeadPid_Succeeds()
    {
        WriteWorkingState("Adele", DateTime.UtcNow.AddMinutes(-10));
        WriteSession("Adele", pid: 999001);
        AgentRegistry.IsSessionPidAliveOverride = _ => false;

        var result = _registry.ReserveAgent("Adele", out var error);

        Assert.True(result, $"ReserveAgent should reclaim a stale-working agent whose session PID is dead (error: {error})");

        var state = _registry.GetAgentState("Adele");
        Assert.NotNull(state);
        Assert.Equal(AgentStatus.Dispatched, state.Status);
    }

    [Fact]
    public void ReserveAgent_StaleWorkingWithAlivePid_Fails()
    {
        WriteWorkingState("Adele", DateTime.UtcNow.AddMinutes(-10));
        WriteSession("Adele", pid: 999002);
        AgentRegistry.IsSessionPidAliveOverride = _ => true;

        var result = _registry.ReserveAgent("Adele", out var error);

        Assert.False(result, "ReserveAgent must refuse to reclaim a working agent whose session PID is still alive");
        Assert.Contains("not free", error);
    }

    [Fact]
    public void ReserveAgent_FreshWorkingWithDeadPid_Fails()
    {
        WriteWorkingState("Adele", DateTime.UtcNow);
        WriteSession("Adele", pid: 999003);
        AgentRegistry.IsSessionPidAliveOverride = _ => throw new InvalidOperationException(
            "Session PID probe should not be consulted for a fresh working session");

        var result = _registry.ReserveAgent("Adele", out var error);

        Assert.False(result);
        Assert.Contains("not free", error);
    }

    [Fact]
    public void GetFreeAgents_IncludesStaleWorkingWithDeadPid()
    {
        WriteWorkingState("Adele", DateTime.UtcNow.AddMinutes(-10));
        WriteSession("Adele", pid: 999004);
        AgentRegistry.IsSessionPidAliveOverride = _ => false;

        var free = _registry.GetFreeAgents();

        Assert.Contains(free, a => a.Name == "Adele");
    }

    [Fact]
    public void GetFreeAgents_IncludesStaleWorkingRegardlessOfPid()
    {
        // Decision 017: display is permissive — surface stale-working agents as
        // reclaim candidates even when their Claude is still alive. The strict
        // session-pid gate lives in IsReservable, exercised by ReserveAgent_*.
        WriteWorkingState("Adele", DateTime.UtcNow.AddMinutes(-10));
        WriteSession("Adele", pid: 999005);
        AgentRegistry.IsSessionPidAliveOverride = _ => throw new InvalidOperationException(
            "Session PID probe must not be consulted from the display path");

        var free = _registry.GetFreeAgents();

        Assert.Contains(free, a => a.Name == "Adele");
    }

    [Fact]
    public void ReserveAgent_StaleWorkingWithNoSessionFile_Succeeds()
    {
        WriteWorkingState("Adele", DateTime.UtcNow.AddMinutes(-10));
        // No .session file — treat as dead so reclaim proceeds.
        AgentRegistry.IsSessionPidAliveOverride = null;

        var result = _registry.ReserveAgent("Adele", out var error);

        Assert.True(result, $"ReserveAgent should reclaim a stale-working agent with no .session (error: {error})");
    }

    [Fact]
    public void ReserveAgent_StaleWorkingWithNoClaimedPidInSession_Succeeds()
    {
        WriteWorkingState("Adele", DateTime.UtcNow.AddMinutes(-10));
        WriteSession("Adele", pid: null);
        AgentRegistry.IsSessionPidAliveOverride = null;

        var result = _registry.ReserveAgent("Adele", out var error);

        Assert.True(result, $"ReserveAgent should reclaim a stale-working agent whose .session has no ClaimedPid (error: {error})");
    }

    private void WriteWorkingState(string agentName, DateTime since)
    {
        var workspace = Path.Combine(_testDir, "dydo", "agents", agentName);
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(workspace, "state.md"), $$"""
            ---
            agent: {{agentName}}
            role: null
            task: null
            status: working
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

    private void WriteSession(string agentName, int? pid)
    {
        var workspace = Path.Combine(_testDir, "dydo", "agents", agentName);
        Directory.CreateDirectory(workspace);
        var session = new AgentSession
        {
            Agent = agentName,
            SessionId = "test-session-" + agentName,
            Claimed = DateTime.UtcNow.AddMinutes(-15),
            ClaimedPid = pid
        };
        File.WriteAllText(
            Path.Combine(workspace, ".session"),
            JsonSerializer.Serialize(session, DydoDefaultJsonContext.Default.AgentSession));
    }
}
