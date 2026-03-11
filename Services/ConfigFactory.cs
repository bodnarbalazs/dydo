namespace DynaDocs.Services;

using DynaDocs.Models;

public static class ConfigFactory
{
    public static DydoConfig CreateDefault(string humanName, int agentCount = 26)
    {
        var agentNames = PresetAgentNames.GetNames(agentCount);

        return new DydoConfig
        {
            Version = 1,
            Structure = new StructureConfig
            {
                Root = ConfigService.DefaultRoot,
                Tasks = "project/tasks"
            },
            Agents = new AgentsConfig
            {
                Pool = agentNames,
                Assignments = new Dictionary<string, List<string>>
                {
                    [humanName] = agentNames
                }
            },
            Integrations = new Dictionary<string, bool>()
        };
    }

    public static void AddHuman(DydoConfig config, string humanName, int agentCount)
    {
        var assignedAgents = config.Agents.Assignments.Values
            .SelectMany(a => a)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var availableAgents = config.Agents.Pool
            .Where(a => !assignedAgents.Contains(a))
            .ToList();

        if (availableAgents.Count < agentCount)
        {
            var currentCount = config.Agents.Pool.Count;
            var newAgents = PresetAgentNames.GetNames(currentCount + agentCount)
                .Skip(currentCount)
                .ToList();

            config.Agents.Pool.AddRange(newAgents);
            availableAgents.AddRange(newAgents);
        }

        var toAssign = availableAgents.Take(agentCount).ToList();
        config.Agents.Assignments[humanName] = toAssign;
    }
}
