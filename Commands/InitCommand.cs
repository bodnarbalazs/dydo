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
            Description = "Integration to configure (claude, none)"
        };

        var joinOption = new Option<bool>("--join")
        {
            Description = "Join an existing DynaDocs project as a new team member"
        };

        var nameOption = new Option<string?>("--name")
        {
            Description = "Human name (skips prompt)"
        };

        var agentCountOption = new Option<int?>("--agents")
        {
            Description = "Number of agents to create/assign (skips prompt)"
        };

        var command = new Command("init", "Initialize DynaDocs with specified integration");
        command.Arguments.Add(integrationArgument);
        command.Options.Add(joinOption);
        command.Options.Add(nameOption);
        command.Options.Add(agentCountOption);

        command.SetAction(parseResult =>
        {
            if (PathUtils.IsInsideWorktree())
            {
                ConsoleOutput.WriteError("Cannot run init inside a worktree. Run from the main project directory.");
                return ExitCodes.ToolError;
            }

            var integration = parseResult.GetValue(integrationArgument)!;
            var join = parseResult.GetValue(joinOption);
            var name = parseResult.GetValue(nameOption);
            var agentCount = parseResult.GetValue(agentCountOption);

            return join
                ? ExecuteJoin(integration, name, agentCount)
                : ExecuteInit(integration, name, agentCount);
        });

        return command;
    }

    private static int ExecuteInit(string integration, string? providedName, int? providedAgentCount)
    {
        if (!IsValidIntegration(integration))
            return IntegrationError(integration);

        var configService = new ConfigService();
        var existingConfig = configService.FindConfigFile();
        if (existingConfig != null)
        {
            ConsoleOutput.WriteError($"DynaDocs already initialized. Config found at: {existingConfig}");
            Console.WriteLine("Use 'dydo init <integration> --join' to join as a new team member.");
            return ExitCodes.ToolError;
        }

        Console.WriteLine();
        Console.WriteLine("DynaDocs Setup");
        Console.WriteLine(new string('─', 14));
        Console.WriteLine();

        var humanName = ResolveHumanName(providedName);
        if (humanName == null) return ExitCodes.ToolError;

        var agentCount = ResolveAgentCount(providedAgentCount, 26);
        if (agentCount < 0) return ExitCodes.ToolError;

        Console.WriteLine();
        Console.WriteLine("Creating...");

        try
        {
            var projectRoot = Environment.CurrentDirectory;
            var config = ConfigFactory.CreateDefault(humanName, agentCount);
            config.Integrations[integration] = true;

            var configPath = Path.Combine(projectRoot, ConfigService.ConfigFileName);
            configService.SaveConfig(config, configPath);
            Console.WriteLine($"  ✓ {ConfigService.ConfigFileName}");

            ScaffoldProject(configService, config, configPath, projectRoot, integration);
            PrintInitSummary(config, humanName);

            return ExitCodes.Success;
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Error: {ex.Message}");
            return ExitCodes.ToolError;
        }
    }

    private static void ScaffoldProject(ConfigService configService, DydoConfig config,
        string configPath, string projectRoot, string integration)
    {
        var projectName = Path.GetFileName(projectRoot);

        WriteIfNotExists(
            Path.Combine(projectRoot, "CLAUDE.md"),
            () => TemplateGenerator.GenerateClaudeMd(projectName),
            "CLAUDE.md (entry point)");

        var dydoRoot = Path.Combine(projectRoot, config.Structure.Root);
        Directory.CreateDirectory(dydoRoot);

        var scaffolder = new FolderScaffolder();
        scaffolder.Scaffold(dydoRoot, config.Agents.Pool);
        FolderScaffolder.StoreInitialFrameworkHashes(dydoRoot, config);
        configService.SaveConfig(config, configPath);
        Console.WriteLine($"  ✓ {config.Structure.Root}/ structure with workflows");

        Directory.CreateDirectory(Path.Combine(dydoRoot, "agents"));

        WriteIfNotExists(
            Path.Combine(dydoRoot, "files-off-limits.md"),
            TemplateGenerator.GenerateFilesOffLimitsMd,
            "files-off-limits.md (security config)");

        UpdateGitignore(projectRoot, config.Structure.Root);
        Console.WriteLine($"  ✓ Updated .gitignore (agents/, local state)");

        if (integration == "claude")
        {
            ConfigureClaudeHooks(projectRoot);
            Console.WriteLine("  ✓ Claude Code hooks configured");
        }
    }

    private static void PrintInitSummary(DydoConfig config, string humanName)
    {
        Console.WriteLine();
        var agentNamesList = string.Join(", ", config.Agents.Pool.Take(5));
        if (config.Agents.Pool.Count > 5)
            agentNamesList += $" ... ({config.Agents.Pool.Count} total)";

        Console.WriteLine($"Agents assigned to {humanName}: {agentNamesList}");
        Console.WriteLine();
        Console.WriteLine("Documentation funnel created:");
        Console.WriteLine("  CLAUDE.md → dydo/index.md → dydo/workflows/*.md → must-reads");
        Console.WriteLine();
        Console.WriteLine("Next steps:");
        Console.WriteLine($"  1. Set environment variable: export DYDO_HUMAN={humanName}");
        Console.WriteLine("  2. Customize dydo/understand/architecture.md for your project");
        Console.WriteLine("  3. Customize dydo/guides/coding-standards.md");
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

    private static int ExecuteJoin(string integration, string? providedName, int? providedAgentCount)
    {
        if (!IsValidIntegration(integration))
            return IntegrationError(integration);

        var configService = new ConfigService();
        var configPath = configService.FindConfigFile();
        if (configPath == null)
        {
            ConsoleOutput.WriteError("No DynaDocs project found. Run 'dydo init <integration>' first to create one.");
            return ExitCodes.ToolError;
        }

        var config = configService.LoadConfig();
        if (config == null)
        {
            ConsoleOutput.WriteError($"Failed to load config from {configPath}");
            return ExitCodes.ToolError;
        }

        Console.WriteLine();
        Console.WriteLine("Joining DynaDocs project...");
        Console.WriteLine();

        var humanName = ResolveHumanName(providedName);
        if (humanName == null) return ExitCodes.ToolError;

        if (config.Agents.Assignments.ContainsKey(humanName))
        {
            var existingAgents = config.Agents.GetAgentsForHuman(humanName);
            Console.WriteLine($"Human '{humanName}' is already a member with agents: {string.Join(", ", existingAgents)}");
            return ExitCodes.Success;
        }

        var defaultCount = Math.Min(5, PresetAgentNames.MaxAgentCount - config.Agents.Pool.Count);
        var agentCount = ResolveAgentCount(providedAgentCount, defaultCount);
        if (agentCount < 0) return ExitCodes.ToolError;

        try
        {
            PerformJoin(configService, config, configPath, integration, humanName, agentCount);
            return ExitCodes.Success;
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Error: {ex.Message}");
            return ExitCodes.ToolError;
        }
    }

    private static void PerformJoin(ConfigService configService, DydoConfig config,
        string configPath, string integration, string humanName, int agentCount)
    {
        ConfigFactory.AddHuman(config, humanName, agentCount);

        if (!config.Integrations.ContainsKey(integration) || !config.Integrations[integration])
            config.Integrations[integration] = true;

        configService.SaveConfig(config, configPath);

        var projectRoot = Path.GetDirectoryName(configPath)!;
        var assignedAgents = config.Agents.GetAgentsForHuman(humanName);

        Console.WriteLine($"  ✓ Assigned {assignedAgents.Count} agents to {humanName}");
        Console.WriteLine($"  ✓ Updated {ConfigService.ConfigFileName}");

        var agentsPath = configService.GetAgentsPath();
        foreach (var agent in assignedAgents)
            Directory.CreateDirectory(Path.Combine(agentsPath, agent));
        Console.WriteLine("  ✓ Created local workspaces");

        if (integration == "claude")
        {
            ConfigureClaudeHooks(projectRoot);
            Console.WriteLine("  ✓ Claude Code hooks configured");
        }

        Console.WriteLine();
        Console.WriteLine($"Agents assigned to {humanName}: {string.Join(", ", assignedAgents)}");
        Console.WriteLine();
        Console.WriteLine("Next steps:");
        Console.WriteLine($"  1. Set environment variable: export DYDO_HUMAN={humanName}");
        Console.WriteLine("  2. Claim an agent: dydo agent claim auto");

        var completionResult = ShellCompletionInstaller.Install();
        if (completionResult != null)
            Console.WriteLine($"  {completionResult}");
    }

    private static string? ResolveHumanName(string? providedName)
    {
        var name = providedName ?? PromptForInput("Your name: ");
        if (string.IsNullOrWhiteSpace(name))
        {
            ConsoleOutput.WriteError("Name is required.");
            return null;
        }
        return name.Trim().ToLowerInvariant();
    }

    private static int ResolveAgentCount(int? providedCount, int defaultValue)
    {
        var agentCount = providedCount ?? PromptForInt($"Number of agents [{defaultValue}]: ", defaultValue);
        if (agentCount < 1 || agentCount > PresetAgentNames.MaxAgentCount)
        {
            ConsoleOutput.WriteError($"Agent count must be between 1 and {PresetAgentNames.MaxAgentCount}.");
            return -1;
        }
        return agentCount;
    }

    private static int IntegrationError(string integration)
    {
        ConsoleOutput.WriteError($"Unknown integration: {integration}. Valid options: claude, none");
        return ExitCodes.ToolError;
    }

    private static bool IsValidIntegration(string integration)
    {
        return integration.ToLowerInvariant() switch
        {
            "claude" => true,
            "none" => true,
            _ => false
        };
    }

    private static void ConfigureClaudeHooks(string projectRoot)
    {
        var claudeDir = Path.Combine(projectRoot, ".claude");
        Directory.CreateDirectory(claudeDir);

        var settingsPath = Path.Combine(claudeDir, "settings.local.json");
        var settings = LoadJsonSettings(settingsPath);

        var hooks = settings["hooks"]?.AsObject() ?? new JsonObject();
        settings["hooks"] = hooks;

        var preToolUse = hooks["PreToolUse"]?.AsArray() ?? new JsonArray();
        hooks["PreToolUse"] = preToolUse;

        RemoveExistingGuardEntries(preToolUse);

        var hookCommand = new JsonObject
        {
            ["type"] = "command",
            ["command"] = "dydo guard"
        };
        var hooksArray = new JsonArray();
        hooksArray.Add((JsonNode)hookCommand);

        var guardEntry = new JsonObject
        {
            ["matcher"] = "Edit|Write|Read|Bash|Glob|Grep|Agent|EnterPlanMode|ExitPlanMode",
            ["hooks"] = hooksArray
        };
        preToolUse.Add((JsonNode)guardEntry);

        var json = settings.ToJsonString(WriteOptions);
        File.WriteAllText(settingsPath, json);
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

    private static void RemoveExistingGuardEntries(JsonArray preToolUse)
    {
        for (int i = preToolUse.Count - 1; i >= 0; i--)
        {
            var entryHooks = preToolUse[i]?["hooks"];
            if (entryHooks != null && entryHooks.ToJsonString().Contains("dydo guard"))
                preToolUse.RemoveAt(i);
        }
    }

    private static void UpdateGitignore(string projectRoot, string dydoRoot)
    {
        var gitignorePath = Path.Combine(projectRoot, ".gitignore");
        var agentsEntry = $"{dydoRoot}/agents/";
        var localStateEntry = $"{dydoRoot}/_system/.local/";

        if (File.Exists(gitignorePath))
        {
            var content = File.ReadAllText(gitignorePath);
            var modified = false;

            if (!content.Contains(agentsEntry))
            {
                if (!content.EndsWith('\n'))
                    content += '\n';

                content += $"\n# DynaDocs agent workspaces (local state)\n{agentsEntry}\n";
                modified = true;
            }

            if (!content.Contains(localStateEntry))
            {
                if (!content.EndsWith('\n'))
                    content += '\n';

                content += $"\n# DynaDocs runtime state\n{localStateEntry}\n";
                modified = true;
            }

            if (modified)
                File.WriteAllText(gitignorePath, content);
        }
        else
        {
            var content = $"# DynaDocs agent workspaces (local state)\n{agentsEntry}\n"
                        + $"\n# DynaDocs runtime state\n{localStateEntry}\n";
            File.WriteAllText(gitignorePath, content);
        }
    }

    private static void WriteIfNotExists(string path, Func<string> contentFactory, string label)
    {
        if (!File.Exists(path))
        {
            File.WriteAllText(path, contentFactory());
            Console.WriteLine($"  ✓ {label}");
        }
    }

    private static string PromptForInput(string prompt)
    {
        Console.Write(prompt);
        return Console.ReadLine() ?? string.Empty;
    }

    private static int PromptForInt(string prompt, int defaultValue)
    {
        Console.Write(prompt);
        var input = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(input))
            return defaultValue;

        if (int.TryParse(input, out var value))
            return value;

        return defaultValue;
    }
}
