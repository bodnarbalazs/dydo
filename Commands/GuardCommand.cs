namespace DynaDocs.Commands;

using System.CommandLine;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DynaDocs.Models;
using DynaDocs.Serialization;
using DynaDocs.Services;
using DynaDocs.Utils;

/// <summary>
/// Security layers (checked in order):
/// 1. Off-limits patterns (files-off-limits.md) - block every operation
/// 1b. Protected patterns (files-off-limits.md) - block writes and deletes, allow reads
/// 2. Dangerous shell-command patterns - always blocked
/// 3. Shell command analysis - extracts file operations and checks each
///
/// Input modes:
/// 1. Stdin JSON (hook mode) - receives JSON from hook system
/// 2. CLI arguments (manual testing) - --action and --path flags
///
/// Output:
/// - Exit code 0, empty stdout = action allowed
/// - Hook-mode refusal: exit code 0 with one PreToolUse deny-decision object on stdout
///   (GuardCommand.Refusal.cs); the same text also goes to stderr
/// - Hook-mode ask (the mode-set rerun on Claude Code only): exit 0 with one ask-decision object
/// - CLI-mode or non-PreToolUse-event refusal: exit code 2, message on stderr
/// </summary>
public static partial class GuardCommand
{
    /// <summary>
    /// MarkerDir holds the warn-nudge pass-through markers in dydo/_system/.local/, which is
    /// gitignored and scan-excluded. ProjectRoot (the directory of the nearest dydo.json) anchors
    /// new-file nudge paths.
    /// </summary>
    internal sealed record GuardEnv(DydoConfig? Config, string MarkerDir, string? ProjectRoot = null)
    {
        private HostMemoryFolders? _memory;

        // Loaded on first use: only a call that names a path pays for the settings reads.
        public HostMemoryFolders Memory => _memory ??= HostMemoryFolders.Load(ProjectRoot);

        public static GuardEnv Load(string? basePath = null)
        {
            var configService = new ConfigService();
            return new GuardEnv(
                configService.LoadConfig(basePath),
                Path.Combine(configService.GetDydoRoot(basePath), "_system", ".local"),
                configService.GetProjectRoot(basePath));
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

        var hostOption = new Option<string?>("--host")
        {
            Description = "The host whose hook runs the guard; dydo init writes 'claude' into the Claude Code hook"
        };

        var command = new Command("guard", "Check if current agent can perform action (used by hooks)");
        command.Options.Add(actionOption);
        command.Options.Add(pathOption);
        command.Options.Add(commandOption);
        command.Options.Add(hostOption);

        command.SetAction(parseResult =>
        {
            var cliAction = parseResult.GetValue(actionOption);
            var cliPath = parseResult.GetValue(pathOption);
            var cliCommand = parseResult.GetValue(commandOption);
            return Execute(cliAction, cliPath, cliCommand, parseResult.GetValue(hostOption));
        });

        return command;
    }

    private static readonly HashSet<string> SearchTools = new(StringComparer.OrdinalIgnoreCase) { "glob", "grep", "agent" };

    // bash/powershell are Claude's shell tools; shell_command/exec/local_shell/unified_exec are
    // Codex's, one per mode (issue 0295). Without Codex's names the call reaches the guard but never
    // the shell analyzer, so off-limits, dangerous-bash and git-safety silently do not bind. Keep in
    // lockstep with InitCommand.CodexShellTools, which puts the same names in the hook matcher.
    private static readonly HashSet<string> ShellTools = new(StringComparer.OrdinalIgnoreCase)
        { "bash", "powershell", "shell_command", "exec", "local_shell", "unified_exec" };

    // Actions that mutate the file they name. The protected tier binds on these only, so an
    // agent can still read its orientation files. The hook maps Edit/Write/NotebookEdit here,
    // and the CLI lane carries the verb directly (arg mode has no tool name - issue 0302).
    private static readonly HashSet<string> MutatingActions =
        new(StringComparer.OrdinalIgnoreCase) { "write", "edit", "delete" };

    // Codex delivers file edits as apply_patch, which HookInputExtensions maps to no action at
    // all. It is in the Codex hook matcher precisely as the Codex form of Edit/Write, so it is
    // named here too - otherwise the protected tier would bind on Claude's lane only.
    private static readonly HashSet<string> MutatingTools =
        new(StringComparer.OrdinalIgnoreCase) { "apply_patch" };

    private static bool IsMutatingCall(string? action, string? toolName) =>
        (action != null && MutatingActions.Contains(action))
        || (toolName != null && MutatingTools.Contains(toolName));

    private static bool ShouldRouteToShellHandler(string? toolName, string? bashCommand)
    {
        if (toolName == null) return false;
        if (!ShellTools.Contains(toolName)) return false;
        return !string.IsNullOrEmpty(bashCommand);
    }

    private record struct GuardContext(
        string? FilePath, string? Action, string? BashCommand,
        string? ToolName, string? SessionId, string? SearchPath,
        bool HasCliArgs, string? AgentId, string? AgentType,
        IReadOnlyList<WriteTarget> WriteTargets, string? Cwd, string? HookEventName,
        string? PermissionMode, string? Host, bool FromCodex);

    // --host is the hook's own marker, not a diagnostic argument: a call carrying only it still
    // reads its payload from stdin and answers as a hook.
    private static GuardContext ParseInput(string? cliAction, string? cliPath, string? cliCommand, string? host)
    {
        var hasCliArgs = cliAction != null || cliPath != null || cliCommand != null;
        string? filePath = null, action = null, bashCommand = null;
        string? toolName = null, sessionId = null, searchPath = null;
        string? agentId = null, agentType = null, cwd = null, hookEventName = null, permissionMode = null;
        var fromCodex = false;
        IReadOnlyList<WriteTarget> writeTargets = [];

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
                    writeTargets = hookInput.GetWriteTargets();
                    cwd = hookInput.Cwd;
                    hookEventName = hookInput.HookEventName;
                    permissionMode = hookInput.PermissionMode;
                    fromCodex = hookInput.TurnId != null;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"WARNING: Failed to parse hook input: {ex.Message}");
            }
        }

        // A CLI-provided --command is a shell command by definition, but arg mode has no hook
        // tool_name - without one, ShouldRouteToShellHandler never fires and the documented
        // manual-testing lane silently allows everything (issue 0302). Route it as bash.
        if (toolName == null && cliCommand != null)
            toolName = "bash";

        return new GuardContext(
            filePath ?? cliPath,
            action ?? cliAction ?? "edit",
            bashCommand ?? cliCommand,
            toolName, sessionId, searchPath, hasCliArgs,
            agentId, agentType, writeTargets, cwd, hookEventName, permissionMode, host, fromCodex);
    }

    private static int Execute(string? cliAction, string? cliPath, string? cliCommand, string? host)
    {
        // Everything the call writes to stderr is held, so a PreToolUse refusal can carry the
        // whole text as its deny reason, then replayed to stderr unchanged.
        var stderr = Console.Error;
        var transcript = new StringWriter();
        Console.SetError(transcript);
        GuardContext ctx;
        int exitCode;
        try
        {
            ctx = ParseInput(cliAction, cliPath, cliCommand, host);
            exitCode = Evaluate(ctx);
        }
        finally
        {
            Console.SetError(stderr);
            stderr.Write(transcript.ToString());
        }

        return Conclude(ctx, exitCode, transcript.ToString());
    }

    private static int Evaluate(GuardContext ctx)
    {
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

        var normalized = ctx with
        {
            FilePath = ResolveTraversal(ctx.FilePath),
            SearchPath = ResolveTraversal(ctx.SearchPath),
            WriteTargets = ResolvePatchTargets(ctx.WriteTargets, ctx.Cwd)
        };
        var sessionId = normalized.SessionId;
        var filePath = normalized.FilePath;
        var searchPath = normalized.SearchPath;

        // Calls carrying agent_id come from sub-agents (Decision 024).
        if (!ctx.HasCliArgs && !string.IsNullOrEmpty(ctx.AgentId))
        {
            return HandleWorkerCall(normalized, filePath, searchPath, offLimitsService, bashAnalyzer, env);
        }

        var memoryWrite = BlockIfHostMemoryWrite(normalized, env);
        if (memoryWrite != null) return memoryWrite.Value;

        // Host memory sits outside the repo: reads skip the repo's off-limits patterns.
        if (!string.IsNullOrEmpty(filePath) && env.Memory.Contains(filePath))
            return ExitCodes.Success;

        var routed = RouteToolLayers(normalized, offLimitsService, bashAnalyzer, env);
        if (routed != null) return routed.Value;

        return ExitCodes.Success;
    }

    /// <summary>
    /// Returns an exit code when the call was fully handled, null to fall through.
    /// </summary>
    private static int? RouteToolLayers(GuardContext context, OffLimitsService offLimitsService,
        IBashCommandAnalyzer bashAnalyzer, GuardEnv env)
    {
        if (!string.IsNullOrEmpty(context.FilePath))
        {
            var blocked = BlockIfPathOffLimits(context.FilePath, offLimitsService);
            if (blocked != null) return blocked.Value;

            if (IsMutatingCall(context.Action, context.ToolName))
            {
                var protectedBlock = BlockIfPathProtected(context.FilePath, offLimitsService);
                if (protectedBlock != null) return protectedBlock.Value;
            }
        }

        var patchBlock = BlockIfPatchTargetOffLimitsOrProtected(context, offLimitsService);
        if (patchBlock != null) return patchBlock.Value;

        // New-file nudges come after off-limits / protected so a nudge never masks a block.
        var newFileNudge = CheckNewFileNudges(NewFileCandidates(context), context.Cwd, env);
        if (newFileNudge != null) return newFileNudge.Value;

        if (ShouldRouteToShellHandler(context.ToolName, context.BashCommand))
        {
            return HandleBashCommand(context, offLimitsService, bashAnalyzer, env);
        }

        if (context.ToolName != null && SearchTools.Contains(context.ToolName))
        {
            return HandleSearchTool(context.SearchPath, context.ToolName, offLimitsService);
        }

        if (context.ToolName == "enterplanmode" || context.ToolName == "exitplanmode")
        {
            Console.Error.WriteLine("BLOCKED: Dydo agents don't use Claude Code's built-in plan mode.");
            Console.Error.WriteLine("  To plan: write a plan record into the repo or Linear, applying the Wayfinder or Code Writer skill.");
            return ExitCodes.ToolError;
        }

        return null;
    }

    /// <summary>
    /// The host memory folders DR 055 closes to agent writes. Claude: projects/*/memory/ and
    /// agent-memory*/ under every ~/.claude* profile and CLAUDE_CONFIG_DIR, any .claude/agent-memory*/
    /// (the project's), and each autoMemoryDirectory the settings name. Codex: $CODEX_HOME/memories/.
    /// Reads stay open; the guard stops accidents, so only plain paths are matched.
    /// </summary>
    internal sealed record HostMemoryFolders(
        string Home, string? ClaudeConfigDir, string CodexHome, IReadOnlyList<string> AutoMemoryDirectories)
    {
        public static HostMemoryFolders Load(string? projectRoot)
        {
            var home = Normalize(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "");
            var configDir = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
            configDir = string.IsNullOrWhiteSpace(configDir) ? null : Normalize(configDir, home);
            var codexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
            codexHome = string.IsNullOrWhiteSpace(codexHome) ? $"{home}/.codex" : Normalize(codexHome, home);

            var settingsFiles = new List<string> { Path.Combine(configDir ?? $"{home}/.claude", "settings.json") };
            if (projectRoot != null)
            {
                settingsFiles.Add(Path.Combine(projectRoot, ".claude", "settings.json"));
                settingsFiles.Add(Path.Combine(projectRoot, ".claude", "settings.local.json"));
            }

            var autoMemory = settingsFiles
                .Select(ReadAutoMemoryDirectory)
                .Where(dir => dir != null)
                .Select(dir => Normalize(dir!, home))
                .Where(dir => Path.IsPathRooted(dir))
                .ToList();
            return new HostMemoryFolders(home, configDir, codexHome, autoMemory);
        }

        public bool Contains(string path)
        {
            var normalized = Normalize(path, Home);
            if (AutoMemoryDirectories.Append($"{CodexHome}/memories").Any(dir => IsUnder(normalized, dir)))
                return true;

            var segments = normalized.Split('/');
            for (var i = 0; i + 1 < segments.Length; i++)
            {
                if (segments[i].Equals(".claude", StringComparison.OrdinalIgnoreCase) && IsAgentMemory(segments[i + 1]))
                    return true;
            }

            return ClaudeRootRemainders(normalized).Any(rest =>
                (rest.Length > 0 && IsAgentMemory(rest[0]))
                || (rest.Length >= 3 && rest[0].Equals("projects", StringComparison.OrdinalIgnoreCase)
                    && rest[2].Equals("memory", StringComparison.OrdinalIgnoreCase)));
        }

        // The segments below each Claude config root the path sits under: a ~/.claude* profile, or
        // CLAUDE_CONFIG_DIR.
        private IEnumerable<string[]> ClaudeRootRemainders(string normalized)
        {
            if (!string.IsNullOrEmpty(Home) && normalized.StartsWith(Home + "/", StringComparison.OrdinalIgnoreCase))
            {
                var underHome = normalized[(Home.Length + 1)..].Split('/');
                if (underHome[0].StartsWith(".claude", StringComparison.OrdinalIgnoreCase))
                    yield return underHome[1..];
            }

            if (ClaudeConfigDir != null && normalized.StartsWith(ClaudeConfigDir + "/", StringComparison.OrdinalIgnoreCase))
                yield return normalized[(ClaudeConfigDir.Length + 1)..].Split('/');
        }

        private static bool IsAgentMemory(string segment) =>
            segment.StartsWith("agent-memory", StringComparison.OrdinalIgnoreCase);

        private static bool IsUnder(string path, string folder) =>
            path.Equals(folder, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase);

        // Forward slashes, '~' expanded, a Git Bash /c/ drive mount read as C:/, '..' collapsed.
        private static string Normalize(string path, string home)
        {
            var p = path.Trim().Replace('\\', '/');
            if (p == "~" || p.StartsWith("~/", StringComparison.Ordinal))
                p = home + p[1..];
            if (OperatingSystem.IsWindows() && p.Length >= 3 && p[0] == '/' && char.IsLetter(p[1]) && p[2] == '/')
                p = $"{char.ToUpperInvariant(p[1])}:{p[2..]}";
            return PathUtils.CollapseRelativeSegments(p).TrimEnd('/');
        }

        private static string? ReadAutoMemoryDirectory(string settingsPath)
        {
            try
            {
                if (!File.Exists(settingsPath))
                    return null;
                var value = JsonNode.Parse(File.ReadAllText(settingsPath))?["autoMemoryDirectory"];
                return value is JsonValue json && json.TryGetValue<string>(out var dir) && dir.Length > 0 ? dir : null;
            }
            catch (Exception)
            {
                // A settings file the guard cannot read names no memory folder; Claude reports it.
                return null;
            }
        }
    }

    /// <summary>
    /// Blocks a file-tool write (Write, Edit, NotebookEdit, apply_patch) whose target is a host
    /// memory folder (DR 055). Reads fall through.
    /// </summary>
    private static int? BlockIfHostMemoryWrite(GuardContext ctx, GuardEnv env)
    {
        var targets = IsMutatingCall(ctx.Action, ctx.ToolName) && !string.IsNullOrEmpty(ctx.FilePath)
            ? ctx.WriteTargets.Select(t => t.Path).Prepend(ctx.FilePath)
            : ctx.WriteTargets.Select(t => t.Path);
        var hit = targets.FirstOrDefault(env.Memory.Contains);
        return hit == null ? null : ReportHostMemoryWrite(hit, null);
    }

    private static int ReportHostMemoryWrite(string path, string? detected)
    {
        Console.Error.WriteLine("BLOCKED: Agents do not write host memory (DR 055).");
        Console.Error.WriteLine($"  Path: {path}");
        if (detected != null)
            Console.Error.WriteLine($"  Detected: {detected}");
        Console.Error.WriteLine("  Put the lesson in a skill, a Decision Record or a doc instead.");
        Console.Error.WriteLine("  See dydo/project/decisions/055-native-agent-memory-off-and-guarded.md");
        return ExitCodes.ToolError;
    }

    /// <summary>
    /// Sub-agent tool calls (agent_id present). Bash runs the shared pipeline with the worker's
    /// dydo-subcommand limit; direct file ops get the host-memory write block, then off-limits and
    /// protected checks, host memory reads exempt from off-limits.
    /// </summary>
    private static int HandleWorkerCall(
        GuardContext ctx, string? filePath, string? searchPath,
        OffLimitsService offLimitsService, IBashCommandAnalyzer bashAnalyzer,
        GuardEnv env)
    {
        if (ShouldRouteToShellHandler(ctx.ToolName, ctx.BashCommand))
            return HandleBashCommand(ctx, offLimitsService, bashAnalyzer, env, isWorker: true);

        var memoryWrite = BlockIfHostMemoryWrite(ctx, env);
        if (memoryWrite != null) return memoryWrite.Value;

        var checkPath = filePath ?? searchPath;
        if (!string.IsNullOrEmpty(checkPath) && !env.Memory.Contains(checkPath))
        {
            var offLimitsBlock = BlockIfPathOffLimits(checkPath, offLimitsService);
            if (offLimitsBlock != null) return offLimitsBlock.Value;
        }

        if (!string.IsNullOrEmpty(filePath))
        {
            if (IsMutatingCall(ctx.Action, ctx.ToolName))
            {
                var protectedBlock = BlockIfPathProtected(filePath, offLimitsService);
                if (protectedBlock != null) return protectedBlock.Value;
            }
        }

        var patchBlock = BlockIfPatchTargetOffLimitsOrProtected(ctx, offLimitsService);
        if (patchBlock != null) return patchBlock.Value;

        return CheckNewFileNudges(NewFileCandidates(ctx), ctx.Cwd, env) ?? ExitCodes.Success;
    }

    /// <summary>
    /// A Codex apply_patch carries no file_path, so the off-limits and protected tiers judge each
    /// of its targets, Update File and Delete File included; every one writes or deletes. Targets
    /// arrive resolved by <see cref="ResolvePatchTargets"/>.
    /// </summary>
    private static int? BlockIfPatchTargetOffLimitsOrProtected(GuardContext ctx, OffLimitsService offLimitsService)
    {
        foreach (var target in ctx.WriteTargets.Where(t => t.FromPatch))
        {
            var block = BlockIfPathOffLimits(target.Path, offLimitsService) ?? BlockIfPathProtected(target.Path, offLimitsService);
            if (block != null) return block;
        }

        return null;
    }

    /// <summary>
    /// One resolver for every tier that judges a patch target (host memory, off-limits, protected),
    /// so they cannot drift: a relative target resolves where Codex applies it, the hook cwd or,
    /// without one, the process working directory, '..' collapsed. A '~' path stays as written for
    /// host memory to expand. A Claude tool path is left to its own normalisation.
    /// </summary>
    internal static IReadOnlyList<WriteTarget> ResolvePatchTargets(IReadOnlyList<WriteTarget> targets, string? cwd)
    {
        var baseDir = string.IsNullOrEmpty(cwd) ? Environment.CurrentDirectory : cwd;
        return targets
            .Select(t => t.FromPatch && !t.Path.StartsWith('~')
                ? t with { Path = PathUtils.CollapseRelativeSegments(Path.Combine(baseDir, t.Path)) }
                : t)
            .ToList();
    }

    // New-file nudges stay new-file by tier: only targets that can create a file, whatever is on disk.
    private static List<string> NewFileCandidates(GuardContext ctx) =>
        ctx.WriteTargets.Where(t => t.MayCreate).Select(t => t.Path).ToList();

    /// <summary>
    /// One copy for every lane so the block message cannot drift.
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

    /// <summary>
    /// Shared protected-tier check (DR 045 §10) for a path the caller is about to change.
    /// Callers gate on the operation being a write or a delete; this one only decides whether
    /// the path is protected and reports it. <paramref name="detected"/> carries the bash
    /// analyzer's op description so a shell block names what it saw.
    /// </summary>
    internal static int? BlockIfPathProtected(
        string path, OffLimitsService offLimitsService, string? detected = null)
    {
        var protectedPattern = offLimitsService.IsPathProtected(path);
        if (protectedPattern == null)
            return null;

        Console.Error.WriteLine("BLOCKED: Path is protected - every agent may read it, none may write or delete it.");
        Console.Error.WriteLine($"  Path: {path}");
        Console.Error.WriteLine($"  Pattern: {protectedPattern}");
        if (detected != null)
            Console.Error.WriteLine($"  Detected: {detected}");
        Console.Error.WriteLine("  This file is human-owned: read it freely, and ask the user for any change.");
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
    /// Dangerous-pattern and nudge checks run for every shell command, dydo invocations included,
    /// so a security-critical nudge cannot be bypassed by prefixing a dydo call (DR-041). Every hard
    /// block runs before any warn nudge, so a refused call neither arms nor spends a warn's once-only
    /// marker and the refusal repeats on every try (DYD-386).
    /// </summary>
    private static int HandleBashCommand(
        GuardContext ctx,
        OffLimitsService offLimitsService,
        IBashCommandAnalyzer bashAnalyzer,
        GuardEnv env,
        bool isWorker = false)
    {
        var command = ctx.BashCommand!;
        var isDydo = IsDydoCommand(command) && !string.IsNullOrEmpty(ctx.SessionId);

        // dydo commands skip the cd-chain coaching (they are never a needless cd compound).
        var hardBlock = (isDydo && isWorker ? BlockIfWorkerDydoCommand(command) : null)
            ?? BlockIfDangerous(command, bashAnalyzer)
            ?? CheckBlockNudges(command, env)
            ?? CheckPrReady(command, ctx.Cwd, env)
            ?? (isDydo ? null : BlockIfCdChain(command, bashAnalyzer))
            ?? AnalyzeAndCheckBashOperations(command, offLimitsService, bashAnalyzer, env.Memory);
        if (hardBlock != null) return hardBlock.Value;

        var caller = ModeSetCallerOf(ctx);
        var modeSet = FindModeSetArgs(command, caller.Shell);
        if (modeSet != null && !caller.CanAsk)
            return RefuseModeSet(modeSet);

        return CheckWarnNudges(command, env, modeSet != null)
            ?? (modeSet == null ? ExitCodes.Success : AskModeSet(modeSet));
    }

    private static int? BlockIfWorkerDydoCommand(string command)
    {
        var workerBlocked = FindWorkerBlockedDydoCommand(command);
        if (workerBlocked == null)
            return null;

        Console.Error.WriteLine("BLOCKED: Sub-agents may run only these dydo commands: "
            + string.Join(", ", WorkerAllowedDydoSubcommands) + " (plus -h/--help/--version).");
        Console.Error.WriteLine($"  Not allowed here: dydo {workerBlocked}");
        Console.Error.WriteLine("  Setup, generation and hook commands (init, adopt, index, guard, map, completions, _complete)");
        Console.Error.WriteLine("  belong to the top-level session that spawned you.");
        return ExitCodes.ToolError;
    }

    private static int? BlockIfDangerous(string command, IBashCommandAnalyzer bashAnalyzer)
    {
        var (isDangerous, dangerReason) = bashAnalyzer.CheckDangerousPatterns(command);
        if (!isDangerous)
            return null;

        Console.Error.WriteLine("BLOCKED: Dangerous command pattern detected.");
        Console.Error.WriteLine($"  Reason: {dangerReason}");
        Console.Error.WriteLine($"  Command: {TruncateCommand(command)}");
        return ExitCodes.ToolError;
    }

    // The auto-approval coaching block.
    private static int? BlockIfCdChain(string command, IBashCommandAnalyzer bashAnalyzer)
    {
        var (isCdChain, _, restCmd) = bashAnalyzer.DetectNeedlessCd(command);
        if (!isCdChain)
            return null;

        Console.Error.WriteLine("BLOCKED: Don't chain cd / Set-Location with other commands - it breaks auto-approval for whitelisted commands.");
        Console.Error.WriteLine($"  If you need to change directory, run it separately first.");
        Console.Error.WriteLine($"  Otherwise just run: {restCmd}");
        return ExitCodes.ToolError;
    }

    /// <summary>
    /// The command nudges that are not warns: a notice prints and passes, a block refuses.
    /// </summary>
    internal static int? CheckBlockNudges(string command, GuardEnv env)
    {
        foreach (var (nudge, match) in MatchingCommandNudges(command, env))
        {
            if (IsWarn(nudge)) continue;

            var verdict = EnforceNudge(nudge, match);
            if (verdict != null) return verdict;
        }

        return null;
    }

    /// <summary>
    /// The command warn nudges, and the mode-set reminder when <paramref name="modeSet"/>, as one
    /// once-only step (<see cref="EnforceWarns"/>).
    /// </summary>
    internal static int? CheckWarnNudges(string command, GuardEnv env, bool modeSet = false)
    {
        var warns = MatchingCommandNudges(command, env)
            .Where(m => IsWarn(m.Nudge))
            .Select(m => new WarnStep(m.Nudge, m.Match, $".nudge-{ComputeNudgeHash(m.Nudge.Pattern)}",
                "Run the same command again to proceed anyway."))
            .ToList();
        if (modeSet)
            warns.Add(ModeSetWarn);
        return EnforceWarns(warns, env);
    }

    private static IEnumerable<(NudgeConfig Nudge, Match Match)> MatchingCommandNudges(string command, GuardEnv env)
    {
        foreach (var nudge in MergeSystemNudges(env.Config?.Nudges))
        {
            if (!nudge.IsCommandMatch) continue;

            var match = CompileNudgePattern(nudge.Pattern)?.Match(command);
            if (match is { Success: true })
                yield return (nudge, match);
        }
    }

    private static bool IsWarn(NudgeConfig nudge) =>
        string.Equals(nudge.Severity, "warn", StringComparison.OrdinalIgnoreCase);

    private static Regex? CompileNudgePattern(string pattern)
    {
        try { return new Regex(pattern, RegexOptions.IgnoreCase); }
        catch (ArgumentException) { return null; }
    }

    /// <summary>
    /// Applies a matched notice or block nudge. Returns an exit code when the call is blocked, null
    /// to continue with the next nudge.
    /// </summary>
    private static int? EnforceNudge(NudgeConfig nudge, Match match)
    {
        var message = ExpandNudgeMessage(nudge, match);
        if (string.Equals(nudge.Severity, "notice", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"NOTICE: {message}");
            return null;
        }

        Console.Error.WriteLine($"BLOCKED: {message}");
        return ExitCodes.ToolError;
    }

    private static string ExpandNudgeMessage(NudgeConfig nudge, Match match)
    {
        var message = nudge.Message;
        for (int i = 1; i < match.Groups.Count; i++)
            message = message.Replace($"${i}", match.Groups[i].Value.Trim());
        return message;
    }

    /// <summary>
    /// A matched warn nudge. <see cref="MarkerName"/> names its pass-through marker, so each nudge
    /// lane keys its own.
    /// </summary>
    private readonly record struct WarnStep(NudgeConfig Nudge, Match Match, string MarkerName, string RetryHint);

    /// <summary>
    /// Warns block once and pass the rerun, all of a call's warns together: while any has no marker,
    /// each unmarked one gets its marker and its message and the call is blocked; once all are
    /// marked, the call spends every marker and passes. The marker is global, not per-agent (DR-041).
    /// Callers run this only after every hard block on the call has passed (DYD-386).
    /// </summary>
    private static int? EnforceWarns(IReadOnlyList<WarnStep> warns, GuardEnv env)
    {
        if (warns.Count == 0)
            return null;

        Directory.CreateDirectory(env.MarkerDir);
        var unmarked = warns.Where(w => !File.Exists(Path.Combine(env.MarkerDir, w.MarkerName))).ToList();
        if (unmarked.Count == 0)
        {
            foreach (var warn in warns)
                File.Delete(Path.Combine(env.MarkerDir, warn.MarkerName));
            return null;
        }

        foreach (var warn in unmarked)
        {
            File.WriteAllText(Path.Combine(env.MarkerDir, warn.MarkerName), DateTime.UtcNow.ToString("o"));
            Console.Error.WriteLine($"BLOCKED: {ExpandNudgeMessage(warn.Nudge, warn.Match)}");
            Console.Error.WriteLine($"  ({warn.RetryHint})");
        }

        return ExitCodes.ToolError;
    }

    internal static string ComputeNudgeHash(string pattern)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(pattern));
        return Convert.ToHexString(bytes)[..8].ToLowerInvariant();
    }

    /// <summary>
    /// Shipped default texts that a dydo.json can still carry; nothing rewrites dydo.json nudges
    /// (init --join included), so the guard refreshes a match to the current default for its
    /// pattern, or drops it when none remains. A message the user edited matches nothing here and
    /// stays.
    /// </summary>
    private static readonly HashSet<string> StaleNudgeMessages =
    [
        "Open-ended Bash poll-loop detected. Prefer a bounded for i in {1..30}; do ...; sleep 1; done, or `gh run watch`, or `dydo wait` for dydo-native waits. Open-ended polls have caused agent crashes (issue 0177).",
        "Use dydo worktree commands instead of git worktree directly.",
        "Use dydo worktree cleanup instead of deleting worktree directories directly.",
        "dydo worktree merge --force bypasses the pre-merge safety check and WILL destroy uncommitted files. If the list shown was only generated artifacts (under 'N generated artifacts ignored'), --force is safe. If any source/test/task files were listed as suspicious, commit them first - re-run to proceed anyway.",
        "Tier-1 agents are managers (Decision 026): delegate implementation to a run-sprint workflow unless this change is trivial. Rule of thumb: if it needs a reviewer, it needs a workflow.",
    ];

    /// <summary>
    /// A nudge's identity is its match form and pattern: a new-file entry never satisfies,
    /// replaces, or is rebuilt by a command default, whatever its pattern. Only the block-severity
    /// system nudges (H19 indirect invocation, all command nudges) are forced: they cannot be
    /// removed or downgraded via dydo.json, and a weakened severity is forced back to block without
    /// clobbering a customized message. Non-block defaults, the new-file notice included, are never
    /// added back here.
    /// </summary>
    internal static List<NudgeConfig> MergeSystemNudges(List<NudgeConfig>? configNudges)
    {
        var nudges = configNudges?.ToList() ?? [];

        for (int i = nudges.Count - 1; i >= 0; i--)
        {
            if (!nudges[i].IsCommandMatch || !StaleNudgeMessages.Contains(nudges[i].Message))
                continue;

            var current = ConfigFactory.DefaultNudges.FirstOrDefault(d => d.IsCommandMatch && d.Pattern == nudges[i].Pattern);
            if (current == null)
                nudges.RemoveAt(i);
            else
                nudges[i] = current;
        }

        foreach (var defaultNudge in ConfigFactory.DefaultNudges)
        {
            if (!defaultNudge.IsCommandMatch || !string.Equals(defaultNudge.Severity, "block", StringComparison.OrdinalIgnoreCase))
                continue;

            var existingIndex = nudges.FindIndex(n => n.IsCommandMatch && n.Pattern == defaultNudge.Pattern);
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
                    Audience = existing.Audience,
                    Match = existing.Match
                };
            }
        }

        return nudges;
    }

    private static int? AnalyzeAndCheckBashOperations(
        string command, OffLimitsService offLimitsService, IBashCommandAnalyzer bashAnalyzer,
        HostMemoryFolders memory)
    {
        var analysis = bashAnalyzer.Analyze(command);

        foreach (var warning in analysis.Warnings)
            Console.Error.WriteLine($"WARNING: {warning}");

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
            var blocked = CheckBashFileOperation(op, offLimitsService, memory);
            if (blocked != null) return blocked.Value;
        }

        return null;
    }

    internal static int? CheckBashFileOperation(
        FileOperation op, OffLimitsService offLimitsService, HostMemoryFolders memory)
    {
        // DR 055: a shell op that can change a host memory file is blocked; reading one is not,
        // and a copy only reads its sources (`cp <memory>/MEMORY.md notes.md`).
        if (memory.Contains(op.Path))
            return op.Type is FileOperationType.Read or FileOperationType.Execute || op.IsCopySource
                ? null
                : ReportHostMemoryWrite(op.Path, $"{op.Type} via {op.Command}");

        var offLimitsPattern = offLimitsService.IsPathOffLimits(op.Path);
        if (offLimitsPattern != null)
        {
            Console.Error.WriteLine("BLOCKED: Command references off-limits path.");
            Console.Error.WriteLine($"  Path: {op.Path}");
            Console.Error.WriteLine($"  Pattern: {offLimitsPattern}");
            Console.Error.WriteLine($"  Detected: {op.Type} via {op.Command}");
            return ExitCodes.ToolError;
        }

        // Anything that is not a read of op.Path can leave different bytes or different
        // permissions there: writes, deletes, moves, copies, chmod/chown/takeown. Stated as an
        // exclusion so a future op type is guarded by default instead of silently exempt, and
        // because the analyzer tags a copy's source and destination alike - `cp <protected>
        // elsewhere` is blocked too. Conservative on purpose: the content stays readable through
        // Read, cat and head. Execute names a binary to run, not a file to change.
        if (op.Type is not (FileOperationType.Read or FileOperationType.Execute))
            return BlockIfPathProtected(op.Path, offLimitsService, $"{op.Type} via {op.Command}");

        return null;
    }

    /// <summary>
    /// Collapses '.'/'..' segments lexically so no traversal sequence
    /// ('.../memory/../../secret') can slip past a path-based guard check (off-limits,
    /// host memory). Pure normalization - no filesystem or worktree remapping.
    /// </summary>
    internal static string? ResolveTraversal(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        return PathUtils.CollapseRelativeSegments(path);
    }

    internal static string TruncateCommand(string command)
    {
        const int maxLength = 100;
        if (command.Length <= maxLength)
            return command;
        return command[..maxLength] + "...";
    }

    private static bool TryReadStdinJson(out string? json)
    {
        json = null;
        try
        {
            // KeyAvailable throws InvalidOperationException when stdin is redirected.
            _ = Console.KeyAvailable;
            return false;
        }
        catch (InvalidOperationException)
        {
            // Stdin is redirected - read with a timeout to avoid blocking forever
            // if the pipe is open but has no data (e.g., chained commands)
            var readTask = Task.Run(() => Console.In.ReadToEnd());
            if (readTask.Wait(TimeSpan.FromMilliseconds(500)))
            {
                json = readTask.Result;
                return !string.IsNullOrWhiteSpace(json);
            }
            return false;
        }
    }

    /// <summary>
    /// mode set still meets its nudge; clean is what Ticket Captains run on close-out, pr-ready before
    /// they open a PR. Setup, generation and hook commands stay with the top-level session.
    /// </summary>
    internal static readonly string[] WorkerAllowedDydoSubcommands =
        ["check", "fix", "gap-check", "graph", "validate", "mode", "clean", "pr-ready", "help", "version"];

    private static readonly HashSet<string> WorkerAllowedDydoWords =
        new([.. WorkerAllowedDydoSubcommands, "-h", "--help", "--version"], StringComparer.Ordinal);

    /// <summary>
    /// Returns the first word a sub-agent may not pass to dydo, or null when the command is allowed.
    /// A command <see cref="IsDydoCommand"/> does not match is allowed. A matched command is allowed
    /// only when every dydo token in it that is followed by a word is followed by an allowed word; a
    /// bare dydo prints help. The token matches anywhere, case-insensitively, with an optional
    /// .exe/.cmd/.ps1/.bat suffix; dydo followed by a letter, digit, '.', '-' or '/' is part of a
    /// longer word or a path (dydo/guides, dydo.json) and is skipped. A matched line therefore blocks
    /// a dydo directory given as its own word, such as dydo check dydo src.
    /// </summary>
    internal static string? FindWorkerBlockedDydoCommand(string command)
    {
        if (!IsDydoCommand(command))
            return null;

        return DydoTokenRegex().Matches(NormaliseForDydoScan(command))
            .Select(token => token.Groups["word"].Value)
            .FirstOrDefault(word => word.Length > 0 && !WorkerAllowedDydoWords.Contains(word));
    }

    /// <summary>
    /// The command as the shell reads its words: backslash-newline and backtick-newline
    /// continuations deleted, then quotes, backslashes, backticks and carets, so d"y"do, d\ydo and
    /// `dydo init` read as dydo.
    /// </summary>
    private static string NormaliseForDydoScan(string command) =>
        WorkerScanStripRegex().Replace(LineContinuationRegex().Replace(command, ""), "");

    [GeneratedRegex(@"[\\`]\r?\n")]
    private static partial Regex LineContinuationRegex();

    [GeneratedRegex(@"[""'\\`^]")]
    private static partial Regex WorkerScanStripRegex();

    // dydo, then the word after any whitespace but a line break, or brace-expansion commas, up to
    // whitespace or a terminator. \s is Unicode-aware on both sides, so form feed, vertical tab, NBSP,
    // U+2003 and NEL separate dydo from its word exactly as PowerShell treats them.
    [GeneratedRegex(@"dydo(?:\.(?:exe|cmd|ps1|bat))?(?![\w.\-/])(?:[^\S\r\n]|,)*(?<word>[^\s;&|)}]*)", RegexOptions.IgnoreCase)]
    private static partial Regex DydoTokenRegex();

    internal static bool IsDydoCommand(string command)
    {
        return DydoCommandRegex().IsMatch(command);
    }

    [GeneratedRegex(@"(?:^|[;&|]\s*)(?:\./)?dydo\s", RegexOptions.IgnoreCase)]
    private static partial Regex DydoCommandRegex();

    private static void RunDailyValidationIfDue()
    {
        try
        {
            var basePath = Environment.CurrentDirectory;
            var configService = new ConfigService();
            var configPath = configService.FindConfigFile(basePath);
            if (configPath == null)
                return;

            var projectRoot = Path.GetDirectoryName(configPath)!;
            var config = configService.LoadConfig(basePath);
            var dydoRoot = config == null
                ? null
                : Path.Combine(projectRoot, config.Structure.Root);
            var timestampPath = dydoRoot == null
                ? null
                : Path.Combine(dydoRoot, "_system", ".local", "last-validation");

            if (timestampPath != null && File.Exists(timestampPath))
            {
                var lastRun = File.GetLastWriteTimeUtc(timestampPath);
                if ((DateTime.UtcNow - lastRun).TotalHours < 24)
                    return;
            }

            var validator = new ValidationService();
            var issues = validator.ValidateSystem(projectRoot);

            if (issues.Count > 0)
            {
                Console.Error.WriteLine("Daily validation found issues:");
                foreach (var issue in issues)
                    Console.Error.WriteLine($"  [{issue.Severity}] {issue.File}: {issue.Message}");
                Console.Error.WriteLine("Run 'dydo validate' for full report.");
                Console.Error.WriteLine();
            }

            if (dydoRoot != null)
            {
                // .local/ is absent in worktrees.
                PathUtils.EnsureLocalDirExists(dydoRoot);
                File.WriteAllText(timestampPath!, DateTime.UtcNow.ToString("O"));
            }
        }
        catch
        {
            // Daily validation must never break the guard
        }
    }

}
