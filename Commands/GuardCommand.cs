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
    /// <summary>
    /// Everything the guard needs from the project: the loaded config (nudges, path sets)
    /// and the machine-local directory where warn-nudge pass-through markers live
    /// (dydo/_system/.local/ — gitignored, scan-excluded). Replaces the old AgentRegistry:
    /// with the roster/claim machinery gone (DR-041), the guard only ever needed these two.
    /// </summary>
    internal sealed record GuardEnv(DydoConfig? Config, string MarkerDir)
    {
        public static GuardEnv Load(string? basePath = null)
        {
            var configService = new ConfigService();
            return new GuardEnv(
                configService.LoadConfig(basePath),
                Path.Combine(configService.GetDydoRoot(basePath), "_system", ".local"));
        }
    }

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
            Description = "Stop-hook mode: no-op retained so existing Stop-hook wiring keeps resolving"
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

    private record struct GuardContext(
        string? FilePath, string? Action, string? BashCommand,
        string? ToolName, string? SessionId, string? SearchPath,
        bool HasCliArgs, string? AgentId, string? AgentType);

    private static GuardContext ParseInput(string? cliAction, string? cliPath, string? cliCommand)
    {
        var hasCliArgs = cliAction != null || cliPath != null || cliCommand != null;
        string? filePath = null, action = null, bashCommand = null;
        string? toolName = null, sessionId = null, searchPath = null;
        string? agentId = null, agentType = null;

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
            toolName, sessionId, searchPath, hasCliArgs,
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
        // wave tool calls through. Loading off-limits patterns and the guard environment
        // happens here, outside the fail-open boundary below.
        OffLimitsService offLimitsService;
        GuardEnv env;
        try
        {
            offLimitsService = new OffLimitsService();
            offLimitsService.LoadPatterns();
            env = GuardEnv.Load();
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
            return Decide(ctx, offLimitsService, env);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"WARNING: dydo guard internal error, allowing tool call: {ex.Message}");
            return ExitCodes.Success;
        }
    }

    private static int Decide(GuardContext ctx, OffLimitsService offLimitsService, GuardEnv env)
    {
        var bashAnalyzer = new BashCommandAnalyzer();

        RunDailyValidationIfDue();
        RestoreExpiredModelCapsIfDue();
        AutoStartWatchdogIfDue();

        var sessionId = ctx.SessionId;

        var filePath = ResolveTraversal(ctx.FilePath);
        var action = ctx.Action;
        var bashCommand = ctx.BashCommand;
        var toolName = ctx.ToolName;
        var searchPath = ResolveTraversal(ctx.SearchPath);

        // ============================================================
        // TIER-2 WORKER LANE (Decision 024): calls carrying agent_id come from
        // sub-agents / workflow workers. Workers are anonymous — no claim, no role
        // state, no staged onboarding, no must-reads. Only the universal layers
        // apply: off-limits, dangerous-bash patterns, nudges, and the shared bash
        // safety checks (dydo-command handling).
        // ============================================================
        if (!ctx.HasCliArgs && !string.IsNullOrEmpty(ctx.AgentId))
        {
            return HandleWorkerCall(ctx, filePath, searchPath, offLimitsService, bashAnalyzer, env);
        }

        // Native auto-memory (~/.claude/projects/*/memory/) is always accessible —
        // it lives outside the repo and outside dydo's jurisdiction (Decision 024 §5).
        if (!string.IsNullOrEmpty(filePath) && IsNativeMemoryPath(filePath))
            return ExitCodes.Success;

        var routed = RouteToolLayers(
            filePath, action, bashCommand, toolName, searchPath,
            sessionId, offLimitsService, bashAnalyzer, env);
        if (routed != null) return routed.Value;

        // Reads are allowed for anyone once past off-limits (checked in RouteToolLayers).
        if (action == "read" && string.IsNullOrEmpty(bashCommand))
            return ExitCodes.Success;

        // Writes are allowed once past off-limits; only tool-scoped nudges remain.
        return HandleWriteOperation(filePath, toolName, env, ctx.AgentType);
    }

    /// <summary>
    /// Stop-hook entry point. The agent-identity needs-human machinery this used to drive was
    /// carved out with the claim ceremony (DR-041) — there is no runtime agent state to reconcile
    /// now — so the Stop hook is a no-op that always exits 0 (a Stop hook must never block turn end).
    /// The option and hook wiring are retained so existing installs' Stop hook keeps resolving.
    /// </summary>
    private static int ExecuteStop() => ExitCodes.Success;

    /// <summary>
    /// Security layers 1–2.6: off-limits on direct file paths, Bash routing,
    /// search-tool gating, and plan-mode blocking. Returns an exit code when the
    /// call was fully handled, null to fall through to staged access control.
    /// </summary>
    private static int? RouteToolLayers(
        string? filePath, string? action, string? bashCommand, string? toolName,
        string? searchPath, string? sessionId,
        IOffLimitsService offLimitsService, IBashCommandAnalyzer bashAnalyzer,
        GuardEnv env)
    {
        // SECURITY LAYER 1: off-limits patterns for direct file operations.
        if (!string.IsNullOrEmpty(filePath))
        {
            var blocked = BlockIfPathOffLimits(filePath, offLimitsService);
            if (blocked != null) return blocked.Value;
        }

        // SECURITY LAYER 2: Bash tool
        if (ShouldRouteToShellHandler(toolName, bashCommand))
        {
            return HandleBashCommand(bashCommand!, sessionId, offLimitsService, bashAnalyzer, env);
        }

        // SECURITY LAYER 2.5: Search tools (Glob/Grep) and Agent tool — off-limits applies
        // to the search root, and the Agent tool gets the Tier-2 worker-lane notice.
        if (toolName != null && SearchTools.Contains(toolName))
        {
            return HandleSearchTool(searchPath, toolName, offLimitsService);
        }

        // SECURITY LAYER 2.6: Dydo agents must not use Claude Code's built-in plan mode.
        if (toolName == "enterplanmode" || toolName == "exitplanmode")
        {
            Console.Error.WriteLine("BLOCKED: Dydo agents don't use Claude Code's built-in plan mode.");
            Console.Error.WriteLine("  To plan: write a plan file into the repo (e.g. under dydo/project/), applying the planner skill.");
            return ExitCodes.ToolError;
        }

        return null;
    }

    private static int HandleWriteOperation(
        string? filePath, string? toolName, GuardEnv env, string? agentType = null)
    {
        if (string.IsNullOrEmpty(filePath))
            return ExitCodes.Success;

        // Tool-scoped nudges (Decision 026 §4) apply to Tier-1 only: absence of
        // agent_type is the Tier-1 signal (Decision 024 verification). Tier-2 worker
        // calls carry agent_id and never reach this lane; the agent_type check covers
        // any anomalous payload that carries a type without an id.
        if (string.IsNullOrEmpty(agentType))
        {
            var nudged = CheckFileNudges(toolName, filePath, env.Config);
            if (nudged != null) return nudged.Value;
        }

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
        GuardContext ctx, string? filePath, string? searchPath,
        IOffLimitsService offLimitsService, IBashCommandAnalyzer bashAnalyzer,
        GuardEnv env)
    {
        if (ShouldRouteToShellHandler(ctx.ToolName, ctx.BashCommand))
            return HandleBashCommand(
                ctx.BashCommand!, ctx.SessionId,
                offLimitsService, bashAnalyzer, env, isWorker: true);

        var checkPath = filePath ?? searchPath;
        if (!string.IsNullOrEmpty(checkPath) && !IsNativeMemoryPath(checkPath))
        {
            var offLimitsBlock = BlockIfPathOffLimits(checkPath, offLimitsService);
            if (offLimitsBlock != null) return offLimitsBlock.Value;
        }

        return ExitCodes.Success;
    }

    /// <summary>
    /// Shared off-limits check for a direct (non-bash) file/search path. Returns an exit
    /// code if the path is off-limits, null otherwise. One copy for every lane so the
    /// block message and audit shape cannot drift.
    /// </summary>
    internal static int? BlockIfPathOffLimits(string path, IOffLimitsService offLimitsService)
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
        string? searchPath, string? toolName, IOffLimitsService offLimitsService)
    {
        if (string.Equals(toolName, "agent", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("NOTICE: You invoked Claude Code's built-in Agent tool. Sub-agent tool calls run in "
                + "the Tier-2 worker lane: anonymous, governed by the universal guard layers "
                + "(off-limits, dangerous-bash, nudges).");
        }

        if (!string.IsNullOrEmpty(searchPath))
        {
            var offLimitsBlock = BlockIfPathOffLimits(searchPath, offLimitsService);
            if (offLimitsBlock != null) return offLimitsBlock.Value;
        }

        return ExitCodes.Success;
    }

    /// <summary>
    /// Handle Bash tool commands with comprehensive analysis. Dangerous-pattern and nudge
    /// checks now run for EVERY shell command, dydo invocations included (DR-041): a dydo
    /// command line is no longer exempt from nudge evaluation, so a security-critical nudge
    /// can no longer be bypassed by prefixing a dydo call.
    /// </summary>
    private static int HandleBashCommand(
        string command,
        string? sessionId,
        IOffLimitsService offLimitsService,
        IBashCommandAnalyzer bashAnalyzer,
        GuardEnv env,
        bool isWorker = false)
    {
        var isDydo = IsDydoCommand(command) && !string.IsNullOrEmpty(sessionId);

        // Tier-2 workers don't run dydo commands — that machinery is the orchestrator's job.
        if (isDydo && isWorker)
        {
            Console.Error.WriteLine("BLOCKED: Sub-agents don't run dydo commands — that belongs to the");
            Console.Error.WriteLine("  top-level orchestrator, not a worker.");
            return ExitCodes.ToolError;
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

        // Configurable nudges — after hardcoded security checks, for every command.
        var nudged = CheckNudges(command, env);
        if (nudged != null) return nudged.Value;

        // dydo commands skip the cd-chain coaching (they are never a needless cd compound);
        // everything else gets the auto-approval coaching block.
        if (!isDydo)
        {
            var (isCdChain, _, restCmd) = bashAnalyzer.DetectNeedlessCd(command);
            if (isCdChain)
            {
                Console.Error.WriteLine("BLOCKED: Don't chain cd / Set-Location with other commands — it breaks auto-approval for whitelisted commands.");
                Console.Error.WriteLine($"  If you need to change directory, run it separately first.");
                Console.Error.WriteLine($"  Otherwise just run: {restCmd}");
                return ExitCodes.ToolError;
            }
        }

        return AnalyzeAndCheckBashOperations(command, offLimitsService, bashAnalyzer);
    }

    internal static int? CheckNudges(string command, GuardEnv env)
    {
        // Always include block-severity default nudges (H19 indirect dydo invocation) even if
        // removed from config. These are security-critical and must not be removable via dydo.json.
        var nudges = MergeSystemNudges(env.Config?.Nudges);
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
                // Warn = "block once, run again to proceed". The pass-through marker lives in
                // machine-local state (dydo/_system/.local/ — gitignored, scan-excluded), keyed
                // by pattern hash — global rather than per-agent (DR-041, identity-free model).
                var hash = ComputeNudgeHash(nudge.Pattern);
                Directory.CreateDirectory(env.MarkerDir);
                var markerPath = Path.Combine(env.MarkerDir, $".nudge-{hash}");

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
    internal static int? CheckFileNudges(string? toolName, string filePath, DydoConfig? config)
    {
        if (string.IsNullOrEmpty(toolName))
            return null;

        var nudges = config?.Nudges;
        if (nudges == null || nudges.Count == 0)
            return null;

        // Resolved lazily — most calls have no tool-scoped nudge for this tool.
        Dictionary<string, List<string>>? pathSets = null;
        string? relPath = null;

        foreach (var nudge in nudges)
        {
            if (nudge.Tools is not { Count: > 0 }) continue;
            if (!nudge.Tools.Any(t => t.Equals(toolName, StringComparison.OrdinalIgnoreCase))) continue;

            pathSets ??= new RoleDefinitionService().ResolvePathSets(config);
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
    /// Pre-2.1 shipped nudge message texts that must self-heal in existing installs.
    /// EnsureDefaultNudges dedupes by pattern, so a config materialized before 2.1 keeps
    /// these stale messages forever unless we rewrite them here. A message the USER edited
    /// matches nothing in this set and is left untouched — docs promise message editability.
    /// </summary>
    private static readonly HashSet<string> StaleNudgeMessages =
    [
        // The three poll-loop warn nudges (until/tail/while) still recommend the deleted `dydo wait`.
        "Open-ended Bash poll-loop detected. Prefer a bounded for i in {1..30}; do ...; sleep 1; done, or `gh run watch`, or `dydo wait` for dydo-native waits. Open-ended polls have caused agent crashes (issue 0177).",
        // Retired worktree block/warn nudges — no current default, so they are removed outright.
        "Use dydo worktree commands instead of git worktree directly.",
        "Use dydo worktree cleanup instead of deleting worktree directories directly.",
        "dydo worktree merge --force bypasses the pre-merge safety check and WILL destroy uncommitted files. If the list shown was only generated artifacts (under 'N generated artifacts ignored'), --force is safe. If any source/test/task files were listed as suspicious, commit them first — re-run to proceed anyway.",
    ];

    /// <summary>
    /// Reconcile config nudges with the shipped defaults. Two independent passes:
    ///   1. Self-heal known-stale shipped messages (ANY severity): a nudge still carrying a
    ///      pre-2.1 default text is refreshed to the current default (matched by pattern) or
    ///      dropped entirely when that default was retired. User-edited messages are untouched.
    ///   2. Guarantee the block-severity system nudges (H19 indirect invocation): always
    ///      present and never downgradable via dydo.json. A weakened severity is forced back
    ///      to block WITHOUT clobbering a user-customized message.
    /// </summary>
    internal static List<NudgeConfig> MergeSystemNudges(List<NudgeConfig>? configNudges)
    {
        var nudges = configNudges?.ToList() ?? [];

        for (int i = nudges.Count - 1; i >= 0; i--)
        {
            if (!StaleNudgeMessages.Contains(nudges[i].Message))
                continue;

            var current = ConfigFactory.DefaultNudges.FirstOrDefault(d => d.Pattern == nudges[i].Pattern);
            if (current == null)
                nudges.RemoveAt(i);
            else
                nudges[i] = current;
        }

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
                var existing = nudges[existingIndex];
                nudges[existingIndex] = new NudgeConfig
                {
                    Pattern = existing.Pattern,
                    Message = existing.Message,
                    Severity = "block",
                    Tools = existing.Tools
                };
            }
        }

        return nudges;
    }

    private static int AnalyzeAndCheckBashOperations(
        string command, IOffLimitsService offLimitsService, IBashCommandAnalyzer bashAnalyzer)
    {
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
            var blocked = CheckBashFileOperation(op, offLimitsService);
            if (blocked != null) return blocked.Value;
        }

        return ExitCodes.Success;
    }

    internal static int? CheckBashFileOperation(FileOperation op, IOffLimitsService offLimitsService)
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
    /// Collapses '.'/'..' segments lexically so no traversal sequence
    /// ('.../memory/../../secret') can slip past a path-based guard check (off-limits,
    /// native-memory). Pure normalization — no filesystem or worktree remapping.
    /// </summary>
    internal static string? ResolveTraversal(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        return PathUtils.CollapseRelativeSegments(path);
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

    // How often the guard checks for expired model caps to restore. The watchdog tick that
    // used to drive ModelCapService.RestoreExpired is gone (DR-041); the guard now self-triggers
    // it — throttled like the daily validation so the hot path stays cheap. A restore only ever
    // does real work when a cap marker's reset time has actually passed.
    private const int ModelCapRestoreThrottleMinutes = 5;

    /// <summary>
    /// Restores any model cap whose reset time has passed, at most once per throttle window.
    /// Cheap when there is nothing to do (RestoreExpired no-ops without a marker directory),
    /// and never allowed to break the guard.
    ///
    /// Concurrency: two hook processes (main thread + a subagent) can both clear the throttle
    /// on a stale stamp and race into RestoreExpired → SaveConfig. This is left unserialized on
    /// purpose — the restore is convergent: both processes rebind the same tiers to the same
    /// original model, write the same config, and TryDelete the same marker (one wins, the other
    /// swallows the not-found). Last-writer-wins produces the identical file, so the race is
    /// benign and a lock would add cleanup/staleness burden for no correctness gain.
    /// </summary>
    private static void RestoreExpiredModelCapsIfDue()
    {
        try
        {
            var basePath = Environment.CurrentDirectory;
            var stampPath = Path.Combine(basePath, "dydo", "_system", ".local", "last-model-cap-restore");

            if (File.Exists(stampPath)
                && (DateTime.UtcNow - File.GetLastWriteTimeUtc(stampPath)).TotalMinutes < ModelCapRestoreThrottleMinutes)
                return;

            ModelCapService.RestoreExpired(DateTimeOffset.Now, basePath);

            PathUtils.EnsureLocalDirExists(Path.Combine(basePath, "dydo"));
            File.WriteAllText(stampPath, DateTime.UtcNow.ToString("O"));
        }
        catch
        {
            // Model-cap restore must never break the guard.
        }
    }

    // How often the guard refreshes the watchdog activity stamp (watchdog-autostart-lease). The hot path is ONE
    // File stat: a stamp younger than this means a session is already active and a daemon (if warranted) is already
    // running — nothing to do. Only a stale/missing stamp pays the refresh + auto-start attempt.
    private const int WatchdogActivityThrottleMinutes = 5;

    /// <summary>
    /// Refreshes the watchdog activity stamp and, when warranted, auto-starts the Notion-sync daemon — at most once
    /// per throttle window (watchdog-autostart-lease). The daemon leases against this stamp: it runs while someone
    /// works in the project and self-exits an hour after the guard stops refreshing. The spawn decision (Notion
    /// configured, no suppress marker, no live daemon) lives in <see cref="WatchdogService.AutoStart"/>; a fresh
    /// stamp short-circuits before any of it, so the steady per-call cost stays a single stat.
    /// </summary>
    internal static void AutoStartWatchdogIfDue()
    {
        try
        {
            var dydoRoot = Path.Combine(Environment.CurrentDirectory, "dydo");
            var stampPath = WatchdogService.ActivityStampPath(dydoRoot);

            // Hot path is ONE stat: File.GetLastWriteTimeUtc returns the 1601 sentinel for a missing stamp — far
            // older than the throttle window — so a first-ever call falls straight through to refresh + auto-start.
            if ((DateTime.UtcNow - File.GetLastWriteTimeUtc(stampPath)).TotalMinutes < WatchdogActivityThrottleMinutes)
                return;

            PathUtils.EnsureLocalDirExists(dydoRoot);
            File.WriteAllText(stampPath, DateTime.UtcNow.ToString("O"));

            WatchdogService.AutoStart(dydoRoot);
        }
        catch
        {
            // Auto-start must never break or block the guard.
        }
    }
}
