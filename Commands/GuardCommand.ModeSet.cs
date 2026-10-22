namespace DynaDocs.Commands;

using System.Text.RegularExpressions;
using DynaDocs.Models;
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

    // Claude Code prompts on a PreToolUse ask in these modes, auto included. bypassPermissions and
    // dontAsk never prompt, and Codex parses ask but runs the tool, so those are denied.
    private static readonly HashSet<string> AskingPermissionModes =
        new(StringComparer.Ordinal) { "default", "acceptEdits", "plan", "auto" };

    private static readonly NudgeConfig ModeSetNudge = new()
    {
        Pattern = ModeSetRegex().ToString(),
        Severity = "warn",
        Message = "dydo mode set changes the mode of every agent on this machine. Only the chief of staff "
            + "sets it, at the overnight-mode muster or the morning brief; any other agent leaves it alone."
    };

    /// <summary>
    /// Positive identification only: Claude Code injects CLAUDECODE=1 into every command hook it
    /// spawns, and an unrecognised host is treated as one that cannot ask.
    /// </summary>
    private static bool CanAskUser(GuardContext ctx) =>
        !ctx.HasCliArgs
        && Environment.GetEnvironmentVariable("CLAUDECODE") == "1"
        && ctx.PermissionMode != null
        && AskingPermissionModes.Contains(ctx.PermissionMode);

    private static int CheckModeSet(string command, GuardEnv env, bool canAsk)
    {
        var match = ModeSetRegex().Match(command);
        if (!match.Success)
            return ExitCodes.Success;

        var firstTry = EnforceNudge(ModeSetNudge, match, ".nudge-mode-set",
            "Chief of staff: run the same command again; the user then approves it or runs it himself.", env);
        if (firstTry != null)
            return firstTry.Value;

        var modeSet = match.Value.Trim();
        if (canAsk)
        {
            Console.Error.WriteLine($"ASK: An agent wants to run `{modeSet}`, which changes the mode of every agent "
                + "on this machine. Approve only if you started or are ending overnight mode.");
            return AskUser;
        }

        Console.Error.WriteLine("BLOCKED: This host cannot ask the user to approve dydo mode set "
            + "(only Claude Code in default, acceptEdits, plan or auto mode can).");
        Console.Error.WriteLine($"  Ask the user to run it in their own terminal: {modeSet}");
        return ExitCodes.ToolError;
    }

    [GeneratedRegex(@"dydo(?:\.(?:exe|cmd|ps1|bat))?\s+mode\s+set\b[^;&|\r\n]*", RegexOptions.IgnoreCase)]
    private static partial Regex ModeSetRegex();
}
