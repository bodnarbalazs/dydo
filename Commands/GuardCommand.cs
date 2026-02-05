namespace DynaDocs.Commands;

using System.CommandLine;
using System.Text.Json;
using System.Text.RegularExpressions;
using DynaDocs.Models;
using DynaDocs.Serialization;
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
        var actionOption = new Option<string?>("--action")
        {
            Description = "Action being attempted (edit, write, delete, read)"
        };

        var pathOption = new Option<string?>("--path")
        {
            Description = "Path being accessed"
        };

        var commandOption = new Option<string?>("--command")
        {
            Description = "Bash command to analyze"
        };

        var command = new Command("guard", "Check if current agent can perform action (used by hooks)");
        command.Options.Add(actionOption);
        command.Options.Add(pathOption);
        command.Options.Add(commandOption);

        command.SetAction(parseResult =>
        {
            var cliAction = parseResult.GetValue(actionOption);
            var cliPath = parseResult.GetValue(pathOption);
            var cliCommand = parseResult.GetValue(commandOption);
            return Execute(cliAction, cliPath, cliCommand);
        });

        return command;
    }

    private static int Execute(string? cliAction, string? cliPath, string? cliCommand)
    {
        string? filePath = null;
        string? action = null;
        string? bashCommand = null;
        string? toolName = null;
        string? sessionId = null;

        // If CLI arguments are provided, use them directly (skip stdin reading)
        // This avoids blocking on stdin when run from IDEs/test runners
        var hasCliArgs = cliAction != null || cliPath != null || cliCommand != null;

        // Try to read stdin JSON (hook mode) only if no CLI args provided
        if (!hasCliArgs && TryReadStdinJson(out var json) && json != null)
        {
            try
            {
                var hookInput = JsonSerializer.Deserialize(json, DydoDefaultJsonContext.Default.HookInput);
                if (hookInput != null)
                {
                    sessionId = hookInput.SessionId;
                    filePath = hookInput.GetFilePath();
                    action = hookInput.GetAction();
                    toolName = hookInput.ToolName?.ToLowerInvariant();
                    bashCommand = hookInput.GetCommand();
                }
            }
            catch (Exception ex)
            {
                // Log the error for debugging - silent failures are dangerous
                Console.Error.WriteLine($"WARNING: Failed to parse hook input: {ex.Message}");
            }
        }

        // Session ID is required for hook mode (non-CLI)
        if (!hasCliArgs && string.IsNullOrEmpty(sessionId))
        {
            Console.Error.WriteLine("BLOCKED: No session_id in hook input.");
            return ExitCodes.ToolError;
        }

        // Initialize services (need registry early for session context lookup)
        var offLimitsService = new OffLimitsService();
        var bashAnalyzer = new BashCommandAnalyzer();
        var registry = new AgentRegistry();
        var auditService = new AuditService();

        // For CLI mode, fall back to session context file (set by guard for subprocess commands)
        if (hasCliArgs && string.IsNullOrEmpty(sessionId))
        {
            sessionId = registry.GetSessionContext();
        }

        // Use CLI arguments (or defaults)
        filePath ??= cliPath;
        action ??= cliAction ?? "edit";
        bashCommand ??= cliCommand;

        // Load off-limits patterns
        offLimitsService.LoadPatterns();

        // ============================================================
        // SECURITY LAYER 1: Check off-limits patterns for direct file operations
        // For read operations, certain files bypass off-limits based on staged access:
        // - Bootstrap files: always readable (for onboarding)
        // - Mode files: readable based on agent state (Stage 1+)
        // ============================================================
        if (!string.IsNullOrEmpty(filePath))
        {
            // Get agent early for staged access checks
            var agentForOffLimits = registry.GetCurrentAgent(sessionId);

            // Check if this read bypasses off-limits due to staged access
            var bypassOffLimits = false;
            if (action == "read")
            {
                // Bootstrap files always bypass off-limits (Stage 0+)
                if (IsBootstrapFile(filePath))
                    bypassOffLimits = true;
                // Mode files for own agent bypass off-limits (Stage 1+)
                else if (agentForOffLimits != null && IsModeFile(filePath, agentForOffLimits.Name))
                    bypassOffLimits = true;
                // With a role set, all mode files bypass off-limits (Stage 2)
                else if (agentForOffLimits != null && !string.IsNullOrEmpty(agentForOffLimits.Role) && IsAnyModeFile(filePath))
                    bypassOffLimits = true;
            }

            if (!bypassOffLimits)
            {
                var offLimitsPattern = offLimitsService.IsPathOffLimits(filePath);
                if (offLimitsPattern != null)
                {
                    // Log blocked event
                    LogAuditEvent(auditService, sessionId, registry, new AuditEvent
                    {
                        EventType = AuditEventType.Blocked,
                        Path = filePath,
                        Tool = toolName,
                        BlockReason = $"Off-limits: {offLimitsPattern}"
                    });

                    Console.Error.WriteLine("BLOCKED: Path is off-limits to all agents.");
                    Console.Error.WriteLine($"  Path: {filePath}");
                    Console.Error.WriteLine($"  Pattern: {offLimitsPattern}");
                    Console.Error.WriteLine("  Configure exceptions in dydo/files-off-limits.md");
                    return ExitCodes.ToolError;
                }
            }
        }

        // ============================================================
        // SECURITY LAYER 2: Handle Bash tool specifically
        // ============================================================
        if (toolName == "bash" && !string.IsNullOrEmpty(bashCommand))
        {
            return HandleBashCommand(bashCommand, sessionId, offLimitsService, bashAnalyzer, registry, auditService);
        }

        // ============================================================
        // SECURITY LAYER 3: Staged access control
        // ============================================================

        // Get current agent (may be null if not claimed)
        var agent = registry.GetCurrentAgent(sessionId);

        // For Read operations, apply staged access control
        if (action == "read" && string.IsNullOrEmpty(bashCommand))
        {
            if (!IsReadAllowed(filePath, agent))
            {
                // Log blocked read
                LogAuditEvent(auditService, sessionId, registry, new AuditEvent
                {
                    EventType = AuditEventType.Blocked,
                    Path = filePath,
                    Tool = toolName,
                    BlockReason = agent == null ? "No agent identity" : "No role set"
                });

                Console.Error.WriteLine("BLOCKED: Read access denied.");
                if (agent == null)
                {
                    Console.Error.WriteLine("  No agent identity assigned to this process.");
                    Console.Error.WriteLine("  Read your workflow.md to learn how to onboard:");
                    Console.Error.WriteLine("    dydo/agents/*/workflow.md");
                    Console.Error.WriteLine("  Then run: dydo agent claim auto");
                }
                else if (string.IsNullOrEmpty(agent.Role))
                {
                    Console.Error.WriteLine($"  Agent {agent.Name} has no role set.");
                    Console.Error.WriteLine("  Read your mode files to understand available roles:");
                    Console.Error.WriteLine($"    dydo/agents/{agent.Name}/modes/*.md");
                    Console.Error.WriteLine("  Then run: dydo agent role <role>");
                }
                return ExitCodes.ToolError;
            }

            // Log allowed read
            LogAuditEvent(auditService, sessionId, registry, new AuditEvent
            {
                EventType = AuditEventType.Read,
                Path = filePath,
                Tool = toolName
            });

            return ExitCodes.Success;
        }

        // For write/edit operations, check role permissions
        if (string.IsNullOrEmpty(filePath))
        {
            // No file path to check - allow (might be a non-file operation)
            return ExitCodes.Success;
        }

        // Check if there's an agent claimed (required for writes)
        if (agent == null)
        {
            LogAuditEvent(auditService, sessionId, registry, new AuditEvent
            {
                EventType = AuditEventType.Blocked,
                Path = filePath,
                Tool = toolName,
                BlockReason = "No agent identity"
            });

            Console.Error.WriteLine("BLOCKED: No agent identity assigned to this process.");
            Console.Error.WriteLine("  Run 'dydo agent claim auto' to claim an agent identity.");
            return ExitCodes.ToolError;
        }

        // Check if agent has a role (required for writes)
        if (string.IsNullOrEmpty(agent.Role))
        {
            LogAuditEvent(auditService, sessionId, registry, new AuditEvent
            {
                EventType = AuditEventType.Blocked,
                Path = filePath,
                Tool = toolName,
                BlockReason = "No role set"
            });

            Console.Error.WriteLine($"BLOCKED: Agent {agent.Name} has no role set.");
            Console.Error.WriteLine("  Run 'dydo agent role <role>' to set your role.");
            return ExitCodes.ToolError;
        }

        // Check role permissions for write operations
        if (action is "write" or "edit" or "delete")
        {
            if (!registry.IsPathAllowed(sessionId, filePath, action, out var error))
            {
                LogAuditEvent(auditService, sessionId, registry, new AuditEvent
                {
                    EventType = AuditEventType.Blocked,
                    Path = filePath,
                    Tool = toolName,
                    BlockReason = error
                });

                Console.Error.WriteLine($"BLOCKED: {error}");
                return ExitCodes.ToolError;
            }
        }

        // Log allowed write/edit/delete
        var eventType = action switch
        {
            "write" => AuditEventType.Write,
            "edit" => AuditEventType.Edit,
            "delete" => AuditEventType.Delete,
            _ => AuditEventType.Edit
        };
        LogAuditEvent(auditService, sessionId, registry, new AuditEvent
        {
            EventType = eventType,
            Path = filePath,
            Tool = toolName
        });

        // ALLOWED
        return ExitCodes.Success;
    }

    /// <summary>
    /// Handle Bash tool commands with comprehensive analysis.
    /// </summary>
    private static int HandleBashCommand(
        string command,
        string? sessionId,
        IOffLimitsService offLimitsService,
        IBashCommandAnalyzer bashAnalyzer,
        AgentRegistry registry,
        IAuditService auditService)
    {
        // ============================================================
        // Handle dydo commands - store session context for subprocess
        // ============================================================
        if (IsDydoCommand(command) && !string.IsNullOrEmpty(sessionId))
        {
            // Store session context so dydo subcommands can identify the session
            registry.StoreSessionContext(sessionId);

            // Special handling for claim commands - also store pending session
            var (isClaim, agentName) = ParseClaimCommand(command);
            if (isClaim && !string.IsNullOrEmpty(agentName))
            {
                // Resolve "auto" to actual agent name
                if (agentName.Equals("auto", StringComparison.OrdinalIgnoreCase))
                {
                    var human = registry.GetCurrentHuman();
                    if (!string.IsNullOrEmpty(human))
                    {
                        var freeAgents = registry.GetFreeAgentsForHuman(human);
                        if (freeAgents.Count > 0)
                            agentName = freeAgents[0].Name;
                        else
                        {
                            // No free agents - let claim command report proper error
                            return ExitCodes.Success;
                        }
                    }
                }
                // Resolve single letter to agent name (e.g., "A" -> "Adele")
                else if (agentName.Length == 1 && char.IsLetter(agentName[0]))
                {
                    var resolved = registry.GetAgentNameFromLetter(agentName[0]);
                    if (resolved != null)
                        agentName = resolved;
                }

                if (registry.IsValidAgentName(agentName))
                    registry.StorePendingSessionId(agentName, sessionId);
            }

            // Allow the dydo command to proceed
            return ExitCodes.Success;
        }

        // ============================================================
        // Check dangerous patterns first (always block)
        // ============================================================
        var (isDangerous, dangerReason) = bashAnalyzer.CheckDangerousPatterns(command);
        if (isDangerous)
        {
            LogAuditEvent(auditService, sessionId, registry, new AuditEvent
            {
                EventType = AuditEventType.Blocked,
                Tool = "bash",
                Command = TruncateCommand(command),
                BlockReason = $"Dangerous pattern: {dangerReason}"
            });

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
            LogAuditEvent(auditService, sessionId, registry, new AuditEvent
            {
                EventType = AuditEventType.Blocked,
                Tool = "bash",
                Command = TruncateCommand(command),
                BlockReason = $"Dangerous pattern: {analysis.DangerousPatternReason}"
            });

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
                LogAuditEvent(auditService, sessionId, registry, new AuditEvent
                {
                    EventType = AuditEventType.Blocked,
                    Tool = "bash",
                    Path = op.Path,
                    Command = TruncateCommand(command),
                    BlockReason = $"Off-limits: {offLimitsPattern}"
                });

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
                var agent = registry.GetCurrentAgent(sessionId);
                if (agent != null && !string.IsNullOrEmpty(agent.Role))
                {
                    var actionName = op.Type.ToString().ToLowerInvariant();
                    if (!registry.IsPathAllowed(sessionId, op.Path, actionName, out var error))
                    {
                        LogAuditEvent(auditService, sessionId, registry, new AuditEvent
                        {
                            EventType = AuditEventType.Blocked,
                            Tool = "bash",
                            Path = op.Path,
                            Command = TruncateCommand(command),
                            BlockReason = error
                        });

                        Console.Error.WriteLine($"BLOCKED: {error}");
                        Console.Error.WriteLine($"  Detected {op.Type} operation on: {op.Path}");
                        Console.Error.WriteLine($"  Via command: {op.Command}");
                        return ExitCodes.ToolError;
                    }
                }
            }
        }

        // Log allowed bash command
        LogAuditEvent(auditService, sessionId, registry, new AuditEvent
        {
            EventType = AuditEventType.Bash,
            Tool = "bash",
            Command = TruncateCommand(command)
        });

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

    /// <summary>
    /// Try to read JSON from stdin using a reliable detection method.
    /// Uses Console.KeyAvailable which throws InvalidOperationException when stdin is redirected.
    /// This is more reliable than Console.IsInputRedirected on Windows.
    /// </summary>
    private static bool TryReadStdinJson(out string? json)
    {
        json = null;
        try
        {
            // KeyAvailable throws InvalidOperationException when stdin is redirected
            // This is more reliable than Console.IsInputRedirected on Windows
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

    /// <summary>
    /// Check if a read operation is allowed based on staged access control.
    /// Stage 0 (no identity): Only bootstrap files (root files, index.md, workflow.md)
    /// Stage 1 (identity, no role): + mode files for claimed agent
    /// Stage 2 (identity + role): All reads allowed (RBAC only restricts writes)
    /// </summary>
    private static bool IsReadAllowed(string? filePath, AgentState? agent)
    {
        // No path = allow (might be a non-file operation)
        if (string.IsNullOrEmpty(filePath))
            return true;

        // Stage 0: Bootstrap files are always allowed
        if (IsBootstrapFile(filePath))
            return true;

        // No identity = only bootstrap files
        if (agent == null)
            return false;

        // Stage 1: Identity claimed - unlock mode files for this agent
        if (IsModeFile(filePath, agent.Name))
            return true;

        // No role = only bootstrap + mode files
        if (string.IsNullOrEmpty(agent.Role))
            return false;

        // Stage 2: Role set - all reads allowed (RBAC only restricts writes per spec)
        // Note: Off-limits check already happened before this point
        return true;
    }

    /// <summary>
    /// Check if a file is a bootstrap file that can be read without identity.
    /// Bootstrap files: root-level files, dydo/index.md, dydo/agents/*/workflow.md
    /// </summary>
    private static bool IsBootstrapFile(string filePath)
    {
        // Normalize path separators for consistent matching
        var normalizedPath = filePath.Replace('\\', '/');

        // Root level files (no directory separator, or just the filename)
        // Check if there's no slash, or if it's just a simple filename
        var lastSlash = normalizedPath.LastIndexOf('/');
        if (lastSlash == -1)
            return true; // No directory component = root file

        // Check for paths that end with just a filename (relative from project root)
        var pathParts = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (pathParts.Length == 1)
            return true; // Single component = root file

        // dydo/index.md
        if (normalizedPath.EndsWith("dydo/index.md", StringComparison.OrdinalIgnoreCase))
            return true;

        // dydo/agents/*/workflow.md
        if (Regex.IsMatch(normalizedPath, @"dydo/agents/[^/]+/workflow\.md$", RegexOptions.IgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// Check if a file is a mode file for the specified agent.
    /// Mode files: dydo/agents/{agentName}/modes/*.md
    /// </summary>
    private static bool IsModeFile(string filePath, string agentName)
    {
        // Normalize path separators for consistent matching
        var normalizedPath = filePath.Replace('\\', '/');

        // dydo/agents/{agentName}/modes/*.md
        var pattern = $@"dydo/agents/{Regex.Escape(agentName)}/modes/[^/]+\.md$";
        return Regex.IsMatch(normalizedPath, pattern, RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Check if a file is any agent's mode file.
    /// Mode files: dydo/agents/*/modes/*.md
    /// </summary>
    private static bool IsAnyModeFile(string filePath)
    {
        // Normalize path separators for consistent matching
        var normalizedPath = filePath.Replace('\\', '/');

        // dydo/agents/*/modes/*.md
        return Regex.IsMatch(normalizedPath, @"dydo/agents/[^/]+/modes/[^/]+\.md$", RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Parse a bash command to detect dydo agent claim commands.
    /// Returns (isClaim, agentName) where agentName may be "auto" or a specific agent name.
    /// </summary>
    private static (bool isClaim, string? agentName) ParseClaimCommand(string command)
    {
        // Match: dydo agent claim <name> or ./dydo agent claim <name>
        // Account for command chaining with ; && ||
        var match = Regex.Match(command,
            @"(?:^|[;&|]\s*)(?:\./)?dydo\s+agent\s+claim\s+(\S+)",
            RegexOptions.IgnoreCase);

        return match.Success ? (true, match.Groups[1].Value) : (false, null);
    }

    /// <summary>
    /// Check if a command is a dydo command.
    /// </summary>
    private static bool IsDydoCommand(string command)
    {
        // Match: dydo ... or ./dydo ...
        return Regex.IsMatch(command,
            @"(?:^|[;&|]\s*)(?:\./)?dydo\s",
            RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Helper to log an audit event with proper error handling.
    /// </summary>
    private static void LogAuditEvent(
        IAuditService auditService,
        string? sessionId,
        IAgentRegistry registry,
        AuditEvent @event)
    {
        if (string.IsNullOrEmpty(sessionId))
            return;

        try
        {
            // Get agent info for session metadata
            var agent = registry.GetCurrentAgent(sessionId);
            var human = registry.GetCurrentHuman();

            auditService.LogEvent(sessionId, @event, agent?.Name, human);
        }
        catch
        {
            // Audit logging should never break the guard
            // Silently ignore errors
        }
    }
}
