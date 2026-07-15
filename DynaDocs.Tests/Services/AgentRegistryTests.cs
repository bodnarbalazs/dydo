namespace DynaDocs.Tests.Services;

using DynaDocs.Models;
using DynaDocs.Services;

/// <summary>
/// Tests for the read-only AgentRegistry that survives the DR-041 carve: path/config resolution
/// and state.md parsing. The claim/roster/identity/session/wait/message/resume machinery was
/// deleted, so this covers only what KEEP code (guard, worktree, validation) reads.
/// </summary>
public class AgentRegistryTests : IDisposable
{
    private readonly string _testDir;

    public AgentRegistryTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-registry-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    private AgentRegistry Registry() => new(_testDir);

    private void WriteState(string agent, string frontmatter)
    {
        var dir = Path.Combine(_testDir, "dydo", "agents", agent);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "state.md"), $"---\n{frontmatter}\n---\n\n# {agent}\n");
    }

    [Fact]
    public void AgentNames_DefaultsToPresetSet_WhenNoConfig()
    {
        var registry = Registry();
        Assert.Contains("Adele", registry.AgentNames);
        Assert.True(registry.IsValidAgentName("adele"));
        Assert.False(registry.IsValidAgentName("NotAnAgent"));
    }

    [Fact]
    public void WorkspacePath_And_GetAgentWorkspace_ResolveUnderAgentsRoot()
    {
        var registry = Registry();
        Assert.EndsWith(Path.Combine("dydo", "agents"), registry.WorkspacePath);
        Assert.Equal(Path.Combine(registry.WorkspacePath, "Adele"), registry.GetAgentWorkspace("Adele"));
    }

    [Fact]
    public void GetAgentState_NoStateFile_ReturnsFreeDefault()
    {
        var state = Registry().GetAgentState("Adele");
        Assert.NotNull(state);
        Assert.Equal("Adele", state!.Name);
        Assert.Equal(AgentStatus.Free, state.Status);
    }

    [Fact]
    public void GetAgentState_InvalidName_ReturnsNull()
    {
        Assert.Null(Registry().GetAgentState("Ghost"));
    }

    [Fact]
    public void GetAgentState_ParsesAllFrontmatterFields()
    {
        WriteState("Adele", """
            agent: Adele
            role: code-writer
            task: my-task
            status: working
            assigned: balazs
            dispatched-by: Brian
            dispatched-by-role: orchestrator
            window-id: win-1
            auto-close: true
            needs-human: true
            needs-human-source: explicit
            started: 2026-07-15T10:00:00.0000000Z
            writable-paths: ["src/**", "tests/**"]
            readonly-paths: ["docs/**"]
            unread-must-reads: ["a.md", "b.md"]
            task-role-history: { "my-task": ["planner", "code-writer"] }
            """);

        var state = Registry().GetAgentState("Adele")!;

        Assert.Equal("code-writer", state.Role);
        Assert.Equal("my-task", state.Task);
        Assert.Equal(AgentStatus.Working, state.Status);
        Assert.Equal("balazs", state.AssignedHuman);
        Assert.Equal("Brian", state.DispatchedBy);
        Assert.Equal("orchestrator", state.DispatchedByRole);
        Assert.Equal("win-1", state.WindowId);
        Assert.True(state.AutoClose);
        Assert.True(state.NeedsHuman);
        Assert.Equal(NeedsHumanSource.Explicit, state.NeedsHumanSource);
        Assert.NotNull(state.Since);
        Assert.Equal(new[] { "src/**", "tests/**" }, state.WritablePaths);
        Assert.Equal(new[] { "docs/**" }, state.ReadOnlyPaths);
        Assert.Equal(new[] { "a.md", "b.md" }, state.UnreadMustReads);
        Assert.Equal(new[] { "planner", "code-writer" }, state.TaskRoleHistory["my-task"]);
    }

    [Fact]
    public void GetAgentState_NullAndEmptyFields_ParseToDefaults()
    {
        WriteState("Brian", """
            agent: Brian
            role: null
            task: null
            status: free
            assigned: unassigned
            writable-paths: []
            task-role-history: {}
            """);

        var state = Registry().GetAgentState("Brian")!;

        Assert.Null(state.Role);
        Assert.Null(state.Task);
        Assert.Equal(AgentStatus.Free, state.Status);
        Assert.Null(state.AssignedHuman);
        Assert.Empty(state.WritablePaths);
        Assert.Empty(state.TaskRoleHistory);
    }

    [Theory]
    [InlineData("dispatched", AgentStatus.Dispatched)]
    [InlineData("reviewing", AgentStatus.Reviewing)]
    [InlineData("garbage", AgentStatus.Free)]
    public void GetAgentState_ParsesStatusValues(string raw, AgentStatus expected)
    {
        WriteState("Charlie", $"agent: Charlie\nstatus: {raw}");
        Assert.Equal(expected, Registry().GetAgentState("Charlie")!.Status);
    }

    [Fact]
    public void GetAllAgentStates_ReturnsOneStatePerName()
    {
        var registry = Registry();
        var states = registry.GetAllAgentStates();
        Assert.Equal(registry.AgentNames.Count, states.Count);
    }

    [Fact]
    public void GetWorktreeId_ReturnsMarkerContent_OrNull()
    {
        var registry = Registry();
        Assert.Null(registry.GetWorktreeId("Adele"));

        var ws = registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(ws);
        File.WriteAllText(Path.Combine(ws, ".worktree"), "  Adele-20260715  \n");
        Assert.Equal("Adele-20260715", registry.GetWorktreeId("Adele"));
    }
}
