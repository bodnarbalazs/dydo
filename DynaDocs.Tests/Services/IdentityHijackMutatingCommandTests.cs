namespace DynaDocs.Tests.Services;

using System.Text.Json;
using DynaDocs.Commands;
using DynaDocs.Models;
using DynaDocs.Serialization;
using DynaDocs.Services;
using DynaDocs.Tests.Integration;
using DynaDocs.Utils;

[Collection("Integration")]
public class IdentityHijackMutatingCommandTests : IDisposable
{
    private const int AdeleOwnerPid = 707070;
    private const int AttackerPid = 808080;
    private readonly string _originalDir;
    private readonly string? _originalAgent;
    private readonly string? _originalHuman;
    private readonly IProcessStarter? _originalStarter;
    private readonly Func<string, int, int?>? _originalFindAncestorOverride;
    private readonly string _testDir;

    public IdentityHijackMutatingCommandTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-mutating-hijack-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);

        _originalDir = Environment.CurrentDirectory;
        _originalAgent = Environment.GetEnvironmentVariable("DYDO_AGENT");
        _originalHuman = Environment.GetEnvironmentVariable("DYDO_HUMAN");
        _originalStarter = TerminalLauncher.ProcessStarterOverride;
        _originalFindAncestorOverride = ProcessUtils.FindAncestorProcessOverride;

        Environment.CurrentDirectory = _testDir;
        Environment.SetEnvironmentVariable("DYDO_AGENT", null);
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        ProcessUtils.FindAncestorProcessOverride = (_, _) => AttackerPid;
        TerminalLauncher.ProcessStarterOverride = new NoOpProcessStarter();

        WriteConfig();
        WriteClaimedAgent("Adele", "session-adele", AgentStatus.Working, AdeleOwnerPid,
            role: "orchestrator", task: "owner-task", host: "codex", model: "gpt-5");
        WriteAgentState("Brian", AgentStatus.Free, role: null, task: null);
        WriteSharedSessionContext("session-adele", "Adele");
    }

    public void Dispose()
    {
        Environment.CurrentDirectory = _originalDir;
        Environment.SetEnvironmentVariable("DYDO_AGENT", _originalAgent);
        Environment.SetEnvironmentVariable("DYDO_HUMAN", _originalHuman);
        TerminalLauncher.ProcessStarterOverride = _originalStarter;
        ProcessUtils.FindAncestorProcessOverride = _originalFindAncestorOverride;
        ProcessUtils.GetParentPidOverride = null;
        ProcessUtils.GetProcessNameOverride = null;
        ProcessUtils.GetProcessCommandLineOverride = null;

        try { Directory.Delete(_testDir, recursive: true); } catch { }
    }

    // ── #0256: DYDO_AGENT env fast-path gets the nearest-host-wins gate ──────────────────────
    // The base setup NULLS DYDO_AGENT (:35), leaving the env fast-path structurally uncovered —
    // exactly why 0256 went unnoticed. These cases inherit DYDO_AGENT=Adele (as a dispatched
    // terminal pins it) and build an INTERPOSED foreign host via the multi-ancestor
    // GetParentPidOverride chain (FindAncestorProcessOverride must be NULL, or
    // NoForeignHostNearerThanClaimedHost short-circuits). role / release / wait-registration must
    // REFUSE the interposed foreign worker while a legit dispatched terminal still passes.

    // this process → claude host → Adele's claimed codex host. Descendant ownership passes, but
    // the nearest agent host is claude, not the claimed codex host — a foreign-vendor worker.
    private void SetupEnvPathInterposedForeignWorker()
    {
        const int claudeMidPid = 606060;
        Environment.SetEnvironmentVariable("DYDO_AGENT", "Adele");
        ProcessUtils.FindAncestorProcessOverride = null;
        ProcessUtils.GetParentPidOverride = pid =>
            pid == Environment.ProcessId ? claudeMidPid :
            pid == claudeMidPid ? AdeleOwnerPid : null;
        ProcessUtils.GetProcessNameOverride = pid => pid == claudeMidPid ? "claude" : "bash";
    }

    // this process → Adele's claimed codex host directly, no foreign host interposed. The legit
    // dispatched codex terminal owning Adele: nearest-host-wins passes.
    private void SetupEnvPathLegitDispatchedTerminal()
    {
        Environment.SetEnvironmentVariable("DYDO_AGENT", "Adele");
        ProcessUtils.FindAncestorProcessOverride = null;
        ProcessUtils.GetParentPidOverride = pid =>
            pid == Environment.ProcessId ? AdeleOwnerPid : null;
        ProcessUtils.GetProcessNameOverride = _ => "bash";
    }

    [Fact]
    public void Role_EnvPathInterposedForeignWorker_RefusesToRebindOuterAgent()
    {
        SetupEnvPathInterposedForeignWorker();

        var (exitCode, _, stderr) = ConsoleCapture.All(() =>
            AgentLifecycleHandlers.ExecuteRole("code-writer", task: null));

        Assert.Equal(ExitCodes.ToolError, exitCode);
        Assert.Contains("No agent identity", stderr);
        Assert.Contains("role: orchestrator",
            File.ReadAllText(Path.Combine(_testDir, "dydo", "agents", "Adele", "state.md")));
    }

    [Fact]
    public void Release_EnvPathInterposedForeignWorker_RefusesToReleaseOuterAgent()
    {
        SetupEnvPathInterposedForeignWorker();

        var (exitCode, _, stderr) = ConsoleCapture.All(AgentLifecycleHandlers.ExecuteRelease);

        Assert.Equal(ExitCodes.ToolError, exitCode);
        Assert.Contains("No agent identity", stderr);
        Assert.True(File.Exists(Path.Combine(_testDir, "dydo", "agents", "Adele", ".session")));
    }

    [Fact]
    public void TaskCreate_UnownedSharedSessionContext_DoesNotStampContextAgentProvenance()
    {
        var (exitCode, _, _) = ConsoleCapture.All(() =>
            TaskCreateHandler.Execute("hijack-provenance", description: null, area: "general"));

        Assert.Equal(ExitCodes.Success, exitCode);

        var content = File.ReadAllText(Path.Combine(_testDir, "dydo", "project", "tasks", "hijack-provenance.md"));
        Assert.Contains("assigned: unassigned", content);
        Assert.DoesNotContain("assigned: Adele", content);
        Assert.DoesNotContain("assigned-vendor:", content);
        Assert.DoesNotContain("assigned-model:", content);
    }

    private void WriteConfig()
    {
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), """
            {
              "version": 1,
              "agents": {
                "pool": ["Adele", "Brian"],
                "assignments": {
                  "testuser": ["Adele", "Brian"]
                }
              }
            }
            """);
    }

    private void WriteClaimedAgent(string agentName, string sessionId, AgentStatus status, int claimedPid,
        string? role, string? task, string host = "claude", string model = "unknown")
    {
        WriteAgentState(agentName, status, role, task);

        var session = new AgentSession
        {
            Agent = agentName,
            SessionId = sessionId,
            Host = host,
            Model = model,
            Claimed = DateTime.UtcNow,
            ClaimedPid = claimedPid
        };

        File.WriteAllText(Path.Combine(GetAgentDir(agentName), ".session"),
            JsonSerializer.Serialize(session, DydoDefaultJsonContext.Default.AgentSession));
    }

    private void WriteAgentState(string agentName, AgentStatus status, string? role, string? task)
    {
        var dir = GetAgentDir(agentName);
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "inbox"));

        File.WriteAllText(Path.Combine(dir, "state.md"), $$"""
            ---
            agent: {{agentName}}
            role: {{role ?? "null"}}
            task: {{task ?? "null"}}
            status: {{status.ToString().ToLowerInvariant()}}
            assigned: testuser
            dispatched-by: null
            dispatched-by-role: null
            unread-messages: []
            ---
            """);
    }

    private void WriteSharedSessionContext(string sessionId, string agentName)
    {
        var agentsDir = Path.Combine(_testDir, "dydo", "agents");
        Directory.CreateDirectory(agentsDir);
        File.WriteAllText(Path.Combine(agentsDir, ".session-context"), $"{sessionId}\n{agentName}");
    }

    private string GetAgentDir(string agentName) =>
        Path.Combine(_testDir, "dydo", "agents", agentName);
}
