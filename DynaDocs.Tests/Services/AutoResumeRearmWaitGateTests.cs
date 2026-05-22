namespace DynaDocs.Tests.Services;

using System.Text.Json;
using DynaDocs.Commands;
using DynaDocs.Models;
using DynaDocs.Serialization;
using DynaDocs.Services;
using DynaDocs.Utils;

/// <summary>
/// Pins the F11 ownership gate's behavior on the auto-resume path (#0207).
///
/// A <c>dydo wait</c> with no claude ancestor and a stale ClaimedPid is — and
/// stays — refused: that is the F11 wait-DoS attacker shape. The pre-#0207
/// launcher-spawned re-arm had exactly that shape (a sibling of <c>claude</c>,
/// not a descendant), which is why #0207 part 1 deletes it rather than trying to
/// whitelist it — there is no signal that separates the doomed re-arm from an
/// attacker. How a resumed agent legitimately arms its own wait is #0207 part 2.
/// </summary>
public class AutoResumeRearmWaitGateTests : IDisposable
{
    private const string ResumeSessionId = "resume-sid-001";
    private const int DeadPreResumeClaudePid = 999001;
    private readonly string _testDir;
    private readonly string _originalDir;

    public AutoResumeRearmWaitGateTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-rearm-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _originalDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(_testDir);
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), """
            { "version": 1, "agents": { "pool": ["Adele", "Zelda"],
              "assignments": { "testuser": ["Adele", "Zelda"] } } }
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
        try { Directory.Delete(_testDir, true); } catch { }
    }

    private void SetUpResumedAdele()
    {
        var agentsDir = Path.Combine(_testDir, "dydo", "agents");
        var workspace = Path.Combine(agentsDir, "Adele");
        Directory.CreateDirectory(workspace);
        var session = new AgentSession
        {
            Agent = "Adele", SessionId = ResumeSessionId,
            Claimed = DateTime.UtcNow, ClaimedPid = DeadPreResumeClaudePid
        };
        File.WriteAllText(Path.Combine(workspace, ".session"),
            JsonSerializer.Serialize(session, DydoDefaultJsonContext.Default.AgentSession));
        File.WriteAllText(Path.Combine(workspace, "state.md"), """
            ---
            agent: Adele
            role: code-writer
            task: t
            status: working
            ---
            """);
        File.WriteAllText(Path.Combine(agentsDir, ".session-context"),
            $"{ResumeSessionId}\nAdele");
    }

    [Fact]
    public void WaitWithoutClaudeAncestor_StaleClaimedPid_RefusedByF11Gate()
    {
        // The F11 wait-DoS attacker shape: a `dydo wait` with no claude ancestor and a
        // stale ClaimedPid. The pre-#0207 launcher re-arm had exactly this shape, which
        // is why part 1 deletes it — there is no signal that separates it from an
        // attacker, so the gate correctly STAYS closed. If the gate were removed the
        // poll loop would be entered and the `.waiting` marker would survive.
        SetUpResumedAdele();
        ProcessUtils.FindAncestorProcessOverride = (_, _) => null;   // no claude ancestor
        Environment.SetEnvironmentVariable("DYDO_AGENT", "Adele");
        ProcessUtils.IsProcessRunningOverride = _ => false;          // poll-loop safety net

        var command = WaitCommand.Create();
        var (exitCode, _, stderr) = ConsoleCapture.All(() => command.Parse(Array.Empty<string>()).Invoke());

        Assert.Equal(ExitCodes.ToolError, exitCode);
        Assert.Contains("does not own", stderr);
        var waitingDir = Path.Combine(_testDir, "dydo", "agents", "Adele", ".waiting");
        Assert.False(Directory.Exists(waitingDir),
            "F11 refused before the poll loop — no _general-wait marker should have been registered.");
    }

    [Fact]
    public void HonestCallerUnderOwnClaude_PassesSameGate()
    {
        SetUpResumedAdele();
        ProcessUtils.FindAncestorProcessOverride = (_, _) => DeadPreResumeClaudePid;
        var registry = new AgentRegistry(_testDir);
        Assert.True(registry.VerifyCallerOwnsAgent("Adele"));
    }
}
