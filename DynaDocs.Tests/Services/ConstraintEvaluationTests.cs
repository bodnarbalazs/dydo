namespace DynaDocs.Tests.Services;

using DynaDocs.Models;
using DynaDocs.Services;

/// <summary>
/// Tests for data-driven constraint evaluation in AgentRegistry.
/// Verifies that constraint-driven CanTakeRole produces identical results
/// to the original hardcoded logic.
/// </summary>
public class ConstraintEvaluationTests : IDisposable
{
    private readonly string _testDir;

    public ConstraintEvaluationTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-constraint-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    private void SetupWithRoleFiles(string[] agents, Dictionary<string, string[]> assignments)
    {
        // Create dydo.json
        var assignmentsJson = string.Join(",\n      ",
            assignments.Select(kv => $"\"{kv.Key}\": [{string.Join(", ", kv.Value.Select(a => $"\"{a}\""))}]"));
        var agentsJson = string.Join(", ", agents.Select(a => $"\"{a}\""));

        var config = $$"""
            {
              "version": 1,
              "agents": {
                "pool": [{{agentsJson}}],
                "assignments": {
                  {{assignmentsJson}}
                }
              }
            }
            """;

        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), config);

        // Write base role files so AgentRegistry uses data-driven path
        new RoleDefinitionService().WriteBaseRoleDefinitions(_testDir);
    }

    private void CreateStateFile(string agentName, AgentStatus status = AgentStatus.Working,
        string? role = null, string? task = null, Dictionary<string, List<string>>? taskRoleHistory = null)
    {
        var workspace = Path.Combine(_testDir, "dydo", "agents", agentName);
        Directory.CreateDirectory(workspace);

        var historyJson = FormatHistory(taskRoleHistory);
        var roleLine = role != null ? $"\nrole: {role}" : "";
        var taskLine = task != null ? $"\ntask: {task}" : "";
        var statusStr = status.ToString().ToLowerInvariant();

        File.WriteAllText(Path.Combine(workspace, "state.md"),
            $"---\nagent: {agentName}\nstatus: {statusStr}\nassigned: testuser{roleLine}{taskLine}\ntask-role-history: {historyJson}\n---\n# {agentName} — Session State\n");
    }

    private static string FormatHistory(Dictionary<string, List<string>>? taskRoleHistory)
    {
        if (taskRoleHistory == null || taskRoleHistory.Count == 0)
            return "{}";

        var entries = taskRoleHistory.Select(kv =>
            $"\"{kv.Key}\": [{string.Join(", ", kv.Value.Select(v => $"\"{v}\""))}]");
        return $"{{ {string.Join(", ", entries)} }}";
    }

    #region role-transition constraint

    [Fact]
    public void DataDriven_RoleTransition_BlocksReviewerAfterCodeWriter()
    {
        SetupWithRoleFiles(["Adele"], new() { ["testuser"] = ["Adele"] });
        CreateStateFile("Adele", taskRoleHistory: new() { ["my-task"] = ["code-writer"] });

        var registry = new AgentRegistry(_testDir);
        var canTake = registry.CanTakeRole("Adele", "reviewer", "my-task", out var reason);

        Assert.False(canTake);
        Assert.Contains("Adele", reason);
        Assert.Contains("code-writer", reason);
        Assert.Contains("my-task", reason);
        Assert.Contains("cannot be reviewer on the same task", reason);
    }

    [Fact]
    public void DataDriven_RoleTransition_AllowedOnDifferentTask()
    {
        SetupWithRoleFiles(["Adele"], new() { ["testuser"] = ["Adele"] });
        CreateStateFile("Adele", taskRoleHistory: new() { ["task-x"] = ["code-writer"] });

        var registry = new AgentRegistry(_testDir);
        var canTake = registry.CanTakeRole("Adele", "reviewer", "task-y", out var reason);

        Assert.True(canTake);
        Assert.Empty(reason);
    }

    [Fact]
    public void DataDriven_RoleTransition_AllowedWithNoHistory()
    {
        SetupWithRoleFiles(["Adele"], new() { ["testuser"] = ["Adele"] });
        CreateStateFile("Adele", taskRoleHistory: new());

        var registry = new AgentRegistry(_testDir);
        var canTake = registry.CanTakeRole("Adele", "reviewer", "some-task", out var reason);

        Assert.True(canTake);
        Assert.Empty(reason);
    }

    #endregion

    #region requires-prior constraint

    [Fact]
    public void DataDriven_RequiresPrior_BlocksOrchestratorWithNoHistory()
    {
        SetupWithRoleFiles(["Adele"], new() { ["testuser"] = ["Adele"] });
        CreateStateFile("Adele", taskRoleHistory: new());

        var registry = new AgentRegistry(_testDir);
        var canTake = registry.CanTakeRole("Adele", "orchestrator", "my-task", out var reason);

        Assert.False(canTake);
        Assert.Contains("Orchestrator requires prior co-thinker or planner experience", reason);
    }

    [Fact]
    public void DataDriven_RequiresPrior_AllowsOrchestratorAfterCoThinker()
    {
        SetupWithRoleFiles(["Adele"], new() { ["testuser"] = ["Adele"] });
        CreateStateFile("Adele", taskRoleHistory: new() { ["my-task"] = ["co-thinker"] });

        var registry = new AgentRegistry(_testDir);
        var canTake = registry.CanTakeRole("Adele", "orchestrator", "my-task", out var reason);

        Assert.True(canTake);
        Assert.Empty(reason);
    }

    [Fact]
    public void DataDriven_RequiresPrior_AllowsOrchestratorAfterPlanner()
    {
        SetupWithRoleFiles(["Adele"], new() { ["testuser"] = ["Adele"] });
        CreateStateFile("Adele", taskRoleHistory: new() { ["my-task"] = ["planner"] });

        var registry = new AgentRegistry(_testDir);
        var canTake = registry.CanTakeRole("Adele", "orchestrator", "my-task", out var reason);

        Assert.True(canTake);
        Assert.Empty(reason);
    }

    [Fact]
    public void DataDriven_RequiresPrior_MessageIncludesCurrentRole()
    {
        SetupWithRoleFiles(["Adele"], new() { ["testuser"] = ["Adele"] });
        CreateStateFile("Adele", role: "code-writer", taskRoleHistory: new() { ["my-task"] = ["code-writer"] });

        var registry = new AgentRegistry(_testDir);
        registry.CanTakeRole("Adele", "orchestrator", "my-task", out var reason);

        Assert.Contains("code-writer", reason);
    }

    #endregion

    #region panel-limit constraint

    [Fact]
    public void DataDriven_PanelLimit_AllowsWhenUnderLimit()
    {
        SetupWithRoleFiles(["Adele", "Brian"], new() { ["testuser"] = ["Adele", "Brian"] });
        CreateStateFile("Adele");

        var registry = new AgentRegistry(_testDir);
        var canTake = registry.CanTakeRole("Adele", "judge", "dispute-1", out var reason);

        Assert.True(canTake);
        Assert.Empty(reason);
    }

    [Fact]
    public void DataDriven_PanelLimit_BlocksWhenAtLimit()
    {
        SetupWithRoleFiles(["Adele", "Brian", "Claire", "David"],
            new() { ["testuser"] = ["Adele", "Brian", "Claire", "David"] });

        CreateStateFile("Adele", role: "judge", task: "dispute-1", status: AgentStatus.Working);
        CreateStateFile("Brian", role: "judge", task: "dispute-1", status: AgentStatus.Working);
        CreateStateFile("Claire", role: "judge", task: "dispute-1", status: AgentStatus.Working);
        CreateStateFile("David");

        var registry = new AgentRegistry(_testDir);
        var canTake = registry.CanTakeRole("David", "judge", "dispute-1", out var reason);

        Assert.False(canTake);
        Assert.Contains("Maximum 3 judges", reason);
        Assert.Contains("dispute-1", reason);
    }

    [Fact]
    public void DataDriven_PanelLimit_AllowsWhenFreeAgentDoesntCount()
    {
        SetupWithRoleFiles(["Adele", "Brian", "Claire", "David"],
            new() { ["testuser"] = ["Adele", "Brian", "Claire", "David"] });

        CreateStateFile("Adele", role: "judge", task: "dispute-1", status: AgentStatus.Working);
        CreateStateFile("Brian", role: "judge", task: "dispute-1", status: AgentStatus.Working);
        CreateStateFile("Claire", role: "judge", task: "dispute-1", status: AgentStatus.Free);
        CreateStateFile("David");

        var registry = new AgentRegistry(_testDir);
        var canTake = registry.CanTakeRole("David", "judge", "dispute-1", out var reason);

        Assert.True(canTake);
        Assert.Empty(reason);
    }

    #endregion

    #region Message variable substitution

    [Fact]
    public void DataDriven_MessageSubstitution_ReviewerMessage_ExactMatch()
    {
        SetupWithRoleFiles(["Adele"], new() { ["testuser"] = ["Adele"] });
        CreateStateFile("Adele", taskRoleHistory: new() { ["my-task"] = ["code-writer"] });

        var registry = new AgentRegistry(_testDir);
        registry.CanTakeRole("Adele", "reviewer", "my-task", out var reason);

        Assert.Equal(
            "Agent Adele was code-writer on task 'my-task' and cannot be reviewer on the same task. Dispatch to a different agent for review.",
            reason);
    }

    [Fact]
    public void DataDriven_MessageSubstitution_OrchestratorMessage_ExactMatch()
    {
        SetupWithRoleFiles(["Adele"], new() { ["testuser"] = ["Adele"] });
        CreateStateFile("Adele", role: "code-writer", taskRoleHistory: new() { ["my-task"] = ["code-writer"] });

        var registry = new AgentRegistry(_testDir);
        registry.CanTakeRole("Adele", "orchestrator", "my-task", out var reason);

        Assert.Equal(
            "You are a code-writer. Orchestrator requires prior co-thinker or planner experience on this task. Ask the user for clarification.",
            reason);
    }

    [Fact]
    public void DataDriven_MessageSubstitution_JudgeMessage_ExactMatch()
    {
        SetupWithRoleFiles(["Adele", "Brian", "Claire", "David"],
            new() { ["testuser"] = ["Adele", "Brian", "Claire", "David"] });

        CreateStateFile("Adele", role: "judge", task: "dispute-1", status: AgentStatus.Working);
        CreateStateFile("Brian", role: "judge", task: "dispute-1", status: AgentStatus.Working);
        CreateStateFile("Claire", role: "judge", task: "dispute-1", status: AgentStatus.Working);
        CreateStateFile("David");

        var registry = new AgentRegistry(_testDir);
        registry.CanTakeRole("David", "judge", "dispute-1", out var reason);

        Assert.Equal(
            "Maximum 3 judges already active on task 'dispute-1'. Escalate to the human.",
            reason);
    }

    #endregion

    #region No-fallback and DenialHint

    [Fact]
    public void CanTakeRole_NoRoleFiles_FallsBackToBaseDefinitions_BlocksSelfReview()
    {
        // SetupWithRoleFiles NOT called — no role files on disk
        var agentsJson = "\"Adele\"";
        var config = $$"""
            {
              "version": 1,
              "agents": {
                "pool": [{{agentsJson}}],
                "assignments": {
                  "testuser": [{{agentsJson}}]
                }
              }
            }
            """;
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), config);
        CreateStateFile("Adele", taskRoleHistory: new() { ["my-task"] = ["code-writer"] });

        AgentRegistry registry = null!;
        var stderrOutput = ConsoleCapture.Stderr(() => { registry = new AgentRegistry(_testDir); });

        // Warning should have been emitted
        Assert.Contains("No role files found", stderrOutput);

        // Constraint enforcement still works via base definitions
        var canTake = registry.CanTakeRole("Adele", "reviewer", "my-task", out var reason);
        Assert.False(canTake);
        Assert.Contains("code-writer", reason);
    }

    [Fact]
    public void GetRoleRestrictionMessage_ReadsDenialHintFromRoleDefinition()
    {
        SetupWithRoleFiles(["Adele"], new() { ["testuser"] = ["Adele"] });
        CreateStateFile("Adele");

        var workspace = Path.Combine(_testDir, "dydo", "agents", "Adele");
        var sessionJson = $"{{\"Agent\":\"Adele\",\"SessionId\":\"test-hint\",\"Claimed\":\"{DateTime.UtcNow:o}\"}}";
        File.WriteAllText(Path.Combine(workspace, ".session"), sessionJson);
        File.WriteAllText(Path.Combine(workspace, "state.md"),
            "---\nagent: Adele\nstatus: working\nassigned: testuser\nrole: reviewer\ntask: t1\nwritable-paths: [\"dydo/agents/Adele/**\"]\nreadonly-paths: [\"**\"]\ntask-role-history: {}\n---\n# Adele — Session State\n");

        var registry = new AgentRegistry(_testDir);
        var result = registry.IsPathAllowed("test-hint", "src/Foo.cs", "edit", out var error);

        Assert.False(result);
        Assert.Contains("Reviewer role can only edit own workspace", error);
    }

    [Fact]
    public void CanTakeRole_RoleWithNoConstraints_AllowedTrivially()
    {
        SetupWithRoleFiles(["Adele"], new() { ["testuser"] = ["Adele"] });
        CreateStateFile("Adele", taskRoleHistory: new() { ["my-task"] = ["code-writer"] });

        var registry = new AgentRegistry(_testDir);
        // code-writer has no constraints in the base definitions
        var canTake = registry.CanTakeRole("Adele", "code-writer", "my-task", out var reason);

        Assert.True(canTake);
        Assert.Empty(reason);
    }

    #endregion
}
