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
/// Security layers (checked in order):
/// 1. Global off-limits patterns (files-off-limits.md) - blocks ALL operations
/// 2. Dangerous command patterns (for Bash tool) - always blocked
/// 3. Bash command analysis - extracts file operations and checks each
/// 4. Role-based permissions - for write operations
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
        var actionOption = new Option<string?>("--action", "Action being attempted (edit, write, delete, read)");
        var pathOption = new Option<string?>("--path", "Path being accessed");
        var commandOption = new Option<string?>("--command", "Bash command to analyze");

        var command = new Command("guard", "Check if current agent can perform action (used by hooks)")
        {
            actionOption,
            pathOption,
            commandOption
        };

        command.SetHandler((InvocationContext ctx) =>
        {
            var cliAction = ctx.ParseResult.GetValueForOption(actionOption);
            var cliPath = ctx.ParseResult.GetValueForOption(pathOption);
            var cliCommand = ctx.ParseResult.GetValueForOption(commandOption);
            ctx.ExitCode = Execute(cliAction, cliPath, cliCommand);
        });

        return command;
    }

    private static int Execute(string? cliAction, string? cliPath, string? cliCommand)
    {
        string? filePath = null;
        string? action = null;
        string? bashCommand = null;
        string? toolName = null;

        // If CLI arguments are provided, use them directly (skip stdin reading)
        // This avoids blocking on stdin when run from IDEs/test runners
        var hasCliArgs = cliAction != null || cliPath != null || cliCommand != null;

        // Try to read stdin JSON (hook mode) only if no CLI args provided
        if (!hasCliArgs && Console.IsInputRedirected)
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
                        toolName = hookInput.ToolName?.ToLowerInvariant();
                        bashCommand = hookInput.GetCommand();
                    }
                }
            }
            catch
            {
                // Failed to parse JSON, fall back to CLI args
            }
        }

        // Use CLI arguments (or defaults)
        filePath ??= cliPath;
        action ??= cliAction ?? "edit";
        bashCommand ??= cliCommand;

        // Initialize services
        var offLimitsService = new OffLimitsService();
        var bashAnalyzer = new BashCommandAnalyzer();
        var registry = new AgentRegistry();

        // Load off-limits patterns
        offLimitsService.LoadPatterns();

        // ============================================================
        // SECURITY LAYER 1: Check off-limits patterns for direct file operations
        // ============================================================
        if (!string.IsNullOrEmpty(filePath))
        {
            var offLimitsPattern = offLimitsService.IsPathOffLimits(filePath);
            if (offLimitsPattern != null)
            {
                Console.Error.WriteLine("BLOCKED: Path is off-limits to all agents.");
                Console.Error.WriteLine($"  Path: {filePath}");
                Console.Error.WriteLine($"  Pattern: {offLimitsPattern}");
                Console.Error.WriteLine("  Configure exceptions in dydo/files-off-limits.md");
                return ExitCodes.ToolError;
            }
        }

        // ============================================================
        // SECURITY LAYER 2: Handle Bash tool specifically
        // ============================================================
        if (toolName == "bash" && !string.IsNullOrEmpty(bashCommand))
        {
            return HandleBashCommand(bashCommand, offLimitsService, bashAnalyzer, registry);
        }

        // ============================================================
        // SECURITY LAYER 3: Role-based permissions for write operations
        // ============================================================

        // For Read operations, only off-limits check applies (done above)
        // Role permissions only restrict writes, not reads
        if (action == "read" && string.IsNullOrEmpty(bashCommand))
        {
            return ExitCodes.Success;
        }

        // For write/edit operations, check role permissions
        if (string.IsNullOrEmpty(filePath))
        {
            // No file path to check - allow (might be a non-file operation)
            return ExitCodes.Success;
        }

        // Check if there's an agent claimed
        var agent = registry.GetCurrentAgent();
        if (agent == null)
        {
            // No agent claimed - warn but allow in non-strict mode
            // This allows dydo to be used in projects without full workflow setup
            Console.Error.WriteLine("WARNING: No agent identity assigned to this process.");
            Console.Error.WriteLine("  Run 'dydo agent claim auto' to claim an agent identity.");
            Console.Error.WriteLine("  Allowing operation (no strict enforcement).");
            return ExitCodes.Success;
        }

        // Check if agent has a role
        if (string.IsNullOrEmpty(agent.Role))
        {
            Console.Error.WriteLine($"WARNING: Agent {agent.Name} has no role set.");
            Console.Error.WriteLine("  Run 'dydo agent role <role>' to set your role.");
            Console.Error.WriteLine("  Allowing operation (no strict enforcement).");
            return ExitCodes.Success;
        }

        // Check role permissions for write operations
        if (action is "write" or "edit" or "delete")
        {
            if (!registry.IsPathAllowed(filePath, action, out var error))
            {
                Console.Error.WriteLine($"BLOCKED: {error}");
                return ExitCodes.ToolError;
            }
        }

        // ALLOWED
        return ExitCodes.Success;
    }

    /// <summary>
    /// Handle Bash tool commands with comprehensive analysis.
    /// </summary>
    private static int HandleBashCommand(
        string command,
        IOffLimitsService offLimitsService,
        IBashCommandAnalyzer bashAnalyzer,
        AgentRegistry registry)
    {
        // ============================================================
        // Check dangerous patterns first (always block)
        // ============================================================
        var (isDangerous, dangerReason) = bashAnalyzer.CheckDangerousPatterns(command);
        if (isDangerous)
        {
            Console.Error.WriteLine("BLOCKED: Dangerous command pattern detected.");
            Console.Error.WriteLine($"  Reason: {dangerReason}");
            Console.Error.WriteLine($"  Command: {TruncateCommand(command)}");
            return ExitCodes.ToolError;
        }

        // ============================================================
        // Analyze command for file operations
        // ============================================================
        var analysis = bashAnalyzer.Analyze(command);

        // Log warnings (don't block, but inform)
        foreach (var warning in analysis.Warnings)
        {
            Console.Error.WriteLine($"WARNING: {warning}");
        }

        // If analysis found dangerous pattern (shouldn't happen, but just in case)
        if (analysis.HasDangerousPattern)
        {
            Console.Error.WriteLine("BLOCKED: Dangerous command pattern detected.");
            Console.Error.WriteLine($"  Reason: {analysis.DangerousPatternReason}");
            return ExitCodes.ToolError;
        }

        // ============================================================
        // Check each detected operation
        // ============================================================
        foreach (var op in analysis.Operations)
        {
            // Check off-limits for ALL operations (read, write, delete)
            var offLimitsPattern = offLimitsService.IsPathOffLimits(op.Path);
            if (offLimitsPattern != null)
            {
                Console.Error.WriteLine("BLOCKED: Command references off-limits path.");
                Console.Error.WriteLine($"  Path: {op.Path}");
                Console.Error.WriteLine($"  Pattern: {offLimitsPattern}");
                Console.Error.WriteLine($"  Detected: {op.Type} via {op.Command}");
                return ExitCodes.ToolError;
            }

            // Check role permissions for write/delete operations
            if (op.Type is FileOperationType.Write or FileOperationType.Delete
                or FileOperationType.Move or FileOperationType.Copy
                or FileOperationType.PermissionChange)
            {
                var agent = registry.GetCurrentAgent();
                if (agent != null && !string.IsNullOrEmpty(agent.Role))
                {
                    var actionName = op.Type.ToString().ToLowerInvariant();
                    if (!registry.IsPathAllowed(op.Path, actionName, out var error))
                    {
                        Console.Error.WriteLine($"BLOCKED: {error}");
                        Console.Error.WriteLine($"  Detected {op.Type} operation on: {op.Path}");
                        Console.Error.WriteLine($"  Via command: {op.Command}");
                        return ExitCodes.ToolError;
                    }
                }
            }
        }

        // ALLOWED
        return ExitCodes.Success;
    }

    /// <summary>
    /// Truncate a command for display in error messages.
    /// </summary>
    private static string TruncateCommand(string command)
    {
        const int maxLength = 100;
        if (command.Length <= maxLength)
            return command;
        return command[..maxLength] + "...";
    }
}
