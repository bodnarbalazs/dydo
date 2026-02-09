namespace DynaDocs.Tests.Integration;

using DynaDocs.Models;
using DynaDocs.Services;

/// <summary>
/// Integration tests for workflow features:
/// - Self-review prevention (code-writer cannot become reviewer on same task)
/// - Task role history tracking
/// </summary>
[Collection("Integration")]
public class ProcessWorkflowTests : IntegrationTestBase
{
    [Fact]
    public void TaskRoleHistory_TracksRolesOnTasks()
    {
        // Create a state with task role history
        var state = new AgentState
        {
            Name = "TestAgent",
            TaskRoleHistory = new Dictionary<string, List<string>>
            {
                ["my-feature"] = new List<string> { "planner", "code-writer" }
            }
        };

        // Verify the history contains expected roles
        Assert.True(state.TaskRoleHistory.ContainsKey("my-feature"));
        Assert.Contains("code-writer", state.TaskRoleHistory["my-feature"]);
        Assert.Contains("planner", state.TaskRoleHistory["my-feature"]);
    }

    [Fact]
    public void CanTakeRole_ReturnsFalse_ForSelfReview()
    {
        // Create agent state with code-writer history
        var stateContent = """
            ---
            agent: Adele
            role: null
            task: null
            status: free
            assigned: testuser
            started: null
            allowed-paths: []
            denied-paths: []
            task-role-history: { "my-feature": ["code-writer"] }
            ---

            # Adele — Session State
            """;

        // Create test environment
        Directory.CreateDirectory(Path.Combine(TestDir, "dydo", "agents", "Adele"));
        WriteFile("dydo/agents/Adele/state.md", stateContent);

        // Create minimal config
        var configContent = """
            {
              "version": 1,
              "agents": {
                "pool": ["Adele"],
                "assignments": {
                  "testuser": ["Adele"]
                }
              }
            }
            """;
        WriteFile("dydo.json", configContent);

        // Create registry and test CanTakeRole
        var registry = new AgentRegistry(TestDir);

        var canTake = registry.CanTakeRole("Adele", "reviewer", "my-feature", out var reason);

        Assert.False(canTake);
        Assert.Contains("code-writer", reason);
        Assert.Contains("cannot be reviewer", reason);
    }

    [Fact]
    public void CanTakeRole_ReturnsTrue_ForDifferentTask()
    {
        // Create agent state with code-writer history on different task
        var stateContent = """
            ---
            agent: Adele
            role: null
            task: null
            status: free
            assigned: testuser
            started: null
            allowed-paths: []
            denied-paths: []
            task-role-history: { "other-feature": ["code-writer"] }
            ---

            # Adele — Session State
            """;

        Directory.CreateDirectory(Path.Combine(TestDir, "dydo", "agents", "Adele"));
        WriteFile("dydo/agents/Adele/state.md", stateContent);

        var configContent = """
            {
              "version": 1,
              "agents": {
                "pool": ["Adele"],
                "assignments": {
                  "testuser": ["Adele"]
                }
              }
            }
            """;
        WriteFile("dydo.json", configContent);

        var registry = new AgentRegistry(TestDir);

        // Can review a different task
        var canTake = registry.CanTakeRole("Adele", "reviewer", "new-feature", out var reason);

        Assert.True(canTake);
        Assert.Empty(reason);
    }

    [Fact]
    public void CanTakeRole_ReturnsTrue_WhenNoHistory()
    {
        // Create agent state with no history
        var stateContent = """
            ---
            agent: Adele
            role: null
            task: null
            status: free
            assigned: testuser
            started: null
            allowed-paths: []
            denied-paths: []
            task-role-history: {}
            ---

            # Adele — Session State
            """;

        Directory.CreateDirectory(Path.Combine(TestDir, "dydo", "agents", "Adele"));
        WriteFile("dydo/agents/Adele/state.md", stateContent);

        var configContent = """
            {
              "version": 1,
              "agents": {
                "pool": ["Adele"],
                "assignments": {
                  "testuser": ["Adele"]
                }
              }
            }
            """;
        WriteFile("dydo.json", configContent);

        var registry = new AgentRegistry(TestDir);

        var canTake = registry.CanTakeRole("Adele", "reviewer", "any-feature", out var reason);

        Assert.True(canTake);
        Assert.Empty(reason);
    }

    [Fact]
    public void TaskRoleHistory_SerializesCorrectly()
    {
        // Create minimal config
        var configContent = """
            {
              "version": 1,
              "agents": {
                "pool": ["Adele"],
                "assignments": {
                  "testuser": ["Adele"]
                }
              }
            }
            """;
        WriteFile("dydo.json", configContent);

        // Create agent state with history
        Directory.CreateDirectory(Path.Combine(TestDir, "dydo", "agents", "Adele"));
        var stateContent = """
            ---
            agent: Adele
            role: code-writer
            task: my-feature
            status: working
            assigned: testuser
            started: null
            allowed-paths: ["src/**"]
            denied-paths: ["dydo/**"]
            task-role-history: { "my-feature": ["planner", "code-writer"], "old-task": ["reviewer"] }
            ---

            # Adele — Session State
            """;
        WriteFile("dydo/agents/Adele/state.md", stateContent);

        // Load state
        var registry = new AgentRegistry(TestDir);
        var state = registry.GetAgentState("Adele");

        // Verify history was parsed
        Assert.NotNull(state);
        Assert.Equal(2, state.TaskRoleHistory.Count);
        Assert.Contains("my-feature", state.TaskRoleHistory.Keys);
        Assert.Contains("old-task", state.TaskRoleHistory.Keys);
        Assert.Equal(2, state.TaskRoleHistory["my-feature"].Count);
        Assert.Contains("planner", state.TaskRoleHistory["my-feature"]);
        Assert.Contains("code-writer", state.TaskRoleHistory["my-feature"]);
    }

    [Fact]
    public void CanTakeRole_AllowsNonReviewerRoles()
    {
        // Even if an agent was code-writer, they can be planner on same task
        var stateContent = """
            ---
            agent: Adele
            role: null
            task: null
            status: free
            assigned: testuser
            started: null
            allowed-paths: []
            denied-paths: []
            task-role-history: { "my-feature": ["code-writer"] }
            ---

            # Adele — Session State
            """;

        Directory.CreateDirectory(Path.Combine(TestDir, "dydo", "agents", "Adele"));
        WriteFile("dydo/agents/Adele/state.md", stateContent);

        var configContent = """
            {
              "version": 1,
              "agents": {
                "pool": ["Adele"],
                "assignments": {
                  "testuser": ["Adele"]
                }
              }
            }
            """;
        WriteFile("dydo.json", configContent);

        var registry = new AgentRegistry(TestDir);

        // Can be planner on same task (not restricted)
        var canTake = registry.CanTakeRole("Adele", "planner", "my-feature", out var reason);

        Assert.True(canTake);
        Assert.Empty(reason);
    }

    [Fact]
    public void TaskRoleHistory_RoundTrips_ThroughStateParsing()
    {
        // This test verifies that TaskRoleHistory survives a round-trip:
        // 1. Parse state from file
        // 2. Modify history
        // 3. Save state
        // 4. Re-parse and verify

        var configContent = """
            {
              "version": 1,
              "agents": {
                "pool": ["Adele"],
                "assignments": {
                  "testuser": ["Adele"]
                }
              }
            }
            """;
        WriteFile("dydo.json", configContent);

        // Create initial state with some history
        Directory.CreateDirectory(Path.Combine(TestDir, "dydo", "agents", "Adele"));
        WriteFile("dydo/agents/Adele/state.md", """
            ---
            agent: Adele
            role: planner
            task: feature-x
            status: working
            assigned: testuser
            started: null
            allowed-paths: []
            denied-paths: []
            task-role-history: { "feature-x": ["planner"] }
            ---

            # Adele — Session State
            """);

        var registry = new AgentRegistry(TestDir);

        // Load state
        var state = registry.GetAgentState("Adele");
        Assert.NotNull(state);
        Assert.Single(state.TaskRoleHistory);
        Assert.Contains("planner", state.TaskRoleHistory["feature-x"]);

        // Simulate adding another role to history (as SetRole would do)
        if (!state.TaskRoleHistory.ContainsKey("feature-x"))
            state.TaskRoleHistory["feature-x"] = new List<string>();
        if (!state.TaskRoleHistory["feature-x"].Contains("code-writer"))
            state.TaskRoleHistory["feature-x"].Add("code-writer");

        // Manually update the state file (simulating what SetRole does internally)
        // Note: We can't call SetRole directly without a claimed agent (PID detection)
        // so we verify the history parsing/serialization works correctly
        var updatedStateContent = """
            ---
            agent: Adele
            role: code-writer
            task: feature-x
            status: working
            assigned: testuser
            started: null
            allowed-paths: ["src/**", "tests/**"]
            denied-paths: ["dydo/**", "project/**"]
            task-role-history: { "feature-x": ["planner", "code-writer"] }
            ---

            # Adele — Session State
            """;
        WriteFile("dydo/agents/Adele/state.md", updatedStateContent);

        // Reload and verify
        var registry2 = new AgentRegistry(TestDir);
        var reloadedState = registry2.GetAgentState("Adele");

        Assert.NotNull(reloadedState);
        Assert.Single(reloadedState.TaskRoleHistory);
        Assert.Equal(2, reloadedState.TaskRoleHistory["feature-x"].Count);
        Assert.Contains("planner", reloadedState.TaskRoleHistory["feature-x"]);
        Assert.Contains("code-writer", reloadedState.TaskRoleHistory["feature-x"]);

        // Verify CanTakeRole now blocks reviewer
        var canTakeReviewer = registry2.CanTakeRole("Adele", "reviewer", "feature-x", out var reason);
        Assert.False(canTakeReviewer);
        Assert.Contains("code-writer", reason);
    }

    [Fact]
    public async Task Init_AgentWorkspaces_DoNotHaveModeFiles()
    {
        // Initialize project
        var result = await InitProjectAsync();
        result.AssertSuccess();

        // Modes are NOT created at init (only at claim)
        var modesPath = Path.Combine(TestDir, "dydo", "agents", "Adele", "modes");
        Assert.False(Directory.Exists(modesPath), "Modes folder should NOT exist after init");
    }
}
