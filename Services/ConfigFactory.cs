namespace DynaDocs.Services;

using DynaDocs.Models;

public static class ConfigFactory
{
    /// <summary>
    /// Dydo-internal scan-exclude entries - invariant. The check/fix loop
    /// guarantees these are present in every project's dydo.json (preserving
    /// any user-added entries alongside).
    /// </summary>
    public static readonly List<string> DydoInternalScanExclude =
    [
        "_system/.local/",
        // The shared scratch workspace (dydo/agents/workspace/): agent work products,
        // not documentation - never scanned, never validated, never mirrored.
        "agents/"
    ];

    /// <summary>
    /// DYD-375: a soft notice when a write tool creates a .md outside the dydo tree. The root
    /// files and tool folders dydo itself scaffolds are excepted; it never blocks.
    /// </summary>
    public static readonly NudgeConfig DocsOutsideDydoNudge = new()
    {
        Pattern = @"^(?!(?:README|CLAUDE|AGENTS|CHANGELOG|THIRD-PARTY-NOTICES)\.md$)(?!(?:dydo|skills|\.claude|\.agents|\.codex|\.github)/).*\.md$",
        Message = "Docs usually live under `dydo/`. If this is a pointer README, or it belongs here, carry on.",
        Severity = "notice",
        Match = "new-file"
    };

    public static readonly List<NudgeConfig> DefaultNudges =
    [
        new()
        {
            Pattern = @"(?:^|[;&|]\s*)npx\s+(?:(?:-\w+|--[\w-]+(?:\s+\S+)?)\s+)*dydo\b(.*)",
            Message = "Don't use npx to run dydo - it's already on your PATH. Just use: dydo $1",
            Severity = "block"
        },
        new()
        {
            Pattern = @"(?:^|[;&|]\s*)dotnet\s+(?:tool\s+run\s+)?dydo\b(.*)",
            Message = "Don't use dotnet to run dydo - it's already on your PATH. Just use: dydo $1",
            Severity = "block"
        },
        new()
        {
            Pattern = @"(?:^|[;&|]\s*)dotnet\s+run\b(?:\s+(?:-\w+|--[\w-]+(?:[=\s]\S+)?))*\s+--\s+((?:check|fix|index|init|graph|guard|completions|complete|validate|map|version|help)\b.*)",
            Message = "Don't use dotnet run to invoke dydo - it's already on your PATH. Just use: dydo $1",
            Severity = "block"
        },
        new()
        {
            Pattern = @"(?:^|[;&|]\s*)(bash|sh|zsh|cmd|powershell|pwsh)\s+(?:(?:-\w+|--[\w-]+(?:\s+\S+)?)\s+)*(?:[""'])?dydo(?=[\s""']|$)(.*?)(?:[""'])?$",
            Message = "Don't use '$1' to run dydo - it's already on your PATH. Just use: dydo $2",
            Severity = "block"
        },
        new()
        {
            Pattern = @"(?:^|[;&|]\s*)(python3?|py)\s+(?:(?:-\w+|--[\w-]+(?:\s+\S+)?)\s+)*(?:[""'])?dydo(?=[\s""']|$)(.*?)(?:[""'])?$",
            Message = "Don't use '$1' to run dydo - it's already on your PATH. Just use: dydo $2",
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
        // DR 045 §3: nothing reaches the user that an independent agent has not reviewed, and
        // the PR body is where that proof lands. Warn = block once, run again to proceed, so an
        // honest exception costs one retry; DR 042's rule escalates it to block if discipline erodes.
        new()
        {
            Pattern = @"(?:^|[;&|]\s*)gh\s+pr\s+create\b(?![\s\S]*Independent review)",
            Message = "This PR carries no review block. Add the independent review under an 'Independent review' heading and follow this project's shared Communication and evidence standard.",
            Severity = "warn"
        },
        DocsOutsideDydoNudge,
    ];

    public static DydoConfig CreateDefault()
    {
        return new DydoConfig
        {
            Version = 1,
            Structure = new StructureConfig { Root = ConfigService.DefaultRoot },
            Integrations = new Dictionary<string, bool>(),
            Nudges = DefaultNudges.Select(Copy).ToList(),
            ScanExclude = DydoInternalScanExclude.ToList()
        };
    }

    /// <summary>
    /// Adds any default nudges missing from the config (matched by match form and pattern).
    /// Returns the number of nudges added.
    /// </summary>
    public static int EnsureDefaultNudges(DydoConfig config)
    {
        var added = 0;

        foreach (var nudge in DefaultNudges)
        {
            if (config.Nudges.Any(current => SameNudge(current, nudge)))
                continue;

            config.Nudges.Add(Copy(nudge));
            added++;
        }

        return added;
    }

    /// <summary>A nudge's identity: its match form (absent = command) and its pattern.</summary>
    public static bool SameNudge(NudgeConfig a, NudgeConfig b) =>
        a.Pattern == b.Pattern && a.IsCommandMatch == b.IsCommandMatch && a.IsNewFileMatch == b.IsNewFileMatch;

    public static NudgeConfig Copy(NudgeConfig nudge) => new()
    {
        Pattern = nudge.Pattern,
        Message = nudge.Message,
        Severity = nudge.Severity,
        Audience = nudge.Audience,
        Match = nudge.Match
    };

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

    public static List<string> FindMissingScanExcludeInvariants(DydoConfig config)
    {
        var existing = new HashSet<string>(config.ScanExclude, StringComparer.OrdinalIgnoreCase);
        return DydoInternalScanExclude.Where(e => !existing.Contains(e)).ToList();
    }
}
