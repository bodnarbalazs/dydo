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
        // Validate integration
        if (!IsValidIntegration(integration))
        {
            ConsoleOutput.WriteError($"Unknown integration: {integration}. Valid options: claude, none");
            return ExitCodes.ToolError;
        }

        // Check if already initialized
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

        // Get human name
        var humanName = providedName ?? PromptForInput("Your name: ");
        if (string.IsNullOrWhiteSpace(humanName))
        {
            ConsoleOutput.WriteError("Name is required.");
            return ExitCodes.ToolError;
        }
        humanName = humanName.Trim().ToLowerInvariant();

        // Get agent count
        var agentCount = providedAgentCount ?? PromptForInt("Number of agents [26]: ", 26);
        if (agentCount < 1 || agentCount > 104)
        {
            ConsoleOutput.WriteError("Agent count must be between 1 and 104.");
            return ExitCodes.ToolError;
        }

        Console.WriteLine();
        Console.WriteLine("Creating...");

        try
        {
            var projectRoot = Environment.CurrentDirectory;
            var projectName = Path.GetFileName(projectRoot);

            // Create dydo.json
            var config = ConfigService.CreateDefault(humanName, agentCount);
            config.Integrations[integration] = true;

            var configPath = Path.Combine(projectRoot, ConfigService.ConfigFileName);
            configService.SaveConfig(config, configPath);
            Console.WriteLine($"  ✓ {ConfigService.ConfigFileName}");

            // Create CLAUDE.md at project root (entry point)
            var claudeMdPath = Path.Combine(projectRoot, "CLAUDE.md");
            if (!File.Exists(claudeMdPath))
            {
                var claudeMdContent = TemplateGenerator.GenerateClaudeMd(projectName);
                File.WriteAllText(claudeMdPath, claudeMdContent);
                Console.WriteLine("  ✓ CLAUDE.md (entry point)");
            }

            // Create dydo/ folder structure with agent workflow files
            var dydoRoot = Path.Combine(projectRoot, config.Structure.Root);
            Directory.CreateDirectory(dydoRoot);

            var scaffolder = new FolderScaffolder();
            scaffolder.Scaffold(dydoRoot, config.Agents.Pool);
            Console.WriteLine($"  ✓ {config.Structure.Root}/ structure with workflows");

            // Create agents folder (gitignored)
            var agentsPath = Path.Combine(dydoRoot, "agents");
            Directory.CreateDirectory(agentsPath);

            // Create files-off-limits.md (security config)
            var offLimitsPath = Path.Combine(dydoRoot, "files-off-limits.md");
            if (!File.Exists(offLimitsPath))
            {
                var offLimitsContent = TemplateGenerator.GenerateFilesOffLimitsMd();
                File.WriteAllText(offLimitsPath, offLimitsContent);
                Console.WriteLine("  ✓ files-off-limits.md (security config)");
            }

            // Update .gitignore
            UpdateGitignore(projectRoot, config.Structure.Root);
            Console.WriteLine($"  ✓ Added {config.Structure.Root}/agents/ to .gitignore");

            // Configure integration
            if (integration == "claude")
            {
                ConfigureClaudeHooks(projectRoot);
                Console.WriteLine("  ✓ Claude Code hooks configured");
            }

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

            return ExitCodes.Success;
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Error: {ex.Message}");
            return ExitCodes.ToolError;
        }
    }

    private static int ExecuteJoin(string integration, string? providedName, int? providedAgentCount)
    {
        // Validate integration
        if (!IsValidIntegration(integration))
        {
            ConsoleOutput.WriteError($"Unknown integration: {integration}. Valid options: claude, none");
            return ExitCodes.ToolError;
        }

        // Find existing config
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

        // Get human name
        var humanName = providedName ?? PromptForInput("Your name: ");
        if (string.IsNullOrWhiteSpace(humanName))
        {
            ConsoleOutput.WriteError("Name is required.");
            return ExitCodes.ToolError;
        }
        humanName = humanName.Trim().ToLowerInvariant();

        // Check if already a member
        if (config.Agents.Assignments.ContainsKey(humanName))
        {
            var existingAgents = config.Agents.GetAgentsForHuman(humanName);
            Console.WriteLine($"Human '{humanName}' is already a member with agents: {string.Join(", ", existingAgents)}");
            return ExitCodes.Success;
        }

        // Get agent count
        var defaultCount = Math.Min(5, 104 - config.Agents.Pool.Count);
        var agentCount = providedAgentCount ?? PromptForInt($"Number of agents [{defaultCount}]: ", defaultCount);
        if (agentCount < 1)
        {
            ConsoleOutput.WriteError("Agent count must be at least 1.");
            return ExitCodes.ToolError;
        }

        try
        {
            // Add human to config
            ConfigService.AddHuman(config, humanName, agentCount);

            // Enable integration if not already
            if (!config.Integrations.ContainsKey(integration) || !config.Integrations[integration])
            {
                config.Integrations[integration] = true;
            }

            // Save updated config
            configService.SaveConfig(config, configPath);

            var projectRoot = Path.GetDirectoryName(configPath)!;
            var assignedAgents = config.Agents.GetAgentsForHuman(humanName);

            Console.WriteLine($"  ✓ Assigned {assignedAgents.Count} agents to {humanName}");
            Console.WriteLine($"  ✓ Updated {ConfigService.ConfigFileName}");

            // Configure local workspace
            var agentsPath = configService.GetAgentsPath();
            foreach (var agent in assignedAgents)
            {
                var agentWorkspace = Path.Combine(agentsPath, agent);
                Directory.CreateDirectory(agentWorkspace);
            }
            Console.WriteLine("  ✓ Created local workspaces");

            // Configure integration
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

            return ExitCodes.Success;
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Error: {ex.Message}");
            return ExitCodes.ToolError;
        }
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

        // Load existing settings or create new
        JsonNode? settings = null;
        if (File.Exists(settingsPath))
        {
            try
            {
                var existingJson = File.ReadAllText(settingsPath);
                settings = JsonNode.Parse(existingJson);
            }
            catch
            {
                // Invalid JSON - start fresh
            }
        }
        settings ??= new JsonObject();

        // Get or create hooks object
        var hooks = settings["hooks"]?.AsObject() ?? new JsonObject();
        settings["hooks"] = hooks;

        // Get or create PreToolUse array
        var preToolUse = hooks["PreToolUse"]?.AsArray() ?? new JsonArray();
        hooks["PreToolUse"] = preToolUse;

        // Remove any existing dydo guard entry (to avoid duplicates on re-init)
        for (int i = preToolUse.Count - 1; i >= 0; i--)
        {
            var entry = preToolUse[i];
            var entryHooks = entry?["hooks"];
            if (entryHooks != null && entryHooks.ToJsonString().Contains("dydo guard"))
            {
                preToolUse.RemoveAt(i);
            }
        }

        // Add dydo guard entry
        var hookCommand = new JsonObject
        {
            ["type"] = "command",
            ["command"] = "dydo guard"
        };
        var hooksArray = new JsonArray();
        hooksArray.Add((JsonNode)hookCommand);

        var guardEntry = new JsonObject
        {
            ["matcher"] = "Edit|Write|Read|Bash",
            ["hooks"] = hooksArray
        };
        preToolUse.Add((JsonNode)guardEntry);

        var json = settings.ToJsonString(WriteOptions);
        File.WriteAllText(settingsPath, json);
    }

    private static void UpdateGitignore(string projectRoot, string dydoRoot)
    {
        var gitignorePath = Path.Combine(projectRoot, ".gitignore");
        var agentsEntry = $"{dydoRoot}/agents/";

        if (File.Exists(gitignorePath))
        {
            var content = File.ReadAllText(gitignorePath);
            if (!content.Contains(agentsEntry))
            {
                // Add entry
                if (!content.EndsWith('\n'))
                    content += '\n';

                content += $"\n# DynaDocs agent workspaces (local state)\n{agentsEntry}\n";
                File.WriteAllText(gitignorePath, content);
            }
        }
        else
        {
            // Create new .gitignore
            var content = $"# DynaDocs agent workspaces (local state)\n{agentsEntry}\n";
            File.WriteAllText(gitignorePath, content);
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
