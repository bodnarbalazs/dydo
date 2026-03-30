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

    #region requires-dispatch pass-through

    [Fact]
    public void CanTakeRole_RequiresDispatch_PassesThrough()
    {
        var roles = new Dictionary<string, RoleDefinition>
        {
            ["code-writer"] = MakeRole("code-writer", [
                new RoleConstraint
                {
                    Type = "requires-dispatch",
                    RequiredRoles = ["reviewer"],
                    OnlyWhenDispatched = true,
                    Message = "Must dispatch reviewer."
                }
            ])
        };
        var evaluator = new RoleConstraintEvaluator(roles, ["Alice"],
            name => MakeState(name));

        Assert.True(evaluator.CanTakeRole("Alice", "code-writer", "task1", out _));
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

    #region CanDispatch — dispatch-restriction

    [Fact]
    public void CanDispatch_BlocksWhenDispatchedByWrongRole()
    {
        var roles = new Dictionary<string, RoleDefinition>
        {
            ["reviewer"] = MakeRole("reviewer", [
                new RoleConstraint
                {
                    Type = "dispatch-restriction",
                    TargetRole = "code-writer",
                    RequiredRoles = ["code-writer"],
                    OnlyWhenDispatched = true,
                    Message = "Reviewers can only dispatch code-writer when dispatched by code-writer.\n  dydo msg --to {dispatcher} --subject {task} --body \"...\""
                }
            ])
        };
        var state = MakeState("Alice", role: "reviewer", task: "task1");
        state.DispatchedBy = "Bob";
        state.DispatchedByRole = "orchestrator";

        var evaluator = new RoleConstraintEvaluator(roles, ["Alice"],
            name => state);

        var result = evaluator.CanDispatch("Alice", "reviewer", "code-writer", "task1", out var reason);

        Assert.False(result);
        Assert.Contains("code-writer", reason);
    }

    [Fact]
    public void CanDispatch_AllowsWhenDispatchedByCorrectRole()
    {
        var roles = new Dictionary<string, RoleDefinition>
        {
            ["reviewer"] = MakeRole("reviewer", [
                new RoleConstraint
                {
                    Type = "dispatch-restriction",
                    TargetRole = "code-writer",
                    RequiredRoles = ["code-writer"],
                    OnlyWhenDispatched = true,
                    Message = "Blocked."
                }
            ])
        };
        var state = MakeState("Alice", role: "reviewer", task: "task1");
        state.DispatchedBy = "Bob";
        state.DispatchedByRole = "code-writer";

        var evaluator = new RoleConstraintEvaluator(roles, ["Alice"],
            name => state);

        Assert.True(evaluator.CanDispatch("Alice", "reviewer", "code-writer", "task1", out _));
    }

    [Fact]
    public void CanDispatch_SkipsWhenNotDispatched()
    {
        var roles = new Dictionary<string, RoleDefinition>
        {
            ["reviewer"] = MakeRole("reviewer", [
                new RoleConstraint
                {
                    Type = "dispatch-restriction",
                    TargetRole = "code-writer",
                    RequiredRoles = ["code-writer"],
                    OnlyWhenDispatched = true,
                    Message = "Blocked."
                }
            ])
        };
        // Human-started reviewer — no DispatchedBy
        var state = MakeState("Alice", role: "reviewer", task: "task1");

        var evaluator = new RoleConstraintEvaluator(roles, ["Alice"],
            name => state);

        Assert.True(evaluator.CanDispatch("Alice", "reviewer", "code-writer", "task1", out _));
    }

    [Fact]
    public void CanDispatch_AllowsUnconstrainedTargetRole()
    {
        var roles = new Dictionary<string, RoleDefinition>
        {
            ["reviewer"] = MakeRole("reviewer", [
                new RoleConstraint
                {
                    Type = "dispatch-restriction",
                    TargetRole = "code-writer",
                    RequiredRoles = ["code-writer"],
                    OnlyWhenDispatched = true,
                    Message = "Blocked."
                }
            ])
        };
        var state = MakeState("Alice", role: "reviewer", task: "task1");
        state.DispatchedBy = "Bob";
        state.DispatchedByRole = "orchestrator";

        var evaluator = new RoleConstraintEvaluator(roles, ["Alice"],
            name => state);

        // Dispatching docs-writer (not code-writer) — no matching constraint
        Assert.True(evaluator.CanDispatch("Alice", "reviewer", "docs-writer", "task1", out _));
    }

    [Fact]
    public void CanDispatch_UnknownSenderRole_ReturnsTrue()
    {
        var evaluator = new RoleConstraintEvaluator(
            new Dictionary<string, RoleDefinition>(),
            ["Alice"],
            name => MakeState(name));

        Assert.True(evaluator.CanDispatch("Alice", "unknown-role", "reviewer", "task1", out _));
    }

    [Fact]
    public void CanDispatch_NoConstraintsOnRole_ReturnsTrue()
    {
        var roles = new Dictionary<string, RoleDefinition>
        {
            ["code-writer"] = MakeRole("code-writer")
        };
        var state = MakeState("Alice", role: "code-writer", task: "task1");

        var evaluator = new RoleConstraintEvaluator(roles, ["Alice"],
            name => state);

        Assert.True(evaluator.CanDispatch("Alice", "code-writer", "reviewer", "task1", out _));
    }

    [Fact]
    public void CanDispatch_MessageSubstitution()
    {
        var roles = new Dictionary<string, RoleDefinition>
        {
            ["reviewer"] = MakeRole("reviewer", [
                new RoleConstraint
                {
                    Type = "dispatch-restriction",
                    TargetRole = "code-writer",
                    RequiredRoles = ["code-writer"],
                    OnlyWhenDispatched = true,
                    Message = "Agent {agent} on {task}: msg to {dispatcher}"
                }
            ])
        };
        var state = MakeState("Alice", role: "reviewer", task: "fix-bug");
        state.DispatchedBy = "Charlie";
        state.DispatchedByRole = "orchestrator";

        var evaluator = new RoleConstraintEvaluator(roles, ["Alice"],
            name => state);

        evaluator.CanDispatch("Alice", "reviewer", "code-writer", "fix-bug", out var reason);

        Assert.Contains("Alice", reason);
        Assert.Contains("fix-bug", reason);
        Assert.Contains("Charlie", reason);
    }

    #endregion

    #region CanRelease

    [Fact]
    public void CanRelease_NoConstraints_ReturnsTrue()
    {
        var roles = new Dictionary<string, RoleDefinition>
        {
            ["code-writer"] = MakeRole("code-writer")
        };
        var evaluator = new RoleConstraintEvaluator(roles, ["Alice"],
            name => MakeState(name));

        Assert.True(evaluator.CanRelease("Alice", "code-writer", "task1", true, null,
            (_, _) => false, out _));
    }

    [Fact]
    public void CanRelease_RequiresDispatch_BlocksWithoutMarker()
    {
        var roles = new Dictionary<string, RoleDefinition>
        {
            ["code-writer"] = MakeRole("code-writer", [
                new RoleConstraint
                {
                    Type = "requires-dispatch",
                    RequiredRoles = ["reviewer"],
                    OnlyWhenDispatched = true,
                    Message = "Must dispatch reviewer for {task}."
                }
            ])
        };
        var evaluator = new RoleConstraintEvaluator(roles, ["Alice"],
            name => MakeState(name));

        var result = evaluator.CanRelease("Alice", "code-writer", "task1", true, null,
            (_, _) => false, out var reason);

        Assert.False(result);
        Assert.Contains("reviewer", reason);
    }

    [Fact]
    public void CanRelease_RequiresDispatch_AllowsWithMarker()
    {
        var roles = new Dictionary<string, RoleDefinition>
        {
            ["code-writer"] = MakeRole("code-writer", [
                new RoleConstraint
                {
                    Type = "requires-dispatch",
                    RequiredRoles = ["reviewer"],
                    OnlyWhenDispatched = true,
                    Message = "Must dispatch reviewer."
                }
            ])
        };
        var evaluator = new RoleConstraintEvaluator(roles, ["Alice"],
            name => MakeState(name));

        Assert.True(evaluator.CanRelease("Alice", "code-writer", "task1", true, null,
            (task, role) => role == "reviewer", out _));
    }

    [Fact]
    public void CanRelease_OnlyWhenDispatched_SkipsDirectAgents()
    {
        var roles = new Dictionary<string, RoleDefinition>
        {
            ["code-writer"] = MakeRole("code-writer", [
                new RoleConstraint
                {
                    Type = "requires-dispatch",
                    RequiredRoles = ["reviewer"],
                    OnlyWhenDispatched = true,
                    Message = "Must dispatch reviewer."
                }
            ])
        };
        var evaluator = new RoleConstraintEvaluator(roles, ["Alice"],
            name => MakeState(name));

        // isDispatched = false → constraint should be skipped
        Assert.True(evaluator.CanRelease("Alice", "code-writer", "task1", false, null,
            (_, _) => false, out _));
    }

    [Fact]
    public void CanRelease_OnlyWhenDispatched_BlocksDispatchedAgents()
    {
        var roles = new Dictionary<string, RoleDefinition>
        {
            ["code-writer"] = MakeRole("code-writer", [
                new RoleConstraint
                {
                    Type = "requires-dispatch",
                    RequiredRoles = ["reviewer"],
                    OnlyWhenDispatched = true,
                    Message = "Must dispatch reviewer."
                }
            ])
        };
        var evaluator = new RoleConstraintEvaluator(roles, ["Alice"],
            name => MakeState(name));

        // isDispatched = true → constraint applies
        var result = evaluator.CanRelease("Alice", "code-writer", "task1", true, null,
            (_, _) => false, out _);

        Assert.False(result);
    }

    [Fact]
    public void CanRelease_MultipleRequiredRoles_AllMustBeMet()
    {
        var roles = new Dictionary<string, RoleDefinition>
        {
            ["multi-role"] = MakeRole("multi-role", [
                new RoleConstraint
                {
                    Type = "requires-dispatch",
                    RequiredRoles = ["reviewer", "judge"],
                    Message = "Must dispatch both."
                }
            ])
        };
        var evaluator = new RoleConstraintEvaluator(roles, ["Alice"],
            name => MakeState(name));

        // Only reviewer dispatched → should fail on judge
        var result = evaluator.CanRelease("Alice", "multi-role", "task1", true, null,
            (task, role) => role == "reviewer", out _);

        Assert.False(result);

        // Both dispatched → should pass
        Assert.True(evaluator.CanRelease("Alice", "multi-role", "task1", true, null,
            (_, _) => true, out _));
    }

    [Fact]
    public void CanRelease_RequireAllFalse_AllowsWhenAnyMarkerPresent()
    {
        var roles = new Dictionary<string, RoleDefinition>
        {
            ["inquisitor"] = MakeRole("inquisitor", [
                new RoleConstraint
                {
                    Type = "requires-dispatch",
                    RequiredRoles = ["judge", "inquisitor"],
                    RequireAll = false,
                    OnlyWhenDispatched = true,
                    Message = "Must dispatch judge or inquisitor."
                }
            ])
        };
        var evaluator = new RoleConstraintEvaluator(roles, ["Alice"],
            name => MakeState(name));

        // Only inquisitor dispatched → should pass (ANY semantics)
        Assert.True(evaluator.CanRelease("Alice", "inquisitor", "task1", true, null,
            (task, role) => role == "inquisitor", out _));

        // Only judge dispatched → should also pass
        Assert.True(evaluator.CanRelease("Alice", "inquisitor", "task1", true, null,
            (task, role) => role == "judge", out _));
    }

    [Fact]
    public void CanRelease_RequireAllFalse_BlocksWhenNoMarkerPresent()
    {
        var roles = new Dictionary<string, RoleDefinition>
        {
            ["inquisitor"] = MakeRole("inquisitor", [
                new RoleConstraint
                {
                    Type = "requires-dispatch",
                    RequiredRoles = ["judge", "inquisitor"],
                    RequireAll = false,
                    OnlyWhenDispatched = true,
                    Message = "Must dispatch judge or inquisitor."
                }
            ])
        };
        var evaluator = new RoleConstraintEvaluator(roles, ["Alice"],
            name => MakeState(name));

        var result = evaluator.CanRelease("Alice", "inquisitor", "task1", true, null,
            (_, _) => false, out var reason);

        Assert.False(result);
        Assert.Contains("judge or inquisitor", reason);
    }

    [Fact]
    public void CanRelease_RequireAllDefault_StillRequiresAll()
    {
        // Verifies that omitting RequireAll preserves ALL semantics
        var roles = new Dictionary<string, RoleDefinition>
        {
            ["multi-role"] = MakeRole("multi-role", [
                new RoleConstraint
                {
                    Type = "requires-dispatch",
                    RequiredRoles = ["reviewer", "judge"],
                    Message = "Must dispatch both."
                }
            ])
        };
        var evaluator = new RoleConstraintEvaluator(roles, ["Alice"],
            name => MakeState(name));

        // Only reviewer → should fail (ALL semantics by default)
        Assert.False(evaluator.CanRelease("Alice", "multi-role", "task1", true, null,
            (task, role) => role == "reviewer", out _));
    }

    [Fact]
    public void CanRelease_UnknownRole_ReturnsTrue()
    {
        var evaluator = new RoleConstraintEvaluator(
            new Dictionary<string, RoleDefinition>(),
            ["Alice"],
            name => MakeState(name));

        Assert.True(evaluator.CanRelease("Alice", "unknown", "task1", true, null,
            (_, _) => false, out _));
    }

    [Fact]
    public void CanRelease_DispatchedByRequiredRole_SkipsConstraint()
    {
        var roles = new Dictionary<string, RoleDefinition>
        {
            ["code-writer"] = MakeRole("code-writer", [
                new RoleConstraint
                {
                    Type = "requires-dispatch",
                    RequiredRoles = ["reviewer"],
                    OnlyWhenDispatched = true,
                    Message = "Must dispatch reviewer."
                }
            ])
        };
        var evaluator = new RoleConstraintEvaluator(roles, ["Alice"],
            name => MakeState(name));

        // Dispatched BY a reviewer — constraint should be satisfied automatically
        Assert.True(evaluator.CanRelease("Alice", "code-writer", "task1", true, "reviewer",
            (_, _) => false, out _));
    }

    [Fact]
    public void CanRelease_DispatchedByNonRequiredRole_StillRequiresMarker()
    {
        var roles = new Dictionary<string, RoleDefinition>
        {
            ["code-writer"] = MakeRole("code-writer", [
                new RoleConstraint
                {
                    Type = "requires-dispatch",
                    RequiredRoles = ["reviewer"],
                    OnlyWhenDispatched = true,
                    Message = "Must dispatch reviewer."
                }
            ])
        };
        var evaluator = new RoleConstraintEvaluator(roles, ["Alice"],
            name => MakeState(name));

        // Dispatched BY an orchestrator (not in requiredRoles) — still needs marker
        var result = evaluator.CanRelease("Alice", "code-writer", "task1", true, "orchestrator",
            (_, _) => false, out _);

        Assert.False(result);
    }

    #endregion
}
