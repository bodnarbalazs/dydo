namespace DynaDocs.Commands;

using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

/// <summary>
/// Guard command for enforcing agent permissions.
/// Designed to work as a hook for AI coding assistants (Claude Code, etc.)
///
/// Input modes:
/// 1. Stdin JSON (hook mode) - receives JSON from hook system
/// 2. CLI arguments (manual testing) - --action and --path flags
///
/// Output:
/// - Exit code 0 = action allowed (silent stdout)
/// - Exit code 2 = action blocked (error message to stderr)
/// </summary>
public static class GuardCommand
{
    public static Command Create()
    {
        var actionOption = new Option<string?>("--action", "Action being attempted (edit, write, delete)");
        var pathOption = new Option<string?>("--path", "Path being accessed");

        var command = new Command("guard", "Check if current agent can perform action (used by hooks)")
        {
            actionOption,
            pathOption
        };

        command.SetHandler((InvocationContext ctx) =>
        {
            var cliAction = ctx.ParseResult.GetValueForOption(actionOption);
            var cliPath = ctx.ParseResult.GetValueForOption(pathOption);
            ctx.ExitCode = Execute(cliAction, cliPath);
        });

        return command;
    }

    private static int Execute(string? cliAction, string? cliPath)
    {
        string? filePath = null;
        string? action = null;

        // Try to read stdin JSON first (hook mode)
        if (Console.IsInputRedirected)
        {
            try
            {
                var json = Console.In.ReadToEnd();
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var hookInput = JsonSerializer.Deserialize<HookInput>(json);
                    if (hookInput != null)
                    {
                        filePath = hookInput.GetFilePath();
                        action = hookInput.GetAction();
                    }
                }
            }
            catch
            {
                // Failed to parse JSON, fall back to CLI args
            }
        }

        // Fall back to CLI arguments
        filePath ??= cliPath;
        action ??= cliAction ?? "edit";

        // Validate we have required info
        if (string.IsNullOrEmpty(filePath))
        {
            Console.Error.WriteLine("No file path provided. Use --path or pipe JSON via stdin.");
            return ExitCodes.ToolError;
        }

        var registry = new AgentRegistry();

        // Check if there's an agent claimed
        var agent = registry.GetCurrentAgent();
        if (agent == null)
        {
            // No agent claimed - warn but allow in non-strict mode
            // This allows dydo to be used in projects without full workflow setup
            Console.Error.WriteLine("No agent identity assigned to this process. Run 'dydo agent claim auto' first.");
            return ExitCodes.ToolError;
        }

        // Check if agent has a role
        if (string.IsNullOrEmpty(agent.Role))
        {
            Console.Error.WriteLine($"Agent {agent.Name} has no role set. Run 'dydo agent role <role>' first.");
            return ExitCodes.ToolError;
        }

        // Check permissions
        if (!registry.IsPathAllowed(filePath, action, out var error))
        {
            // BLOCKED - exit code 2, error to stderr
            Console.Error.WriteLine(error);
            return ExitCodes.ToolError;
        }

        // ALLOWED - exit code 0, silent (no stdout)
        return ExitCodes.Success;
    }
}
