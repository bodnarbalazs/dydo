namespace DynaDocs.Commands;

using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Nodes;
using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

/// <summary>
/// Initialize DynaDocs in a project with explicit integration selection.
/// Supports interactive setup for new projects or joining existing ones.
/// </summary>
public static class InitCommand
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true
    };

    public static Command Create()
    {
        var integrationArgument = new Argument<string>("integration")
        {
            Arity = ArgumentArity.ExactlyOne,
            Description = "Integration to configure (claude, codex, all, none)"
        };

        var joinOption = new Option<bool>("--join")
        {
            Description = "Join an existing DynaDocs project as a new team member"
        };

        var command = new Command("init", "Initialize DynaDocs with specified integration");
        command.Arguments.Add(integrationArgument);
        command.Options.Add(joinOption);

        command.SetAction(parseResult =>
        {
            var integration = parseResult.GetValue(integrationArgument)!;
            var join = parseResult.GetValue(joinOption);

            return join
                ? ExecuteJoin(integration)
                : ExecuteInit(integration);
        });

        return command;
    }

    private static int ExecuteInit(string integration)
    {
        if (!IsValidIntegration(integration))
            return IntegrationError(integration);
        integration = integration.ToLowerInvariant();

        var configService = new ConfigService();
        var existingConfig = configService.FindConfigFile();
        if (existingConfig != null)
        {
            ConsoleOutput.WriteError($"DynaDocs already initialized. Config found at: {existingConfig}");
            Console.WriteLine("Use 'dydo init <integration> --join' to add an integration to this project or wire up this machine.");
            return ExitCodes.ToolError;
        }

        Console.WriteLine();
        Console.WriteLine("DynaDocs Setup");
        Console.WriteLine(new string('─', 14));
        Console.WriteLine();
        Console.WriteLine("Creating...");

        try
        {
            var projectRoot = Environment.CurrentDirectory;
            var config = ConfigFactory.CreateDefault();
            var integrations = ExpandIntegrations(integration);
            foreach (var name in integrations)
                config.Integrations[name] = true;

            var configPath = Path.Combine(projectRoot, ConfigService.ConfigFileName);
            configService.SaveConfig(config, configPath);
            Console.WriteLine($"  ✓ {ConfigService.ConfigFileName}");

            ScaffoldProject(configService, config, configPath, projectRoot, integrations);
            PrintInitSummary(integrations);

            return ExitCodes.Success;
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Error: {ex.Message}");
            return ExitCodes.ToolError;
        }
    }

    // "all" expands to every hook-wired integration; "claude"/"codex"/"none" stay themselves.
    private static string[] ExpandIntegrations(string integration) =>
        integration == "all" ? ["claude", "codex"] : [integration];

    private static void ScaffoldProject(ConfigService configService, DydoConfig config,
        string configPath, string projectRoot, string[] integrations)
    {
        var projectName = Path.GetFileName(projectRoot);

        WriteIfNotExists(
            Path.Combine(projectRoot, "CLAUDE.md"),
            () => TemplateGenerator.GenerateEntryPointMd(projectName),
            "CLAUDE.md (entry point)");

        if (integrations.Contains("codex"))
        {
            WriteIfNotExists(
                Path.Combine(projectRoot, "AGENTS.md"),
                () => GenerateAgentsMd(projectName),
                "AGENTS.md (Codex entry point)");
        }

        var dydoRoot = Path.Combine(projectRoot, config.Structure.Root);
        Directory.CreateDirectory(dydoRoot);

        var scaffolder = new FolderScaffolder();
        scaffolder.Scaffold(dydoRoot);
        FolderScaffolder.StoreInitialFrameworkHashes(dydoRoot, config);
        configService.SaveConfig(config, configPath);
        Console.WriteLine($"  ✓ {config.Structure.Root}/ structure with workflows");

        WriteIfNotExists(
            Path.Combine(dydoRoot, "files-off-limits.md"),
            TemplateGenerator.GenerateFilesOffLimitsMd,
            "files-off-limits.md (security config)");

        UpdateGitignore(projectRoot, config.Structure.Root, includeClaudeSettings: integrations.Contains("claude"));
        Console.WriteLine($"  ✓ Updated .gitignore (agents/, local state)");

        if (integrations.Contains("claude"))
        {
            ConfigureClaudeHooks(projectRoot);
            Console.WriteLine("  ✓ Claude Code hooks configured");
        }
        if (integrations.Contains("codex"))
        {
            ConfigureCodexHooks(projectRoot);
            Console.WriteLine("  - Codex hooks configured");
        }
    }

    private static void PrintInitSummary(string[] integrations)
    {
        Console.WriteLine();
        Console.WriteLine("Documentation funnel created:");
        if (integrations.Contains("codex"))
            Console.WriteLine("  AGENTS.md -> dydo/index.md (orientation) -> the docs");
        if (!integrations.Contains("codex") || integrations.Contains("claude"))
        {
            Console.WriteLine("  CLAUDE.md → dydo/index.md (orientation) → the docs");
        }
        Console.WriteLine();
        Console.WriteLine("Next steps:");
        Console.WriteLine("  1. Customize dydo/understand/architecture.md for your project");
        Console.WriteLine("  2. Customize dydo/guides/coding-standards.md");
        Console.WriteLine();
        Console.WriteLine("Source and test paths default to src/** and tests/**.");
        Console.WriteLine("If your project uses a different layout, update dydo.json:");
        Console.WriteLine();
        Console.WriteLine("  \"paths\": {");
        Console.WriteLine("    \"source\": [\"Commands/**\", \"Services/**\", ...],");
        Console.WriteLine("    \"tests\": [\"YourTests/**\"]");
        Console.WriteLine("  }");

        var completionResult = ShellCompletionInstaller.Install();
        if (completionResult != null)
            Console.WriteLine($"  {completionResult}");
    }

    // With the 26-agent roster gone (DR-041), "join" no longer assigns a pool of agents to a new
    // human — there is no roster to draw from. It reduces to "wire up this machine's local
    // integration for an already-initialized project" (a fresh clone, or adding a second
    // integration): configure hooks without re-scaffolding or overwriting the tree.
    private static int ExecuteJoin(string integration)
    {
        if (!IsValidIntegration(integration))
            return IntegrationError(integration);
        integration = integration.ToLowerInvariant();

        var configService = new ConfigService();
        var configPath = configService.FindConfigFile();
        if (configPath == null)
        {
            ConsoleOutput.WriteError("No DynaDocs project found. Run 'dydo init <integration>' first to create one.");
            return ExitCodes.ToolError;
        }

        Console.WriteLine();
        Console.WriteLine("Joining DynaDocs project...");
        Console.WriteLine();

        try
        {
            var projectRoot = Path.GetDirectoryName(configPath)!;
            var integrations = ExpandIntegrations(integration);
            var config = configService.LoadConfig(projectRoot);

            if (integrations.Contains("claude"))
            {
                WriteIfNotExists(
                    Path.Combine(projectRoot, "CLAUDE.md"),
                    () => TemplateGenerator.GenerateEntryPointMd(Path.GetFileName(projectRoot)),
                    "CLAUDE.md (entry point)");
                ConfigureClaudeHooks(projectRoot);
                UpdateGitignore(projectRoot, config?.Structure.Root ?? "dydo", includeClaudeSettings: true);
                Console.WriteLine("  ✓ Claude Code hooks configured");
            }

            if (integrations.Contains("codex"))
            {
                WriteIfNotExists(
                    Path.Combine(projectRoot, "AGENTS.md"),
                    () => GenerateAgentsMd(Path.GetFileName(projectRoot)),
                    "AGENTS.md (Codex entry point)");
                ConfigureCodexHooks(projectRoot);
                Console.WriteLine("  - Codex hooks configured");
            }

            // Joining wires this machine, but the integration set is project state: record it
            // so dydo.json reflects every integration the project actually uses (issue 0300).
            if (config != null && integrations.Any(name => !config.Integrations.GetValueOrDefault(name)))
            {
                foreach (var name in integrations)
                    config.Integrations[name] = true;
                configService.SaveConfig(config, configPath);
                Console.WriteLine($"  ✓ Recorded integration(s) in {ConfigService.ConfigFileName}: {string.Join(", ", integrations)}");
            }

            var completionResult = ShellCompletionInstaller.Install();
            if (completionResult != null)
            {
                Console.WriteLine();
                Console.WriteLine($"  {completionResult}");
            }

            return ExitCodes.Success;
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Error: {ex.Message}");
            return ExitCodes.ToolError;
        }
    }


    private static int IntegrationError(string integration)
    {
        ConsoleOutput.WriteError($"Unknown integration: {integration}. Valid options: claude, codex, all, none");
        return ExitCodes.ToolError;
    }

    private static bool IsValidIntegration(string integration)
    {
        return integration.ToLowerInvariant() switch
        {
            "claude" => true,
            "codex" => true,
            "all" => true,
            "none" => true,
            _ => false
        };
    }

    private static readonly string[] DydoAllowEntries =
    {
        "Bash(dydo:*)",
        "PowerShell(dydo:*)"
    };

    private static void ConfigureClaudeHooks(string projectRoot)
    {
        var claudeDir = Path.Combine(projectRoot, ".claude");
        Directory.CreateDirectory(claudeDir);

        var settingsPath = Path.Combine(claudeDir, "settings.local.json");
        var settings = LoadJsonSettings(settingsPath);

        ConfigureGuardHook(settings, GuardMatcher);
        ConfigureStopHook(settings);
        ConfigureAllowList(settings);

        var json = settings.ToJsonString(WriteOptions);
        File.WriteAllText(settingsPath, json);
    }

    // AskUserQuestion is kept in the matcher for legacy compatibility: it once let the guard set the
    // derived needs-human flag (Decision 030 §1), machinery removed with the claim ceremony (DR-041).
    // The guard now passes it through harmlessly; the entry stays so existing hook wiring is stable.
    private const string GuardMatcher =
        "Edit|Write|Read|Bash|Glob|Grep|Agent|EnterPlanMode|ExitPlanMode|PowerShell|NotebookEdit|AskUserQuestion";

    // Codex exposes file edits as apply_patch and shell execution under several tool names
    // depending on mode (shell_command interactive/exec, plus the code-mode aliases exec,
    // local_shell, unified_exec). All must be in the matcher or the guard never sees codex
    // shell commands — the PreToolUse hook fires but the tool name doesn't match, so no
    // off-limits / dangerous-bash / nudge layer binds on the shell lane (issue 0295).
    // These names must stay in lockstep with GuardCommand.ShellTools, which routes them to
    // the shell analyzer once they reach the guard.
    internal const string CodexShellTools = "shell_command|exec|local_shell|unified_exec";

    // Codex's own matcher, not Claude's: the documented Codex names come first (shell and
    // unified exec arrive as Bash, apply_patch also as Edit/Write, spawn_agent as Agent), and
    // the legacy shell names above are retained on purpose — they were added empirically
    // (issue 0295) and the documentation reading is unproven against the installed Codex.
    // Claude-only UI names (Read, Glob, Grep, PowerShell, plan mode, AskUserQuestion) are
    // dropped: Codex never emits them, so they could only ever be dead configuration.
    private const string CodexGuardMatcher = "Bash|apply_patch|Edit|Write|Agent|" + CodexShellTools;

    private static void ConfigureGuardHook(JsonNode settings, string matcher)
    {
        var hooks = settings["hooks"]?.AsObject() ?? new JsonObject();
        settings["hooks"] = hooks;

        var preToolUse = hooks["PreToolUse"]?.AsArray() ?? new JsonArray();
        hooks["PreToolUse"] = preToolUse;

        RemoveDydoGuardEntries(preToolUse, "dydo guard");

        var hookCommand = new JsonObject
        {
            ["type"] = "command",
            ["command"] = "dydo guard"
        };
        var hooksArray = new JsonArray();
        hooksArray.Add((JsonNode)hookCommand);

        var guardEntry = new JsonObject
        {
            ["matcher"] = matcher,
            ["hooks"] = hooksArray
        };
        preToolUse.Add((JsonNode)guardEntry);
    }

    // Stop-hook wiring, retained for legacy compatibility: `dydo guard --stop` is now a no-op (the
    // turn-end needs-human derivation of Decision 030 §1 was removed with the claim ceremony, DR-041),
    // but the hook stays wired so existing installs keep resolving. EXTENDS the shared hooks block: the
    // Stop array is created if absent, unknown Stop entries are preserved, and the dydo stop entry is
    // de-duplicated so re-running init stays idempotent.
    private static void ConfigureStopHook(JsonNode settings)
    {
        var hooks = settings["hooks"]?.AsObject() ?? new JsonObject();
        settings["hooks"] = hooks;

        var stop = hooks["Stop"]?.AsArray() ?? new JsonArray();
        hooks["Stop"] = stop;

        RemoveDydoGuardEntries(stop, "dydo guard --stop");

        var hookCommand = new JsonObject
        {
            ["type"] = "command",
            ["command"] = "dydo guard --stop"
        };
        var hooksArray = new JsonArray();
        hooksArray.Add((JsonNode)hookCommand);

        var stopEntry = new JsonObject
        {
            ["hooks"] = hooksArray
        };
        stop.Add((JsonNode)stopEntry);
    }

    private static void ConfigureAllowList(JsonNode settings)
    {
        var permissions = settings["permissions"]?.AsObject() ?? new JsonObject();
        settings["permissions"] = permissions;

        var allow = permissions["allow"]?.AsArray() ?? new JsonArray();
        permissions["allow"] = allow;

        foreach (var entry in DydoAllowEntries)
        {
            var hasEntry = allow.Any(existing => existing?.GetValue<string>() == entry);
            if (!hasEntry)
                allow.Add((JsonNode)entry);
        }
    }

    private static JsonNode LoadJsonSettings(string settingsPath)
    {
        if (File.Exists(settingsPath))
        {
            try
            {
                return JsonNode.Parse(File.ReadAllText(settingsPath)) ?? new JsonObject();
            }
            catch
            {
                // Invalid JSON — start fresh
            }
        }
        return new JsonObject();
    }

    internal static void ConfigureCodexHooks(string projectRoot)
    {
        var codexDir = Path.Combine(projectRoot, ".codex");
        Directory.CreateDirectory(codexDir);

        var hooksPath = Path.Combine(codexDir, "hooks.json");
        var settings = LoadJsonSettings(hooksPath);

        ConfigureGuardHook(settings, CodexGuardMatcher);
        ConfigureStopHook(settings);

        File.WriteAllText(hooksPath, settings.ToJsonString(WriteOptions));
    }

    internal static JsonNode BuildCodexHooks()
    {
        var settings = new JsonObject();
        ConfigureGuardHook(settings, CodexGuardMatcher);
        ConfigureStopHook(settings);
        return settings;
    }

    // Removes only exact dydo-managed hook commands, leaving custom hooks in mixed entries intact.
    private static void RemoveDydoGuardEntries(JsonArray entries, string managedCommand)
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i] is not JsonObject entry ||
                entry["hooks"] is not JsonArray entryHooks)
                continue;

            for (int hookIndex = entryHooks.Count - 1; hookIndex >= 0; hookIndex--)
            {
                if (entryHooks[hookIndex] is JsonObject hook &&
                    hook["command"] is JsonValue commandNode &&
                    commandNode.TryGetValue<string>(out var command) &&
                    command == managedCommand)
                    entryHooks.RemoveAt(hookIndex);
            }

            if (entryHooks.Count == 0)
                entries.RemoveAt(i);
        }
    }

    // The Claude guard wiring lives in .claude/settings.local.json — personal-scope by Claude
    // Code convention, so each machine runs `dydo init claude --join` and the file never gets
    // committed. Codex has no local-settings tier: its wiring (.codex/hooks.json) is committed
    // and works from a fresh clone (issue 0303 records this deliberate asymmetry).
    private const string ClaudeSettingsEntry = ".claude/settings.local.json";

    private static void UpdateGitignore(string projectRoot, string dydoRoot, bool includeClaudeSettings = false)
    {
        var gitignorePath = Path.Combine(projectRoot, ".gitignore");
        var sections = new List<(string Comment, string Entry)>
        {
            ("# DynaDocs agent workspaces (local state)", $"{dydoRoot}/agents/"),
            ("# DynaDocs runtime state", $"{dydoRoot}/_system/.local/"),
        };
        if (includeClaudeSettings)
            sections.Add(("# Claude Code personal settings (machine-local, wired by 'dydo init claude --join')", ClaudeSettingsEntry));

        var content = File.Exists(gitignorePath) ? File.ReadAllText(gitignorePath) : "";
        var modified = !File.Exists(gitignorePath);

        foreach (var (comment, entry) in sections)
        {
            if (content.Contains(entry))
                continue;

            if (content.Length > 0 && !content.EndsWith('\n'))
                content += '\n';
            if (content.Length > 0)
                content += '\n';

            content += $"{comment}\n{entry}\n";
            modified = true;
        }

        if (modified)
            File.WriteAllText(gitignorePath, content);
    }

    private static void WriteIfNotExists(string path, Func<string> contentFactory, string label)
    {
        if (!File.Exists(path))
        {
            File.WriteAllText(path, contentFactory());
            Console.WriteLine($"  ✓ {label}");
        }
    }

    // AGENTS.md gets the same entry-point content as CLAUDE.md (one authored template).
    private static string GenerateAgentsMd(string projectName) =>
        TemplateGenerator.GenerateEntryPointMd(projectName);

}
