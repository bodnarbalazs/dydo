namespace DynaDocs.Tests.Services;

using DynaDocs.Models;
using DynaDocs.Services;

public class RoleConstraintEvaluatorTests
{
    private static AgentState MakeState(string name, string? role = null, string? task = null,
        Dictionary<string, List<string>>? taskRoleHistory = null) =>
        new()
        {
            Name = name,
            Role = role,
            Task = task,
            Status = AgentStatus.Working,
            TaskRoleHistory = taskRoleHistory ?? new()
        };

    private static RoleDefinition MakeRole(string name, List<RoleConstraint>? constraints = null) =>
        new()
        {
            Name = name,
            Description = "Test",
            WritablePaths = ["src/**"],
            ReadOnlyPaths = [],
            TemplateFile = $"mode-{name}.template.md",
            Constraints = constraints ?? []
        };

    #region CanTakeRole — basic

    [Fact]
    public void CanTakeRole_AgentNotFound_ReturnsFalse()
    {
        var evaluator = new RoleConstraintEvaluator(
            new Dictionary<string, RoleDefinition>(),
            ["Alice"],
            _ => null);

        var result = evaluator.CanTakeRole("Alice", "code-writer", "task1", out var reason);

        Assert.False(result);
        Assert.Contains("not found", reason);
    }

    [Fact]
    public void CanTakeRole_NoConstraints_ReturnsTrue()
    {
        var roles = new Dictionary<string, RoleDefinition>
        {
            ["code-writer"] = MakeRole("code-writer")
        };
        var evaluator = new RoleConstraintEvaluator(roles, ["Alice"],
            name => MakeState(name));

        Assert.True(evaluator.CanTakeRole("Alice", "code-writer", "task1", out _));
    }

    [Fact]
    public void CanTakeRole_UnknownRole_ReturnsTrue()
    {
        var evaluator = new RoleConstraintEvaluator(
            new Dictionary<string, RoleDefinition>(),
            ["Alice"],
            name => MakeState(name));

        Assert.True(evaluator.CanTakeRole("Alice", "unknown-role", "task1", out _));
    }

    #endregion

    #region role-transition constraint

    [Fact]
    public void CanTakeRole_RoleTransition_BlocksIfPreviousRoleMatches()
    {
        var roles = new Dictionary<string, RoleDefinition>
        {
            ["reviewer"] = MakeRole("reviewer", [
                new RoleConstraint
                {
                    Type = "role-transition",
                    FromRole = "code-writer",
                    Message = "{agent} cannot review {task} — previously wrote code."
                }
            ])
        };
        var evaluator = new RoleConstraintEvaluator(roles, ["Alice"],
            name => MakeState(name, taskRoleHistory: new()
            {
                ["task1"] = ["code-writer"]
            }));

        var result = evaluator.CanTakeRole("Alice", "reviewer", "task1", out var reason);

        Assert.False(result);
        Assert.Contains("Alice", reason);
        Assert.Contains("task1", reason);
    }

    [Fact]
    public void CanTakeRole_RoleTransition_AllowsIfNoPreviousRole()
    {
        var roles = new Dictionary<string, RoleDefinition>
        {
            ["reviewer"] = MakeRole("reviewer", [
                new RoleConstraint
                {
                    Type = "role-transition",
                    FromRole = "code-writer",
                    Message = "Blocked."
                }
            ])
        };
        var evaluator = new RoleConstraintEvaluator(roles, ["Alice"],
            name => MakeState(name));

        Assert.True(evaluator.CanTakeRole("Alice", "reviewer", "task1", out _));
    }

    #endregion

    #region requires-prior constraint

    [Fact]
    public void CanTakeRole_RequiresPrior_BlocksIfNoRequiredRole()
    {
        var roles = new Dictionary<string, RoleDefinition>
        {
            ["test-writer"] = MakeRole("test-writer", [
                new RoleConstraint
                {
                    Type = "requires-prior",
                    RequiredRoles = ["planner"],
                    Message = "{agent} needs planner role first for {task}."
                }
            ])
        };
        var evaluator = new RoleConstraintEvaluator(roles, ["Alice"],
            name => MakeState(name));

        var result = evaluator.CanTakeRole("Alice", "test-writer", "task1", out var reason);

        Assert.False(result);
        Assert.Contains("planner", reason);
    }

    [Fact]
    public void CanTakeRole_RequiresPrior_AllowsIfRequiredRolePresent()
    {
        var roles = new Dictionary<string, RoleDefinition>
        {
            ["test-writer"] = MakeRole("test-writer", [
                new RoleConstraint
                {
                    Type = "requires-prior",
                    RequiredRoles = ["planner"],
                    Message = "Blocked."
                }
            ])
        };
        var evaluator = new RoleConstraintEvaluator(roles, ["Alice"],
            name => MakeState(name, taskRoleHistory: new()
            {
                ["task1"] = ["planner"]
            }));

        Assert.True(evaluator.CanTakeRole("Alice", "test-writer", "task1", out _));
    }

    #endregion

    #region panel-limit constraint

    [Fact]
    public void CanTakeRole_PanelLimit_BlocksIfAtMax()
    {
        var roles = new Dictionary<string, RoleDefinition>
        {
            ["reviewer"] = MakeRole("reviewer", [
                new RoleConstraint
                {
                    Type = "panel-limit",
                    MaxCount = 1,
                    Message = "Only one reviewer per task."
                }
            ])
        };
        // Bob is already reviewing task1
        var states = new Dictionary<string, AgentState>
        {
            ["Alice"] = MakeState("Alice"),
            ["Bob"] = new()
            {
                Name = "Bob",
                Role = "reviewer",
                Task = "task1",
                Status = AgentStatus.Working
            }
        };
        var evaluator = new RoleConstraintEvaluator(roles, ["Alice", "Bob"],
            name => states.GetValueOrDefault(name));

        var result = evaluator.CanTakeRole("Alice", "reviewer", "task1", out var reason);

        Assert.False(result);
        Assert.Contains("one reviewer", reason);
    }

    [Fact]
    public void CanTakeRole_PanelLimit_AllowsIfUnderMax()
    {
        var roles = new Dictionary<string, RoleDefinition>
        {
            ["reviewer"] = MakeRole("reviewer", [
                new RoleConstraint
                {
                    Type = "panel-limit",
                    MaxCount = 2,
                    Message = "Max 2 reviewers."
                }
            ])
        };
        var states = new Dictionary<string, AgentState>
        {
            ["Alice"] = MakeState("Alice"),
            ["Bob"] = new()
            {
                Name = "Bob",
                Role = "reviewer",
                Task = "task1",
                Status = AgentStatus.Working
            }
        };
        var evaluator = new RoleConstraintEvaluator(roles, ["Alice", "Bob"],
            name => states.GetValueOrDefault(name));

        Assert.True(evaluator.CanTakeRole("Alice", "reviewer", "task1", out _));
    }

    [Fact]
    public void CanTakeRole_PanelLimit_IgnoresFreeAgents()
    {
        var roles = new Dictionary<string, RoleDefinition>
        {
            ["reviewer"] = MakeRole("reviewer", [
                new RoleConstraint
                {
                    Type = "panel-limit",
                    MaxCount = 1,
                    Message = "Max 1."
                }
            ])
        };
        var states = new Dictionary<string, AgentState>
        {
            ["Alice"] = MakeState("Alice"),
            ["Bob"] = new()
            {
                Name = "Bob",
                Role = "reviewer",
                Task = "task1",
                Status = AgentStatus.Free
            }
        };
        var evaluator = new RoleConstraintEvaluator(roles, ["Alice", "Bob"],
            name => states.GetValueOrDefault(name));

        Assert.True(evaluator.CanTakeRole("Alice", "reviewer", "task1", out _));
    }

    #endregion

    #region Unknown constraint type

    [Fact]
    public void CanTakeRole_UnknownConstraintType_ReturnsFalse()
    {
        var roles = new Dictionary<string, RoleDefinition>
        {
            ["role1"] = MakeRole("role1", [
                new RoleConstraint { Type = "not-real", Message = "msg" }
            ])
        };
        var evaluator = new RoleConstraintEvaluator(roles, ["Alice"],
            name => MakeState(name));

        var result = evaluator.CanTakeRole("Alice", "role1", "task1", out var reason);

        Assert.False(result);
        Assert.Contains("Unknown constraint type", reason);
    }

    #endregion
}
