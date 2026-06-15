namespace DynaDocs.Services;

using DynaDocs.Models;

public class RoleConstraintEvaluator
{
    private readonly Dictionary<string, RoleDefinition> _roleDefinitions;
    private readonly IReadOnlyList<string> _agentNames;
    private readonly Func<string, AgentState?> _getAgentState;

    public RoleConstraintEvaluator(
        Dictionary<string, RoleDefinition> roleDefinitions,
        IReadOnlyList<string> agentNames,
        Func<string, AgentState?> getAgentState)
    {
        _roleDefinitions = roleDefinitions;
        _agentNames = agentNames;
        _getAgentState = getAgentState;
    }

    /// <summary>
    /// Checks if an agent can take a specific role on a task.
    /// Evaluates constraints from role definitions (data-driven).
    /// </summary>
    public bool CanTakeRole(string agentName, string role, string task, out string reason)
    {
        reason = string.Empty;

        var state = _getAgentState(agentName);
        if (state == null)
        {
            reason = $"Agent {agentName} not found.";
            return false;
        }

        if (_roleDefinitions.TryGetValue(role, out var roleDef))
        {
            foreach (var constraint in roleDef.Constraints)
            {
                if (!EvaluateConstraint(constraint, agentName, role, task, state, out reason))
                    return false;
            }
        }

        return true;
    }

    private bool EvaluateConstraint(RoleConstraint constraint, string agentName, string role,
        string task, AgentState state, out string reason)
    {
        reason = string.Empty;

        switch (constraint.Type)
        {
            case "role-transition":
                return EvaluateRoleTransitionConstraint(constraint, agentName, task, state, out reason);

            case "requires-prior":
                return EvaluateRequiresPriorConstraint(constraint, agentName, task, state, out reason);

            case "panel-limit":
                return EvaluatePanelLimitConstraint(constraint, agentName, role, task, state,
                    _agentNames, _getAgentState, out reason);

            case "requires-dispatch":
            case "dispatch-restriction":
                return true;

            default:
                reason = $"Unknown constraint type: '{constraint.Type}'.";
                return false;
        }
    }

    private static bool EvaluateRoleTransitionConstraint(RoleConstraint constraint, string agentName,
        string task, AgentState state, out string reason)
    {
        reason = string.Empty;
        if (state.TaskRoleHistory.TryGetValue(task, out var previousRoles) &&
            previousRoles.Contains(constraint.FromRole!, StringComparer.OrdinalIgnoreCase))
        {
            reason = SubstituteConstraintVars(constraint.Message, agentName, task, state.Role);
            return false;
        }
        return true;
    }

    private static bool EvaluateRequiresPriorConstraint(RoleConstraint constraint, string agentName,
        string task, AgentState state, out string reason)
    {
        reason = string.Empty;
        if (!state.TaskRoleHistory.TryGetValue(task, out var taskRoles) ||
            !constraint.RequiredRoles!.Any(r => taskRoles.Contains(r, StringComparer.OrdinalIgnoreCase)))
        {
            reason = SubstituteConstraintVars(constraint.Message, agentName, task, state.Role);
            return false;
        }
        return true;
    }

    private static bool EvaluatePanelLimitConstraint(RoleConstraint constraint, string agentName,
        string role, string task, AgentState state,
        IReadOnlyList<string> agentNames, Func<string, AgentState?> getAgentState, out string reason)
    {
        reason = string.Empty;
        int activeCount = 0;
        foreach (var name in agentNames)
        {
            if (string.Equals(name, agentName, StringComparison.OrdinalIgnoreCase))
                continue;
            var s = getAgentState(name);
            if (s != null &&
                string.Equals(s.Role, role, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(s.Task, task, StringComparison.OrdinalIgnoreCase) &&
                s.Status != AgentStatus.Free)
            {
                activeCount++;
            }
        }
        if (activeCount >= constraint.MaxCount!.Value)
        {
            reason = SubstituteConstraintVars(constraint.Message, agentName, task, state.Role);
            return false;
        }
        return true;
    }

    internal static string SubstituteConstraintVars(string message, string agentName, string task,
        string? currentRole, string? dispatcher = null)
    {
        return message
            .Replace("{agent}", agentName)
            .Replace("{task}", task)
            .Replace("{current_role}", currentRole ?? "unknown role")
            .Replace("{dispatcher}", dispatcher ?? "unknown");
    }
}
