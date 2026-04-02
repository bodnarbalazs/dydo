namespace DynaDocs.Services;

using DynaDocs.Models;

public static class ConfigFactory
{
    public static readonly List<string> DefaultQueues = ["merge"];

    public static readonly List<NudgeConfig> DefaultNudges =
    [
        new()
        {
            Pattern = @"(?:^|[;&|]\s*)npx\s+(?:(?:-\w+|--[\w-]+(?:\s+\S+)?)\s+)*dydo\b(.*)",
            Message = "Don't use npx to run dydo — it's already on your PATH. Just use: dydo $1",
            Severity = "block"
        },
        new()
        {
            Pattern = @"(?:^|[;&|]\s*)dotnet\s+(?:tool\s+run\s+)?dydo\b(.*)",
            Message = "Don't use dotnet to run dydo — it's already on your PATH. Just use: dydo $1",
            Severity = "block"
        },
        new()
        {
            Pattern = @"(?:^|[;&|]\s*)dotnet\s+run\b(?:\s+(?:-\w+|--[\w-]+(?:[=\s]\S+)?))*\s+--\s+((?:agent|guard|whoami|dispatch|inbox|message|msg|wait|task|review|clean|workspace|audit|template|init|check|fix|index|graph|completions|complete|version|help|roles|validate|issue|inquisition|watchdog)\b.*)",
            Message = "Don't use dotnet run to invoke dydo — it's already on your PATH. Just use: dydo $1",
            Severity = "block"
        },
        new()
        {
            Pattern = @"(?:^|[;&|]\s*)(bash|sh|zsh|cmd|powershell|pwsh)\s+(?:(?:-\w+|--[\w-]+(?:\s+\S+)?)\s+)*(?:[""'])?dydo(?=[\s""']|$)(.*?)(?:[""'])?$",
            Message = "Don't use '$1' to run dydo — it's already on your PATH. Just use: dydo $2",
            Severity = "block"
        },
        new()
        {
            Pattern = @"(?:^|[;&|]\s*)(python3?|py)\s+(?:(?:-\w+|--[\w-]+(?:\s+\S+)?)\s+)*(?:[""'])?dydo(?=[\s""']|$)(.*?)(?:[""'])?$",
            Message = "Don't use '$1' to run dydo — it's already on your PATH. Just use: dydo $2",
            Severity = "block"
        },
        new()
        {
            Pattern = @"git\b[^;|&]*\bworktree\s+(add|remove)\b",
            Message = "Use dydo worktree commands instead of git worktree directly.",
            Severity = "block"
        },
        new()
        {
            Pattern = @"rm\b[^;|&]*dydo/_system/\.local/worktrees/",
            Message = "Use dydo worktree cleanup instead of deleting worktree directories directly.",
            Severity = "block"
        },
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
            Nudges = DefaultNudges.Select(n => new NudgeConfig
            {
                Pattern = n.Pattern,
                Message = n.Message,
                Severity = n.Severity
            }).ToList(),
            Queues = DefaultQueues.ToList()
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

    public static int EnsureDefaultQueues(DydoConfig config)
    {
        var existing = new HashSet<string>(config.Queues, StringComparer.OrdinalIgnoreCase);
        var added = 0;

        foreach (var queue in DefaultQueues)
        {
            if (existing.Contains(queue))
                continue;

            config.Queues.Add(queue);
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
