namespace DynaDocs.Commands;

using System.Text.RegularExpressions;
using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

/// <summary>
/// The built-in mode-set nudge (DYD-351). <c>dydo mode set</c> changes the mode every agent on the
/// machine reads. Where a hook's ask is a real prompt, the first try is blocked with a reminder, as a
/// warn nudge, and the rerun asks the user. Everywhere else every try is denied, so the user runs the
/// command in his own terminal (DYD-386). It lives in code, not dydo.json, so no config can drop or soften it.
/// </summary>
public static partial class GuardCommand
{
    // An internal verdict only: Execute turns it into an ask decision or a block, never an exit code.
    private const int AskUser = -1;

    /// <summary>The <c>--host</c> value in the Claude Code hook command dydo init writes.</summary>
    internal const string ClaudeHost = "claude";

    // Claude Code prompts on a PreToolUse ask in these modes, auto included. bypassPermissions and
    // dontAsk never prompt, and Codex parses ask but runs the tool, so those are denied.
    private static readonly HashSet<string> AskingPermissionModes =
        new(StringComparer.Ordinal) { "default", "acceptEdits", "plan", "auto" };

    // Codex's non-Bash shell tool names (issue 0295); its Bash calls are told apart by turn_id.
    private static readonly HashSet<string> CodexShellToolNames =
        new(StringComparer.OrdinalIgnoreCase) { "shell_command", "exec", "local_shell", "unified_exec" };

    private readonly record struct ModeSetCaller(bool CanAsk, ShellDialect Shell);

    private static readonly NudgeConfig ModeSetNudge = new()
    {
        Pattern = LooseModeSetRegex().ToString(),
        Severity = "warn",
        Message = "dydo mode set changes the mode of every agent on this machine. Only the chief of staff "
            + "sets it, at the overnight-mode muster or the morning brief; any other agent leaves it alone."
    };

    /// <summary>
    /// Positive identification only: the Claude Code hook dydo init writes runs
    /// <c>dydo guard --host claude</c>, and no other hook carries the marker. The environment proves
    /// nothing, since CLAUDECODE=1 is inherited by a Codex session started from a Claude Code shell.
    /// An unmarked hook is treated as a host that cannot ask.
    /// </summary>
    private static bool CanAskUser(GuardContext ctx) =>
        !ctx.HasCliArgs
        && ctx.Host == ClaudeHost
        && ctx.PermissionMode != null
        && AskingPermissionModes.Contains(ctx.PermissionMode);

    private static ModeSetCaller ModeSetCallerOf(GuardContext ctx) => new(CanAskUser(ctx),
        ctx.HasCliArgs
            ? ShellDialect.Unknown
            : ExecutingShell(ctx.ToolName, ctx.Host, ctx.FromCodex, OperatingSystem.IsWindows(),
                Environment.GetEnvironmentVariable("SHELL")));

    /// <summary>
    /// The shell a hook's tool call runs in. Claude Code's PowerShell tool runs PowerShell and its
    /// Bash tool runs bash, Git Bash on Windows. Codex (a turn_id in the payload, or one of its other
    /// shell tool names) runs PowerShell on Windows and the user's shell elsewhere. An unmarked Bash
    /// call on Windows may be either host, so it and any other call are Unknown, read both ways.
    /// </summary>
    internal static ShellDialect ExecutingShell(string? toolName, string? host, bool fromCodex, bool windows, string? userShell)
    {
        if (string.Equals(toolName, "powershell", StringComparison.OrdinalIgnoreCase))
            return ShellDialect.PowerShell;
        var bash = string.Equals(toolName, "bash", StringComparison.OrdinalIgnoreCase);
        if ((fromCodex && bash) || (toolName != null && CodexShellToolNames.Contains(toolName)))
            return windows || IsPowerShell(userShell) ? ShellDialect.PowerShell : ShellDialect.Posix;
        return bash && (host == ClaudeHost || !windows) ? ShellDialect.Posix : ShellDialect.Unknown;
    }

    private static bool IsPowerShell(string? shellPath) =>
        Path.GetFileNameWithoutExtension(shellPath ?? "").ToLowerInvariant() is "pwsh" or "powershell";

    // The reminder joins the call's warn nudges; only a host that can ask reaches it.
    private static readonly WarnStep ModeSetWarn = new(ModeSetNudge, Match.Empty, ".nudge-mode-set",
        "Chief of staff: run the same command again; the user then approves it or runs it himself.");

    // A host that cannot ask refuses every try, so the reminder's marker is neither armed nor spent.
    private static int RefuseModeSet(IReadOnlyList<string> args)
    {
        Console.Error.WriteLine("BLOCKED: This host cannot ask the user to approve dydo mode set "
            + "(only Claude Code through the hook dydo init writes, in default, acceptEdits, plan or auto mode, can).");
        Console.Error.WriteLine($"  Ask the user to run it in their own terminal: {ModeSetCommand(args)}");
        return ExitCodes.ToolError;
    }

    private static int AskModeSet(IReadOnlyList<string> args)
    {
        Console.Error.WriteLine($"ASK: An agent wants to run `{ModeSetCommand(args)}`, which changes the mode of every agent "
            + "on this machine. Approve only if you started or are ending overnight mode.");
        return AskUser;
    }

    private static string ModeSetCommand(IReadOnlyList<string> args) => string.Join(' ', args.Prepend("dydo mode set"));

    /// <summary>
    /// The words after <c>dydo mode set</c> when the command runs it, or null. The
    /// <see cref="DydoInvocationScanner"/> decides, reading the command as its shell does; where it cannot be sure (an unbalanced quote, a
    /// shell reading a pipe), any dydo ... mode ... set in the command's words counts, failing closed.
    /// </summary>
    private static IReadOnlyList<string>? FindModeSetArgs(string command, ShellDialect shell)
    {
        var scan = DydoInvocationScanner.Find(command, shell);
        var invocation = scan.Invocations.FirstOrDefault(words => words.Count >= 2
            && string.Equals(words[0], "mode", StringComparison.OrdinalIgnoreCase)
            && string.Equals(words[1], "set", StringComparison.OrdinalIgnoreCase));
        if (invocation != null)
            return invocation.Skip(2).ToList();
        if (!scan.Uncertain)
            return null;

        var loose = LooseModeSetRegex().Match(NormaliseForDydoScan(command));
        return loose.Success
            ? loose.Groups["args"].Value.Split([' ', '\t', ','], StringSplitOptions.RemoveEmptyEntries)
            : null;
    }

    [GeneratedRegex(@"\bdydo\b[\s\S]*?\bmode\b\W*\bset\b(?<args>[^;&|)}\r\n]*)", RegexOptions.IgnoreCase)]
    private static partial Regex LooseModeSetRegex();
}
