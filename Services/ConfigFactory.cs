namespace DynaDocs.Services;

using DynaDocs.Models;

public static class ConfigFactory
{
    /// <summary>
    /// Dydo-internal scan-exclude entries — invariant. The check/fix loop
    /// guarantees these are present in every project's dydo.json (preserving
    /// any user-added entries alongside).
    /// </summary>
    public static readonly List<string> DydoInternalScanExclude =
    [
        "_system/.local/",
        "_system/audit/",
        // The shared scratch workspace (dydo/agents/workspace/): agent work products,
        // not documentation — never scanned, never validated, never mirrored.
        "agents/"
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
            Pattern = @"(?:^|[;&|]\s*)dotnet\s+run\b(?:\s+(?:-\w+|--[\w-]+(?:[=\s]\S+)?))*\s+--\s+((?:check|fix|index|init|graph|guard|sync|completions|complete|template|validate|version|help)\b.*)",
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
            Pattern = @"\buntil\s+\[",
            Message = "Open-ended Bash poll-loop detected. Prefer a bounded for i in {1..30}; do ...; sleep 1; done, or `gh run watch`. Open-ended polls have caused agent crashes (issue 0177).",
            Severity = "warn"
        },
        new()
        {
            Pattern = @"\btail\b(?=[^;|&\r\n]*(?:\s-\S*f\S*|\s--follow(?:=\S+)?)(?:\s|$))",
            Message = "Open-ended Bash poll-loop detected. Prefer a bounded for i in {1..30}; do ...; sleep 1; done, or `gh run watch`. Open-ended polls have caused agent crashes (issue 0177).",
            Severity = "warn"
        },
        new()
        {
            Pattern = @"\bwhile\s+(?:true|:)\s*;\s*do\b(?:(?!\bdone\b)[\s\S])*\bsleep\b",
            Message = "Open-ended Bash poll-loop detected. Prefer a bounded for i in {1..30}; do ...; sleep 1; done, or `gh run watch`. Open-ended polls have caused agent crashes (issue 0177).",
            Severity = "warn"
        },
        // DR 045 §3: nothing reaches the human that an independent agent has not reviewed, and
        // the PR body is where that proof lands. Warn = block once, run again to proceed, so an
        // honest exception costs one retry; DR 042's rule escalates it to block if discipline erodes.
        new()
        {
            Pattern = @"(?:^|[;&|]\s*)gh\s+pr\s+create\b(?![\s\S]*Independent review)",
            Message = "This PR carries no review block. Paste the independent reviewer's block under an 'Independent review' heading in the body — rubric, reviewer and model, candidate and base SHA, verdict, gates rerun, findings.",
            Severity = "warn"
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
                ["standard"] = "claude-opus-5",
                ["light"] = "claude-haiku-4-5"
            },
            ["openai"] = new()
            {
                ["strong"] = "gpt-5.6-sol",
                ["standard"] = "gpt-5.6-terra",
                ["light"] = "gpt-5.6-luna"
            }
        },
        Agents = new Dictionary<string, string>
        {
            ["implementer"] = "standard",
            ["hardener"] = "strong",
            ["docs-writer"] = "standard",
            ["reviewer"] = "strong",
            ["inquisitor"] = "strong",
            ["project-planner"] = "strong",
            ["specifier"] = "strong",
            ["issue-captain"] = "strong",
            ["research"] = "standard",
            ["scout"] = "standard"
        }
    };

    public static DydoConfig CreateDefault()
    {
        return new DydoConfig
        {
            Version = 1,
            Structure = new StructureConfig { Root = ConfigService.DefaultRoot },
            Integrations = new Dictionary<string, bool>(),
            Nudges = DefaultNudges.Select(n => new NudgeConfig
            {
                Pattern = n.Pattern,
                Message = n.Message,
                Severity = n.Severity,
                Audience = n.Audience
            }).ToList(),
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
        var added = 0;

        foreach (var nudge in DefaultNudges)
        {
            if (config.Nudges.Any(current => current.Pattern == nudge.Pattern))
                continue;

            config.Nudges.Add(new NudgeConfig
            {
                Pattern = nudge.Pattern,
                Message = nudge.Message,
                Severity = nudge.Severity,
                Audience = nudge.Audience
            });
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
}
