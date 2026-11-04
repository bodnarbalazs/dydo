namespace DynaDocs.Commands;

using System.Text.RegularExpressions;
using DynaDocs.Models;
using DynaDocs.Services;

/// <summary>
/// The built-in mode-set nudge (DYD-351). <c>dydo mode set</c> changes the mode every agent on the
/// machine reads. Every caller meets a command-keyed warn: the first try stops with the chief-of-staff
/// reminder and the same command's retry passes. This prevents accidents; it authenticates no role
/// or human approval (DYD-433). It lives in code, so no config can drop or soften it.
/// </summary>
public static partial class GuardCommand
{
    /// <summary>The <c>--host</c> value in the Claude Code hook command dydo init writes.</summary>
    internal const string ClaudeHost = "claude";

    // Codex's non-Bash shell tool names (issue 0295); its Bash calls are told apart by turn_id.
    private static readonly HashSet<string> CodexShellToolNames =
        new(StringComparer.OrdinalIgnoreCase) { "shell_command", "exec", "local_shell", "unified_exec" };

    private static readonly NudgeConfig ModeSetNudge = new()
    {
        Pattern = LooseModeSetRegex().ToString(),
        Severity = "warn",
        Message = "dydo mode set changes the mode of every agent on this machine. Run this only if you are "
            + "the chief of staff readying overnight mode or ending it; otherwise stop."
    };

    private static ShellDialect ExecutingShellOf(GuardContext ctx) =>
        ctx.HasCliArgs
            ? ShellDialect.Unknown
            : ExecutingShell(ctx.ToolName, ctx.Host, ctx.FromCodex, OperatingSystem.IsWindows(),
                Environment.GetEnvironmentVariable("SHELL"));

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

    /// <summary>
    /// Whether the command runs <c>dydo mode set</c>. The
    /// <see cref="DydoInvocationScanner"/> decides, reading the command as its shell does; where it cannot be sure (an unbalanced quote, a
    /// shell reading a pipe), any dydo ... mode ... set in the command's words counts, failing closed.
    /// </summary>
    private static bool IsModeSet(string command, ShellDialect shell)
    {
        var scan = DydoInvocationScanner.Find(command, shell);
        var modeSet = scan.Invocations.Any(words => words.Count >= 2
            && string.Equals(words[0], "mode", StringComparison.OrdinalIgnoreCase)
            && string.Equals(words[1], "set", StringComparison.OrdinalIgnoreCase));
        return modeSet || (scan.Uncertain && LooseModeSetRegex().IsMatch(NormaliseForDydoScan(command)));
    }

    [GeneratedRegex(@"\bdydo\b[\s\S]*?\bmode\b\W*\bset\b(?<args>[^;&|)}\r\n]*)", RegexOptions.IgnoreCase)]
    private static partial Regex LooseModeSetRegex();
}
