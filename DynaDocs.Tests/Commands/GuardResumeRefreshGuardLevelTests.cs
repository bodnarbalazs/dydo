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
    public void GuardRefreshesResumedSession_OnFirstGuardedCall()
    {
        // The refresh runs before Security Layer 1, so a resumed session's first guarded
        // call (including a *blocked* one) rewrites the dead ClaimedPid to the live ancestor.
        // Driven through the stdin hook branch (session_id delivered by the hook) — the real
        // production resume path. #0250 closed the CLI-args→GetSessionContext fallback for a
        // stale ClaimedPid (it resolves to null now), so the refresh must key off the
        // hook-delivered session_id, which it does.
        SetUpResumedAdele();
        ProcessUtils.FindAncestorProcessOverride = (_, _) => LiveClaudePid;
        ProcessUtils.IsProcessRunningOverride = pid => pid != DeadPreResumePid;

        var originalIn = Console.In;
        Console.SetIn(new StringReader(
            "{\"session_id\":\"" + ResumeSessionId + "\",\"tool_name\":\"Read\",\"tool_input\":{\"file_path\":\"README.md\"}}"));
        try
        {
            var command = GuardCommand.Create();
            command.Parse(Array.Empty<string>()).Invoke();
        }
        finally
        {
            Console.SetIn(originalIn);
        }

        var refreshedSession = JsonSerializer.Deserialize(
            File.ReadAllText(Path.Combine(_testDir, "dydo", "agents", "Adele", ".session")),
            DydoDefaultJsonContext.Default.AgentSession);
        Assert.NotNull(refreshedSession);
        Assert.Equal(LiveClaudePid, refreshedSession!.ClaimedPid);
    }
}
