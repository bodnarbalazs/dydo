namespace DynaDocs.Services;

using DynaDocs.Models;

public static class ConfigFactory
{
    public static readonly List<string> DefaultQueues = ["merge"];

    /// <summary>
    /// Dydo-internal scan-exclude entries — invariant. The check/fix loop
    /// guarantees these are present in every project's dydo.json (preserving
    /// any user-added entries alongside).
    /// </summary>
    public static readonly List<string> DydoInternalScanExclude =
    [
        "_system/.local/",
        "_system/audit/"
    ];

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
            Pattern = @"\bgit\b[^;|&]*\bworktree\s+(add|remove)\b",
            Message = "Use dydo worktree commands instead of git worktree directly.",
            Severity = "block"
        },
        new()
        {
            Pattern = @"rm\b[^;|&]*dydo/_system/\.local/worktrees/",
            Message = "Use dydo worktree cleanup instead of deleting worktree directories directly.",
            Severity = "block"
        },
        new()
        {
            Pattern = @"\bdydo\s+worktree\s+merge\b[^;|&]*--force\b",
            Message = "dydo worktree merge --force bypasses the pre-merge safety check and WILL destroy uncommitted files. If the list shown was only generated artifacts (under 'N generated artifacts ignored'), --force is safe. If any source/test/task files were listed as suspicious, commit them first — re-run to proceed anyway.",
            Severity = "warn"
        },
        new()
        {
            Pattern = @"\buntil\s+\[",
            Message = "Open-ended Bash poll-loop detected. Prefer a bounded for i in {1..30}; do ...; sleep 1; done, or `gh run watch`, or `dydo wait` for dydo-native waits. Open-ended polls have caused agent crashes (issue 0177).",
            Severity = "warn"
        },
        // Decision 026 §4: Tier-1 agents are managers — soft reminder on direct source
        // writes. Notice severity = exit-0 stderr warning, never a block, so the
        // trivial-edit exception stays frictionless.
        new()
        {
            Tools = ["Edit", "Write", "NotebookEdit"],
            Pattern = "{source}|{tests}",
            Message = "Tier-1 agents are managers (Decision 026): delegate implementation to a run-sprint workflow unless this change is trivial. Rule of thumb: if it needs a reviewer, it needs a workflow.",
            Severity = "notice"
        },
    ];

    /// <summary>
    /// Shipped model-tier defaults (Decision 028): judgment work runs strong,
    /// defined production work runs standard. Returns a fresh instance so callers
    /// can't cross-mutate a shared default.
    /// </summary>
    public static ModelsConfig CreateDefaultModels() => new()
    {
        Tiers = new Dictionary<string, Dictionary<string, string>>
        {
            ["anthropic"] = new()
            {
                ["strong"] = "claude-fable-5",
                ["standard"] = "claude-opus-4-8",
                ["light"] = "claude-haiku-4-5"
            },
            ["openai"] = new()
            {
                ["strong"] = "gpt-5.5",
                ["standard"] = "gpt-5.5",
                ["light"] = "gpt-5.5"
            }
        },
        Roles = new Dictionary<string, string>
        {
            ["code-writer"] = "standard",
            ["test-writer"] = "standard",
            ["docs-writer"] = "standard",
            ["reviewer"] = "strong",
            ["sprint-auditor"] = "strong",
            ["inquisitor"] = "strong",
            ["judge"] = "strong",
            ["planner"] = "strong"
        }
    };

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
                Severity = n.Severity,
                Tools = n.Tools?.ToList()
            }).ToList(),
            Queues = DefaultQueues.ToList(),
            ScanExclude = DydoInternalScanExclude.ToList(),
            Models = CreateDefaultModels()
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
                Severity = nudge.Severity,
                Tools = nudge.Tools?.ToList()
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

    /// <summary>
    /// Adds any dydo-internal scan-exclude entries missing from the config.
    /// Idempotent; user-added entries are preserved. Returns the number added.
    /// </summary>
    public static int EnsureDefaultScanExclude(DydoConfig config)
    {
        var existing = new HashSet<string>(config.ScanExclude, StringComparer.OrdinalIgnoreCase);
        var added = 0;

        foreach (var entry in DydoInternalScanExclude)
        {
            if (existing.Contains(entry))
                continue;

            config.ScanExclude.Add(entry);
            added++;
        }

        return added;
    }

    /// <summary>
    /// Returns the dydo-internal scan-exclude entries that are missing from
    /// the config. An empty list means the invariants hold.
    /// </summary>
    public static List<string> FindMissingScanExcludeInvariants(DydoConfig config)
    {
        var existing = new HashSet<string>(config.ScanExclude, StringComparer.OrdinalIgnoreCase);
        return DydoInternalScanExclude.Where(e => !existing.Contains(e)).ToList();
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
