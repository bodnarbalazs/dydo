namespace DynaDocs.Tests.Commands;

using DynaDocs.Commands;
using DynaDocs.Services;

[Collection("ConsoleOutput")]
public class AgentListHandlerTests : IDisposable
{
    private readonly string _testDir;
    private readonly AgentRegistry _registry;

    public AgentListHandlerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-alh-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);

        // Create config with a small pool and human assignment
        var configDir = Path.Combine(_testDir, "dydo");
        Directory.CreateDirectory(configDir);
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), """
            {
              "version": 1,
              "agents": {
                "pool": ["Adele", "Brian", "Charlie"],
                "assignments": {
                  "testuser": ["Adele", "Brian"],
                  "alice": ["Charlie"]
                }
              }
            }
            """);

        _registry = new AgentRegistry(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public void ShowAllAgents_WithWorktrees_ShowsWorktreeColumn()
    {
        CreateAgentWorkspace("Adele");
        CreateWorktreeMarker("Adele", "Adele-20260314120000");
        CreateWorktreeDir("Adele-20260314120000");

        var stdout = CaptureStdout(() =>
            AgentListHandler.ShowAllAgents(_registry, false, "testuser"));

        Assert.Contains("Worktree", stdout);
        Assert.Contains("Adele-0314", stdout);
    }

    [Fact]
    public void ShowAllAgents_ShowsHostColumn()
    {
        WriteAgentSession("Adele", "codex");

        var stdout = CaptureStdout(() =>
            AgentListHandler.ShowAllAgents(_registry, false, "testuser"));

        Assert.Contains("Host", stdout);
        var columns = SplitAgentLine(stdout, "Adele");
        Assert.Equal("codex", columns[2]);
    }

    [Fact]
    public void ShowAllAgents_ShowsModelColumn_WithDisplayName()
    {
        WriteAgentSession("Adele", "claude", "claude-opus-4-8");

        var stdout = CaptureStdout(() =>
            AgentListHandler.ShowAllAgents(_registry, false, "testuser"));

        Assert.Contains("Model", stdout);    // header column
        Assert.Contains("Opus 4.8", stdout); // resolved display name (defaults apply)
    }

    [Fact]
    public void ShowAllAgents_UnknownModel_ModelColumnFallsBackToVendor()
    {
        WriteAgentSession("Adele", "codex"); // no runtime model captured

        var stdout = CaptureStdout(() =>
            AgentListHandler.ShowAllAgents(_registry, false, "testuser"));

        var columns = SplitAgentLine(stdout, "Adele");
        Assert.Equal("codex", columns[2]); // Host
        Assert.Equal("codex", columns[3]); // Model — vendor fallback when model unknown
    }

    [Fact]
    public void ShowAllAgents_NoSession_ShowsModelDash()
    {
        CreateAgentWorkspace("Adele");

        var stdout = CaptureStdout(() =>
            AgentListHandler.ShowAllAgents(_registry, false, "testuser"));

        var columns = SplitAgentLine(stdout, "Adele");
        Assert.Equal("-", columns[3]); // Model dash when no session
    }

    [Fact]
    public void ShowAllAgents_NoSession_ShowsHostDash()
    {
        CreateAgentWorkspace("Adele");

        var stdout = CaptureStdout(() =>
            AgentListHandler.ShowAllAgents(_registry, false, "testuser"));

        var columns = SplitAgentLine(stdout, "Adele");
        Assert.Equal("-", columns[2]);
    }

    [Fact]
    public void ShowAllAgents_WithoutWorktrees_OmitsWorktreeColumn()
    {
        CreateAgentWorkspace("Adele");

        var stdout = CaptureStdout(() =>
            AgentListHandler.ShowAllAgents(_registry, false, "testuser"));

        Assert.DoesNotContain("Worktree", stdout);
    }

    [Fact]
    public void ShowAllAgents_FreeOnly_NoFreeAgents_PrintsNoFreeMessage()
    {
        WriteAgentState("Adele", "working");
        WriteAgentState("Brian", "working");
        WriteAgentState("Charlie", "working");

        var stdout = CaptureStdout(() =>
            AgentListHandler.ShowAllAgents(_registry, true, "testuser"));

        Assert.Contains("No free agents", stdout);
    }

    [Fact]
    public void ShowAllAgents_NoAgents_PrintsNoAgentsMessage()
    {
        // Use a registry with an empty pool
        var emptyDir = Path.Combine(Path.GetTempPath(), "dydo-alh-empty-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(emptyDir);
        File.WriteAllText(Path.Combine(emptyDir, "dydo.json"), """
            {
              "version": 1,
              "agents": { "pool": [], "assignments": {} }
            }
            """);
        try
        {
            var emptyRegistry = new AgentRegistry(emptyDir);
            var stdout = CaptureStdout(() =>
                AgentListHandler.ShowAllAgents(emptyRegistry, false, null));

            Assert.Contains("No agents found", stdout);
        }
        finally
        {
            try { Directory.Delete(emptyDir, true); } catch { }
        }
    }

    [Fact]
    public void ShowAllAgents_WithStaleWorktree_ShowsQuestionMark()
    {
        CreateAgentWorkspace("Adele");
        CreateWorktreeMarker("Adele", "Adele-20260314120000");
        // Don't create the worktree dir → stale

        var stdout = CaptureStdout(() =>
            AgentListHandler.ShowAllAgents(_registry, false, "testuser"));

        Assert.Contains("?", stdout);
    }

    [Fact]
    public void ShowHumanAgents_WithWorktrees_ShowsWorktreeColumn()
    {
        WriteAgentState("Adele", "working");
        CreateWorktreeMarker("Adele", "Adele-20260314120000");
        CreateWorktreeDir("Adele-20260314120000");

        var stdout = CaptureStdout(() =>
            AgentListHandler.ShowHumanAgents(_registry, false, "testuser"));

        Assert.Contains("Worktree", stdout);
        Assert.Contains("Adele-0314", stdout);
    }

    [Fact]
    public void ShowHumanAgents_ShowsHostColumn()
    {
        WriteAgentState("Adele", "working");
        WriteAgentSession("Adele", "claude");

        var stdout = CaptureStdout(() =>
            AgentListHandler.ShowHumanAgents(_registry, false, "testuser"));

        Assert.Contains("Host", stdout);
        var columns = SplitAgentLine(stdout, "Adele");
        Assert.Equal("claude", columns[2]);
    }

    [Fact]
    public void ShowHumanAgents_NoHuman_ReturnsError()
    {
        var result = -1;
        CaptureStderr(() =>
        {
            result = AgentListHandler.ShowHumanAgents(_registry, false, null);
        });

        Assert.Equal(2, result);
    }

    [Fact]
    public void ShowHumanAgents_NoAgentsForHuman_PrintsNoAgentsMessage()
    {
        var stdout = CaptureStdout(() =>
            AgentListHandler.ShowHumanAgents(_registry, false, "nobody"));

        Assert.Contains("No agents found", stdout);
    }

    [Fact]
    public void ShowHumanAgents_FreeOnly_NoFreeAgents_PrintsNoFreeMessage()
    {
        WriteAgentState("Adele", "working");
        WriteAgentState("Brian", "working");

        var stdout = CaptureStdout(() =>
            AgentListHandler.ShowHumanAgents(_registry, true, "testuser"));

        Assert.Contains("No free agents", stdout);
    }

    [Fact]
    public void ShowAllAgents_WithInbox_ShowsStarAndLegend()
    {
        CreateAgentWorkspace("Adele");
        var inboxDir = Path.Combine(_registry.GetAgentWorkspace("Adele"), "inbox");
        Directory.CreateDirectory(inboxDir);
        File.WriteAllText(Path.Combine(inboxDir, "item.md"), "test");

        var stdout = CaptureStdout(() =>
            AgentListHandler.ShowAllAgents(_registry, false, "testuser"));

        Assert.Contains("Adele*", stdout);
        Assert.Contains("* = unread inbox", stdout);
    }

    [Fact]
    public void ShowAllAgents_StaleWorktree_ShowsLegendEntry()
    {
        CreateAgentWorkspace("Adele");
        CreateWorktreeMarker("Adele", "Adele-20260314120000");
        // No worktree dir → stale

        var stdout = CaptureStdout(() =>
            AgentListHandler.ShowAllAgents(_registry, false, "testuser"));

        Assert.Contains("? = stale worktree", stdout);
    }

    [Fact]
    public void ShowAllAgents_Dispatched_ShowsDispatched()
    {
        WriteAgentState("Adele", "dispatched");

        var stdout = CaptureStdout(() =>
            AgentListHandler.ShowAllAgents(_registry, false, "testuser"));

        Assert.Contains("dispatched", stdout);
        Assert.DoesNotContain("queued", stdout);
    }

    private void CreateAgentWorkspace(string name)
    {
        Directory.CreateDirectory(_registry.GetAgentWorkspace(name));
    }

    private void WriteAgentState(string name, string status)
    {
        var workspace = _registry.GetAgentWorkspace(name);
        Directory.CreateDirectory(workspace);
        // Fresh timestamp so the state never trips IsStaleDispatch / IsStaleWorking
        // (stale-reclaim semantics are exercised in dedicated tests).
        File.WriteAllText(Path.Combine(workspace, "state.md"), $$"""
            ---
            agent: {{name}}
            role: null
            task: null
            status: {{status}}
            assigned: testuser
            dispatched-by: null
            started: {{DateTime.UtcNow:o}}
            writable-paths: []
            readonly-paths: []
            unread-must-reads: []
            unread-messages: []
            task-role-history: {}
            ---
            # {{name}} — Session State
            """);
    }

    private void CreateWorktreeMarker(string agent, string worktreeId)
    {
        var workspace = _registry.GetAgentWorkspace(agent);
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(workspace, ".worktree"), worktreeId);
    }

    private void CreateWorktreeDir(string worktreeId)
    {
        Directory.CreateDirectory(Path.Combine(_testDir, "dydo", "_system", ".local", "worktrees", worktreeId));
    }

    private void WriteAgentSession(string name, string host, string? model = null)
    {
        var workspace = _registry.GetAgentWorkspace(name);
        Directory.CreateDirectory(workspace);
        var modelLine = model == null ? "" : $"\n  \"Model\": \"{model}\",";
        File.WriteAllText(Path.Combine(workspace, ".session"), $$"""
            {
              "Agent": "{{name}}",
              "SessionId": "session-{{name}}",
              "Host": "{{host}}",{{modelLine}}
              "Claimed": "2026-01-01T00:00:00Z"
            }
            """);
    }

    private static string[] SplitAgentLine(string stdout, string agentName)
    {
        var line = stdout.Split(Environment.NewLine)
            .Single(l => l.StartsWith(agentName, StringComparison.Ordinal));
        return line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    private static string CaptureStdout(Action action) => ConsoleCapture.Stdout(action);

    private static string CaptureStderr(Action action) => ConsoleCapture.Stderr(action);
}
