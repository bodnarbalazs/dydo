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

        var stopOption = new Option<bool>("--stop")
        {
            Description = "Stop-hook mode: derive the needs-human flag from turn-end (used by the Stop hook)"
        };

        var command = new Command("guard", "Check if current agent can perform action (used by hooks)");
        command.Options.Add(actionOption);
        command.Options.Add(pathOption);
        command.Options.Add(commandOption);
        command.Options.Add(stopOption);

        command.SetAction(parseResult =>
        {
            if (parseResult.GetValue(stopOption))
                return ExecuteStop();

            var cliAction = parseResult.GetValue(actionOption);
            var cliPath = parseResult.GetValue(pathOption);
            var cliCommand = parseResult.GetValue(commandOption);
            return Execute(cliAction, cliPath, cliCommand);
        });

        return command;
    }

    // Data-driven lookups to reduce cyclomatic complexity
    private static readonly HashSet<string> SearchTools = new(StringComparer.OrdinalIgnoreCase) { "glob", "grep", "agent" };

    // bash/powershell are Claude's shell tools; shell_command/exec/local_shell/unified_exec are
    // codex's, one per mode (issue 0295). Without codex's names here the guard receives a codex
    // shell call (it is in the hook matcher) but ShouldRouteToShellHandler never sends it to the
    // shell analyzer, so off-limits / dangerous-bash / git-safety silently do not bind. Kept in
    // lockstep with InitCommand.CodexShellTools, which puts the same names in the hook matcher.
    private static readonly HashSet<string> ShellTools = new(StringComparer.OrdinalIgnoreCase)
        { "bash", "powershell", "shell_command", "exec", "local_shell", "unified_exec" };

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
        string? AgentId, string? AgentType, string Host, string Model);

    private static GuardContext ParseInput(string? cliAction, string? cliPath, string? cliCommand)
    {
        var hasCliArgs = cliAction != null || cliPath != null || cliCommand != null;
        string? filePath = null, action = null, bashCommand = null;
        string? toolName = null, sessionId = null, searchPath = null;
        string? agentId = null, agentType = null;
        var host = AgentSession.UnknownHost;
        var model = AgentSession.UnknownModel;
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
                    host = InferHost(hookInput, json);
                    model = InferModel(hookInput, json);
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
            agentId, agentType, host, model);
    }

    internal static string InferHost(HookInput hookInput, string? rawJson = null)
    {
        var explicitHost = TryReadExplicitHost(rawJson);
        if (explicitHost != null)
            return explicitHost;

        return InferHostFromPath(hookInput.TranscriptPath);
    }

    /// <summary>
    /// Capture the concrete runtime model for a hook call (c1-6). The chain, in truth-order:
    /// <list type="number">
    ///   <item>An explicit <c>model</c>/<c>dydo_model</c> field on the payload — what a codex
    ///     host already delivers (<c>gpt-5-codex</c>), and what a future Claude payload would
    ///     carry.</item>
    ///   <item>Otherwise, for a Claude session, the transcript: Claude Code writes the real
    ///     runtime model id onto every assistant entry, so the most recent one is the concrete
    ///     binding (truthful under <c>dydo model cap</c>, which rewrites what actually runs).</item>
    ///   <item>Otherwise <c>unknown</c> — never guessed from a role default.</item>
    /// </list>
    /// This is the leg that lets a Tier-1 Claude claim persist a concrete model: the captured
    /// value flows through <see cref="ParseInput"/> into the claim's
    /// <c>StorePendingSessionId</c> / <c>StoreSessionContext</c> write.
    /// </summary>
    internal static string InferModel(HookInput hookInput, string? rawJson = null)
    {
        var explicitModel = InferModel(rawJson);
        if (explicitModel != AgentSession.UnknownModel)
            return explicitModel;

        var transcriptModel = InferModelFromTranscript(hookInput?.TranscriptPath);
        return transcriptModel ?? AgentSession.UnknownModel;
    }

    internal static string InferModel(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return AgentSession.UnknownModel;

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return AgentSession.UnknownModel;

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!IsExplicitModelProperty(property.Name) || property.Value.ValueKind != JsonValueKind.String)
                    continue;

                return AgentSession.NormalizeModel(property.Value.GetString());
            }
        }
        catch (JsonException)
        {
            return AgentSession.UnknownModel;
        }

        return AgentSession.UnknownModel;
    }

    // Read at most this many trailing bytes of the transcript. The most recent assistant entry
    // (carrying the runtime model id) is always near the end of an append-only JSONL transcript,
    // so a bounded tail read keeps the guard's hot path cheap regardless of transcript size.
    private const int TranscriptTailBytes = 512 * 1024;

    /// <summary>
    /// Extract the runtime model id from a Claude Code transcript by scanning its tail for the
    /// most recent assistant entry's <c>message.model</c>. Returns null when the file is absent,
    /// unreadable, or carries no usable model — the guard then keeps <c>unknown</c>.
    /// </summary>
    internal static string? InferModelFromTranscript(string? transcriptPath)
    {
        if (string.IsNullOrWhiteSpace(transcriptPath) || !File.Exists(transcriptPath))
            return null;

        try
        {
            var lines = ReadTranscriptTailLines(transcriptPath);
            for (var i = lines.Count - 1; i >= 0; i--)
            {
                var model = TryExtractAssistantModel(lines[i]);
                if (model != null)
                    return AgentSession.NormalizeModel(model);
            }
        }
        catch (Exception)
        {
            return null;
        }

        return null;
    }

    private static List<string> ReadTranscriptTailLines(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var start = stream.Length > TranscriptTailBytes ? stream.Length - TranscriptTailBytes : 0;
        stream.Seek(start, SeekOrigin.Begin);
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        var lines = reader.ReadToEnd().Split('\n').ToList();

        // A tail read that started mid-file leaves the first element a partial line fragment.
        if (start > 0 && lines.Count > 0)
            lines.RemoveAt(0);

        return lines;
    }

    private static string? TryExtractAssistantModel(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            if (!root.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String
                || !string.Equals(typeEl.GetString(), "assistant", StringComparison.OrdinalIgnoreCase))
                return null;

            if (!root.TryGetProperty("message", out var msgEl) || msgEl.ValueKind != JsonValueKind.Object
                || !msgEl.TryGetProperty("model", out var modelEl) || modelEl.ValueKind != JsonValueKind.String)
                return null;

            var model = modelEl.GetString();
            // Claude Code stamps "<synthetic>" on injected assistant turns — not a real binding.
            if (string.IsNullOrWhiteSpace(model) || model == "<synthetic>")
                return null;

            return model;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? TryReadExplicitHost(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return null;

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!IsExplicitHostProperty(property.Name) || property.Value.ValueKind != JsonValueKind.String)
                    continue;

                return AgentSession.NormalizeHost(property.Value.GetString());
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static bool IsExplicitHostProperty(string propertyName) =>
        propertyName.Equals("host", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Equals("dydo_host", StringComparison.OrdinalIgnoreCase);

    private static bool IsExplicitModelProperty(string propertyName) =>
        propertyName.Equals("model", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Equals("dydo_model", StringComparison.OrdinalIgnoreCase);

    private static string InferHostFromPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return AgentSession.UnknownHost;

        var normalized = path.Replace('\\', '/').ToLowerInvariant();
        if (normalized.Contains("/.claude/", StringComparison.Ordinal))
            return "claude";
        if (normalized.Contains("/.codex/", StringComparison.Ordinal))
            return "codex";

        return AgentSession.UnknownHost;
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

        // needs-human derived-flag reconcile (Decision 030 §1): an AskUserQuestion call marks the
        // calling agent as waiting on a human; any other guarded tool call means the human answered
        // and work resumed, so the flag self-clears. Runs for identified (Tier-1) hook calls only,
        // before the routing below so it fires whether or not the triggering call is allowed.
        if (toolName != null && !string.IsNullOrEmpty(sessionId))
            ReconcileNeedsHuman(toolName, sessionId, registry);

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

        // Reads are allowed for anyone once past off-limits (checked in RouteToolLayers).
        if (action == "read" && string.IsNullOrEmpty(bashCommand))
        {
            EmitWorktreeAllowIfNeeded();
            return ExitCodes.Success;
        }

        // Writes are allowed once past off-limits; only tool-scoped nudges remain.
        return HandleWriteOperation(filePath, toolName, registry, ctx.AgentType);
    }

    /// <summary>
    /// needs-human derived-flag reconcile on a Tier-1 PreToolUse call (Decision 030 §1). AskUserQuestion
    /// is the human-in-the-loop tool — its call sets a DERIVED flag. Every other guarded tool call from
    /// an agent that was waiting means the human answered and work resumed, so a derived flag self-clears.
    /// An EXPLICIT flag (a deliberate <c>dydo hand raise</c>) is left untouched — it is not erased by the
    /// raiser's next tool call, only by <c>dydo hand lower</c> or release. Both paths are no-ops when the
    /// flag is already in the target state, so the common case writes nothing.
    /// </summary>
    internal static void ReconcileNeedsHuman(string toolName, string sessionId, AgentRegistry registry)
    {
        var agent = registry.GetCurrentAgent(sessionId);
        if (agent == null) return;

        var isAsk = string.Equals(toolName, "askuserquestion", StringComparison.OrdinalIgnoreCase);
        if (isAsk && !agent.NeedsHuman)
            registry.SetNeedsHuman(agent.Name, true, NeedsHumanSource.Derived);
        else if (!isAsk && agent.NeedsHuman && agent.NeedsHumanSource != NeedsHumanSource.Explicit)
            registry.SetNeedsHuman(agent.Name, false);
    }

    /// <summary>
    /// Stop-hook entry point (Decision 030 §1). A terminal question always ends the turn, so a turn that
    /// ends while the agent is still working on an in-flight task means the session is idle, waiting on a
    /// human — pure session state, no text analysis. Always exit 0: a Stop hook must never block turn end.
    /// </summary>
    private static int ExecuteStop()
    {
        try
        {
            if (!TryReadStdinJson(out var json) || json == null)
                return ExitCodes.Success;

            var hookInput = JsonSerializer.Deserialize(json, DydoDefaultJsonContext.Default.HookInput);
            ApplyStopSignal(hookInput?.SessionId, new AgentRegistry());
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"WARNING: dydo guard --stop internal error: {ex.Message}");
        }
        return ExitCodes.Success;
    }

    /// <summary>Turn-end needs-human rule: set the flag when the session is idle mid-work — status
    /// working with an in-flight task. No-op otherwise (already flagged, released, or no task).</summary>
    internal static void ApplyStopSignal(string? sessionId, AgentRegistry registry)
    {
        if (string.IsNullOrEmpty(sessionId))
            return;
        var agent = registry.GetCurrentAgent(sessionId);
        if (agent is { Status: AgentStatus.Working } && !string.IsNullOrEmpty(agent.Task) && !agent.NeedsHuman)
            registry.SetNeedsHuman(agent.Name, true, NeedsHumanSource.Derived);
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
        if (!string.IsNullOrEmpty(filePath))
        {
            var blocked = BlockIfPathOffLimits(filePath, toolName, sessionId, offLimitsService, registry);
            if (blocked != null) return blocked.Value;
        }

        // SECURITY LAYER 2: Bash tool
        if (ShouldRouteToShellHandler(toolName, bashCommand))
        {
            return HandleBashCommand(bashCommand!, sessionId, offLimitsService, bashAnalyzer, registry, runInBackground);
        }

        // SECURITY LAYER 2.5: Search tools (Glob/Grep) and Agent tool — off-limits applies
        // to the search root, and the Agent tool gets the Tier-2 worker-lane notice.
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
        string? filePath, string? toolName, AgentRegistry registry, string? agentType = null)
    {
        if (string.IsNullOrEmpty(filePath))
            return ExitCodes.Success;

        // Tool-scoped nudges (Decision 026 §4) apply to Tier-1 only: absence of
        // agent_type is the Tier-1 signal (Decision 024 verification). Tier-2 worker
        // calls carry agent_id and never reach this lane; the agent_type check covers
        // any anomalous payload that carries a type without an id.
        if (string.IsNullOrEmpty(agentType))
        {
            var nudged = CheckFileNudges(toolName, filePath, registry);
            if (nudged != null) return nudged.Value;
        }

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
            return HandleBashCommand(
                ctx.BashCommand!, ctx.SessionId,
                offLimitsService, bashAnalyzer, registry, runInBackground, isWorker: true);

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
    internal static int? BlockIfPathOffLimits(
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

    private static int HandleSearchTool(
        string? searchPath, string? toolName, string? sessionId,
        IOffLimitsService offLimitsService, AgentRegistry registry)
    {
        if (string.Equals(toolName, "agent", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("NOTICE: You invoked Claude Code's built-in Agent tool. Sub-agent tool calls run in "
                + "the Tier-2 worker lane: anonymous, audited under their agent_id/agent_type, governed by the universal "
                + "guard layers (off-limits, dangerous-bash, nudges).");
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
            return HandleDydoBashCommand(
                command, sessionId, offLimitsService, bashAnalyzer, registry);
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
            // Tool-scoped nudges match file paths of direct tool calls (CheckFileNudges),
            // not bash command text — their patterns are globs, not regexes.
            if (nudge.Tools is { Count: > 0 }) continue;

            Regex regex;
            try { regex = new Regex(nudge.Pattern, RegexOptions.IgnoreCase); }
            catch { continue; }

            var match = regex.Match(command);
            if (!match.Success) continue;

            var message = nudge.Message;
            for (int i = 1; i < match.Groups.Count; i++)
                message = message.Replace($"${i}", match.Groups[i].Value.Trim());

            if (string.Equals(nudge.Severity, "notice", StringComparison.OrdinalIgnoreCase))
            {
                // Soft nudge: warn on stderr, never block (exit 0).
                Console.Error.WriteLine($"NOTICE: {message}");
                continue;
            }

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
    /// Evaluates tool-scoped nudges (NudgeConfig.Tools) against a direct file-op path.
    /// Ships the Decision 026 §4 Tier-1 source-write reminder: severity "notice" is an
    /// exit-0 stderr warning, never a block — the trivial-edit exception stays frictionless.
    /// Patterns are '|'-separated globs; {source}/{tests} expand to the dydo.json path sets.
    /// Returns an exit code only for block-severity matches, null otherwise.
    /// </summary>
    internal static int? CheckFileNudges(string? toolName, string filePath, AgentRegistry registry)
    {
        if (string.IsNullOrEmpty(toolName))
            return null;

        var nudges = registry.Config?.Nudges;
        if (nudges == null || nudges.Count == 0)
            return null;

        // Resolved lazily — most calls have no tool-scoped nudge for this tool.
        Dictionary<string, List<string>>? pathSets = null;
        string? relPath = null;

        foreach (var nudge in nudges)
        {
            if (nudge.Tools is not { Count: > 0 }) continue;
            if (!nudge.Tools.Any(t => t.Equals(toolName, StringComparison.OrdinalIgnoreCase))) continue;

            pathSets ??= new RoleDefinitionService().ResolvePathSets(registry.Config);
            relPath ??= RelativizeToProjectRoot(filePath);

            if (!MatchesFileNudgePattern(nudge.Pattern, relPath, pathSets)) continue;

            if (string.Equals(nudge.Severity, "block", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine($"BLOCKED: {nudge.Message}");
                return ExitCodes.ToolError;
            }

            // "notice" (and any non-block severity): soft — warn on stderr, allow.
            Console.Error.WriteLine($"NOTICE: {nudge.Message}");
        }

        return null;
    }

    /// <summary>
    /// A tool-scoped nudge pattern is a '|'-separated list of glob patterns;
    /// a {name} token expands to the corresponding dydo.json path set.
    /// </summary>
    private static bool MatchesFileNudgePattern(
        string pattern, string relPath, Dictionary<string, List<string>> pathSets)
    {
        foreach (var token in pattern.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            List<string> globs =
                token.StartsWith('{') && token.EndsWith('}') && pathSets.TryGetValue(token[1..^1], out var set)
                    ? set
                    : [token];

            if (globs.Any(glob => GlobMatcher.IsMatch(relPath, glob)))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Maps an absolute tool path to a project-root-relative one so it can match the
    /// repo-relative path-set globs. Paths outside the project stay absolute and
    /// simply won't match. Mirrors OffLimitsService's relativization.
    /// </summary>
    private static string RelativizeToProjectRoot(string path)
    {
        if (!Path.IsPathRooted(path))
            return path;

        var root = PathUtils.FindMainProjectRoot();
        if (root == null)
            return path;

        var relative = Path.GetRelativePath(root, path);
        var firstSegment = relative.Replace('\\', '/').Split('/', 2)[0];
        return Path.IsPathRooted(relative) || firstSegment == ".." ? path : relative;
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

    private static int HandleDydoBashCommand(
        string command, string sessionId,
        IOffLimitsService offLimitsService, IBashCommandAnalyzer bashAnalyzer,
        AgentRegistry registry)
    {
        // agent is resolved only for the git-safety worktree checks below; it is null
        // in the identity-free model and the checks degrade to the no-worktree branch.
        var agent = registry.GetCurrentAgent(sessionId);

        var (isDangerous, dangerReason) = bashAnalyzer.CheckDangerousPatterns(command);
        if (isDangerous)
        {
            Console.Error.WriteLine("BLOCKED: Dangerous command pattern detected.");
            Console.Error.WriteLine($"  Reason: {dangerReason}");
            Console.Error.WriteLine($"  Command: {TruncateCommand(command)}");
            return ExitCodes.ToolError;
        }

        return AnalyzeAndCheckBashOperations(
            command, sessionId, agent, offLimitsService, bashAnalyzer, registry);
    }

    private static int HandleNonDydoBash(
        string command, string? sessionId,
        IOffLimitsService offLimitsService, IBashCommandAnalyzer bashAnalyzer,
        AgentRegistry registry)
    {
        // agent is resolved only for the git-safety worktree checks in
        // AnalyzeAndCheckBashOperations; null in the identity-free model.
        var agent = registry.GetCurrentAgent(sessionId);
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
        // Native memory is exempt for any op type (out of dydo's jurisdiction).
        // Everything else is subject to the universal off-limits check.
        if (IsNativeMemoryPath(op.Path))
            return null;

        var offLimitsPattern = offLimitsService.IsPathOffLimits(op.Path);
        if (offLimitsPattern != null)
        {
            Console.Error.WriteLine("BLOCKED: Command references off-limits path.");
            Console.Error.WriteLine($"  Path: {op.Path}");
            Console.Error.WriteLine($"  Pattern: {offLimitsPattern}");
            Console.Error.WriteLine($"  Detected: {op.Type} via {op.Command}");
            return ExitCodes.ToolError;
        }

        return null;
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
    /// Check if a command is a dydo command.
    /// </summary>
    internal static bool IsDydoCommand(string command)
    {
        return DydoCommandRegex().IsMatch(command);
    }

    // Matches git stash and all variants (pop, push, apply, drop, list, show, save, etc.)
    [GeneratedRegex(@"(?:^|\s|;|&&|\|\|)git\s+stash(?:\s|$|;|&&|\|\|)", RegexOptions.IgnoreCase)]
    private static partial Regex GitStashRegex();

    [GeneratedRegex(@"(?:^|\s|;|&&|\|\|)git\s+merge(?:\s|$|;|&&|\|\|)", RegexOptions.IgnoreCase)]
    private static partial Regex GitMergeRegex();

    [GeneratedRegex(@"(?:^|[;&|]\s*)(?:\./)?dydo\s", RegexOptions.IgnoreCase)]
    private static partial Regex DydoCommandRegex();

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
