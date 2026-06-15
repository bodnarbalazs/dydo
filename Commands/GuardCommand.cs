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
    private static readonly HashSet<string> ShellTools = new(StringComparer.OrdinalIgnoreCase) { "bash", "powershell" };

    private static bool ShouldRouteToShellHandler(string? toolName, string? bashCommand)
    {
        if (toolName == null) return false;
        if (!ShellTools.Contains(toolName)) return false;
        return !string.IsNullOrEmpty(bashCommand);
    }

    // Claude Code hook response that explicitly approves a tool call, bypassing the permission prompt.
    // Used in worktree contexts where permission patterns fail to match worktree-resolved paths.
    private const string WorktreeAllowJson =
        """{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"allow"}}""";

    private static readonly string[] WorktreePathSegments = ["dydo", "_system", ".local", "worktrees"];

    internal static Func<bool>? IsWorktreeContextOverride;

    internal static bool IsWorktreeContext()
    {
        if (IsWorktreeContextOverride != null)
            return IsWorktreeContextOverride();
        var segments = Directory.GetCurrentDirectory()
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        // Require the exact sequence "dydo/_system/.local/worktrees/" plus a worktree id after it.
        // An unanchored substring match would accept sibling paths like "worktrees-notes" or
        // "worktrees.backup" — treat those as non-worktree contexts.
        for (var i = 0; i + WorktreePathSegments.Length < segments.Length; i++)
        {
            var match = true;
            for (var j = 0; j < WorktreePathSegments.Length; j++)
            {
                if (!segments[i + j].Equals(WorktreePathSegments[j], StringComparison.OrdinalIgnoreCase))
                {
                    match = false;
                    break;
                }
            }
            if (match) return true;
        }
        return false;
    }

    private static void EmitWorktreeAllowIfNeeded()
    {
        if (IsWorktreeContext())
            Console.WriteLine(WorktreeAllowJson);
    }
    private static readonly HashSet<string> WriteActions = new(StringComparer.OrdinalIgnoreCase) { "write", "edit", "delete" };

    private record struct GuardContext(
        string? FilePath, string? Action, string? BashCommand,
        string? ToolName, string? SessionId, string? SearchPath,
        bool? RunInBackground, bool HasCliArgs,
        string? AgentId, string? AgentType);

    private static GuardContext ParseInput(string? cliAction, string? cliPath, string? cliCommand)
    {
        var hasCliArgs = cliAction != null || cliPath != null || cliCommand != null;
        string? filePath = null, action = null, bashCommand = null;
        string? toolName = null, sessionId = null, searchPath = null;
        string? agentId = null, agentType = null;
        bool? runInBackground = null;

        if (!hasCliArgs && TryReadStdinJson(out var json) && json != null)
        {
            try
            {
                var hookInput = JsonSerializer.Deserialize(json, DydoDefaultJsonContext.Default.HookInput);
                if (hookInput != null)
                {
                    sessionId = hookInput.SessionId;
                    agentId = hookInput.AgentId;
                    agentType = hookInput.AgentType;
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
            toolName, sessionId, searchPath, runInBackground, hasCliArgs,
            agentId, agentType);
    }

    private static int Execute(string? cliAction, string? cliPath, string? cliCommand)
    {
        var ctx = ParseInput(cliAction, cliPath, cliCommand);

        if (!ctx.HasCliArgs && string.IsNullOrEmpty(ctx.SessionId))
        {
            Console.Error.WriteLine("BLOCKED: No session_id in hook input.");
            return ExitCodes.ToolError;
        }

        // Init/config load fails CLOSED: a guard that can't load its own rules must not
        // wave tool calls through. Loading off-limits patterns and the registry happens
        // here, outside the fail-open boundary below.
        OffLimitsService offLimitsService;
        AgentRegistry registry;
        try
        {
            offLimitsService = new OffLimitsService();
            offLimitsService.LoadPatterns();
            registry = new AgentRegistry();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"BLOCKED: dydo guard could not initialize ({ex.Message}).");
            return ExitCodes.ToolError;
        }

        // Decision logic fails OPEN: an unexpected fault evaluating one call must not
        // brick the agent on every subsequent tool. Deliberate blocks are returns, not throws.
        try
        {
            return Decide(ctx, offLimitsService, registry);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"WARNING: dydo guard internal error, allowing tool call: {ex.Message}");
            return ExitCodes.Success;
        }
    }

    private static int Decide(GuardContext ctx, OffLimitsService offLimitsService, AgentRegistry registry)
    {
        var bashAnalyzer = new BashCommandAnalyzer();

        RunDailyValidationIfDue();

        var sessionId = ctx.SessionId;
        if (ctx.HasCliArgs && string.IsNullOrEmpty(sessionId))
            sessionId = registry.GetSessionContext();

        // #0207 part 2: on a resumed claude session's first guarded tool call, rewrite
        // .session.ClaimedPid from the dead pre-resume PID to the live claude ancestor,
        // reset resume bookkeeping (#0153), and emit the recovery_kind=auto Claim event +
        // resume_outcome=succeeded log. All gates inside the method — every non-resume call
        // exits cheaply at step 4 (IsProcessRunning on the live ClaimedPid). Placed before
        // Security Layer 1 so it covers every tool type uniformly and runs even when the
        // triggering call is itself blocked downstream.
        registry.RefreshResumedAgentSession(sessionId);

        var filePath = ResolveWorktreePath(ctx.FilePath);
        var action = ctx.Action;
        var bashCommand = ctx.BashCommand;
        var toolName = ctx.ToolName;
        var searchPath = ResolveWorktreePath(ctx.SearchPath);
        var runInBackground = ctx.RunInBackground;

        // ============================================================
        // TIER-2 WORKER LANE (Decision 024): calls carrying agent_id come from
        // sub-agents / workflow workers. Workers are anonymous — no claim, no role
        // state, no staged onboarding, no must-reads. Only the universal layers
        // apply: off-limits, dangerous-bash patterns, nudges, and the shared bash
        // safety checks (git stash/merge, dydo-command handling).
        // ============================================================
        if (!ctx.HasCliArgs && !string.IsNullOrEmpty(ctx.AgentId))
        {
            return HandleWorkerCall(ctx, filePath, searchPath, runInBackground, offLimitsService, bashAnalyzer, registry);
        }

        // Native auto-memory (~/.claude/projects/*/memory/) is always accessible —
        // it lives outside the repo and outside dydo's jurisdiction (Decision 024 §5).
        if (!string.IsNullOrEmpty(filePath) && IsNativeMemoryPath(filePath))
        {
            EmitWorktreeAllowIfNeeded();
            return ExitCodes.Success;
        }

        var routed = RouteToolLayers(
            filePath, action, bashCommand, toolName, searchPath, runInBackground,
            sessionId, offLimitsService, bashAnalyzer, registry);
        if (routed != null) return routed.Value;

        // ============================================================
        // SECURITY LAYER 3: Staged access control
        // ============================================================

        // Get current agent (may be null if not claimed)
        var agent = registry.GetCurrentAgent(sessionId);

        // For Read operations, apply staged access control
        if (action == "read" && string.IsNullOrEmpty(bashCommand))
        {
            return HandleReadOperation(filePath, toolName, agent, sessionId, registry);
        }

        // For write/edit operations
        return HandleWriteOperation(filePath, action, toolName, agent, sessionId, registry);
    }

    /// <summary>
    /// Security layers 1–2.6: off-limits on direct file paths, Bash routing,
    /// search-tool gating, and plan-mode blocking. Returns an exit code when the
    /// call was fully handled, null to fall through to staged access control.
    /// </summary>
    private static int? RouteToolLayers(
        string? filePath, string? action, string? bashCommand, string? toolName,
        string? searchPath, bool? runInBackground, string? sessionId,
        IOffLimitsService offLimitsService, IBashCommandAnalyzer bashAnalyzer,
        AgentRegistry registry)
    {
        // SECURITY LAYER 1: off-limits patterns for direct file operations.
        // For reads, bootstrap/mode files bypass off-limits based on staged access.
        if (!string.IsNullOrEmpty(filePath))
        {
            var blocked = CheckDirectFileOffLimits(filePath, action, toolName, sessionId, offLimitsService, registry);
            if (blocked != null) return blocked.Value;
        }

        // SECURITY LAYER 2: Bash tool
        if (ShouldRouteToShellHandler(toolName, bashCommand))
        {
            return HandleBashCommand(bashCommand!, sessionId, offLimitsService, bashAnalyzer, registry, runInBackground);
        }

        // SECURITY LAYER 2.5: Search tools (Glob/Grep) and Agent tool — require
        // Stage 2 (identity + role) because they scan broadly across directories.
        if (toolName != null && SearchTools.Contains(toolName))
        {
            return HandleSearchTool(searchPath, toolName, sessionId, offLimitsService, registry);
        }

        // SECURITY LAYER 2.6: Dydo agents must not use Claude Code's built-in plan mode.
        if (toolName == "enterplanmode" || toolName == "exitplanmode")
        {
            Console.Error.WriteLine("BLOCKED: Dydo agents don't use Claude Code's built-in plan mode.");
            Console.Error.WriteLine("  To plan: write a plan to your workspace (dydo/agents/<you>/plan-<topic>.md), applying the planner skill.");
            Console.Error.WriteLine("  For working notes: write to your workspace (dydo/agents/<you>/notes-<topic>.md)");
            return ExitCodes.ToolError;
        }

        return null;
    }

    private static int HandleWriteOperation(
        string? filePath, string? action, string? toolName, AgentState? agent,
        string? sessionId, AgentRegistry registry)
    {
        if (string.IsNullOrEmpty(filePath))
            return ExitCodes.Success;

        var identityBlock = RequireWriteIdentity(agent, filePath, toolName, sessionId, registry);
        if (identityBlock != null) return identityBlock.Value;

        // Re-read agent once after identity check (identity check may have modified state)
        agent = registry.GetCurrentAgent(sessionId);
        if (agent != null)
        {
            var blocked = NotifyUnreadMessages(agent, filePath, toolName, null, sessionId, registry);
            if (blocked != null) return blocked.Value;

            blocked = CheckPendingState(agent, filePath, toolName, null, sessionId, registry);
            if (blocked != null) return blocked.Value;
        }

        if (agent != null && agent.UnreadMustReads.Count > 0)
        {
            WriteMustReadError(agent);
            return ExitCodes.ToolError;
        }

        // Per-role path RBAC removed (Decision 024 §2): off-limits + nudges are the
        // universal enforcement; coarse scope belongs to native permission profiles.
        // The one identity-scoped exception that survives: no cross-agent workspace writes.
        var crossAgent = BlockIfCrossAgentWorkspace(filePath, agent, toolName, null, sessionId, registry);
        if (crossAgent != null) return crossAgent.Value;

        EmitWorktreeAllowIfNeeded();

        return ExitCodes.Success;
    }

    /// <summary>
    /// Native auto-memory paths (~/.claude/projects/&lt;project&gt;/memory/) are outside the
    /// repo and outside dydo's jurisdiction — always readable and writable. Anchored to
    /// the real user profile and requires 'memory' to be the immediate child of the
    /// project directory, so neither a repo-internal lookalike nor a '..' escape qualifies.
    /// </summary>
    internal static bool IsNativeMemoryPath(string filePath)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).Replace('\\', '/').TrimEnd('/');
        if (string.IsNullOrEmpty(home))
            return false;

        var normalized = PathUtils.CollapseRelativeSegments(filePath);
        var root = $"{home}/.claude/projects/";
        if (!normalized.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            return false;

        var afterProject = normalized[root.Length..];
        var slash = afterProject.IndexOf('/');
        if (slash < 0)
            return false;

        var rest = afterProject[(slash + 1)..];
        return rest.Equals("memory", StringComparison.OrdinalIgnoreCase)
            || rest.StartsWith("memory/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Tier-2 worker lane: sub-agent / workflow-worker tool calls (agent_id present).
    /// Workers are anonymous (no claim/role/onboarding). Bash reuses the shared
    /// pipeline (dangerous patterns, nudges, git-safety, off-limits) minus the Tier-1
    /// identity gates; direct file ops get the universal off-limits check (native
    /// memory exempt). RBAC and must-reads do not apply.
    /// </summary>
    private static int HandleWorkerCall(
        GuardContext ctx, string? filePath, string? searchPath, bool? runInBackground,
        IOffLimitsService offLimitsService, IBashCommandAnalyzer bashAnalyzer,
        AgentRegistry registry)
    {
        if (ShouldRouteToShellHandler(ctx.ToolName, ctx.BashCommand))
            return HandleBashCommand(ctx.BashCommand!, ctx.SessionId, offLimitsService, bashAnalyzer, registry, runInBackground, isWorker: true);

        var checkPath = filePath ?? searchPath;
        if (!string.IsNullOrEmpty(checkPath) && !IsNativeMemoryPath(checkPath))
        {
            var offLimitsBlock = BlockIfPathOffLimits(checkPath, ctx.ToolName, ctx.SessionId, offLimitsService, registry);
            if (offLimitsBlock != null) return offLimitsBlock.Value;
        }

        EmitWorktreeAllowIfNeeded();
        return ExitCodes.Success;
    }

    /// <summary>
    /// Shared off-limits check for a direct (non-bash) file/search path. Returns an exit
    /// code if the path is off-limits, null otherwise. One copy for every lane so the
    /// block message and audit shape cannot drift.
    /// </summary>
    private static int? BlockIfPathOffLimits(
        string path, string? toolName, string? sessionId,
        IOffLimitsService offLimitsService, AgentRegistry registry)
    {
        var offLimitsPattern = offLimitsService.IsPathOffLimits(path);
        if (offLimitsPattern == null)
            return null;

        Console.Error.WriteLine("BLOCKED: Path is off-limits to all agents.");
        Console.Error.WriteLine($"  Path: {path}");
        Console.Error.WriteLine($"  Pattern: {offLimitsPattern}");
        Console.Error.WriteLine("  Configure exceptions in dydo/files-off-limits.md");
        return ExitCodes.ToolError;
    }

    private static int? CheckDirectFileOffLimits(
        string filePath, string? action, string? toolName, string? sessionId,
        IOffLimitsService offLimitsService, AgentRegistry registry)
    {
        var agentForOffLimits = registry.GetCurrentAgent(sessionId);

        if (action == "read" && ShouldBypassOffLimits(filePath, agentForOffLimits))
            return null;

        return BlockIfPathOffLimits(filePath, toolName, sessionId, offLimitsService, registry);
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
        AgentRegistry registry)
    {
        if (!IsReadAllowed(filePath, agent))
        {
            WriteAccessDeniedError(agent?.Name, null);
            return ExitCodes.ToolError;
        }

        if (agent != null)
        {
            var blocked = CheckPendingState(agent, filePath, toolName, null, sessionId, registry);
            if (blocked != null) return blocked.Value;
        }


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
        IOffLimitsService offLimitsService, AgentRegistry registry)
    {
        var searchAgent = registry.GetCurrentAgent(sessionId);
        var blocked = RequireIdentityAndRole(searchAgent, searchPath, toolName, sessionId, registry);
        if (blocked != null) return blocked.Value;

        blocked = NotifyUnreadMessages(searchAgent!, searchPath, toolName, null, sessionId, registry);
        if (blocked != null) return blocked.Value;

        blocked = CheckPendingState(searchAgent!, searchPath, toolName, null, sessionId, registry);
        if (blocked != null) return blocked.Value;

        if (string.Equals(toolName, "agent", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("NOTICE: You invoked Claude Code's built-in Agent tool. Sub-agent tool calls run in "
                + "the Tier-2 worker lane: anonymous, audited under their agent_id/agent_type, governed by the universal "
                + "guard layers (off-limits, dangerous-bash, nudges) — not by your identity or onboarding state.");
        }

        if (!string.IsNullOrEmpty(searchPath))
        {
            var offLimitsBlock = BlockIfPathOffLimits(searchPath, toolName, sessionId, offLimitsService, registry);
            if (offLimitsBlock != null) return offLimitsBlock.Value;
        }


        EmitWorktreeAllowIfNeeded();

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
        bool? runInBackground = null,
        bool isWorker = false)
    {
        // Handle dydo commands first — they have their own safety checks and shouldn't be
        // subject to nudge pattern matching on their argument text (fixes false positives).
        // Tier-2 workers are blocked outright: dydo identity/dispatch/messaging is the
        // orchestrator's job, and routing a worker through HandleDydoBashCommand would
        // resolve and mutate the PARENT's session state (shared session_id).
        if (IsDydoCommand(command) && !string.IsNullOrEmpty(sessionId))
        {
            if (isWorker)
            {
                Console.Error.WriteLine("BLOCKED: Sub-agents don't run dydo commands — identity, dispatch, and");
                Console.Error.WriteLine("  messaging belong to the top-level orchestrator, not a worker.");
                return ExitCodes.ToolError;
            }
            return HandleDydoBashCommand(command, sessionId, registry, runInBackground);
        }

        // Hardcoded dangerous patterns — security checks before configurable nudges
        var (isDangerous, dangerReason) = bashAnalyzer.CheckDangerousPatterns(command);
        if (isDangerous)
        {
            Console.Error.WriteLine("BLOCKED: Dangerous command pattern detected.");
            Console.Error.WriteLine($"  Reason: {dangerReason}");
            Console.Error.WriteLine($"  Command: {TruncateCommand(command)}");
            return ExitCodes.ToolError;
        }

        // Configurable nudges — after hardcoded security checks
        var nudged = CheckNudges(command, sessionId, registry);
        if (nudged != null) return nudged.Value;

        // COACHING: Block needless cd+command compounds
        var (isCdChain, cdPath, restCmd) = bashAnalyzer.DetectNeedlessCd(command);
        if (isCdChain)
        {
            Console.Error.WriteLine("BLOCKED: Don't chain cd / Set-Location with other commands — it breaks auto-approval for whitelisted commands.");
            Console.Error.WriteLine($"  If you need to change directory, run it separately first.");
            Console.Error.WriteLine($"  Otherwise just run: {restCmd}");
            return ExitCodes.ToolError;
        }

        // Tier-2 workers are anonymous: skip the Tier-1 identity gates (unread
        // messages / pending state / must-reads) and go straight to the universal
        // git-safety + off-limits op analysis with no agent context.
        if (isWorker)
            return AnalyzeAndCheckBashOperations(command, sessionId, agent: null, offLimitsService, bashAnalyzer, registry, isWorker: true);

        // Non-dydo bash: check agent state, then analyze command
        return HandleNonDydoBash(command, sessionId, offLimitsService, bashAnalyzer, registry);
    }

    internal static int? CheckNudges(string command, string? sessionId, AgentRegistry registry)
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
                    Console.Error.WriteLine($"BLOCKED: {message}");
                    Console.Error.WriteLine("  (Run the same command again to proceed anyway.)");
                    return ExitCodes.ToolError;
                }

                File.Delete(markerPath);
                continue;
            }

            // Block severity: always block
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

        foreach (var defaultNudge in ConfigFactory.DefaultNudges)
        {
            if (!string.Equals(defaultNudge.Severity, "block", StringComparison.OrdinalIgnoreCase))
                continue;

            var existingIndex = nudges.FindIndex(n => n.Pattern == defaultNudge.Pattern);
            if (existingIndex < 0)
            {
                nudges.Add(defaultNudge);
            }
            else if (!string.Equals(nudges[existingIndex].Severity, "block", StringComparison.OrdinalIgnoreCase))
            {
                // Severity was downgraded — enforce block
                nudges[existingIndex] = defaultNudge;
            }
        }

        return nudges;
    }

    private static int HandleDydoBashCommand(string command, string sessionId, AgentRegistry registry, bool? runInBackground)
    {
        // #0196: phase-1 single-line write removed. The unverifiable shape it produced is now
        // discarded by AgentSessionManager.GetSessionContext, so writing it served no purpose
        // beyond opening the cross-terminal race window. Only the verified phase-2 write below
        // (with agent name) is published.
        HandleClaimSessionStorage(command, sessionId, registry);

        var agent = registry.GetCurrentAgent(sessionId);

        if (IsHumanOnlyDydoCommand(command))
        {
            if (agent != null)
            {
                Console.Error.WriteLine("BLOCKED: This command is human-only. Agents cannot run it.");
                return ExitCodes.ToolError;
            }
        }

        if (IsDydoWaitCommand(command) && runInBackground != true)
        {
            Console.Error.WriteLine("BLOCKED: 'dydo wait' must run in background. Use run_in_background to avoid blocking other work.");
            return ExitCodes.ToolError;
        }

        if (!IsDydoDispatchCommand(command) && !IsDydoWaitAnyForm(command))
        {
            if (agent != null)
            {
                var blocked = CheckPendingState(agent, null, "bash", TruncateCommand(command), sessionId, registry);
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
        AgentRegistry registry)
    {
        var agent = registry.GetCurrentAgent(sessionId);
        if (agent != null)
        {
            var blocked = NotifyUnreadMessages(agent, null, "bash", command, sessionId, registry);
            if (blocked != null) return blocked.Value;

            blocked = CheckPendingState(agent, null, "bash", command, sessionId, registry);
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
                    WriteMustReadError(agent);
                    return ExitCodes.ToolError;
                }
            }
        }

        return AnalyzeAndCheckBashOperations(command, sessionId, agent, offLimitsService, bashAnalyzer, registry);
    }

    private static int AnalyzeAndCheckBashOperations(
        string command, string? sessionId, AgentState? agent,
        IOffLimitsService offLimitsService, IBashCommandAnalyzer bashAnalyzer,
        AgentRegistry registry, bool isWorker = false)
    {
        // git stash is only safe in worktrees (isolated stash stack); block otherwise
        if (GitStashRegex().IsMatch(command))
        {
            if (agent == null || registry.GetWorktreeId(agent.Name) == null)
            {
                const string reason = "git stash is unsafe in multi-agent environments. "
                    + "Stashes are a global stack -- other agents' stash operations will interfere. "
                    + "Commit your changes instead.";
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
            Console.Error.WriteLine("BLOCKED: Command contains bypass patterns (command substitution or variable expansion) "
                + "that make file operation analysis unreliable.");
            Console.Error.WriteLine("  Write operations cannot be verified. Use literal paths instead.");
            return ExitCodes.ToolError;
        }

        foreach (var op in analysis.Operations)
        {
            var blocked = CheckBashFileOperation(op, command, sessionId, offLimitsService, registry, agent, isWorker);
            if (blocked != null) return blocked.Value;
        }


        EmitWorktreeAllowIfNeeded();

        return ExitCodes.Success;
    }

    internal static int? CheckBashFileOperation(
        FileOperation op, string command, string? sessionId,
        IOffLimitsService offLimitsService, AgentRegistry registry,
        AgentState? cachedAgent = null, bool isWorker = false)
    {
        // For reads, apply the same bootstrap/mode file bypass as direct reads (#60).
        // Native memory is exempt for any op type (out of dydo's jurisdiction).
        // Tier-2 workers get neither the onboarding bootstrap bypass nor the staged
        // read gate below — only the universal off-limits check applies to them.
        var agent = cachedAgent ?? registry.GetCurrentAgent(sessionId);
        var skipOffLimits = IsNativeMemoryPath(op.Path)
            || (!isWorker && op.Type is FileOperationType.Read && ShouldBypassOffLimits(op.Path, agent));

        var offLimitsPattern = skipOffLimits ? null : offLimitsService.IsPathOffLimits(op.Path);
        if (offLimitsPattern != null)
        {
            Console.Error.WriteLine("BLOCKED: Command references off-limits path.");
            Console.Error.WriteLine($"  Path: {op.Path}");
            Console.Error.WriteLine($"  Pattern: {offLimitsPattern}");
            Console.Error.WriteLine($"  Detected: {op.Type} via {op.Command}");
            return ExitCodes.ToolError;
        }

        // Staged access control for read operations (mirrors HandleReadOperation).
        // Workers are anonymous and exempt — they never onboard.
        if (!isWorker && op.Type is FileOperationType.Read)
        {
            if (!IsReadAllowed(op.Path, agent))
            {
                WriteAccessDeniedError(agent?.Name, null);
                return ExitCodes.ToolError;
            }
        }

        // Per-role path RBAC removed (Decision 024 §2), but an agent still cannot write
        // INTO another agent's workspace (cross-agent tampering — plans/notes/inbox).
        if (IsWriteLikeOp(op.Type))
            return BlockIfCrossAgentWorkspace(op.Path, agent, "bash", command, sessionId, registry);

        return null;
    }

    internal static bool IsWriteLikeOp(FileOperationType type) =>
        type is FileOperationType.Write or FileOperationType.Delete
            or FileOperationType.Move or FileOperationType.Copy
            or FileOperationType.PermissionChange;

    /// <summary>
    /// Off-limits protects each agent's system files (state.md/.session/workflow/modes),
    /// but the rest of a workspace (plans, notes, inbox) needs an identity-scoped check:
    /// an agent may write only its own dydo/agents/&lt;self&gt;/ tree, never another's.
    /// Tier-2 workers (agent == null here) are anonymous and not subject to this.
    /// </summary>
    private static int? BlockIfCrossAgentWorkspace(
        string? filePath, AgentState? agent, string? toolName, string? command,
        string? sessionId, AgentRegistry registry)
    {
        if (agent == null || string.IsNullOrEmpty(filePath) || !IsOtherAgentWorkspace(filePath, agent.Name))
            return null;

        Console.Error.WriteLine($"BLOCKED: Agent {agent.Name} cannot write into another agent's workspace.");
        Console.Error.WriteLine($"  Path: {filePath}");
        Console.Error.WriteLine("  Each agent owns only dydo/agents/<self>/. Use dydo msg to coordinate.");
        return ExitCodes.ToolError;
    }

    internal static bool IsOtherAgentWorkspace(string filePath, string agentName)
    {
        var match = AgentWorkspaceRegex().Match(filePath.Replace('\\', '/'));
        return match.Success && !string.Equals(match.Groups[1].Value, agentName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Check if agent has identity and role. Returns exit code if blocked, null if OK.
    /// </summary>
    private static int? RequireIdentityAndRole(
        AgentState? agent, string? path, string? toolName,
        string? sessionId, IAgentRegistry registry)
    {
        if (agent == null)
        {
            WriteAccessDeniedError(null, null);
            return ExitCodes.ToolError;
        }
        if (string.IsNullOrEmpty(agent.Role))
        {
            WriteAccessDeniedError(agent.Name, null);
            return ExitCodes.ToolError;
        }
        return null;
    }

    private static int? RequireWriteIdentity(
        AgentState? agent, string? filePath, string? toolName,
        string? sessionId, IAgentRegistry registry)
    {
        if (agent == null)
        {
            Console.Error.WriteLine("BLOCKED: No agent identity assigned to this process.");
            Console.Error.WriteLine("  Run 'dydo agent claim auto' to claim an agent identity.");
            return ExitCodes.ToolError;
        }
        if (string.IsNullOrEmpty(agent.Role))
        {
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
    internal static int? NotifyUnreadMessages(
        AgentState agent, string? path, string? toolName, string? command,
        string? sessionId, IAgentRegistry registry)
    {
        // Self-heal phantom ids — drop ids whose inbox file is missing. Without this, a
        // non-atomic inbox clear (crash mid-operation, manual cleanup) leaves the
        // agent blocked forever: state.md says "N unread" but there is no file to read.
        if (agent.UnreadMessages.Count > 0)
        {
            var workspace = registry.GetAgentWorkspace(agent.Name);
            var phantoms = agent.UnreadMessages
                .Where(msgId => FindMessageInfo(workspace, msgId) == null)
                .ToList();
            if (phantoms.Count > 0)
            {
                foreach (var id in phantoms)
                    registry.MarkMessageRead(sessionId, id);
                agent = registry.GetCurrentAgent(sessionId) ?? agent;
            }
        }

        if (agent.UnreadMessages.Count == 0)
            return null;


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
        string? sessionId, AgentRegistry registry)
    {
        var pendingMarkers = SelfHealAndGetPendingMarkers(registry, agent.Name);
        var missingGeneralWait = MissingGeneralWait(agent, registry);
        if (pendingMarkers.Count == 0 && !missingGeneralWait)
            return null;


        WritePendingStateBlock(pendingMarkers, missingGeneralWait);
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

        // Collapse '.'/'..' lexically first so no traversal sequence reaches a
        // path-based guard check (off-limits, native-memory, cross-agent).
        var resolved = PathUtils.CollapseRelativeSegments(path);

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

        // Native auto-memory is outside dydo's jurisdiction — readable at any stage
        if (IsNativeMemoryPath(filePath))
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
    /// Check if a command is a human-only dydo command (task approve/reject, roles reset, guard lift/restore, agent clean --force).
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
    /// Self-heal wait markers with dead listener PIDs, then return non-listening task markers.
    /// Task markers with listening=true but dead PID flip to listening=false.
    /// Sentinel markers (e.g. general wait) with dead PID are deleted — they're transient
    /// per-process state, not "pending" task channels for the agent to register.
    /// </summary>
    private static List<Models.WaitMarker> SelfHealAndGetPendingMarkers(AgentRegistry registry, string agentName)
    {
        var markers = registry.GetWaitMarkers(agentName);
        foreach (var marker in markers)
        {
            if (!marker.Listening) continue;

            if (marker.Pid == null || !ProcessUtils.IsProcessRunning(marker.Pid.Value))
            {
                if (marker.Task.StartsWith('_'))
                    registry.RemoveWaitMarker(agentName, marker.Task);
                else
                    registry.ResetWaitMarkerListening(agentName, marker.Task);
            }
        }

        return registry.GetNonListeningWaitMarkers(agentName)
            .Where(m => !m.Task.StartsWith('_'))
            .ToList();
    }

    /// <summary>
    /// True when the agent has a role set but no listening general wait. Decision 021
    /// universalises the general-wait obligation: every claimed agent runs a single
    /// always-active general wait once their role lands, so reachability and message
    /// surfacing don't depend on role.
    /// </summary>
    private static bool MissingGeneralWait(AgentState agent, AgentRegistry registry)
    {
        if (string.IsNullOrEmpty(agent.Role))
            return false;

        var markers = registry.GetWaitMarkers(agent.Name);
        var general = markers.FirstOrDefault(m => m.Task.StartsWith('_'));
        if (general == null || !general.Listening) return true;
        if (general.Pid == null || !ProcessUtils.IsProcessRunning(general.Pid.Value)) return true;
        return false;
    }

    /// <summary>
    /// Emit the standard pending-state block message to stderr.
    /// </summary>
    private static void WritePendingStateBlock(List<Models.WaitMarker> pendingMarkers, bool missingGeneralWait)
    {
        if (pendingMarkers.Count > 0)
        {
            var taskNames = string.Join(", ", pendingMarkers.Select(m => m.Task));
            Console.Error.WriteLine($"BLOCKED: Register waits before continuing. Pending: [{taskNames}].");
            Console.Error.WriteLine("  Run: dydo wait --task <name> (in background)");
        }
        if (missingGeneralWait)
        {
            Console.Error.WriteLine("BLOCKED: Agent must keep a general wait active.");
            Console.Error.WriteLine("  Run: dydo wait (in background)");
        }
    }

    // Matches git stash and all variants (pop, push, apply, drop, list, show, save, etc.)
    [GeneratedRegex(@"(?:^|\s|;|&&|\|\|)git\s+stash(?:\s|$|;|&&|\|\|)", RegexOptions.IgnoreCase)]
    private static partial Regex GitStashRegex();

    [GeneratedRegex(@"(?:^|\s|;|&&|\|\|)git\s+merge(?:\s|$|;|&&|\|\|)", RegexOptions.IgnoreCase)]
    private static partial Regex GitMergeRegex();

    // Matches human-only dydo subcommands: task approve, task reject, roles reset, guard lift, guard restore, agent clean --force.
    // The agent-clean alternative uses [^;&|]* to keep --force lookup inside one chain segment, and \b around clean/force
    // to guard against 'cleanup' / '--forcefully' false matches. Bare 'agent clean <name>' (no --force) stays allowed.
    [GeneratedRegex(@"(?:^|[;&|]\s*)(?:\./)?dydo\s+(?:task\s+(?:approve|reject)|roles\s+reset|guard\s+(?:lift|restore)|agent\s+clean\b[^;&|]*--force)\b", RegexOptions.IgnoreCase)]
    private static partial Regex HumanOnlyDydoCommandRegex();

    [GeneratedRegex(@"dydo/agents/[^/]+/workflow\.md$", RegexOptions.IgnoreCase)]
    private static partial Regex AgentWorkflowRegex();

    [GeneratedRegex(@"dydo/agents/([^/]+)/", RegexOptions.IgnoreCase)]
    private static partial Regex AgentWorkspaceRegex();

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
