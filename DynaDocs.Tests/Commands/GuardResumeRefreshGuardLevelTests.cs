namespace DynaDocs.Tests.Commands;

using System.Text.Json;
using DynaDocs.Commands;
using DynaDocs.Models;
using DynaDocs.Serialization;
using DynaDocs.Services;
using DynaDocs.Utils;

/// <summary>
/// Guard-level test for #0207 part 2: confirms a resumed session's first guarded tool
/// call produces a <c>recovery_kind=auto</c> Claim audit event ahead of the tool's own
/// audit event (plan edge case H1). Driven through <c>dydo guard --action … --path …</c>
/// (the CLI-args branch — H3 of the plan), since GuardCommand.Execute is private.
/// </summary>
[Collection("ProcessUtils")]
public class GuardResumeRefreshGuardLevelTests : IDisposable
{
    private const string ResumeSessionId = "sess-guard-resume-001";
    private const int DeadPreResumePid = 999991;
    private const int LiveClaudePid = 232323;

    private readonly string _testDir;
    private readonly string _originalDir;

    public GuardResumeRefreshGuardLevelTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-guard-level-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _originalDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(_testDir);

        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), """
            { "version": 1, "agents": { "pool": ["Adele"],
              "assignments": { "testuser": ["Adele"] } } }
            """);
        // Off-limits file so OffLimitsService.LoadPatterns doesn't break.
        Directory.CreateDirectory(Path.Combine(_testDir, "dydo"));
        File.WriteAllText(Path.Combine(_testDir, "dydo", "files-off-limits.md"), """
            ---
            type: config
            ---
            ```
            ```
            """);
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDir);
        Environment.SetEnvironmentVariable("DYDO_AGENT", null);
        Environment.SetEnvironmentVariable("DYDO_HUMAN", null);
        ProcessUtils.FindAncestorProcessOverride = null;
        ProcessUtils.IsProcessRunningOverride = null;
        WatchdogLogger.LogPathOverride = null;
        WatchdogService.ResumeWarmupGateOverride = null;
        try { Directory.Delete(_testDir, true); } catch { }
    }

    private void SetUpResumedAdele()
    {
        var workspace = Path.Combine(_testDir, "dydo", "agents", "Adele");
        Directory.CreateDirectory(workspace);
        var session = new AgentSession
        {
            Agent = "Adele",
            SessionId = ResumeSessionId,
            Claimed = DateTime.UtcNow.AddMinutes(-10),
            ClaimedPid = DeadPreResumePid
        };
        File.WriteAllText(Path.Combine(workspace, ".session"),
            JsonSerializer.Serialize(session, DydoDefaultJsonContext.Default.AgentSession));
        File.WriteAllText(Path.Combine(workspace, "state.md"), $$"""
            ---
            agent: Adele
            role: co-thinker
            task: t
            status: working
            assigned: testuser
            started: {{DateTime.UtcNow.AddMinutes(-15):o}}
            resume-attempts: 2
            last-resume-launched-at: {{DateTime.UtcNow.AddSeconds(-60):o}}
            pre-resume-pid: 12345
            launched-pid: 54321
            writable-paths: ['**']
            readonly-paths: []
            unread-must-reads: []
            unread-messages: []
            task-role-history: {}
            ---
            """);
        File.WriteAllText(Path.Combine(_testDir, "dydo", "agents", ".session-context"),
            $"{ResumeSessionId}\nAdele");
    }

    [Fact]
    public void GuardEmitsClaimRecoveryAuto_BeforeToolAuditEvent()
    {
        // H1 of the plan: the refresh runs before Security Layer 1, so a resumed session's
        // first guarded call (including a *blocked* call) produces the recovery_kind=auto
        // event ahead of the tool's own audit event. We exercise the CLI-args branch
        // (H3) via `dydo guard --action read --path README.md`.
        SetUpResumedAdele();
        ProcessUtils.FindAncestorProcessOverride = (_, _) => LiveClaudePid;
        ProcessUtils.IsProcessRunningOverride = pid => pid != DeadPreResumePid;

        var command = GuardCommand.Create();
        var result = command.Parse(new[] { "--action", "read", "--path", "README.md" }).Invoke();

        // Read all events from the audit session and check ordering.
        var events = ReadAllEventsForSession(ResumeSessionId);

        // Find the recovery Claim event.
        var claimIdx = events.FindIndex(e => e.EventType == AuditEventType.Claim && e.RecoveryKind == "auto");
        Assert.True(claimIdx >= 0, "Resumed session's first guarded call must emit a recovery_kind=auto Claim event.");

        // The tool's own Read event must come AFTER the Claim event (or not at all if it was
        // blocked — H1 still pins the ordering invariant). We assert claim is at index 0 of
        // its kind: any Read in this session was emitted by THIS call, so it comes after.
        for (var i = 0; i < claimIdx; i++)
            Assert.NotEqual(AuditEventType.Read, events[i].EventType);

        // Also pin: side-effects of refresh actually happened.
        var refreshedSession = JsonSerializer.Deserialize(
            File.ReadAllText(Path.Combine(_testDir, "dydo", "agents", "Adele", ".session")),
            DydoDefaultJsonContext.Default.AgentSession);
        Assert.NotNull(refreshedSession);
        Assert.Equal(LiveClaudePid, refreshedSession!.ClaimedPid);
    }

    private List<AuditEvent> ReadAllEventsForSession(string sessionId)
    {
        var auditDir = Path.Combine(_testDir, "dydo", "_system", "audit");
        if (!Directory.Exists(auditDir)) return new();
        foreach (var yearDir in Directory.GetDirectories(auditDir))
        {
            var file = Directory.GetFiles(yearDir, $"*-{sessionId}.json").FirstOrDefault();
            if (file == null) continue;
            var session = JsonSerializer.Deserialize(File.ReadAllText(file),
                DydoDefaultJsonContext.Default.AuditSession);
            if (session == null) continue;
            AuditService.MergeSidecarEvents(yearDir, sessionId, session);
            return session.Events.ToList();
        }
        return new();
    }
}
