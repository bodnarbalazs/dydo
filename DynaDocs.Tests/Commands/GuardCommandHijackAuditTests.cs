namespace DynaDocs.Tests.Commands;

using System.Text.Json;
using DynaDocs.Models;
using DynaDocs.Serialization;
using DynaDocs.Services;

// Closes #0194 (F10). Pre-F1, AgentRegistry.SetRole resolved the calling agent via
// GetCurrentAgent(GetSessionContext()), where GetSessionContext blindly trusted DYDO_AGENT.
// Lifecycle audit events stamped that hijacked agent name, so the audit attribution lied
// to anyone investigating the incident. Post-F1, GetSessionContext returns the verified
// caller's sid, agent.Name resolves to the truthful agent, and the audit event records it
// correctly. No production code change in lifecycle logging itself — this test pins the
// contract so a regression that re-trusts the env path would surface here.
public class GuardCommandHijackAuditTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _originalDir;

    public GuardCommandHijackAuditTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-audit-hijack-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _originalDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(_testDir);

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

    private void WriteClaimedAgent(string agentName, string sessionId, int claimedPid)
    {
        var workspace = Path.Combine(_testDir, "dydo", "agents", agentName);
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
        File.WriteAllText(Path.Combine(workspace, "state.md"), $$"""
            ---
            agent: {{agentName}}
            role: null
            task: null
            status: working
            assigned: testuser
            ---
            """);
    }

    [Fact]
    public void SetRole_UnderHijackAttempt_AuditRecordsActualCaller()
    {
        const int charliePid = 131313;
        const int zeldaPid = 424242;

        WriteClaimedAgent("Charlie", "sid-charlie", charliePid);
        WriteClaimedAgent("Zelda", "sid-zelda", zeldaPid);

        // Caller is Zelda's claude tab. DYDO_AGENT inherited "Charlie" from upstream.
        ProcessUtils.FindAncestorProcessOverride = (_, _) => zeldaPid;
        Environment.SetEnvironmentVariable("DYDO_AGENT", "Charlie");

        var registry = new AgentRegistry(_testDir);
        // Simulate Zelda's claude tab having published verified context.
        registry.StoreSessionContext("sid-zelda", "Zelda");

        var sessionId = registry.GetSessionContext();
        Assert.Equal("sid-zelda", sessionId);

        var ok = registry.SetRole(sessionId, "co-thinker", "t1", out var error);
        Assert.True(ok, $"SetRole failed: {error}");

        var auditService = new AuditService(new ConfigService(), _testDir);

        // Zelda's session is the only one that should have an audit event for this role-set.
        var zeldaAudit = auditService.GetSession("sid-zelda");
        Assert.NotNull(zeldaAudit);
        Assert.Equal("Zelda", zeldaAudit.AgentName);
        Assert.Contains(zeldaAudit.Events, e =>
            e.EventType == AuditEventType.Role && e.Role == "co-thinker" && e.Task == "t1");

        // Charlie's session received no role event — the hijack was rejected by F1, so the
        // audit attribution stayed honest.
        var charlieAudit = auditService.GetSession("sid-charlie");
        if (charlieAudit != null)
        {
            Assert.DoesNotContain(charlieAudit.Events, e =>
                e.EventType == AuditEventType.Role && e.Role == "co-thinker" && e.Task == "t1");
        }
    }
}
