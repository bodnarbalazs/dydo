namespace DynaDocs.Services;

using System.Text.RegularExpressions;
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
    /// <paramref name="dispatcherRole"/> is the role of the agent performing a dispatch (the
    /// caller), threaded through from <see cref="AgentSelector"/> so constraint evaluation and
    /// messages see the real caller — not the target's (often unset) role (#0237). Null on the
    /// self-conversion path (an agent setting its own role), where the caller is the agent itself.
    /// </summary>
    public bool CanTakeRole(string agentName, string role, string task, out string reason,
        string? dispatcherRole = null)
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
                if (!EvaluateConstraint(constraint, agentName, role, task, state, dispatcherRole, out reason))
                    return false;
            }
        }

        return true;
    }

    private bool EvaluateConstraint(RoleConstraint constraint, string agentName, string role,
        string task, AgentState state, string? dispatcherRole, out string reason)
    {
        reason = string.Empty;

        // The caller is the dispatcher when dispatching, or the agent itself when it self-converts.
        var callerRole = dispatcherRole ?? state.Role;

        switch (constraint.Type)
        {
            case "role-transition":
                return EvaluateRoleTransitionConstraint(constraint, agentName, task, state, callerRole, out reason);

            case "requires-prior":
                return EvaluateRequiresPriorConstraint(constraint, agentName, task, state, dispatcherRole, callerRole, out reason);

            case "panel-limit":
                return EvaluatePanelLimitConstraint(constraint, agentName, role, task, state, callerRole,
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
        string task, AgentState state, string? callerRole, out string reason)
    {
        reason = string.Empty;
        if (state.TaskRoleHistory.TryGetValue(task, out var previousRoles) &&
            previousRoles.Contains(constraint.FromRole!, StringComparer.OrdinalIgnoreCase))
        {
            reason = SubstituteConstraintVars(constraint.Message, agentName, task, callerRole);
            return false;
        }
        return true;
    }

    private static bool EvaluateRequiresPriorConstraint(RoleConstraint constraint, string agentName,
        string task, AgentState state, string? dispatcherRole, string? callerRole, out string reason)
    {
        reason = string.Empty;

        // The documented chief-of-staff routing path: a chief-of-staff caller performs the
        // top-level dispatch of a fresh orchestrator (or co-thinker) session, so it satisfies the
        // prior-experience gate directly instead of being forced through the dispatch-a-co-thinker-
        // then-self-convert workaround that the enforcement previously demanded (#0237).
        if (string.Equals(dispatcherRole, "chief-of-staff", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!state.TaskRoleHistory.TryGetValue(task, out var taskRoles) ||
            !constraint.RequiredRoles!.Any(r => taskRoles.Contains(r, StringComparer.OrdinalIgnoreCase)))
        {
            reason = SubstituteConstraintVars(constraint.Message, agentName, task, callerRole);
            return false;
        }
        return true;
    }

    private static bool EvaluatePanelLimitConstraint(RoleConstraint constraint, string agentName,
        string role, string task, AgentState state, string? callerRole,
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
            reason = SubstituteConstraintVars(constraint.Message, agentName, task, callerRole);
            return false;
        }
        return true;
    }

    internal static string SubstituteConstraintVars(string message, string agentName, string task,
        string? currentRole, string? dispatcher = null)
    {
        var role = string.IsNullOrEmpty(currentRole) ? "unknown role" : currentRole;
        // The role value is unknown at authoring time, so the indefinite article in front of the
        // "{current_role}" placeholder can't be baked into the message text — agree it here.
        var article = "aeiou".IndexOf(char.ToLowerInvariant(role[0])) >= 0 ? "an" : "a";
        // Anchor the article fix-up to a standalone "a" word so we don't rewrite the tail of a
        // word ending in 'a' (e.g. "extra {current_role}") — \b requires a word boundary
        // immediately before the article.
        var articled = Regex.Replace(message, @"\ba \{current_role\}", $"{article} {{current_role}}");
        return articled
            .Replace("{agent}", agentName)
            .Replace("{task}", task)
            .Replace("{current_role}", role)
            .Replace("{dispatcher}", dispatcher ?? "unknown");
    }
}
