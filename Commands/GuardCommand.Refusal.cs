namespace DynaDocs.Commands;

using System.Text;
using System.Text.Json;
using DynaDocs.Utils;

/// <summary>
/// The PreToolUse refusal contract (LC-1021). Codex runs a command hook through the session
/// shell, <c>powershell|pwsh -NoProfile -Command "dydo guard"</c>, and both PowerShells turn a
/// native exit 2 into exit 1 there; Codex treats exit 1 as a failed hook and runs the tool. So a
/// hook-mode refusal exits 0 and decides through stdout instead: exactly one deny-decision
/// object, which Codex and Claude Code both honour as a block.
/// </summary>
public static partial class GuardCommand
{
    // `dydo guard` is wired to PreToolUse only; a payload naming no event is still that hook.
    private static bool IsPreToolUseRefusal(GuardContext ctx, int exitCode) =>
        exitCode == ExitCodes.ToolError
        && !ctx.HasCliArgs
        && (ctx.HookEventName == null || ctx.HookEventName == "PreToolUse");

    /// <summary>
    /// Writes the deny decision carrying <paramref name="reason"/> to stdout and returns exit 0.
    /// The default encoder escapes every non-ASCII character, so PowerShell 5.1 re-encoding the
    /// passed-through stdout cannot corrupt the object.
    /// </summary>
    private static int DenyPreToolUse(string reason)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteStartObject("hookSpecificOutput");
            json.WriteString("hookEventName", "PreToolUse");
            json.WriteString("permissionDecision", "deny");
            json.WriteString("permissionDecisionReason", reason.Trim());
            json.WriteEndObject();
            json.WriteEndObject();
        }

        Console.Out.WriteLine(Encoding.UTF8.GetString(buffer.ToArray()));
        return ExitCodes.Success;
    }
}
