namespace DynaDocs.Commands;

using System.Text.RegularExpressions;
using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

/// <summary>
/// The built-in mode-set nudge (DYD-351). <c>dydo mode set</c> changes the mode every agent on the
/// machine reads, so an agent's first try is blocked with a reminder, as a warn nudge. The rerun
/// asks the user where a hook's ask is a real prompt, and is denied everywhere else so the user runs
/// the command in his own terminal. It lives in code, not dydo.json, so no config can drop or soften it.
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

    private static int CheckModeSet(string command, GuardEnv env, bool canAsk)
    {
        var args = FindModeSetArgs(command);
        if (args == null)
            return ExitCodes.Success;

        var firstTry = EnforceNudge(ModeSetNudge, Match.Empty, ".nudge-mode-set",
            "Chief of staff: run the same command again; the user then approves it or runs it himself.", env);
        if (firstTry != null)
            return firstTry.Value;

        var modeSet = string.Join(' ', args.Prepend("dydo mode set"));
        if (canAsk)
        {
            Console.Error.WriteLine($"ASK: An agent wants to run `{modeSet}`, which changes the mode of every agent "
                + "on this machine. Approve only if you started or are ending overnight mode.");
            return AskUser;
        }

        Console.Error.WriteLine("BLOCKED: This host cannot ask the user to approve dydo mode set "
            + "(only Claude Code through the hook dydo init writes, in default, acceptEdits, plan or auto mode, can).");
        Console.Error.WriteLine($"  Ask the user to run it in their own terminal: {modeSet}");
        return ExitCodes.ToolError;
    }

    /// <summary>
    /// The words after <c>dydo mode set</c> when the command runs it, or null. The shell-aware
    /// <see cref="DydoInvocationScanner"/> decides; where it cannot be sure (an unbalanced quote, a
    /// shell reading a pipe), any dydo ... mode ... set in the command's words counts, failing closed.
    /// </summary>
    private static IReadOnlyList<string>? FindModeSetArgs(string command)
    {
        var scan = DydoInvocationScanner.Find(command);
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
