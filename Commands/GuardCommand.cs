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
public static partial class GuardCommand
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
        string? searchPath = null;
        bool? runInBackground = null;

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
                    searchPath = hookInput.GetSearchPath();
                    runInBackground = hookInput.ToolInput?.RunInBackground;
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

        // Daily validation: warn about config issues on first guard call per day
        RunDailyValidationIfDue();

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
            var blocked = CheckDirectFileOffLimits(filePath, action, toolName, sessionId, offLimitsService, registry, auditService);
            if (blocked != null) return blocked.Value;
        }

        // ============================================================
        // SECURITY LAYER 2: Handle Bash tool specifically
        // ============================================================
        if (toolName == "bash" && !string.IsNullOrEmpty(bashCommand))
        {
            return HandleBashCommand(bashCommand, sessionId, offLimitsService, bashAnalyzer, registry, auditService, runInBackground);
        }

        // ============================================================
        // SECURITY LAYER 2.5: Search tools (Glob/Grep) and Agent tool
        // These always require Stage 2 (identity + role).
        // Search tools are broad read operations that scan across directories.
        // Agent tool spawns sub-processes that inherit the session — blocking
        // pre-claim prevents sub-agents from bypassing the onboarding flow.
        // ============================================================
        if (toolName is "glob" or "grep" or "agent")
        {
            return HandleSearchTool(searchPath, toolName, sessionId, offLimitsService, registry, auditService);
        }

        // ============================================================
        // SECURITY LAYER 3: Staged access control
        // ============================================================

        // Get current agent (may be null if not claimed)
        var agent = registry.GetCurrentAgent(sessionId);

        // For Read operations, apply staged access control
        if (action == "read" && string.IsNullOrEmpty(bashCommand))
        {
            return HandleReadOperation(filePath, toolName, agent, sessionId, registry, auditService);
        }

        // For write/edit operations
        return HandleWriteOperation(filePath, action, toolName, agent, sessionId, registry, auditService);
    }

    private static int HandleWriteOperation(
        string? filePath, string? action, string? toolName, AgentState? agent,
        string? sessionId, AgentRegistry registry, IAuditService auditService)
    {
        if (string.IsNullOrEmpty(filePath))
            return ExitCodes.Success;

        if (agent == null)
        {
            LogAuditEvent(auditService, sessionId, registry, new AuditEvent
            {
                EventType = AuditEventType.Blocked, Path = filePath, Tool = toolName,
                BlockReason = "No agent identity"
            });
            Console.Error.WriteLine("BLOCKED: No agent identity assigned to this process.");
            Console.Error.WriteLine("  Run 'dydo agent claim auto' to claim an agent identity.");
            return ExitCodes.ToolError;
        }

        if (string.IsNullOrEmpty(agent.Role))
        {
            LogAuditEvent(auditService, sessionId, registry, new AuditEvent
            {
                EventType = AuditEventType.Blocked, Path = filePath, Tool = toolName,
                BlockReason = "No role set"
            });
            Console.Error.WriteLine($"BLOCKED: Agent {agent.Name} has no role set.");
            Console.Error.WriteLine($"  1. Read your mode file first: dydo/agents/{agent.Name}/modes/<role>.md");
            Console.Error.WriteLine($"  2. Then set your role: dydo agent role <role> --task <task-name>");
            return ExitCodes.ToolError;
        }

        // Check unread messages
        agent = registry.GetCurrentAgent(sessionId);
        if (agent != null)
        {
            var blocked = NotifyUnreadMessages(agent, filePath, toolName, null, auditService, sessionId, registry);
            if (blocked != null) return blocked.Value;

            blocked = CheckPendingState(agent, filePath, toolName, null, auditService, sessionId, registry);
            if (blocked != null) return blocked.Value;
        }

        // Check must-read enforcement
        agent = registry.GetCurrentAgent(sessionId);
        if (agent != null && agent.UnreadMustReads.Count > 0)
        {
            LogAuditEvent(auditService, sessionId, registry, new AuditEvent
            {
                EventType = AuditEventType.Blocked, Path = filePath, Tool = toolName,
                BlockReason = "Must-read files not yet read"
            });
            WriteMustReadError(agent);
            return ExitCodes.ToolError;
        }

        // Check role permissions
        if (action is "write" or "edit" or "delete")
        {
            if (!registry.IsPathAllowed(sessionId, filePath, action, out var error))
            {
                LogAuditEvent(auditService, sessionId, registry, new AuditEvent
                {
                    EventType = AuditEventType.Blocked, Path = filePath, Tool = toolName,
                    BlockReason = error
                });
                Console.Error.WriteLine($"BLOCKED: {error}");
                return ExitCodes.ToolError;
            }
        }

        var eventType = action switch
        {
            "write" => AuditEventType.Write,
            "edit" => AuditEventType.Edit,
            "delete" => AuditEventType.Delete,
            _ => AuditEventType.Edit
        };
        LogAuditEvent(auditService, sessionId, registry, new AuditEvent
        {
            EventType = eventType, Path = filePath, Tool = toolName
        });

        return ExitCodes.Success;
    }

    private static int? CheckDirectFileOffLimits(
        string filePath, string? action, string? toolName, string? sessionId,
        IOffLimitsService offLimitsService, AgentRegistry registry, IAuditService auditService)
    {
        var agentForOffLimits = registry.GetCurrentAgent(sessionId);

        if (action == "read" && ShouldBypassOffLimits(filePath, agentForOffLimits))
            return null;

        var offLimitsPattern = offLimitsService.IsPathOffLimits(filePath);
        if (offLimitsPattern == null)
            return null;

        LogAuditEvent(auditService, sessionId, registry, new AuditEvent
        {
            EventType = AuditEventType.Blocked, Path = filePath, Tool = toolName,
            BlockReason = $"Off-limits: {offLimitsPattern}"
        });
        Console.Error.WriteLine("BLOCKED: Path is off-limits to all agents.");
        Console.Error.WriteLine($"  Path: {filePath}");
        Console.Error.WriteLine($"  Pattern: {offLimitsPattern}");
        Console.Error.WriteLine("  Configure exceptions in dydo/files-off-limits.md");
        return ExitCodes.ToolError;
    }

    private static bool ShouldBypassOffLimits(string filePath, AgentState? agent)
    {
        if (IsBootstrapFile(filePath))
            return true;
        if (agent != null && IsModeFile(filePath, agent.Name))
            return true;
        if (agent != null && !string.IsNullOrEmpty(agent.Role) && IsAnyModeFile(filePath))
            return true;
        return false;
    }

    private static int HandleReadOperation(
        string? filePath, string? toolName, AgentState? agent, string? sessionId,
        AgentRegistry registry, IAuditService auditService)
    {
        if (!IsReadAllowed(filePath, agent))
        {
            LogAuditEvent(auditService, sessionId, registry, new AuditEvent
            {
                EventType = AuditEventType.Blocked, Path = filePath, Tool = toolName,
                BlockReason = agent == null ? "No agent identity" : "No role set"
            });
            WriteAccessDeniedError(agent?.Name, null);
            return ExitCodes.ToolError;
        }

        if (agent != null)
        {
            var blocked = CheckPendingState(agent, filePath, toolName, null, auditService, sessionId, registry);
            if (blocked != null) return blocked.Value;
        }

        LogAuditEvent(auditService, sessionId, registry, new AuditEvent
        {
            EventType = AuditEventType.Read, Path = filePath, Tool = toolName
        });

        // Track must-read completion
        if (agent != null && agent.UnreadMustReads.Count > 0 && !string.IsNullOrEmpty(filePath))
        {
            var relPath = NormalizeForMustReadComparison(filePath);
            if (agent.UnreadMustReads.Any(p => p.Equals(relPath, StringComparison.OrdinalIgnoreCase)))
                registry.MarkMustReadComplete(sessionId, relPath);
        }

        // Track message reads
        if (agent != null && agent.UnreadMessages.Count > 0 && !string.IsNullOrEmpty(filePath))
        {
            var messageId = ExtractMessageIdFromPath(filePath);
            if (messageId != null && agent.UnreadMessages.Contains(messageId))
                registry.MarkMessageRead(sessionId, messageId);
        }

        return ExitCodes.Success;
    }

    private static int HandleSearchTool(
        string? searchPath, string? toolName, string? sessionId,
        IOffLimitsService offLimitsService, AgentRegistry registry, IAuditService auditService)
    {
        var searchAgent = registry.GetCurrentAgent(sessionId);
        var blocked = RequireIdentityAndRole(searchAgent, searchPath, toolName, auditService, sessionId, registry);
        if (blocked != null) return blocked.Value;

        blocked = NotifyUnreadMessages(searchAgent!, searchPath, toolName, null, auditService, sessionId, registry);
        if (blocked != null) return blocked.Value;

        blocked = CheckPendingState(searchAgent!, searchPath, toolName, null, auditService, sessionId, registry);
        if (blocked != null) return blocked.Value;

        if (!string.IsNullOrEmpty(searchPath))
        {
            var offLimitsPattern = offLimitsService.IsPathOffLimits(searchPath);
            if (offLimitsPattern != null)
            {
                LogAuditEvent(auditService, sessionId, registry, new AuditEvent
                {
                    EventType = AuditEventType.Blocked, Path = searchPath, Tool = toolName,
                    BlockReason = $"Off-limits: {offLimitsPattern}"
                });
                Console.Error.WriteLine("BLOCKED: Path is off-limits to all agents.");
                Console.Error.WriteLine($"  Path: {searchPath}");
                Console.Error.WriteLine($"  Pattern: {offLimitsPattern}");
                Console.Error.WriteLine("  Configure exceptions in dydo/files-off-limits.md");
                return ExitCodes.ToolError;
            }
        }

        LogAuditEvent(auditService, sessionId, registry, new AuditEvent
        {
            EventType = AuditEventType.Read, Path = searchPath, Tool = toolName
        });
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
        IAuditService auditService,
        bool? runInBackground = null)
    {
        // Block indirect dydo invocations (npx dydo, dotnet dydo)
        var blocked = CheckIndirectDydoBlock(command, sessionId, registry, auditService);
        if (blocked != null) return blocked.Value;

        // Handle dydo commands
        if (IsDydoCommand(command) && !string.IsNullOrEmpty(sessionId))
            return HandleDydoBashCommand(command, sessionId, registry, auditService, runInBackground);

        // COACHING: Block needless cd+command compounds
        var (isCdChain, cdPath, restCmd) = bashAnalyzer.DetectNeedlessCd(command);
        if (isCdChain)
        {
            LogAuditEvent(auditService, sessionId, registry, new AuditEvent
            {
                EventType = AuditEventType.Blocked, Tool = "bash",
                Command = TruncateCommand(command), BlockReason = "Needless cd compound"
            });
            Console.Error.WriteLine("BLOCKED: Don't chain cd with other commands — it breaks auto-approval for whitelisted commands.");
            Console.Error.WriteLine($"  If you need to change directory, run cd separately first.");
            Console.Error.WriteLine($"  Otherwise just run: {restCmd}");
            return ExitCodes.ToolError;
        }

        // Non-dydo bash: check agent state, then analyze command
        return HandleNonDydoBash(command, sessionId, offLimitsService, bashAnalyzer, registry, auditService);
    }

    private static int? CheckIndirectDydoBlock(string command, string? sessionId, AgentRegistry registry, IAuditService auditService)
    {
        var (isIndirect, invoker, dydoArgs) = CheckIndirectDydoInvocation(command);
        if (!isIndirect) return null;

        LogAuditEvent(auditService, sessionId, registry, new AuditEvent
        {
            EventType = AuditEventType.Blocked, Tool = "bash",
            Command = TruncateCommand(command), BlockReason = $"Indirect dydo invocation via {invoker}"
        });

        var correctedCommand = string.IsNullOrEmpty(dydoArgs) ? "dydo" : $"dydo {dydoArgs}";
        if (IsDydoOnPath())
        {
            Console.Error.WriteLine($"BLOCKED: Don't use '{invoker}' to run dydo — it's already on your PATH.");
            Console.Error.WriteLine($"  Just use: {correctedCommand}");
        }
        else
        {
            Console.Error.WriteLine($"BLOCKED: Don't use '{invoker}' to run dydo — dydo is not on your PATH.");
            Console.Error.WriteLine($"  Add it to your PATH, then use: {correctedCommand}");
        }
        return ExitCodes.ToolError;
    }

    private static int HandleDydoBashCommand(string command, string sessionId, AgentRegistry registry, IAuditService auditService, bool? runInBackground)
    {
        registry.StoreSessionContext(sessionId);

        HandleClaimSessionStorage(command, sessionId, registry);

        if (IsDydoWaitCommand(command) && runInBackground != true)
        {
            LogAuditEvent(auditService, sessionId, registry, new AuditEvent
            {
                EventType = AuditEventType.Blocked, Tool = "bash",
                Command = TruncateCommand(command), BlockReason = "dydo wait in foreground"
            });
            Console.Error.WriteLine("BLOCKED: 'dydo wait' must run in background. Use run_in_background to avoid blocking other work.");
            return ExitCodes.ToolError;
        }

        if (!IsDydoDispatchCommand(command) && !IsDydoWaitAnyForm(command))
        {
            var agent = registry.GetCurrentAgent(sessionId);
            if (agent != null)
            {
                var blocked = CheckPendingState(agent, null, "bash", TruncateCommand(command), auditService, sessionId, registry);
                if (blocked != null) return blocked.Value;
            }
        }

        return ExitCodes.Success;
    }

    private static void HandleClaimSessionStorage(string command, string sessionId, AgentRegistry registry)
    {
        var (isClaim, agentName) = ParseClaimCommand(command);
        if (!isClaim || string.IsNullOrEmpty(agentName)) return;

        if (agentName.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            var human = registry.GetCurrentHuman();
            if (!string.IsNullOrEmpty(human))
            {
                var freeAgents = registry.GetFreeAgentsForHuman(human);
                if (freeAgents.Count > 0)
                    agentName = freeAgents[0].Name;
                else
                    return;
            }
        }
        else if (agentName.Length == 1 && char.IsLetter(agentName[0]))
        {
            var resolved = registry.GetAgentNameFromLetter(agentName[0]);
            if (resolved != null)
                agentName = resolved;
        }

        if (registry.IsValidAgentName(agentName))
            registry.StorePendingSessionId(agentName, sessionId);
    }

    private static int HandleNonDydoBash(
        string command, string? sessionId,
        IOffLimitsService offLimitsService, IBashCommandAnalyzer bashAnalyzer,
        AgentRegistry registry, IAuditService auditService)
    {
        var agent = registry.GetCurrentAgent(sessionId);
        if (agent != null)
        {
            var blocked = NotifyUnreadMessages(agent, null, "bash", command, auditService, sessionId, registry);
            if (blocked != null) return blocked.Value;

            blocked = CheckPendingState(agent, null, "bash", command, auditService, sessionId, registry);
            if (blocked != null) return blocked.Value;

            // Must-read enforcement for write-like operations
            if (agent.UnreadMustReads.Count > 0)
            {
                var preAnalysis = bashAnalyzer.Analyze(command);
                var hasWriteOps = preAnalysis.Operations.Any(op =>
                    op.Type is FileOperationType.Write or FileOperationType.Delete
                    or FileOperationType.Move or FileOperationType.Copy
                    or FileOperationType.PermissionChange);

                if (hasWriteOps)
                {
                    LogAuditEvent(auditService, sessionId, registry, new AuditEvent
                    {
                        EventType = AuditEventType.Blocked, Tool = "bash",
                        Command = TruncateCommand(command), BlockReason = "Must-read files not yet read"
                    });
                    WriteMustReadError(agent);
                    return ExitCodes.ToolError;
                }
            }
        }

        return AnalyzeAndCheckBashOperations(command, sessionId, offLimitsService, bashAnalyzer, registry, auditService);
    }

    private static int AnalyzeAndCheckBashOperations(
        string command, string? sessionId,
        IOffLimitsService offLimitsService, IBashCommandAnalyzer bashAnalyzer,
        AgentRegistry registry, IAuditService auditService)
    {
        var (isDangerous, dangerReason) = bashAnalyzer.CheckDangerousPatterns(command);
        if (isDangerous)
        {
            LogAuditEvent(auditService, sessionId, registry, new AuditEvent
            {
                EventType = AuditEventType.Blocked, Tool = "bash",
                Command = TruncateCommand(command), BlockReason = $"Dangerous pattern: {dangerReason}"
            });
            Console.Error.WriteLine("BLOCKED: Dangerous command pattern detected.");
            Console.Error.WriteLine($"  Reason: {dangerReason}");
            Console.Error.WriteLine($"  Command: {TruncateCommand(command)}");
            return ExitCodes.ToolError;
        }

        var analysis = bashAnalyzer.Analyze(command);

        foreach (var warning in analysis.Warnings)
            Console.Error.WriteLine($"WARNING: {warning}");

        if (analysis.HasDangerousPattern)
        {
            LogAuditEvent(auditService, sessionId, registry, new AuditEvent
            {
                EventType = AuditEventType.Blocked, Tool = "bash",
                Command = TruncateCommand(command), BlockReason = $"Dangerous pattern: {analysis.DangerousPatternReason}"
            });
            Console.Error.WriteLine("BLOCKED: Dangerous command pattern detected.");
            Console.Error.WriteLine($"  Reason: {analysis.DangerousPatternReason}");
            return ExitCodes.ToolError;
        }

        foreach (var op in analysis.Operations)
        {
            var blocked = CheckBashFileOperation(op, command, sessionId, offLimitsService, registry, auditService);
            if (blocked != null) return blocked.Value;
        }

        LogAuditEvent(auditService, sessionId, registry, new AuditEvent
        {
            EventType = AuditEventType.Bash, Tool = "bash", Command = TruncateCommand(command)
        });
        return ExitCodes.Success;
    }

    private static int? CheckBashFileOperation(
        FileOperation op, string command, string? sessionId,
        IOffLimitsService offLimitsService, AgentRegistry registry, IAuditService auditService)
    {
        var offLimitsPattern = offLimitsService.IsPathOffLimits(op.Path);
        if (offLimitsPattern != null)
        {
            LogAuditEvent(auditService, sessionId, registry, new AuditEvent
            {
                EventType = AuditEventType.Blocked, Tool = "bash", Path = op.Path,
                Command = TruncateCommand(command), BlockReason = $"Off-limits: {offLimitsPattern}"
            });
            Console.Error.WriteLine("BLOCKED: Command references off-limits path.");
            Console.Error.WriteLine($"  Path: {op.Path}");
            Console.Error.WriteLine($"  Pattern: {offLimitsPattern}");
            Console.Error.WriteLine($"  Detected: {op.Type} via {op.Command}");
            return ExitCodes.ToolError;
        }

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
                        EventType = AuditEventType.Blocked, Tool = "bash", Path = op.Path,
                        Command = TruncateCommand(command), BlockReason = error
                    });
                    Console.Error.WriteLine($"BLOCKED: {error}");
                    Console.Error.WriteLine($"  Detected {op.Type} operation on: {op.Path}");
                    Console.Error.WriteLine($"  Via command: {op.Command}");
                    return ExitCodes.ToolError;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Check if agent has identity and role. Returns exit code if blocked, null if OK.
    /// </summary>
    private static int? RequireIdentityAndRole(
        AgentState? agent, string? path, string? toolName,
        IAuditService auditService, string? sessionId, IAgentRegistry registry)
    {
        if (agent == null)
        {
            LogAuditEvent(auditService, sessionId, registry, new AuditEvent
            {
                EventType = AuditEventType.Blocked, Path = path, Tool = toolName,
                BlockReason = "No agent identity"
            });
            WriteAccessDeniedError(null, null);
            return ExitCodes.ToolError;
        }
        if (string.IsNullOrEmpty(agent.Role))
        {
            LogAuditEvent(auditService, sessionId, registry, new AuditEvent
            {
                EventType = AuditEventType.Blocked, Path = path, Tool = toolName,
                BlockReason = "No role set"
            });
            WriteAccessDeniedError(agent.Name, null);
            return ExitCodes.ToolError;
        }
        return null;
    }

    private static void WriteAccessDeniedError(string? agentName, string? extra)
    {
        Console.Error.WriteLine("BLOCKED: Read access denied.");
        if (agentName == null)
        {
            Console.Error.WriteLine("  No agent identity assigned to this process.");
            Console.Error.WriteLine("  Read your workflow.md to learn how to onboard:");
            Console.Error.WriteLine("    dydo/agents/*/workflow.md");
            Console.Error.WriteLine("  Then run: dydo agent claim auto");
        }
        else
        {
            Console.Error.WriteLine($"  Agent {agentName} has no role set.");
            Console.Error.WriteLine("  Read your mode files to understand available roles:");
            Console.Error.WriteLine($"    dydo/agents/{agentName}/modes/*.md");
            Console.Error.WriteLine("  Then run: dydo agent role <role>");
        }
    }

    /// <summary>
    /// Notify about unread messages. Returns exit code if blocked, null if OK.
    /// </summary>
    private static int? NotifyUnreadMessages(
        AgentState agent, string? path, string? toolName, string? command,
        IAuditService auditService, string? sessionId, IAgentRegistry registry)
    {
        if (agent.UnreadMessages.Count == 0)
            return null;

        LogAuditEvent(auditService, sessionId, registry, new AuditEvent
        {
            EventType = AuditEventType.Blocked, Path = path, Tool = toolName,
            Command = command != null ? TruncateCommand(command) : null,
            BlockReason = "Unread messages"
        });

        Console.Error.WriteLine($"NOTICE: You have {agent.UnreadMessages.Count} unread message(s).");
        foreach (var msgId in agent.UnreadMessages)
        {
            var msgInfo = FindMessageInfo(registry.GetAgentWorkspace(agent.Name), msgId);
            if (msgInfo != null)
                Console.Error.WriteLine($"  From: {msgInfo.Value.From} | Subject: {msgInfo.Value.Subject ?? "(none)"}");
        }
        Console.Error.WriteLine();
        Console.Error.WriteLine("  Your tool call was valid but paused to deliver this notification.");
        Console.Error.WriteLine("  Read your message(s) to continue:");
        Console.Error.WriteLine("    1. Run: dydo inbox show");
        Console.Error.WriteLine("    2. Read the message file(s) shown");
        Console.Error.WriteLine("    3. Then: dydo inbox clear --id <id>");
        Console.Error.WriteLine();
        Console.Error.WriteLine("  After reading, retry your previous action — it will succeed.");
        return ExitCodes.ToolError;
    }

    /// <summary>
    /// Check for pending wait markers. Returns exit code if blocked, null if OK.
    /// </summary>
    private static int? CheckPendingState(
        AgentState agent, string? path, string? toolName, string? command,
        IAuditService auditService, string? sessionId, AgentRegistry registry)
    {
        var pendingMarkers = SelfHealAndGetPendingMarkers(registry, agent.Name);
        if (pendingMarkers.Count == 0)
            return null;

        LogAuditEvent(auditService, sessionId, registry, new AuditEvent
        {
            EventType = AuditEventType.Blocked, Path = path, Tool = toolName,
            Command = command != null ? TruncateCommand(command) : null,
            BlockReason = "Pending wait markers"
        });

        WritePendingStateBlock(pendingMarkers);
        return ExitCodes.ToolError;
    }

    private static void WriteMustReadError(AgentState agent)
    {
        Console.Error.WriteLine($"BLOCKED: You have not read the required files for the {agent.Role} mode:");
        foreach (var unread in agent.UnreadMustReads)
            Console.Error.WriteLine($"  - {unread}");
        Console.Error.WriteLine("Read these files before performing other operations.");
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
    /// Includes a timeout to prevent indefinite blocking if the pipe stays open.
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
            // Stdin is redirected — read with a timeout to avoid blocking forever
            // if the pipe is open but has no data (e.g., chained commands)
            var readTask = Task.Run(() => Console.In.ReadToEnd());
            if (readTask.Wait(TimeSpan.FromMilliseconds(500)))
            {
                json = readTask.Result;
                return !string.IsNullOrWhiteSpace(json);
            }
            return false; // Timed out — no stdin data available
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

        // After claiming with a role set, block reading other agents' workflows
        if (agent != null && !string.IsNullOrEmpty(agent.Role) && IsOtherAgentWorkflow(filePath, agent.Name))
            return false;

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
    /// Check if a file is another agent's workflow file.
    /// Returns true if the path is a workflow.md but NOT for the specified agent.
    /// </summary>
    private static bool IsOtherAgentWorkflow(string filePath, string agentName)
    {
        var normalizedPath = filePath.Replace('\\', '/');
        if (!Regex.IsMatch(normalizedPath, @"dydo/agents/[^/]+/workflow\.md$", RegexOptions.IgnoreCase))
            return false; // Not a workflow file at all
        // It IS a workflow file — check if it's for a different agent
        return !normalizedPath.Contains($"dydo/agents/{agentName}/", StringComparison.OrdinalIgnoreCase);
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
    /// Check if a command is 'dydo wait' (not --cancel).
    /// </summary>
    private static bool IsDydoWaitCommand(string command)
    {
        if (!Regex.IsMatch(command, @"(?:^|[;&|]\s*)(?:\./)?dydo\s+wait\b", RegexOptions.IgnoreCase))
            return false;

        // Allow 'dydo wait --cancel' and 'dydo wait --task foo --cancel'
        return !Regex.IsMatch(command, @"--cancel\b", RegexOptions.IgnoreCase);
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
    /// Check if a command is 'dydo dispatch' (allowed during pending state).
    /// </summary>
    private static bool IsDydoDispatchCommand(string command)
    {
        return Regex.IsMatch(command,
            @"(?:^|[;&|]\s*)(?:\./)?dydo\s+dispatch\b",
            RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Check if a command is 'dydo wait' in any form (allowed during pending state).
    /// </summary>
    private static bool IsDydoWaitAnyForm(string command)
    {
        return Regex.IsMatch(command,
            @"(?:^|[;&|]\s*)(?:\./)?dydo\s+wait\b",
            RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Self-heal wait markers with dead listener PIDs, then return non-listening markers.
    /// Markers with listening=true but dead PID are flipped to listening=false.
    /// </summary>
    private static List<Models.WaitMarker> SelfHealAndGetPendingMarkers(AgentRegistry registry, string agentName)
    {
        var markers = registry.GetWaitMarkers(agentName);
        foreach (var marker in markers)
        {
            if (!marker.Listening) continue;

            // If PID is null (legacy) or dead, flip to non-listening
            if (marker.Pid == null || !ProcessUtils.IsProcessRunning(marker.Pid.Value))
            {
                registry.ResetWaitMarkerListening(agentName, marker.Task);
            }
        }

        return registry.GetNonListeningWaitMarkers(agentName);
    }

    /// <summary>
    /// Emit the standard pending-state block message to stderr.
    /// </summary>
    private static void WritePendingStateBlock(List<Models.WaitMarker> pendingMarkers)
    {
        var taskNames = string.Join(", ", pendingMarkers.Select(m => m.Task));
        Console.Error.WriteLine($"BLOCKED: Register waits before continuing. Pending: [{taskNames}].");
        Console.Error.WriteLine("  Run: dydo wait --task <name> (in background)");
    }

    // Matches: npx [flags...] dydo [args...]
    // Handles optional flags like -q, --yes, -y, --quiet, --package, etc.
    [GeneratedRegex(@"(?:^|[;&|]\s*)npx\s+(?:(?:-\w+|--[\w-]+(?:\s+\S+)?)\s+)*dydo\b(.*)", RegexOptions.IgnoreCase)]
    private static partial Regex IndirectNpxDydoRegex();

    // Matches: dotnet [tool run] dydo [args...]
    [GeneratedRegex(@"(?:^|[;&|]\s*)dotnet\s+(?:tool\s+run\s+)?dydo\b(.*)", RegexOptions.IgnoreCase)]
    private static partial Regex IndirectDotnetDydoRegex();

    // Matches: dotnet run [flags...] -- <dydo-subcommand> [args...]
    // Only matches when args after -- start with a known dydo subcommand.
    [GeneratedRegex(@"(?:^|[;&|]\s*)dotnet\s+run\b(?:\s+(?:-\w+|--[\w-]+(?:[=\s]\S+)?))*\s+--\s+((?:agent|guard|whoami|dispatch|inbox|message|msg|wait|task|review|clean|workspace|audit|template|init|check|fix|index|graph|completions|complete|version|help|roles|validate|issue|inquisition|watchdog)\b.*)", RegexOptions.IgnoreCase)]
    private static partial Regex IndirectDotnetRunRegex();

    // Matches: bash/sh/zsh/cmd/powershell/pwsh [flags...] dydo [args...]
    // Also matches: bash -c "dydo ...", sh -c 'dydo ...'
    [GeneratedRegex(@"(?:^|[;&|]\s*)(?:bash|sh|zsh|cmd|powershell|pwsh)\s+(?:(?:-\w+|--[\w-]+(?:\s+\S+)?)\s+)*(?:[""'])?dydo\b(.*?)(?:[""'])?$", RegexOptions.IgnoreCase)]
    private static partial Regex IndirectShellDydoRegex();

    /// <summary>
    /// Check if a command invokes dydo indirectly via npx or dotnet.
    /// Returns the invoker name and the args that follow dydo.
    /// </summary>
    private static (bool isIndirect, string? invoker, string? dydoArgs) CheckIndirectDydoInvocation(string command)
    {
        var npxMatch = IndirectNpxDydoRegex().Match(command);
        if (npxMatch.Success)
            return (true, "npx", npxMatch.Groups[1].Value.Trim());

        var dotnetMatch = IndirectDotnetDydoRegex().Match(command);
        if (dotnetMatch.Success)
            return (true, "dotnet", dotnetMatch.Groups[1].Value.Trim());

        var dotnetRunMatch = IndirectDotnetRunRegex().Match(command);
        if (dotnetRunMatch.Success)
            return (true, "dotnet run", dotnetRunMatch.Groups[1].Value.Trim());

        var shellMatch = IndirectShellDydoRegex().Match(command);
        if (shellMatch.Success)
        {
            var shellName = Regex.Match(command, @"(?:bash|sh|zsh|cmd|powershell|pwsh)", RegexOptions.IgnoreCase).Value.ToLowerInvariant();
            return (true, shellName, shellMatch.Groups[1].Value.Trim().TrimEnd('"', '\''));
        }

        return (false, null, null);
    }

    /// <summary>
    /// Check if dydo is available on the system PATH.
    /// </summary>
    private static bool IsDydoOnPath()
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVar))
            return false;

        var separator = OperatingSystem.IsWindows() ? ';' : ':';
        var dirs = pathVar.Split(separator, StringSplitOptions.RemoveEmptyEntries);
        var names = new[] { "dydo", "dydo.exe", "dydo.cmd" };

        foreach (var dir in dirs)
        {
            foreach (var name in names)
            {
                try
                {
                    if (File.Exists(Path.Combine(dir, name)))
                        return true;
                }
                catch
                {
                    // Skip inaccessible directories
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Normalizes a file path for must-read comparison by extracting the project-relative
    /// portion starting from "dydo/".
    /// </summary>
    private static string NormalizeForMustReadComparison(string filePath)
    {
        var normalized = filePath.Replace('\\', '/');
        var dydoIndex = normalized.IndexOf("dydo/", StringComparison.OrdinalIgnoreCase);
        return dydoIndex >= 0 ? normalized[dydoIndex..] : normalized;
    }

    /// <summary>
    /// Extracts from/subject from a message file in an agent's inbox.
    /// </summary>
    private static (string From, string? Subject)? FindMessageInfo(string workspace, string messageId)
    {
        var inboxPath = Path.Combine(workspace, "inbox");
        if (!Directory.Exists(inboxPath))
            return null;

        foreach (var file in Directory.GetFiles(inboxPath, $"{messageId}-msg-*.md"))
        {
            try
            {
                var content = File.ReadAllText(file);
                if (!content.StartsWith("---")) continue;
                var endIndex = content.IndexOf("---", 3);
                if (endIndex < 0) continue;

                var yaml = content[3..endIndex];
                string? from = null, subject = null;

                foreach (var line in yaml.Split('\n'))
                {
                    var colonIndex = line.IndexOf(':');
                    if (colonIndex < 0) continue;
                    var key = line[..colonIndex].Trim();
                    var value = line[(colonIndex + 1)..].Trim();
                    switch (key)
                    {
                        case "from": from = value; break;
                        case "subject": subject = value; break;
                    }
                }

                if (from != null)
                    return (from, subject);
            }
            catch { }
        }

        return null;
    }

    /// <summary>
    /// Extracts message ID from an inbox message file path.
    /// Matches paths like */inbox/{id}-msg-*.md and returns the {id} portion.
    /// </summary>
    private static string? ExtractMessageIdFromPath(string filePath)
    {
        var normalized = filePath.Replace('\\', '/');
        var match = Regex.Match(normalized, @"/inbox/([a-f0-9]+)-msg-[^/]+\.md$", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
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

    /// <summary>
    /// Non-blocking daily validation. Runs on first guard call per 24h period.
    /// Warns about config issues via stderr but never blocks enforcement.
    /// </summary>
    private static void RunDailyValidationIfDue()
    {
        try
        {
            var basePath = Environment.CurrentDirectory;
            var timestampPath = Path.Combine(basePath, "dydo", "_system", ".last-validation");

            if (File.Exists(timestampPath))
            {
                var lastRun = File.GetLastWriteTimeUtc(timestampPath);
                if ((DateTime.UtcNow - lastRun).TotalHours < 24)
                    return;
            }

            var validator = new ValidationService();
            var issues = validator.ValidateSystem(basePath);

            if (issues.Count > 0)
            {
                Console.Error.WriteLine("Daily validation found issues:");
                foreach (var issue in issues)
                    Console.Error.WriteLine($"  [{issue.Severity}] {issue.File}: {issue.Message}");
                Console.Error.WriteLine("Run 'dydo validate' for full report.");
                Console.Error.WriteLine();
            }

            // Update timestamp (create parent dir if needed)
            var dir = Path.GetDirectoryName(timestampPath);
            if (dir != null) Directory.CreateDirectory(dir);
            File.WriteAllText(timestampPath, DateTime.UtcNow.ToString("O"));
        }
        catch
        {
            // Daily validation must never break the guard
        }
    }
}
