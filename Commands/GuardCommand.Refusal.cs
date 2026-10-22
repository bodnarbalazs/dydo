namespace DynaDocs.Commands;

using System.Text;
using System.Text.Json;
using DynaDocs.Utils;

/// <summary>
/// The PreToolUse refusal contract (LC-1021). Codex runs a command hook through the session
/// shell, <c>powershell|pwsh -NoProfile -Command "dydo guard"</c>, and both PowerShells turn a
/// native exit 2 into exit 1 there; Codex treats exit 1 as a failed hook and runs the tool. So a
/// hook-mode refusal exits 0 and decides through stdout instead: exactly one deny-decision
/// object, which Codex and Claude Code both honour as a block. The one ask decision (the mode-set
/// rerun, GuardCommand.ModeSet.cs) is emitted only where Claude Code is positively identified.
/// </summary>
public static partial class GuardCommand
{
    // `dydo guard` is wired to PreToolUse only; a payload naming no event is still that hook.
    private static bool IsPreToolUseHook(GuardContext ctx) =>
        !ctx.HasCliArgs && (ctx.HookEventName == null || ctx.HookEventName == "PreToolUse");

    /// <summary>
    /// Turns a verdict into the guard's answer: a PreToolUse block or ask becomes a decision on
    /// stdout; anywhere else an ask cannot be answered, so it blocks with exit 2.
    /// </summary>
    private static int Conclude(GuardContext ctx, int verdict, string reason) =>
        (verdict, IsPreToolUseHook(ctx)) switch
        {
            (ExitCodes.ToolError, true) => DecidePreToolUse("deny", reason),
            (AskUser, true) => DecidePreToolUse("ask", reason),
            (AskUser, false) => ExitCodes.ToolError,
            _ => verdict
        };

    /// <summary>
    /// Writes the <paramref name="decision"/> carrying <paramref name="reason"/> to stdout and returns
    /// exit 0. The default encoder escapes every non-ASCII character, so PowerShell 5.1 re-encoding
    /// the passed-through stdout cannot corrupt the object.
    /// </summary>
    private static int DecidePreToolUse(string decision, string reason)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteStartObject("hookSpecificOutput");
            json.WriteString("hookEventName", "PreToolUse");
            json.WriteString("permissionDecision", decision);
            json.WriteString("permissionDecisionReason", reason.Trim());
            json.WriteEndObject();
            json.WriteEndObject();
        }

        Console.Out.WriteLine(Encoding.UTF8.GetString(buffer.ToArray()));
        return ExitCodes.Success;
    }
}
