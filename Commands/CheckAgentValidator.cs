namespace DynaDocs.Commands;

using DynaDocs.Models;
using DynaDocs.Services;

/// <summary>
/// Validates agent configurations and workspaces for CheckCommand.
/// </summary>
internal static class CheckAgentValidator
{
    public static List<string> Validate(DydoConfig config, IConfigService configService) =>
        Validate(config, configService, new AgentRegistry());

    internal static List<string> Validate(DydoConfig config, IConfigService configService, IAgentRegistry registry)
    {
        var warnings = new List<string>();
        var agentsPath = configService.GetAgentsPath();

        ValidatePoolAgents(config, registry, warnings);
        ValidateOrphanedWorkspaces(config, agentsPath, warnings);

        return warnings;
    }

    private static void ValidatePoolAgents(DydoConfig config, IAgentRegistry registry, List<string> warnings)
    {
        foreach (var agentName in config.Agents.Pool)
        {
            var configHuman = config.Agents.GetHumanForAgent(agentName);
            var state = registry.GetAgentState(agentName);
            var agentWorkspace = registry.GetAgentWorkspace(agentName);

            if (!Directory.Exists(agentWorkspace))
            {
                if (state?.Status != AgentStatus.Free)
                {
                    warnings.Add($"Agent '{agentName}' is {state?.Status.ToString().ToLowerInvariant()} but workspace missing.");
                }
                continue;
            }

            if (state?.AssignedHuman != null && configHuman != null)
            {
                if (!state.AssignedHuman.Equals(configHuman, StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add($"Agent '{agentName}' state.md says assigned to '{state.AssignedHuman}' but dydo.json assigns to '{configHuman}'.");
                }
            }

            var session = registry.GetSession(agentName);
            if (session != null)
            {
                var sessionAge = DateTime.UtcNow - session.Claimed;
                if (sessionAge.TotalHours > 24)
                {
                    warnings.Add($"Agent '{agentName}' has stale session (claimed {sessionAge.TotalHours:F0} hours ago).");
                }
            }
        }
    }

    private static void ValidateOrphanedWorkspaces(DydoConfig config, string agentsPath, List<string> warnings)
    {
        if (!Directory.Exists(agentsPath))
            return;

        foreach (var dir in Directory.GetDirectories(agentsPath))
        {
            var folderName = Path.GetFileName(dir);

            if (folderName.StartsWith('.'))
                continue;

            if (!config.Agents.Pool.Contains(folderName, StringComparer.OrdinalIgnoreCase))
            {
                warnings.Add($"Orphaned workspace '{folderName}' not in agent pool.");
            }
        }
    }
}
