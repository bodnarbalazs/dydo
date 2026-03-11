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
                if (state.TaskRoleHistory.TryGetValue(task, out var previousRoles) &&
                    previousRoles.Contains(constraint.FromRole!))
                {
                    reason = SubstituteConstraintVars(constraint.Message, agentName, task, state.Role);
                    return false;
                }
                return true;

            case "requires-prior":
                if (!state.TaskRoleHistory.TryGetValue(task, out var taskRoles) ||
                    !constraint.RequiredRoles!.Any(r => taskRoles.Contains(r)))
                {
                    reason = SubstituteConstraintVars(constraint.Message, agentName, task, state.Role);
                    return false;
                }
                return true;

            case "panel-limit":
                int activeCount = 0;
                foreach (var name in _agentNames)
                {
                    var s = _getAgentState(name);
                    if (s != null && s.Role == role && s.Task == task &&
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

            default:
                reason = $"Unknown constraint type: '{constraint.Type}'.";
                return false;
        }
    }

    private static string SubstituteConstraintVars(string message, string agentName, string task, string? currentRole)
    {
        return message
            .Replace("{agent}", agentName)
            .Replace("{task}", task)
            .Replace("{current_role}", currentRole ?? "unknown role");
    }
}
