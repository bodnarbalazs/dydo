namespace DynaDocs.Commands;

using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using DynaDocs.Models;
using DynaDocs.Rules;
using DynaDocs.Services;
using DynaDocs.Utils;

public static class CheckCommand
{
    public static Command Create()
    {
        var pathArgument = new Argument<string?>("path", () => null, "Path to docs folder or file to check");

        var command = new Command("check", "Validate documentation and report violations")
        {
            pathArgument
        };

        command.SetHandler((InvocationContext ctx) =>
        {
            var path = ctx.ParseResult.GetValueForArgument(pathArgument);
            ctx.ExitCode = Execute(path);
        });

        return command;
    }

    private static int Execute(string? path)
    {
        try
        {
            var hasErrors = false;
            var hasWarnings = false;
            var configService = new ConfigService();
            var config = configService.LoadConfig();

            // Documentation validation
            var basePath = ResolvePath(path);
            if (basePath != null)
            {
                Console.WriteLine($"Checking {basePath}...");
                Console.WriteLine();

                var parser = new MarkdownParser();
                var scanner = new DocScanner(parser);
                var linkResolver = new LinkResolver();

                var docs = scanner.ScanDirectory(basePath);
                var folders = scanner.GetAllFolders(basePath);

                var rules = CreateRules(linkResolver);
                var result = new ValidationResult { TotalFilesChecked = docs.Count };

                foreach (var doc in docs)
                {
                    foreach (var rule in rules)
                    {
                        result.AddRange(rule.Validate(doc, docs, basePath));
                    }
                }

                foreach (var folder in folders)
                {
                    foreach (var rule in rules)
                    {
                        result.AddRange(rule.ValidateFolder(folder, docs, basePath));
                    }
                }

                ConsoleOutput.WriteViolations(result);

                if (result.HasErrors)
                    hasErrors = true;
            }
            else if (string.IsNullOrEmpty(path))
            {
                Console.WriteLine("No docs folder found.");
            }
            else
            {
                ConsoleOutput.WriteError($"Path not found: {path}");
                return ExitCodes.ToolError;
            }

            // Agent validation (only if config exists)
            if (config != null)
            {
                Console.WriteLine();
                Console.WriteLine("Checking agent assignments...");

                var agentWarnings = ValidateAgents(config, configService);
                if (agentWarnings.Count > 0)
                {
                    hasWarnings = true;
                    foreach (var warning in agentWarnings)
                    {
                        ConsoleOutput.WriteWarning(warning);
                    }
                }
                else
                {
                    Console.WriteLine("  No issues found.");
                }
            }

            Console.WriteLine();
            if (hasErrors)
            {
                Console.WriteLine("Found errors.");
                return ExitCodes.ValidationErrors;
            }
            else if (hasWarnings)
            {
                Console.WriteLine("Found warnings (no errors).");
                return ExitCodes.Success;
            }
            else
            {
                Console.WriteLine("All checks passed.");
                return ExitCodes.Success;
            }
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Error: {ex.Message}");
            return ExitCodes.ToolError;
        }
    }

    private static List<string> ValidateAgents(DydoConfig config, IConfigService configService)
    {
        var warnings = new List<string>();
        var registry = new AgentRegistry();
        var agentsPath = configService.GetAgentsPath();

        // Check each agent in the pool
        foreach (var agentName in config.Agents.Pool)
        {
            var configHuman = config.Agents.GetHumanForAgent(agentName);
            var state = registry.GetAgentState(agentName);
            var agentWorkspace = registry.GetAgentWorkspace(agentName);

            // Check if workspace exists
            if (!Directory.Exists(agentWorkspace))
            {
                if (state?.Status != AgentStatus.Free)
                {
                    warnings.Add($"Agent '{agentName}' is {state?.Status.ToString().ToLowerInvariant()} but workspace missing.");
                }
                continue;
            }

            // Check state.md assigned human matches config
            if (state?.AssignedHuman != null && configHuman != null)
            {
                if (!state.AssignedHuman.Equals(configHuman, StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add($"Agent '{agentName}' state.md says assigned to '{state.AssignedHuman}' but dydo.json assigns to '{configHuman}'.");
                }
            }

            // Check for stale sessions
            var session = registry.GetSession(agentName);
            if (session != null)
            {
                if (!ProcessUtils.IsProcessRunning(session.TerminalPid))
                {
                    warnings.Add($"Agent '{agentName}' has stale session (terminal PID {session.TerminalPid} no longer running).");
                }
            }
        }

        // Check for orphaned workspaces (folders that exist but agent not in pool)
        if (Directory.Exists(agentsPath))
        {
            foreach (var dir in Directory.GetDirectories(agentsPath))
            {
                var folderName = Path.GetFileName(dir);

                // Skip hidden folders
                if (folderName.StartsWith('.'))
                    continue;

                if (!config.Agents.Pool.Contains(folderName, StringComparer.OrdinalIgnoreCase))
                {
                    warnings.Add($"Orphaned workspace '{folderName}' not in agent pool.");
                }
            }
        }

        return warnings;
    }

    private static string? ResolvePath(string? path)
    {
        if (!string.IsNullOrEmpty(path))
        {
            if (File.Exists(path) || Directory.Exists(path))
                return Path.GetFullPath(path);
            return null;
        }

        return PathUtils.FindDocsFolder(Environment.CurrentDirectory);
    }

    private static List<IRule> CreateRules(ILinkResolver linkResolver)
    {
        return
        [
            new NamingRule(),
            new RelativeLinksRule(),
            new FrontmatterRule(),
            new SummaryRule(),
            new BrokenLinksRule(linkResolver),
            new HubFilesRule(),
            new OrphanDocsRule()
        ];
    }
}
