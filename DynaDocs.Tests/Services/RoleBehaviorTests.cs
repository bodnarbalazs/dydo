namespace DynaDocs.Tests.Services;

using DynaDocs.Models;
using DynaDocs.Services;

/// <summary>
/// Regression contract for the role/permission/constraint system.
/// These tests must pass unchanged after the data-driven refactor (decision 008).
/// </summary>
public class RoleBehaviorTests : IDisposable
{
    private readonly string _testDir;

    public RoleBehaviorTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-role-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    private void SetupConfig(string[] agents, Dictionary<string, string[]> assignments,
        List<string>? sourcePaths = null, List<string>? testPaths = null)
    {
        var configPath = Path.Combine(_testDir, "dydo.json");
        var assignmentsJson = string.Join(",\n      ",
            assignments.Select(kv => $"\"{kv.Key}\": [{string.Join(", ", kv.Value.Select(a => $"\"{a}\""))}]"));
        var agentsJson = string.Join(", ", agents.Select(a => $"\"{a}\""));

        var pathsSection = "";
        if (sourcePaths != null || testPaths != null)
        {
            var src = sourcePaths != null
                ? $"\"source\": [{string.Join(", ", sourcePaths.Select(p => $"\"{p}\""))}]"
                : "";
            var tst = testPaths != null
                ? $"\"tests\": [{string.Join(", ", testPaths.Select(p => $"\"{p}\""))}]"
                : "";
            var parts = new[] { src, tst }.Where(s => s.Length > 0);
            pathsSection = $",\n  \"paths\": {{ {string.Join(", ", parts)} }}";
        }

        var config = $$"""
            {
              "version": 1,
              "agents": {
                "pool": [{{agentsJson}}],
                "assignments": {
                  {{assignmentsJson}}
                }
              }{{pathsSection}}
            }
            """;

        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        File.WriteAllText(configPath, config);
    }

    private void CreateSessionFile(string agentName, string sessionId, string? role = null,
        string? task = null, Dictionary<string, List<string>>? taskRoleHistory = null,
        List<string>? writablePaths = null, List<string>? readOnlyPaths = null)
    {
        var workspace = Path.Combine(_testDir, "dydo", "agents", agentName);
        Directory.CreateDirectory(workspace);

        var sessionJson = $"{{\"Agent\":\"{agentName}\",\"SessionId\":\"{sessionId}\",\"Claimed\":\"{DateTime.UtcNow:o}\"}}";
        File.WriteAllText(Path.Combine(workspace, ".session"), sessionJson);

        var historyJson = FormatHistory(taskRoleHistory);
        var roleLine = role != null ? $"\nrole: {role}" : "";
        var taskLine = task != null ? $"\ntask: {task}" : "";
        var writableLine = writablePaths != null
            ? $"\nwritable-paths: [{string.Join(", ", writablePaths.Select(p => $"\"{p}\""))}]"
            : "";
        var readOnlyLine = readOnlyPaths != null
            ? $"\nreadonly-paths: [{string.Join(", ", readOnlyPaths.Select(p => $"\"{p}\""))}]"
            : "";

        File.WriteAllText(Path.Combine(workspace, "state.md"),
            $"---\nagent: {agentName}\nstatus: working\nassigned: testuser{roleLine}{taskLine}{writableLine}{readOnlyLine}\ntask-role-history: {historyJson}\n---\n# {agentName} — Session State\n");
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

    #region Permission Mapping via RoleDefinitionService

    private static Dictionary<string, (List<string> Writable, List<string> ReadOnly)> BuildPerms(
        List<string> sourcePaths, List<string> testPaths)
    {
        var svc = new RoleDefinitionService();
        var pathSets = new Dictionary<string, List<string>>
        {
            ["source"] = sourcePaths,
            ["tests"] = testPaths
        };
        return svc.BuildPermissionMap(RoleDefinitionService.GetBaseRoleDefinitions(), pathSets);
    }

    [Fact]
    public void PermissionMap_ReturnsAllNineRoles()
    {
        var perms = BuildPerms(["src/**"], ["tests/**"]);

        Assert.Equal(9, perms.Count);
        Assert.Contains("code-writer", perms.Keys);
        Assert.Contains("reviewer", perms.Keys);
        Assert.Contains("co-thinker", perms.Keys);
        Assert.Contains("docs-writer", perms.Keys);
        Assert.Contains("planner", perms.Keys);
        Assert.Contains("test-writer", perms.Keys);
        Assert.Contains("orchestrator", perms.Keys);
        Assert.Contains("inquisitor", perms.Keys);
        Assert.Contains("judge", perms.Keys);
    }

    [Fact]
    public void PermissionMap_CodeWriter_IncludesSourceTestsAndSelfWorkspace()
    {
        var perms = BuildPerms(["Commands/**", "Services/**"], ["DynaDocs.Tests/**"]);
        var (writable, readOnly) = perms["code-writer"];

        Assert.Contains("Commands/**", writable);
        Assert.Contains("Services/**", writable);
        Assert.Contains("DynaDocs.Tests/**", writable);
        Assert.Contains("dydo/agents/{self}/**", writable);
        Assert.Contains("dydo/**", readOnly);
        Assert.Contains("project/**", readOnly);
    }

    [Fact]
    public void PermissionMap_Reviewer_OnlyWritesSelfWorkspace()
    {
        var perms = BuildPerms(["src/**"], ["tests/**"]);
        var (writable, readOnly) = perms["reviewer"];

        Assert.Single(writable);
        Assert.Equal("dydo/agents/{self}/**", writable[0]);
        Assert.Contains("**", readOnly);
    }

    [Fact]
    public void PermissionMap_CoThinker_WritesDecisionsAndSelf()
    {
        var perms = BuildPerms(["src/**"], ["tests/**"]);
        var (writable, readOnly) = perms["co-thinker"];

        Assert.Contains("dydo/agents/{self}/**", writable);
        Assert.Contains("dydo/project/decisions/**", writable);
        Assert.Equal(2, writable.Count);
        Assert.Contains("src/**", readOnly);
        Assert.Contains("tests/**", readOnly);
    }

    [Fact]
    public void PermissionMap_DocsWriter_WritesDydoSubdirs()
    {
        var perms = BuildPerms(["src/**"], ["tests/**"]);
        var (writable, readOnly) = perms["docs-writer"];

        Assert.Contains("dydo/understand/**", writable);
        Assert.Contains("dydo/guides/**", writable);
        Assert.Contains("dydo/reference/**", writable);
        Assert.Contains("dydo/project/**", writable);
        Assert.Contains("dydo/_system/**", writable);
        Assert.Contains("dydo/_assets/**", writable);
        Assert.Contains("dydo/*.md", writable);
        Assert.Contains("dydo/agents/{self}/**", writable);
        Assert.Contains("src/**", readOnly);
        Assert.Contains("tests/**", readOnly);
    }

    [Fact]
    public void PermissionMap_Planner_WritesTasksAndSelf()
    {
        var perms = BuildPerms(["src/**"], ["tests/**"]);
        var (writable, readOnly) = perms["planner"];

        Assert.Contains("dydo/agents/{self}/**", writable);
        Assert.Contains("dydo/project/tasks/**", writable);
        Assert.Equal(2, writable.Count);
        Assert.Contains("src/**", readOnly);
    }

    [Fact]
    public void PermissionMap_TestWriter_WritesTestsAndPitfalls()
    {
        var perms = BuildPerms(["src/**"], ["tests/**"]);
        var (writable, readOnly) = perms["test-writer"];

        Assert.Contains("dydo/agents/{self}/**", writable);
        Assert.Contains("tests/**", writable);
        Assert.Contains("dydo/project/pitfalls/**", writable);
        Assert.Contains("src/**", readOnly);
    }

    [Fact]
    public void PermissionMap_Orchestrator_WritesTasksDecisionsAndSelf()
    {
        var perms = BuildPerms(["src/**"], ["tests/**"]);
        var (writable, readOnly) = perms["orchestrator"];

        Assert.Contains("dydo/agents/{self}/**", writable);
        Assert.Contains("dydo/project/tasks/**", writable);
        Assert.Contains("dydo/project/decisions/**", writable);
        Assert.Equal(3, writable.Count);
        Assert.Contains("**", readOnly);
    }

    [Fact]
    public void PermissionMap_Inquisitor_WritesInquisitionsAndSelf()
    {
        var perms = BuildPerms(["src/**"], ["tests/**"]);
        var (writable, readOnly) = perms["inquisitor"];

        Assert.Contains("dydo/agents/{self}/**", writable);
        Assert.Contains("dydo/project/inquisitions/**", writable);
        Assert.Equal(2, writable.Count);
        Assert.Contains("src/**", readOnly);
        Assert.Contains("tests/**", readOnly);
    }

    [Fact]
    public void PermissionMap_Judge_OnlyWritesSelfWorkspace()
    {
        var perms = BuildPerms(["src/**"], ["tests/**"]);
        var (writable, readOnly) = perms["judge"];

        Assert.Equal(2, writable.Count);
        Assert.Contains("dydo/agents/{self}/**", writable);
        Assert.Contains("dydo/project/issues/**", writable);
        Assert.Contains("src/**", readOnly);
        Assert.Contains("tests/**", readOnly);
    }

    [Fact]
    public void PermissionMap_UsesConfiguredSourceAndTestPaths()
    {
        var perms = BuildPerms(
            ["Commands/**", "Services/**", "Models/**"],
            ["DynaDocs.Tests/**"]);

        var (writable, _) = perms["code-writer"];
        Assert.Contains("Commands/**", writable);
        Assert.Contains("Services/**", writable);
        Assert.Contains("Models/**", writable);
        Assert.Contains("DynaDocs.Tests/**", writable);

        var (testWritable, testReadOnly) = perms["test-writer"];
        Assert.Contains("DynaDocs.Tests/**", testWritable);
        Assert.Contains("Commands/**", testReadOnly);
        Assert.Contains("Services/**", testReadOnly);
        Assert.Contains("Models/**", testReadOnly);
    }

    #endregion

    #region IsPathAllowed — Per-Role Write Access

    [Fact]
    public void IsPathAllowed_NoIdentity_FailsWithSpecificMessage()
    {
        var registry = new AgentRegistry(_testDir);
        var result = registry.IsPathAllowed(null, "src/file.cs", "edit", out var error);

        Assert.False(result);
        Assert.Equal("No agent identity assigned to this session. Run 'dydo agent claim auto' first.", error);
    }

    [Fact]
    public void IsPathAllowed_IdentityButNoRole_FailsWithSpecificMessage()
    {
        SetupConfig(["Adele"], new() { ["testuser"] = ["Adele"] });
        CreateSessionFile("Adele", "test-session");

        var registry = new AgentRegistry(_testDir);
        var result = registry.IsPathAllowed("test-session", "src/file.cs", "edit", out var error);

        Assert.False(result);
        Assert.Equal("Agent Adele has no role set. Run 'dydo agent role <role>' first.", error);
    }

    [Theory]
    [InlineData("src/Foo.cs", true)]
    [InlineData("src/sub/Bar.cs", true)]
    [InlineData("tests/UnitTests.cs", true)]
    [InlineData("dydo/agents/Adele/notes.md", true)]
    [InlineData("dydo/project/tasks/foo.md", false)]
    [InlineData("dydo/agents/Brian/notes.md", false)]
    public void IsPathAllowed_CodeWriter_WritePaths(string path, bool expected)
    {
        SetupConfig(["Adele", "Brian"], new() { ["testuser"] = ["Adele", "Brian"] },
            sourcePaths: ["src/**"], testPaths: ["tests/**"]);
        CreateSessionFile("Adele", "test-cw", role: "code-writer", task: "t1",
            writablePaths: ["src/**", "tests/**", "dydo/agents/Adele/**"],
            readOnlyPaths: ["dydo/**", "project/**"]);

        var registry = new AgentRegistry(_testDir);
        var result = registry.IsPathAllowed("test-cw", path, "edit", out var error);

        Assert.Equal(expected, result);
        if (!expected) Assert.NotEmpty(error);
    }

    [Theory]
    [InlineData("dydo/agents/Adele/notes.md", true)]
    [InlineData("src/Foo.cs", false)]
    [InlineData("tests/Bar.cs", false)]
    [InlineData("dydo/agents/Brian/notes.md", false)]
    public void IsPathAllowed_Reviewer_OnlyOwnWorkspace(string path, bool expected)
    {
        SetupConfig(["Adele", "Brian"], new() { ["testuser"] = ["Adele", "Brian"] },
            sourcePaths: ["src/**"], testPaths: ["tests/**"]);
        CreateSessionFile("Adele", "test-rev", role: "reviewer", task: "t1",
            writablePaths: ["dydo/agents/Adele/**"],
            readOnlyPaths: ["**"]);

        var registry = new AgentRegistry(_testDir);
        var result = registry.IsPathAllowed("test-rev", path, "edit", out var error);

        Assert.Equal(expected, result);
        if (!expected)
            Assert.Contains("Reviewer role can only edit own workspace", error);
    }

    [Theory]
    [InlineData("dydo/agents/Adele/notes.md", true)]
    [InlineData("dydo/project/decisions/001.md", true)]
    [InlineData("src/Foo.cs", false)]
    [InlineData("tests/Bar.cs", false)]
    public void IsPathAllowed_CoThinker_DecisionsAndSelf(string path, bool expected)
    {
        SetupConfig(["Adele"], new() { ["testuser"] = ["Adele"] },
            sourcePaths: ["src/**"], testPaths: ["tests/**"]);
        CreateSessionFile("Adele", "test-ct", role: "co-thinker", task: "t1",
            writablePaths: ["dydo/agents/Adele/**", "dydo/project/decisions/**"],
            readOnlyPaths: ["src/**", "tests/**"]);

        var registry = new AgentRegistry(_testDir);
        var result = registry.IsPathAllowed("test-ct", path, "edit", out var error);

        Assert.Equal(expected, result);
        if (!expected)
            Assert.Contains("Co-thinker role can edit own workspace and decisions", error);
    }

    [Theory]
    [InlineData("dydo/understand/overview.md", true)]
    [InlineData("dydo/guides/setup.md", true)]
    [InlineData("dydo/reference/api.md", true)]
    [InlineData("dydo/project/tasks/t1.md", true)]
    [InlineData("dydo/_system/config.md", true)]
    [InlineData("dydo/agents/Adele/notes.md", true)]
    [InlineData("src/Foo.cs", false)]
    public void IsPathAllowed_DocsWriter_DydoSubdirsAndSelf(string path, bool expected)
    {
        SetupConfig(["Adele"], new() { ["testuser"] = ["Adele"] },
            sourcePaths: ["src/**"], testPaths: ["tests/**"]);
        CreateSessionFile("Adele", "test-dw", role: "docs-writer", task: "t1",
            writablePaths: ["dydo/understand/**", "dydo/guides/**", "dydo/reference/**", "dydo/project/**",
                "dydo/_system/**", "dydo/_assets/**", "dydo/*.md", "dydo/agents/Adele/**"],
            readOnlyPaths: ["src/**", "tests/**"]);

        var registry = new AgentRegistry(_testDir);
        var result = registry.IsPathAllowed("test-dw", path, "edit", out var error);

        Assert.Equal(expected, result);
        if (!expected)
            Assert.Contains("Docs-writer role can only edit dydo/**", error);
    }

    [Theory]
    [InlineData("dydo/agents/Adele/notes.md", true)]
    [InlineData("dydo/project/tasks/foo.md", true)]
    [InlineData("src/Foo.cs", false)]
    [InlineData("tests/Bar.cs", false)]
    public void IsPathAllowed_Planner_TasksAndSelf(string path, bool expected)
    {
        SetupConfig(["Adele"], new() { ["testuser"] = ["Adele"] },
            sourcePaths: ["src/**"], testPaths: ["tests/**"]);
        CreateSessionFile("Adele", "test-pl", role: "planner", task: "t1",
            writablePaths: ["dydo/agents/Adele/**", "dydo/project/tasks/**"],
            readOnlyPaths: ["src/**"]);

        var registry = new AgentRegistry(_testDir);
        var result = registry.IsPathAllowed("test-pl", path, "edit", out var error);

        Assert.Equal(expected, result);
        if (!expected)
            Assert.Contains("Planner role can only edit own workspace and tasks", error);
    }

    [Theory]
    [InlineData("tests/MyTest.cs", true)]
    [InlineData("dydo/project/pitfalls/gotcha.md", true)]
    [InlineData("dydo/agents/Adele/notes.md", true)]
    [InlineData("src/Foo.cs", false)]
    public void IsPathAllowed_TestWriter_TestsAndPitfalls(string path, bool expected)
    {
        SetupConfig(["Adele"], new() { ["testuser"] = ["Adele"] },
            sourcePaths: ["src/**"], testPaths: ["tests/**"]);
        CreateSessionFile("Adele", "test-tw", role: "test-writer", task: "t1",
            writablePaths: ["dydo/agents/Adele/**", "tests/**", "dydo/project/pitfalls/**"],
            readOnlyPaths: ["src/**"]);

        var registry = new AgentRegistry(_testDir);
        var result = registry.IsPathAllowed("test-tw", path, "edit", out var error);

        Assert.Equal(expected, result);
        if (!expected)
            Assert.Contains("Test-writer role can edit own workspace, tests, and pitfalls", error);
    }

    [Theory]
    [InlineData("dydo/agents/Adele/notes.md", true)]
    [InlineData("dydo/project/tasks/foo.md", true)]
    [InlineData("dydo/project/decisions/001.md", true)]
    [InlineData("src/Foo.cs", false)]
    public void IsPathAllowed_Orchestrator_TasksDecisionsAndSelf(string path, bool expected)
    {
        SetupConfig(["Adele"], new() { ["testuser"] = ["Adele"] },
            sourcePaths: ["src/**"], testPaths: ["tests/**"]);
        CreateSessionFile("Adele", "test-orch", role: "orchestrator", task: "t1",
            writablePaths: ["dydo/agents/Adele/**", "dydo/project/tasks/**", "dydo/project/decisions/**"],
            readOnlyPaths: ["**"]);

        var registry = new AgentRegistry(_testDir);
        var result = registry.IsPathAllowed("test-orch", path, "edit", out var error);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("dydo/agents/Adele/notes.md", true)]
    [InlineData("dydo/project/inquisitions/q1.md", true)]
    [InlineData("src/Foo.cs", false)]
    [InlineData("tests/Bar.cs", false)]
    public void IsPathAllowed_Inquisitor_InquisitionsAndSelf(string path, bool expected)
    {
        SetupConfig(["Adele"], new() { ["testuser"] = ["Adele"] },
            sourcePaths: ["src/**"], testPaths: ["tests/**"]);
        CreateSessionFile("Adele", "test-inq", role: "inquisitor", task: "t1",
            writablePaths: ["dydo/agents/Adele/**", "dydo/project/inquisitions/**"],
            readOnlyPaths: ["src/**", "tests/**"]);

        var registry = new AgentRegistry(_testDir);
        var result = registry.IsPathAllowed("test-inq", path, "edit", out var error);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("dydo/agents/Adele/notes.md", true)]
    [InlineData("src/Foo.cs", false)]
    [InlineData("tests/Bar.cs", false)]
    [InlineData("dydo/project/tasks/foo.md", false)]
    public void IsPathAllowed_Judge_OnlyOwnWorkspace(string path, bool expected)
    {
        SetupConfig(["Adele"], new() { ["testuser"] = ["Adele"] },
            sourcePaths: ["src/**"], testPaths: ["tests/**"]);
        CreateSessionFile("Adele", "test-judge", role: "judge", task: "t1",
            writablePaths: ["dydo/agents/Adele/**"],
            readOnlyPaths: ["src/**", "tests/**"]);

        var registry = new AgentRegistry(_testDir);
        var result = registry.IsPathAllowed("test-judge", path, "edit", out var error);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsPathAllowed_CanWriteOwnWorkspace_CannotWriteOtherAgentWorkspace()
    {
        SetupConfig(["Adele", "Brian"], new() { ["testuser"] = ["Adele", "Brian"] },
            sourcePaths: ["src/**"], testPaths: ["tests/**"]);
        CreateSessionFile("Adele", "test-own", role: "code-writer", task: "t1",
            writablePaths: ["src/**", "tests/**", "dydo/agents/Adele/**"],
            readOnlyPaths: ["dydo/**", "project/**"]);

        var registry = new AgentRegistry(_testDir);

        var ownResult = registry.IsPathAllowed("test-own", "dydo/agents/Adele/plan.md", "edit", out _);
        Assert.True(ownResult);

        var otherResult = registry.IsPathAllowed("test-own", "dydo/agents/Brian/plan.md", "edit", out var error);
        Assert.False(otherResult);
        Assert.NotEmpty(error);
    }

    #endregion

    #region CanTakeRole — Self-Review Prevention

    [Fact]
    public void CanTakeRole_ReviewerWithCodeWriterHistory_BlockedOnSameTask()
    {
        SetupConfig(["Adele"], new() { ["testuser"] = ["Adele"] });
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
    public void CanTakeRole_ReviewerWithCodeWriterHistory_AllowedOnDifferentTask()
    {
        SetupConfig(["Adele"], new() { ["testuser"] = ["Adele"] });
        CreateStateFile("Adele", taskRoleHistory: new() { ["task-x"] = ["code-writer"] });

        var registry = new AgentRegistry(_testDir);
        var canTake = registry.CanTakeRole("Adele", "reviewer", "task-y", out var reason);

        Assert.True(canTake);
        Assert.Empty(reason);
    }

    [Fact]
    public void CanTakeRole_ReviewerWithNoHistory_Allowed()
    {
        SetupConfig(["Adele"], new() { ["testuser"] = ["Adele"] });
        CreateStateFile("Adele", taskRoleHistory: new());

        var registry = new AgentRegistry(_testDir);
        var canTake = registry.CanTakeRole("Adele", "reviewer", "some-task", out var reason);

        Assert.True(canTake);
        Assert.Empty(reason);
    }

    [Fact]
    public void CanTakeRole_ReviewerWithPlannerAndCodeWriterHistory_Blocked()
    {
        SetupConfig(["Adele"], new() { ["testuser"] = ["Adele"] });
        CreateStateFile("Adele", taskRoleHistory: new() { ["my-task"] = ["planner", "code-writer"] });

        var registry = new AgentRegistry(_testDir);
        var canTake = registry.CanTakeRole("Adele", "reviewer", "my-task", out var reason);

        Assert.False(canTake);
        Assert.Contains("code-writer", reason);
        Assert.Contains("my-task", reason);
    }

    #endregion

    #region CanTakeRole — Orchestrator Graduation

    [Fact]
    public void CanTakeRole_OrchestratorWithNoHistory_Blocked()
    {
        SetupConfig(["Adele"], new() { ["testuser"] = ["Adele"] });
        CreateStateFile("Adele", taskRoleHistory: new());

        var registry = new AgentRegistry(_testDir);
        var canTake = registry.CanTakeRole("Adele", "orchestrator", "my-task", out var reason);

        Assert.False(canTake);
        Assert.Contains("Orchestrator requires prior co-thinker or planner experience", reason);
    }

    [Fact]
    public void CanTakeRole_OrchestratorWithCodeWriterOnly_Blocked()
    {
        SetupConfig(["Adele"], new() { ["testuser"] = ["Adele"] });
        CreateStateFile("Adele", role: "code-writer", taskRoleHistory: new() { ["my-task"] = ["code-writer"] });

        var registry = new AgentRegistry(_testDir);
        var canTake = registry.CanTakeRole("Adele", "orchestrator", "my-task", out var reason);

        Assert.False(canTake);
        Assert.Contains("code-writer", reason);
    }

    [Fact]
    public void CanTakeRole_OrchestratorWithCoThinkerHistory_Allowed()
    {
        SetupConfig(["Adele"], new() { ["testuser"] = ["Adele"] });
        CreateStateFile("Adele", taskRoleHistory: new() { ["my-task"] = ["co-thinker"] });

        var registry = new AgentRegistry(_testDir);
        var canTake = registry.CanTakeRole("Adele", "orchestrator", "my-task", out var reason);

        Assert.True(canTake);
        Assert.Empty(reason);
    }

    [Fact]
    public void CanTakeRole_OrchestratorWithPlannerHistory_Allowed()
    {
        SetupConfig(["Adele"], new() { ["testuser"] = ["Adele"] });
        CreateStateFile("Adele", taskRoleHistory: new() { ["my-task"] = ["planner"] });

        var registry = new AgentRegistry(_testDir);
        var canTake = registry.CanTakeRole("Adele", "orchestrator", "my-task", out var reason);

        Assert.True(canTake);
        Assert.Empty(reason);
    }

    [Fact]
    public void CanTakeRole_OrchestratorWithCoThinkerAndCodeWriter_Allowed()
    {
        SetupConfig(["Adele"], new() { ["testuser"] = ["Adele"] });
        CreateStateFile("Adele", taskRoleHistory: new() { ["my-task"] = ["co-thinker", "code-writer"] });

        var registry = new AgentRegistry(_testDir);
        var canTake = registry.CanTakeRole("Adele", "orchestrator", "my-task", out var reason);

        Assert.True(canTake);
        Assert.Empty(reason);
    }

    [Fact]
    public void CanTakeRole_OrchestratorErrorMessage_IncludesCurrentRole()
    {
        SetupConfig(["Adele"], new() { ["testuser"] = ["Adele"] });
        CreateStateFile("Adele", role: "code-writer", taskRoleHistory: new() { ["my-task"] = ["code-writer"] });

        var registry = new AgentRegistry(_testDir);
        registry.CanTakeRole("Adele", "orchestrator", "my-task", out var reason);

        Assert.Contains("code-writer", reason);
    }

    #endregion

    #region CanTakeRole — Judge Panel Limit

    [Fact]
    public void CanTakeRole_Judge_ZeroActiveJudges_Allowed()
    {
        SetupConfig(["Adele", "Brian"], new() { ["testuser"] = ["Adele", "Brian"] });
        CreateStateFile("Adele");

        var registry = new AgentRegistry(_testDir);
        var canTake = registry.CanTakeRole("Adele", "judge", "dispute-1", out var reason);

        Assert.True(canTake);
        Assert.Empty(reason);
    }

    [Fact]
    public void CanTakeRole_Judge_TwoActiveJudges_Allowed()
    {
        SetupConfig(["Adele", "Brian", "Claire"],
            new() { ["testuser"] = ["Adele", "Brian", "Claire"] });

        CreateStateFile("Adele", role: "judge", task: "dispute-1", status: AgentStatus.Working);
        CreateStateFile("Brian", role: "judge", task: "dispute-1", status: AgentStatus.Working);
        CreateStateFile("Claire");

        var registry = new AgentRegistry(_testDir);
        var canTake = registry.CanTakeRole("Claire", "judge", "dispute-1", out var reason);

        Assert.True(canTake);
        Assert.Empty(reason);
    }

    [Fact]
    public void CanTakeRole_Judge_ThreeActiveJudges_Blocked()
    {
        SetupConfig(["Adele", "Brian", "Claire", "David"],
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
    public void CanTakeRole_Judge_ThreeJudgesButOneFree_Allowed()
    {
        SetupConfig(["Adele", "Brian", "Claire", "David"],
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

    [Fact]
    public void CanTakeRole_Judge_ThreeOnTaskX_AllowedOnTaskY()
    {
        SetupConfig(["Adele", "Brian", "Claire", "David"],
            new() { ["testuser"] = ["Adele", "Brian", "Claire", "David"] });

        CreateStateFile("Adele", role: "judge", task: "task-x", status: AgentStatus.Working);
        CreateStateFile("Brian", role: "judge", task: "task-x", status: AgentStatus.Working);
        CreateStateFile("Claire", role: "judge", task: "task-x", status: AgentStatus.Working);
        CreateStateFile("David");

        var registry = new AgentRegistry(_testDir);
        var canTake = registry.CanTakeRole("David", "judge", "task-y", out var reason);

        Assert.True(canTake);
        Assert.Empty(reason);
    }

    #endregion

    #region GetRoleRestrictionMessage — Denial Messages

    [Theory]
    [InlineData("reviewer", "Reviewer role can only edit own workspace.")]
    [InlineData("code-writer", "Code-writer role can only edit configured source/test paths and own workspace.")]
    [InlineData("co-thinker", "Co-thinker role can edit own workspace and decisions.")]
    [InlineData("docs-writer", "Docs-writer role can only edit dydo/** (except other agents' workspaces) and own workspace.")]
    [InlineData("planner", "Planner role can only edit own workspace and tasks.")]
    [InlineData("test-writer", "Test-writer role can edit own workspace, tests, and pitfalls.")]
    public void IsPathAllowed_DenialMessage_ContainsRoleSpecificText(string role, string expectedMessage)
    {
        SetupConfig(["Adele"], new() { ["testuser"] = ["Adele"] },
            sourcePaths: ["src/**"], testPaths: ["tests/**"]);

        var perms = BuildPerms(["src/**"], ["tests/**"]);
        var (writable, readOnly) = perms[role];
        writable = writable.Select(p => p.Replace("{self}", "Adele")).ToList();
        readOnly = readOnly.Select(p => p.Replace("{self}", "Adele")).ToList();

        CreateSessionFile("Adele", $"test-msg-{role}", role: role, task: "t1",
            writablePaths: writable, readOnlyPaths: readOnly);

        var registry = new AgentRegistry(_testDir);

        // Use a path none of these roles can write to
        var result = registry.IsPathAllowed($"test-msg-{role}", "some/random/forbidden/file.txt", "edit", out var error);

        Assert.False(result);
        Assert.Contains(expectedMessage, error);
    }

    #endregion

    #region SetRole — Behavior

    [Fact]
    public void SetRole_InvalidRole_FailsWithValidRoleList()
    {
        SetupConfig(["Adele"], new() { ["testuser"] = ["Adele"] });
        CreateSessionFile("Adele", "test-invalid");

        var registry = new AgentRegistry(_testDir);
        var result = registry.SetRole("test-invalid", "hacker", null, out var error);

        Assert.False(result);
        Assert.Contains("Invalid role: hacker", error);
        Assert.Contains("code-writer", error);
        Assert.Contains("reviewer", error);
    }

    [Theory]
    [InlineData("code-writer")]
    [InlineData("reviewer")]
    [InlineData("co-thinker")]
    [InlineData("docs-writer")]
    [InlineData("planner")]
    [InlineData("test-writer")]
    [InlineData("inquisitor")]
    [InlineData("judge")]
    public void SetRole_ValidRoleName_Succeeds(string role)
    {
        SetupConfig(["Adele"], new() { ["testuser"] = ["Adele"] });
        CreateSessionFile("Adele", $"test-valid-{role}");

        var registry = new AgentRegistry(_testDir);
        var result = registry.SetRole($"test-valid-{role}", role, null, out var error);

        Assert.True(result, $"SetRole('{role}') failed: {error}");
    }

    [Fact]
    public void SetRole_Orchestrator_WithRequiredHistory_Succeeds()
    {
        SetupConfig(["Adele"], new() { ["testuser"] = ["Adele"] });
        CreateSessionFile("Adele", "test-orch-ok", taskRoleHistory: new() { ["my-task"] = ["planner"] });

        var registry = new AgentRegistry(_testDir);
        var result = registry.SetRole("test-orch-ok", "orchestrator", "my-task", out var error);

        Assert.True(result, $"SetRole('orchestrator') failed: {error}");
    }

    [Fact]
    public void SetRole_TaskRoleHistory_AccumulatesAcrossRoleChanges()
    {
        SetupConfig(["Adele"], new() { ["testuser"] = ["Adele"] });
        CreateSessionFile("Adele", "test-history");

        var registry = new AgentRegistry(_testDir);
        registry.SetRole("test-history", "planner", "my-task", out _);

        registry = new AgentRegistry(_testDir);
        registry.SetRole("test-history", "code-writer", "my-task", out _);

        registry = new AgentRegistry(_testDir);
        var state = registry.GetCurrentAgent("test-history");
        Assert.NotNull(state);
        Assert.True(state.TaskRoleHistory.ContainsKey("my-task"));
        Assert.Contains("planner", state.TaskRoleHistory["my-task"]);
        Assert.Contains("code-writer", state.TaskRoleHistory["my-task"]);
    }

    [Fact]
    public void SetRole_SubstitutesSelfInPaths()
    {
        SetupConfig(["Adele"], new() { ["testuser"] = ["Adele"] });
        CreateSessionFile("Adele", "test-self");

        var registry = new AgentRegistry(_testDir);
        registry.SetRole("test-self", "code-writer", null, out _);

        registry = new AgentRegistry(_testDir);
        var state = registry.GetCurrentAgent("test-self");
        Assert.NotNull(state);
        Assert.Contains("dydo/agents/Adele/**", state.WritablePaths);
        Assert.DoesNotContain("dydo/agents/{self}/**", state.WritablePaths);
    }

    [Fact]
    public void SetRole_WithTask_AutoCreatesTaskFile()
    {
        SetupConfig(["Adele"], new() { ["testuser"] = ["Adele"] });
        CreateSessionFile("Adele", "test-autocreate");

        var registry = new AgentRegistry(_testDir);
        registry.SetRole("test-autocreate", "code-writer", "new-feature", out _);

        var taskFile = Path.Combine(_testDir, "dydo", "project", "tasks", "new-feature.md");
        Assert.True(File.Exists(taskFile));
        var content = File.ReadAllText(taskFile);
        Assert.Contains("name: new-feature", content);
    }

    #endregion
}
