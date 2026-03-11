namespace DynaDocs.Services;

using DynaDocs.Models;

public static class AgentClaimValidator
{
    public static (bool CanClaim, string? Error) Validate(string agentName, string? humanName, DydoConfig? config)
    {
        if (string.IsNullOrEmpty(humanName))
        {
            return (false, $"DYDO_HUMAN environment variable not set.\nSet it to identify which human is operating this terminal:\n  export DYDO_HUMAN=your_name");
        }

        if (config == null)
            return (true, null);

        if (!config.Agents.Pool.Contains(agentName, StringComparer.OrdinalIgnoreCase))
            return (false, $"Agent '{agentName}' is not in the configured agent pool.");

        var assignedHuman = config.Agents.GetHumanForAgent(agentName);
        if (assignedHuman == null)
            return (true, null);

        if (!assignedHuman.Equals(humanName, StringComparison.OrdinalIgnoreCase))
        {
            var claimableAgents = config.Agents.GetAgentsForHuman(humanName);
            var agentList = claimableAgents.Count > 0
                ? string.Join(", ", claimableAgents)
                : "(none assigned)";

            return (false, $"Agent {agentName} is assigned to human '{assignedHuman}', not '{humanName}'.\nClaimable agents for human '{humanName}': {agentList}\nUse 'dydo agent claim auto' to claim the first available.");
        }

        return (true, null);
    }

    public static string? FindFirstFree(string humanName, DydoConfig config, Func<string, bool> isAgentFree)
    {
        var assignedAgents = config.Agents.GetAgentsForHuman(humanName);

        foreach (var agent in assignedAgents)
        {
            if (isAgentFree(agent))
                return agent;
        }

        return null;
    }
}
