namespace DynaDocs.Commands;

using System.CommandLine;
using System.Text.Json;
using DynaDocs.Serialization;
using DynaDocs.Services;
using DynaDocs.Utils;

/// <summary>
/// Shows the current agent identity and human information.
/// Uses objective language suitable for both humans and AI readers.
///
/// Supports two modes:
/// 1. Hook mode (stdin JSON): Uses session_id to identify agent
/// 2. CLI mode (no stdin): Shows general info without specific agent identity
/// </summary>
public static class WhoamiCommand
{
    public static Command Create()
    {
        var command = new Command("whoami", "Show current agent identity");

        command.SetAction(_ => Execute());

        return command;
    }

    private static int Execute()
    {
        var registry = new AgentRegistry();
        var configService = new ConfigService();

        // Try to read session_id from stdin JSON (hook mode)
        string? sessionId = null;
        if (TryReadStdinJson(out var json) && json != null)
        {
            try
            {
                var hookInput = JsonSerializer.Deserialize(json, DydoDefaultJsonContext.Default.HookInput);
                sessionId = hookInput?.SessionId;
            }
            catch { /* ignore parse errors */ }
        }

        // Fall back to session context file (set by guard for subprocess commands)
        if (string.IsNullOrEmpty(sessionId))
        {
            sessionId = registry.GetSessionContext();
        }

        var human = registry.GetCurrentHuman();
        var agent = registry.GetCurrentAgent(sessionId);

        if (agent != null)
        {
            // Agent is claimed
            Console.WriteLine($"Agent identity for this process: {agent.Name}");
            Console.WriteLine($"  Assigned human: {agent.AssignedHuman ?? registry.GetHumanForAgent(agent.Name) ?? "(unassigned)"}");

            if (!string.IsNullOrEmpty(agent.Role))
                Console.WriteLine($"  Role: {agent.Role}");
            else
                Console.WriteLine("  Role: (none set)");

            if (!string.IsNullOrEmpty(agent.Task))
            {
                Console.WriteLine($"  Task: {agent.Task}");
                var taskFilePath = Path.Combine(configService.GetTasksPath(), $"{agent.Task}.md");
                if (File.Exists(taskFilePath))
                    Console.WriteLine($"  Task file: {taskFilePath}");
            }

            Console.WriteLine($"  Status: {agent.Status.ToString().ToLowerInvariant()}");
            Console.WriteLine($"  Workspace: {registry.GetAgentWorkspace(agent.Name)}");

            if (agent.WritablePaths.Count > 0)
                Console.WriteLine($"  Writable paths: {string.Join(", ", agent.WritablePaths)}");

            if (agent.ReadOnlyPaths.Count > 0 && agent.ReadOnlyPaths[0] != "**")
                Console.WriteLine($"  Read-only paths: {string.Join(", ", agent.ReadOnlyPaths)}");
        }
        else
        {
            // No agent claimed
            Console.WriteLine("No agent identity assigned to this process.");

            if (!string.IsNullOrEmpty(human))
            {
                Console.WriteLine($"  Human (from DYDO_HUMAN): {human}");

                var claimableAgents = registry.GetAgentsForHuman(human);
                if (claimableAgents.Count > 0)
                {
                    var freeAgents = registry.GetFreeAgentsForHuman(human);
                    Console.WriteLine($"  Claimable agents for {human}: {string.Join(", ", claimableAgents)}");

                    if (freeAgents.Count > 0)
                        Console.WriteLine($"  Free agents: {string.Join(", ", freeAgents.Select(a => a.Name))}");
                    else
                        Console.WriteLine("  Free agents: (none - all busy)");
                }
                else
                {
                    Console.WriteLine($"  No agents assigned to {human} in dydo.json");
                }

                Console.WriteLine();
                Console.WriteLine("To claim an agent, run:");
                Console.WriteLine("  dydo agent claim auto       # Claims first available");
                Console.WriteLine("  dydo agent claim Adele      # Claims specific agent");
            }
            else
            {
                Console.WriteLine("  DYDO_HUMAN environment variable not set.");
                Console.WriteLine();
                Console.WriteLine("To use dydo, set the DYDO_HUMAN variable:");
                Console.WriteLine("  export DYDO_HUMAN=your_name    # Bash/Zsh");
                Console.WriteLine("  $env:DYDO_HUMAN = \"your_name\"  # PowerShell");
            }
        }

        return ExitCodes.Success;
    }

    /// <summary>
    /// Try to read JSON from stdin using a reliable detection method.
    /// </summary>
    private static bool TryReadStdinJson(out string? json)
    {
        json = null;
        try
        {
            // KeyAvailable throws InvalidOperationException when stdin is redirected
            _ = Console.KeyAvailable;
            return false; // Not redirected, no stdin to read
        }
        catch (InvalidOperationException)
        {
            // Stdin is redirected, read it
            json = Console.In.ReadToEnd();
            return !string.IsNullOrWhiteSpace(json);
        }
    }
}
