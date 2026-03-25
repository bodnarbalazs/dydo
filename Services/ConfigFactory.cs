namespace DynaDocs.Services;

using DynaDocs.Models;

public static class ConfigFactory
{
    public static readonly List<NudgeConfig> DefaultNudges =
    [
        new()
        {
            Pattern = @"dotnet\s+test\b.*(?:coverlet|cobertura|--collect\b)",
            Message = "Consider using your project coverage tool instead of running coverage directly.",
            Severity = "warn"
        }
    ];

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
            Integrations = new Dictionary<string, bool>(),
            Nudges = new List<NudgeConfig>(DefaultNudges)
        };
    }

    /// <summary>
    /// Adds any default nudges missing from the config (matched by pattern).
    /// Returns the number of nudges added.
    /// </summary>
    public static int EnsureDefaultNudges(DydoConfig config)
    {
        var existingPatterns = new HashSet<string>(config.Nudges.Select(n => n.Pattern));
        var added = 0;

        foreach (var nudge in DefaultNudges)
        {
            if (existingPatterns.Contains(nudge.Pattern))
                continue;

            config.Nudges.Add(new NudgeConfig
            {
                Pattern = nudge.Pattern,
                Message = nudge.Message,
                Severity = nudge.Severity
            });
            added++;
        }

        return added;
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
