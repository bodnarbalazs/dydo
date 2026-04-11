namespace DynaDocs.Commands;

using System.CommandLine;
using System.Security.Cryptography;
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

        command.Subcommands.Add(GuardLiftCommand.CreateLiftCommand());
        command.Subcommands.Add(GuardLiftCommand.CreateRestoreCommand());

        return command;
    }

    // Data-driven lookups to reduce cyclomatic complexity
    private static readonly HashSet<string> SearchTools = new(StringComparer.OrdinalIgnoreCase) { "glob", "grep", "agent" };

    // Claude Code hook response that explicitly approves a tool call, bypassing the permission prompt.
    // Used in worktree contexts where permission patterns fail to match worktree-resolved paths.
    private const string WorktreeAllowJson =
        """{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"allow"}}""";

    private const string WorktreePathMarker = "dydo/_system/.local/worktrees/";

    internal static Func<bool>? IsWorktreeContextOverride;

    internal static bool IsWorktreeContext()
    {
        if (IsWorktreeContextOverride != null)
            return IsWorktreeContextOverride();
        var cwd = Directory.GetCurrentDirectory().Replace('\\', '/');
        return cwd.Contains(WorktreePathMarker);
    }

    private static void EmitWorktreeAllowIfNeeded()
    {
        if (IsWorktreeContext())
            Console.WriteLine(WorktreeAllowJson);
    }
    private static readonly HashSet<string> WriteActions = new(StringComparer.OrdinalIgnoreCase) { "write", "edit", "delete" };
    private static readonly Dictionary<string, AuditEventType> ActionAuditMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["write"] = AuditEventType.Write,
        ["edit"] = AuditEventType.Edit,
        ["delete"] = AuditEventType.Delete,
    };

    private record struct GuardContext(
        string? FilePath, string? Action, string? BashCommand,
        string? ToolName, string? SessionId, string? SearchPath,
        bool? RunInBackground, bool HasCliArgs);

    private static GuardContext ParseInput(string? cliAction, string? cliPath, string? cliCommand)
    {
        var hasCliArgs = cliAction != null || cliPath != null || cliCommand != null;
        string? filePath = null, action = null, bashCommand = null;
        string? toolName = null, sessionId = null, searchPath = null;
        bool? runInBackground = null;

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
                Console.Error.WriteLine($"WARNING: Failed to parse hook input: {ex.Message}");
            }
        }

        return new GuardContext(
            filePath ?? cliPath,
            action ?? cliAction ?? "edit",
            bashCommand ?? cliCommand,
            toolName, sessionId, searchPath, runInBackground, hasCliArgs);
    }

    private static int Execute(string? cliAction, string? cliPath, string? cliCommand)
    {
        var ctx = ParseInput(cliAction, cliPath, cliCommand);

        if (!ctx.HasCliArgs && string.IsNullOrEmpty(ctx.SessionId))
        {
            Console.Error.WriteLine("BLOCKED: No session_id in hook input.");
            return ExitCodes.ToolError;
        }

        var offLimitsService = new OffLimitsService();
        var bashAnalyzer = new BashCommandAnalyzer();
        var registry = new AgentRegistry();
        var auditService = new AuditService();

        RunDailyValidationIfDue();

        var sessionId = ctx.SessionId;
        if (ctx.HasCliArgs && string.IsNullOrEmpty(sessionId))
            sessionId = registry.GetSessionContext();

        var filePath = ResolveWorktreePath(ctx.FilePath);
        var action = ctx.Action;
        var bashCommand = ctx.BashCommand;
        var toolName = ctx.ToolName;
        var searchPath = ResolveWorktreePath(ctx.SearchPath);
        var runInBackground = ctx.RunInBackground;

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
        if (toolName != null && SearchTools.Contains(toolName))
        {
            return HandleSearchTool(searchPath, toolName, sessionId, offLimitsService, registry, auditService);
        }

        // ============================================================
        // SECURITY LAYER 2.6: Blocked tools (plan mode)
        // Dydo agents must not use Claude Code's built-in plan mode.
        // ============================================================
        if (toolName == "enterplanmode" || toolName == "exitplanmode")
        {
            LogAuditEvent(auditService, sessionId, registry, new AuditEvent
            {
                EventType = AuditEventType.Blocked, Tool = toolName,
                BlockReason = "Built-in plan mode is not allowed for dydo agents"
            });
            Console.Error.WriteLine("BLOCKED: Dydo agents don't use Claude Code's built-in plan mode.");
            Console.Error.WriteLine("  To plan: switch to planner role ('dydo agent role planner --task <name>')");
            Console.Error.WriteLine("  For working notes: write to your workspace (dydo/agents/<you>/notes-<topic>.md)");
            return ExitCodes.ToolError;
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

        var identityBlock = RequireWriteIdentity(agent, filePath, toolName, auditService, sessionId, registry);
        if (identityBlock != null) return identityBlock.Value;

        // Re-read agent once after identity check (identity check may have modified state)
        agent = registry.GetCurrentAgent(sessionId);
        if (agent != null)
        {
            var blocked = NotifyUnreadMessages(agent, filePath, toolName, null, auditService, sessionId, registry);
            if (blocked != null) return blocked.Value;

            blocked = CheckPendingState(agent, filePath, toolName, null, auditService, sessionId, registry);
            if (blocked != null) return blocked.Value;
        }

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

        // Guard lift: skip RBAC if lifted
        if (agent != null && IsGuardLifted(agent.Name))
        {
            var liftedEventType = ActionAuditMap.GetValueOrDefault(action ?? "", AuditEventType.Edit);
            LogAuditEvent(auditService, sessionId, registry, new AuditEvent
            {
                EventType = liftedEventType, Path = filePath, Tool = toolName, Lifted = true
            });
            EmitWorktreeAllowIfNeeded();
            return ExitCodes.Success;
        }

        // Check role permissions
        if (action != null && WriteActions.Contains(action))
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

        var eventType = ActionAuditMap.GetValueOrDefault(action ?? "", AuditEventType.Edit);
        LogAuditEvent(auditService, sessionId, registry, new AuditEvent
        {
            EventType = eventType, Path = filePath, Tool = toolName
        });

        EmitWorktreeAllowIfNeeded();

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

    internal static bool ShouldBypassOffLimits(string filePath, AgentState? agent)
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

        TrackReadCompletion(agent, filePath, sessionId, registry);

        EmitWorktreeAllowIfNeeded();

        return ExitCodes.Success;
    }

    private static void TrackReadCompletion(AgentState? agent, string? filePath, string? sessionId, AgentRegistry registry)
    {
        if (agent == null || string.IsNullOrEmpty(filePath))
            return;

        // Track must-read completion
        if (agent.UnreadMustReads.Count > 0)
        {
            var relPath = NormalizeForMustReadComparison(filePath);
            if (agent.UnreadMustReads.Any(p => p.Equals(relPath, StringComparison.OrdinalIgnoreCase)))
                registry.MarkMustReadComplete(sessionId, relPath);
        }

        // Track message reads
        if (agent.UnreadMessages.Count > 0)
        {
            var messageId = ExtractMessageIdFromPath(filePath);
            if (messageId != null && agent.UnreadMessages.Contains(messageId))
                registry.MarkMessageRead(sessionId, messageId);
        }
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
        // Handle dydo commands first — they have their own safety checks and shouldn't be
        // subject to nudge pattern matching on their argument text (fixes false positives)
        if (IsDydoCommand(command) && !string.IsNullOrEmpty(sessionId))
            return HandleDydoBashCommand(command, sessionId, registry, auditService, runInBackground);

        // Hardcoded dangerous patterns — security checks before configurable nudges
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

        // Configurable nudges — after hardcoded security checks
        var nudged = CheckNudges(command, sessionId, registry, auditService);
        if (nudged != null) return nudged.Value;

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

    internal static int? CheckNudges(string command, string? sessionId, AgentRegistry registry, IAuditService auditService)
    {
        // Always include block-severity default nudges (H19/H20) even if removed from config.
        // These are security-critical and must not be removable via dydo.json editing.
        var nudges = MergeSystemNudges(registry.Config?.Nudges);
        if (nudges.Count == 0)
            return null;

        foreach (var nudge in nudges)
        {
            Regex regex;
            try { regex = new Regex(nudge.Pattern, RegexOptions.IgnoreCase); }
            catch { continue; }

            var match = regex.Match(command);
            if (!match.Success) continue;

            var message = nudge.Message;
            for (int i = 1; i < match.Groups.Count; i++)
                message = message.Replace($"${i}", match.Groups[i].Value.Trim());

            if (string.Equals(nudge.Severity, "warn", StringComparison.OrdinalIgnoreCase))
            {
                var agent = registry.GetCurrentAgent(sessionId);
                if (agent == null) continue;

                var hash = ComputeNudgeHash(nudge.Pattern);
                var workspace = registry.GetAgentWorkspace(agent.Name);
                var markerPath = Path.Combine(workspace, $".nudge-{hash}");

                if (!File.Exists(markerPath))
                {
                    File.WriteAllText(markerPath, DateTime.UtcNow.ToString("o"));
                    LogAuditEvent(auditService, sessionId, registry, new AuditEvent
                    {
                        EventType = AuditEventType.Blocked, Tool = "bash",
                        Command = TruncateCommand(command),
                        BlockReason = $"Nudge (warn): {message}"
                    });
                    Console.Error.WriteLine($"BLOCKED: {message}");
                    Console.Error.WriteLine("  (Run the same command again to proceed anyway.)");
                    return ExitCodes.ToolError;
                }

                File.Delete(markerPath);
                continue;
            }

            // Block severity: always block
            LogAuditEvent(auditService, sessionId, registry, new AuditEvent
            {
                EventType = AuditEventType.Blocked, Tool = "bash",
                Command = TruncateCommand(command),
                BlockReason = $"Nudge (block): {message}"
            });
            Console.Error.WriteLine($"BLOCKED: {message}");
            return ExitCodes.ToolError;
        }

        return null;
    }

    internal static string ComputeNudgeHash(string pattern)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(pattern));
        return Convert.ToHexString(bytes)[..8].ToLowerInvariant();
    }

    /// <summary>
    /// Merge block-severity default nudges into the config nudges list.
    /// Ensures security-critical nudges (H19 indirect invocation, H20 worktree lifecycle)
    /// are always enforced even if removed from dydo.json.
    /// </summary>
    internal static List<NudgeConfig> MergeSystemNudges(List<NudgeConfig>? configNudges)
    {
        var nudges = configNudges?.ToList() ?? [];
        var existingPatterns = new HashSet<string>(nudges.Select(n => n.Pattern));

        foreach (var defaultNudge in ConfigFactory.DefaultNudges)
        {
            if (string.Equals(defaultNudge.Severity, "block", StringComparison.OrdinalIgnoreCase)
                && !existingPatterns.Contains(defaultNudge.Pattern))
            {
                nudges.Add(defaultNudge);
            }
        }

        return nudges;
    }

    private static int HandleDydoBashCommand(string command, string sessionId, AgentRegistry registry, IAuditService auditService, bool? runInBackground)
    {
        // Phase 1: store session ID without agent name (backwards-compatible)
        registry.StoreSessionContext(sessionId);

        HandleClaimSessionStorage(command, sessionId, registry);

        var agent = registry.GetCurrentAgent(sessionId);

        if (IsHumanOnlyDydoCommand(command))
        {
            if (agent != null)
            {
                LogAuditEvent(auditService, sessionId, registry, new AuditEvent
                {
                    EventType = AuditEventType.Blocked, Tool = "bash",
                    Command = TruncateCommand(command),
                    BlockReason = "Human-only command"
                });
                Console.Error.WriteLine("BLOCKED: This command is human-only. Agents cannot run it.");
                return ExitCodes.ToolError;
            }
        }

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
            if (agent != null)
            {
                var blocked = CheckPendingState(agent, null, "bash", TruncateCommand(command), auditService, sessionId, registry);
                if (blocked != null) return blocked.Value;
            }
        }

        // Phase 2: enrich session context with agent name for race detection.
        if (agent != null)
            registry.StoreSessionContext(sessionId, agent.Name);

        EmitWorktreeAllowIfNeeded();

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

                // git merge is implicitly write-like (modifies working tree + refs)
                var isGitMerge = GitMergeRegex().IsMatch(command);

                if (hasWriteOps || isGitMerge)
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

        return AnalyzeAndCheckBashOperations(command, sessionId, agent, offLimitsService, bashAnalyzer, registry, auditService);
    }

    private static int AnalyzeAndCheckBashOperations(
        string command, string? sessionId, AgentState? agent,
        IOffLimitsService offLimitsService, IBashCommandAnalyzer bashAnalyzer,
        AgentRegistry registry, IAuditService auditService)
    {
        // git stash is only safe in worktrees (isolated stash stack); block otherwise
        if (GitStashRegex().IsMatch(command))
        {
            if (agent == null || registry.GetWorktreeId(agent.Name) == null)
            {
                const string reason = "git stash is unsafe in multi-agent environments. "
                    + "Stashes are a global stack -- other agents' stash operations will interfere. "
                    + "Commit your changes instead.";
                LogAuditEvent(auditService, sessionId, registry, new AuditEvent
                {
                    EventType = AuditEventType.Blocked, Tool = "bash",
                    Command = TruncateCommand(command), BlockReason = reason
                });
                Console.Error.WriteLine($"BLOCKED: {reason}");
                return ExitCodes.ToolError;
            }
        }

        // git merge must go through dydo worktree merge
        if (GitMergeRegex().IsMatch(command))
        {
            if (agent != null)
            {
                var inWorktree = registry.GetWorktreeId(agent.Name) != null;
                var hasMergeSource = File.Exists(
                    Path.Combine(registry.GetAgentWorkspace(agent.Name), ".merge-source"));

                if (inWorktree || hasMergeSource)
                {
                    const string reason = "Use dydo worktree merge to merge worktree branches. "
                        + "Do not use git merge directly.";
                    LogAuditEvent(auditService, sessionId, registry, new AuditEvent
                    {
                        EventType = AuditEventType.Blocked, Tool = "bash",
                        Command = TruncateCommand(command), BlockReason = reason
                    });
                    Console.Error.WriteLine($"BLOCKED: {reason}");
                    return ExitCodes.ToolError;
                }
            }
        }

        var analysis = bashAnalyzer.Analyze(command);

        foreach (var warning in analysis.Warnings)
            Console.Error.WriteLine($"WARNING: {warning}");

        // Block write/delete operations when bypass attempts make analysis unreliable
        if (analysis.HasBypassAttempt && analysis.Operations.Any(op =>
            op.Type is FileOperationType.Write or FileOperationType.Delete
            or FileOperationType.Move or FileOperationType.Copy
            or FileOperationType.PermissionChange))
        {
            LogAuditEvent(auditService, sessionId, registry, new AuditEvent
            {
                EventType = AuditEventType.Blocked, Tool = "bash",
                Command = TruncateCommand(command),
                BlockReason = "Write with bypass attempt (command substitution/variable expansion)"
            });
            Console.Error.WriteLine("BLOCKED: Command contains bypass patterns (command substitution or variable expansion) "
                + "that make file operation analysis unreliable.");
            Console.Error.WriteLine("  Write operations cannot be verified. Use literal paths instead.");
            return ExitCodes.ToolError;
        }

        foreach (var op in analysis.Operations)
        {
            var blocked = CheckBashFileOperation(op, command, sessionId, offLimitsService, registry, auditService, agent);
            if (blocked != null) return blocked.Value;
        }

        LogAuditEvent(auditService, sessionId, registry, new AuditEvent
        {
            EventType = AuditEventType.Bash, Tool = "bash", Command = TruncateCommand(command)
        });

        EmitWorktreeAllowIfNeeded();

        return ExitCodes.Success;
    }

    internal static int? CheckBashFileOperation(
        FileOperation op, string command, string? sessionId,
        IOffLimitsService offLimitsService, AgentRegistry registry, IAuditService auditService,
        AgentState? cachedAgent = null)
    {
        // For reads, apply the same bootstrap/mode file bypass as direct reads (#60)
        var agent = cachedAgent ?? registry.GetCurrentAgent(sessionId);
        var skipOffLimits = op.Type is FileOperationType.Read && ShouldBypassOffLimits(op.Path, agent);

        var offLimitsPattern = skipOffLimits ? null : offLimitsService.IsPathOffLimits(op.Path);
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

        // Staged access control for read operations (mirrors HandleReadOperation)
        if (op.Type is FileOperationType.Read)
        {
            if (!IsReadAllowed(op.Path, agent))
            {
                LogAuditEvent(auditService, sessionId, registry, new AuditEvent
                {
                    EventType = AuditEventType.Blocked, Tool = "bash", Path = op.Path,
                    Command = TruncateCommand(command),
                    BlockReason = agent == null ? "No agent identity" : "No role set"
                });
                WriteAccessDeniedError(agent?.Name, null);
                return ExitCodes.ToolError;
            }
        }

        return CheckBashWriteRbac(op, command, agent, sessionId, registry, auditService);
    }

    private static int? CheckBashWriteRbac(
        FileOperation op, string command, AgentState? agent,
        string? sessionId, AgentRegistry registry, IAuditService auditService)
    {
        if (op.Type is not (FileOperationType.Write or FileOperationType.Delete
            or FileOperationType.Move or FileOperationType.Copy
            or FileOperationType.PermissionChange))
            return null;

        if (agent == null || string.IsNullOrEmpty(agent.Role))
            return null;

        if (IsGuardLifted(agent.Name))
            return null;

        var actionName = op.Type.ToString().ToLowerInvariant();
        if (registry.IsPathAllowed(sessionId, op.Path, actionName, out var error))
            return null;

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

    private static int? RequireWriteIdentity(
        AgentState? agent, string? filePath, string? toolName,
        IAuditService auditService, string? sessionId, IAgentRegistry registry)
    {
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
            {
                var displayPath = Path.GetRelativePath(Directory.GetCurrentDirectory(), msgInfo.Value.FilePath)
                    .Replace('\\', '/');
                Console.Error.WriteLine($"  From: {msgInfo.Value.From} | Subject: {msgInfo.Value.Subject ?? "(none)"}");
                Console.Error.WriteLine($"  File: {displayPath}");
            }
        }
        Console.Error.WriteLine();
        Console.Error.WriteLine("  Your tool call was valid but paused to deliver this notification.");
        Console.Error.WriteLine("  Read your message(s) and then clear them to continue:");
        Console.Error.WriteLine("    1. Read each file listed above");
        Console.Error.WriteLine("    2. Then: dydo inbox clear --id <id>");
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
    /// Resolves a path for worktree-aware guard checks.
    /// When CWD is inside a worktree, converts relative paths to absolute first
    /// (so ../../../ chains resolve correctly), then normalizes to main project paths.
    /// In non-worktree contexts, paths pass through unchanged.
    /// </summary>
    internal static string? ResolveWorktreePath(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        var resolved = path;

        // Only resolve relative paths to absolute when CWD is inside a worktree
        if (!Path.IsPathRooted(resolved) && PathUtils.GetMainProjectRoot(Environment.CurrentDirectory) != null)
            resolved = Path.GetFullPath(resolved);

        return PathUtils.NormalizeWorktreePath(resolved) ?? resolved;
    }

    /// <summary>
    /// Truncate a command for display in error messages.
    /// </summary>
    internal static string TruncateCommand(string command)
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
    internal static bool IsReadAllowed(string? filePath, AgentState? agent)
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
    internal static bool IsBootstrapFile(string filePath)
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
        if (AgentWorkflowRegex().IsMatch(normalizedPath))
            return true;

        return false;
    }

    /// <summary>
    /// Check if a file is a mode file for the specified agent.
    /// Mode files: dydo/agents/{agentName}/modes/*.md
    /// </summary>
    internal static bool IsModeFile(string filePath, string agentName)
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
    internal static bool IsOtherAgentWorkflow(string filePath, string agentName)
    {
        var normalizedPath = filePath.Replace('\\', '/');
        if (!AgentWorkflowRegex().IsMatch(normalizedPath))
            return false; // Not a workflow file at all
        // It IS a workflow file — check if it's for a different agent
        return !normalizedPath.Contains($"dydo/agents/{agentName}/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Check if a file is any agent's mode file.
    /// Mode files: dydo/agents/*/modes/*.md
    /// </summary>
    internal static bool IsAnyModeFile(string filePath)
    {
        // Normalize path separators for consistent matching
        var normalizedPath = filePath.Replace('\\', '/');

        // dydo/agents/*/modes/*.md
        return AgentModeFileRegex().IsMatch(normalizedPath);
    }

    /// <summary>
    /// Parse a bash command to detect dydo agent claim commands.
    /// Returns (isClaim, agentName) where agentName may be "auto" or a specific agent name.
    /// </summary>
    internal static (bool isClaim, string? agentName) ParseClaimCommand(string command)
    {
        // Match: dydo agent claim <name> or ./dydo agent claim <name>
        // Account for command chaining with ; && ||
        var match = DydoClaimCommandRegex().Match(command);

        return match.Success ? (true, match.Groups[1].Value) : (false, null);
    }

    /// <summary>
    /// Check if a command is 'dydo wait' (not --cancel).
    /// </summary>
    internal static bool IsDydoWaitCommand(string command)
    {
        if (!DydoWaitCommandRegex().IsMatch(command))
            return false;

        // Allow 'dydo wait --cancel' and 'dydo wait --task foo --cancel'
        return !CancelFlagRegex().IsMatch(command);
    }

    /// <summary>
    /// Check if a command is a dydo command.
    /// </summary>
    internal static bool IsDydoCommand(string command)
    {
        return DydoCommandRegex().IsMatch(command);
    }

    /// <summary>
    /// Check if a command is 'dydo dispatch' (allowed during pending state).
    /// </summary>
    private static bool IsDydoDispatchCommand(string command)
    {
        return DydoDispatchCommandRegex().IsMatch(command);
    }

    /// <summary>
    /// Check if a command is a human-only dydo command (task approve/reject, roles reset, guard lift/restore).
    /// </summary>
    internal static bool IsHumanOnlyDydoCommand(string command)
    {
        return HumanOnlyDydoCommandRegex().IsMatch(command);
    }

    /// <summary>
    /// Check if a command is 'dydo wait' in any form (allowed during pending state).
    /// </summary>
    private static bool IsDydoWaitAnyForm(string command)
    {
        return DydoWaitCommandRegex().IsMatch(command);
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

    // Matches git stash and all variants (pop, push, apply, drop, list, show, save, etc.)
    [GeneratedRegex(@"(?:^|\s|;|&&|\|\|)git\s+stash(?:\s|$|;|&&|\|\|)", RegexOptions.IgnoreCase)]
    private static partial Regex GitStashRegex();

    [GeneratedRegex(@"(?:^|\s|;|&&|\|\|)git\s+merge(?:\s|$|;|&&|\|\|)", RegexOptions.IgnoreCase)]
    private static partial Regex GitMergeRegex();

    // Matches human-only dydo subcommands: task approve, task reject, roles reset, guard lift, guard restore
    [GeneratedRegex(@"(?:^|[;&|]\s*)(?:\./)?dydo\s+(?:task\s+(?:approve|reject)|roles\s+reset|guard\s+(?:lift|restore))\b", RegexOptions.IgnoreCase)]
    private static partial Regex HumanOnlyDydoCommandRegex();

    [GeneratedRegex(@"dydo/agents/[^/]+/workflow\.md$", RegexOptions.IgnoreCase)]
    private static partial Regex AgentWorkflowRegex();

    [GeneratedRegex(@"dydo/agents/[^/]+/modes/[^/]+\.md$", RegexOptions.IgnoreCase)]
    private static partial Regex AgentModeFileRegex();

    [GeneratedRegex(@"(?:^|[;&|]\s*)(?:\./)?dydo\s+agent\s+claim\s+(\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex DydoClaimCommandRegex();

    [GeneratedRegex(@"(?:^|[;&|]\s*)(?:\./)?dydo\s+wait\b", RegexOptions.IgnoreCase)]
    private static partial Regex DydoWaitCommandRegex();

    [GeneratedRegex(@"--cancel\b", RegexOptions.IgnoreCase)]
    private static partial Regex CancelFlagRegex();

    [GeneratedRegex(@"(?:^|[;&|]\s*)(?:\./)?dydo\s", RegexOptions.IgnoreCase)]
    private static partial Regex DydoCommandRegex();

    [GeneratedRegex(@"(?:^|[;&|]\s*)(?:\./)?dydo\s+dispatch\b", RegexOptions.IgnoreCase)]
    private static partial Regex DydoDispatchCommandRegex();

    [GeneratedRegex(@"/inbox/([a-f0-9]+)-msg-[^/]+\.md$", RegexOptions.IgnoreCase)]
    private static partial Regex InboxMessageIdRegex();

    /// <summary>
    /// Normalizes a file path for must-read comparison by extracting the project-relative
    /// portion starting from "dydo/".
    /// </summary>
    internal static string NormalizeForMustReadComparison(string filePath)
    {
        var normalized = PathUtils.NormalizeWorktreePath(filePath)?.Replace('\\', '/') ?? filePath.Replace('\\', '/');
        var dydoIndex = normalized.IndexOf("dydo/", StringComparison.OrdinalIgnoreCase);
        return dydoIndex >= 0 ? normalized[dydoIndex..] : normalized;
    }

    /// <summary>
    /// Extracts from/subject from a message file in an agent's inbox.
    /// </summary>
    internal static (string From, string? Subject, string FilePath)? FindMessageInfo(string workspace, string messageId)
    {
        var inboxPath = Path.Combine(workspace, "inbox");
        if (!Directory.Exists(inboxPath))
            return null;

        foreach (var file in Directory.GetFiles(inboxPath, $"{messageId}-msg-*.md"))
        {
            try
            {
                var content = File.ReadAllText(file);
                var fields = FrontmatterParser.ParseFields(content);
                if (fields == null) continue;

                fields.TryGetValue("from", out var from);
                fields.TryGetValue("subject", out var subject);

                if (from != null)
                    return (from, subject, file);
            }
            catch { }
        }

        return null;
    }

    /// <summary>
    /// Extracts message ID from an inbox message file path.
    /// Matches paths like */inbox/{id}-msg-*.md and returns the {id} portion.
    /// </summary>
    internal static string? ExtractMessageIdFromPath(string filePath)
    {
        var normalized = filePath.Replace('\\', '/');
        var match = InboxMessageIdRegex().Match(normalized);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Helper to log an audit event with proper error handling.
    /// </summary>
    internal static void LogAuditEvent(
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

    private static bool IsGuardLifted(string agentName)
    {
        var service = new GuardLiftService();
        return service.IsLifted(agentName);
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
            var timestampPath = Path.Combine(basePath, "dydo", "_system", ".local", "last-validation");

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

            // Ensure .local/ dir exists (absent in worktrees)
            PathUtils.EnsureLocalDirExists(Path.Combine(basePath, "dydo"));
            File.WriteAllText(timestampPath, DateTime.UtcNow.ToString("O"));
        }
        catch
        {
            // Daily validation must never break the guard
        }
    }
}
