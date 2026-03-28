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
    public void ShowAllAgents_DispatchedWithQueuedMarker_ShowsQueued()
    {
        WriteAgentState("Adele", "dispatched");
        File.WriteAllText(Path.Combine(_registry.GetAgentWorkspace("Adele"), ".queued"), "default:1");

        var stdout = CaptureStdout(() =>
            AgentListHandler.ShowAllAgents(_registry, false, "testuser"));

        Assert.Contains("queued", stdout);
    }

    [Fact]
    public void ShowAllAgents_DispatchedWithoutQueuedMarker_ShowsDispatched()
    {
        WriteAgentState("Adele", "dispatched");

        var stdout = CaptureStdout(() =>
            AgentListHandler.ShowAllAgents(_registry, false, "testuser"));

        Assert.Contains("dispatched", stdout);
        Assert.DoesNotContain("queued", stdout);
    }

    [Fact]
    public void ShowAllAgents_QueuedAgents_SummaryIncludesQueuedCount()
    {
        WriteAgentState("Adele", "dispatched");
        File.WriteAllText(Path.Combine(_registry.GetAgentWorkspace("Adele"), ".queued"), "default:1");
        WriteAgentState("Brian", "dispatched");
        File.WriteAllText(Path.Combine(_registry.GetAgentWorkspace("Brian"), ".queued"), "default:2");

        var stdout = CaptureStdout(() =>
            AgentListHandler.ShowAllAgents(_registry, false, "testuser"));

        Assert.Contains("2 queued", stdout);
    }

    [Fact]
    public void ShowHumanAgents_DispatchedWithQueuedMarker_ShowsQueued()
    {
        WriteAgentState("Adele", "dispatched");
        File.WriteAllText(Path.Combine(_registry.GetAgentWorkspace("Adele"), ".queued"), "default:1");

        var stdout = CaptureStdout(() =>
            AgentListHandler.ShowHumanAgents(_registry, false, "testuser"));

        Assert.Contains("queued", stdout);
    }

    private void CreateAgentWorkspace(string name)
    {
        Directory.CreateDirectory(_registry.GetAgentWorkspace(name));
    }

    private void WriteAgentState(string name, string status)
    {
        var workspace = _registry.GetAgentWorkspace(name);
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(workspace, "state.md"), $$"""
            ---
            agent: {{name}}
            role: null
            task: null
            status: {{status}}
            assigned: testuser
            dispatched-by: null
            started: 2026-01-01T00:00:00Z
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

    private static string CaptureStdout(Action action)
    {
        var original = Console.Out;
        var sw = new StringWriter();
        Console.SetOut(sw);
        try
        {
            action();
            return sw.ToString();
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    private static string CaptureStderr(Action action)
    {
        var original = Console.Error;
        var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            action();
            return sw.ToString();
        }
        finally
        {
            Console.SetError(original);
        }
    }
}
