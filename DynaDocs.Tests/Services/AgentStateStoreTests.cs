namespace DynaDocs.Tests.Services;

using DynaDocs.Models;
using DynaDocs.Services;

public class AgentStateStoreTests : IDisposable
{
    private readonly string _testDir;
    private readonly AgentStateStore _store;

    public AgentStateStoreTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-statestore-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _store = new AgentStateStore(
            agent => Path.Combine(_testDir, agent),
            _ => "tester",
            ["Alice", "Bob"]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    private static bool AlwaysValid(string _) => true;
    private static bool NeverValid(string _) => false;

    #region GetAgentState

    [Fact]
    public void GetAgentState_InvalidName_ReturnsNull()
    {
        Assert.Null(_store.GetAgentState("Alice", NeverValid));
    }

    [Fact]
    public void GetAgentState_NoStateFile_ReturnsFreeState()
    {
        var state = _store.GetAgentState("Alice", AlwaysValid);

        Assert.NotNull(state);
        Assert.Equal("Alice", state.Name);
        Assert.Equal(AgentStatus.Free, state.Status);
        Assert.Equal("tester", state.AssignedHuman);
    }

    [Fact]
    public void GetAgentState_WithStateFile_ParsesCorrectly()
    {
        var ws = Path.Combine(_testDir, "Alice");
        Directory.CreateDirectory(ws);
        File.WriteAllText(Path.Combine(ws, "state.md"), """
            ---
            agent: Alice
            role: code-writer
            task: my-task
            status: working
            assigned: tester
            dispatched-by: Brian
            window-id: null
            auto-close: false
            started: 2026-03-13T10:00:00Z
            writable-paths: ["src/**", "tests/**"]
            readonly-paths: ["dydo/**"]
            unread-must-reads: []
            unread-messages: []
            task-role-history: {"my-task": ["planner"]}
            ---
            """);

        var state = _store.GetAgentState("Alice", AlwaysValid);

        Assert.NotNull(state);
        Assert.Equal("code-writer", state.Role);
        Assert.Equal("my-task", state.Task);
        Assert.Equal(AgentStatus.Working, state.Status);
        Assert.Equal("Brian", state.DispatchedBy);
        Assert.Contains("src/**", state.WritablePaths);
        Assert.Contains("dydo/**", state.ReadOnlyPaths);
        Assert.True(state.TaskRoleHistory.ContainsKey("my-task"));
    }

    #endregion

    #region GetAllAgentStates

    [Fact]
    public void GetAllAgentStates_ReturnsAllAgents()
    {
        var states = _store.GetAllAgentStates(AlwaysValid);
        Assert.Equal(2, states.Count);
    }

    #endregion

    #region UpdateAgentState

    [Fact]
    public void UpdateAgentState_WritesAndReads()
    {
        _store.UpdateAgentState("Alice", s =>
        {
            s.Role = "reviewer";
            s.Task = "review-task";
            s.Status = AgentStatus.Reviewing;
        }, AlwaysValid);

        var state = _store.GetAgentState("Alice", AlwaysValid);

        Assert.Equal("reviewer", state!.Role);
        Assert.Equal("review-task", state.Task);
        Assert.Equal(AgentStatus.Reviewing, state.Status);
    }

    #endregion

    #region SetDispatchMetadata

    [Fact]
    public void SetDispatchMetadata_SetsWindowIdAndAutoClose()
    {
        _store.SetDispatchMetadata("Alice", "win-123", true, AlwaysValid);

        var state = _store.GetAgentState("Alice", AlwaysValid);
        Assert.Equal("win-123", state!.WindowId);
        Assert.True(state.AutoClose);
    }

    #endregion

    #region ParseStateFile edge cases

    [Fact]
    public void ParseStateFile_MalformedContent_ReturnsFallback()
    {
        var ws = Path.Combine(_testDir, "Alice");
        Directory.CreateDirectory(ws);
        File.WriteAllText(Path.Combine(ws, "state.md"), "not frontmatter");

        var state = _store.ParseStateFile("Alice", Path.Combine(ws, "state.md"));
        Assert.NotNull(state);
        Assert.Equal("Alice", state.Name);
    }

    [Fact]
    public void ParseStateFile_UnclosedFrontmatter_ReturnsFallback()
    {
        var ws = Path.Combine(_testDir, "Alice");
        Directory.CreateDirectory(ws);
        File.WriteAllText(Path.Combine(ws, "state.md"), "---\nagent: Alice\nno closing");

        var state = _store.ParseStateFile("Alice", Path.Combine(ws, "state.md"));
        Assert.NotNull(state);
        Assert.Equal("Alice", state.Name);
    }

    [Fact]
    public void ParseStateFile_StatusDispatchedMapsCorrectly()
    {
        var ws = Path.Combine(_testDir, "Alice");
        Directory.CreateDirectory(ws);
        File.WriteAllText(Path.Combine(ws, "state.md"), """
            ---
            agent: Alice
            role: null
            task: null
            status: dispatched
            assigned: tester
            dispatched-by: null
            window-id: null
            auto-close: true
            started: null
            writable-paths: []
            readonly-paths: []
            unread-must-reads: []
            unread-messages: []
            task-role-history: {}
            ---
            """);

        var state = _store.ParseStateFile("Alice", Path.Combine(ws, "state.md"));
        Assert.Equal(AgentStatus.Dispatched, state!.Status);
        Assert.True(state.AutoClose);
    }

    #endregion

    #region Static parse utilities

    [Theory]
    [InlineData("[]", 0)]
    [InlineData("", 0)]
    [InlineData("[\"src/**\", \"tests/**\"]", 2)]
    public void ParsePathList_VariousInputs(string input, int expectedCount)
    {
        Assert.Equal(expectedCount, AgentStateStore.ParsePathList(input).Count);
    }

    [Fact]
    public void ParsePathList_NoSquareBrackets_ReturnsEmpty()
    {
        Assert.Empty(AgentStateStore.ParsePathList("no brackets"));
    }

    [Fact]
    public void ParseTaskRoleHistory_EmptyOrBraces_ReturnsEmpty()
    {
        Assert.Empty(AgentStateStore.ParseTaskRoleHistory("{}"));
        Assert.Empty(AgentStateStore.ParseTaskRoleHistory(""));
    }

    [Fact]
    public void ParseTaskRoleHistory_ValidInput_ParsesCorrectly()
    {
        var result = AgentStateStore.ParseTaskRoleHistory("{\"task1\": [\"planner\", \"code-writer\"]}");
        Assert.True(result.ContainsKey("task1"));
        Assert.Equal(2, result["task1"].Count);
    }

    [Fact]
    public void FormatTaskRoleHistory_EmptyDict_ReturnsBraces()
    {
        Assert.Equal("{}", AgentStateStore.FormatTaskRoleHistory(new()));
    }

    [Fact]
    public void FormatTaskRoleHistory_RoundTrips()
    {
        var original = new Dictionary<string, List<string>>
        {
            ["task1"] = ["planner", "code-writer"]
        };

        var formatted = AgentStateStore.FormatTaskRoleHistory(original);
        var parsed = AgentStateStore.ParseTaskRoleHistory(formatted);

        Assert.Equal(original.Keys, parsed.Keys);
        Assert.Equal(original["task1"], parsed["task1"]);
    }

    #endregion
}
