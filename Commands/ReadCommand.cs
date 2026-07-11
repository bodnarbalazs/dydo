namespace DynaDocs.Commands;

using System.CommandLine;
using DynaDocs.Services;
using DynaDocs.Utils;

/// <summary>
/// Host-agnostic read verb: PRINTS a target's content and registers the read in one code path
/// (display-equals-ack). Shell-based hosts (codex) have no Read-tool the guard can observe, so
/// their inbox items and must-reads never register — this verb closes that gap without a
/// blind-ack path: content emission and read registration always happen together.
/// </summary>
public static class ReadCommand
{
    public static Command Create()
    {
        var targetArgument = new Argument<string>("target")
        {
            Description = "Inbox message id or file path to print and register as read"
        };

        var command = new Command("read", "Print a target's content and register the read (inbox item or must-read)");
        command.Arguments.Add(targetArgument);

        command.SetAction(parseResult => Execute(parseResult.GetValue(targetArgument)!));

        return command;
    }

    private static int Execute(string target)
    {
        var registry = new AgentRegistry();
        var sessionId = registry.GetSessionContext();
        var agent = registry.GetCurrentAgent(sessionId);

        if (agent == null)
        {
            ConsoleOutput.WriteError("No agent identity assigned to this process. Claim an agent first with 'dydo agent claim'.");
            return ExitCodes.ToolError;
        }

        var workspace = registry.GetAgentWorkspace(agent.Name);

        var item = InboxItemParser.GetInboxItems(workspace)
            .FirstOrDefault(i => i.Id.Equals(target, StringComparison.OrdinalIgnoreCase));
        if (item != null)
        {
            // Display-equals-ack: emit the FULL body (not the truncated inbox-show preview) before
            // registering the read, so no content is acked without being printed.
            InboxService.PrintInboxItem(item, fullBody: true);
            registry.MarkMessageRead(sessionId, item.Id);
            return ExitCodes.Success;
        }

        // Guard parity: `dydo read` is a host-agnostic file reader, so it must honour the SAME
        // universal off-limits tier (secrets/credentials) the guard enforces for the Read tool
        // (GuardCommand.CheckDirectFileOffLimits -> BlockIfPathOffLimits) and for shell readers
        // (BashCommandAnalyzer.ReadCommands). This must run before File.Exists so off-limits
        // verdicts do not leak whether a matching secret path exists.
        //
        // Resolve the target EXACTLY as the guard does (ResolveWorktreePath): it remaps a
        // dydo-worktree CWD (dydo/_system/.local/worktrees/<id>/...) back to the main-project
        // equivalent BEFORE the off-limits/exemption checks. That matters because dispatched
        // shell-host agents run from inside a worktree (TerminalLauncher cd's there); a plain
        // Path.GetFullPath would leave the worktree-marker segment in the path and misfire the
        // hardcoded dydo/_system/** pattern on EVERY file. The read-registration half below
        // already worktree-normalizes (ReadTrackingService.NormalizeForMustReadComparison), so
        // this keeps both halves on the same resolved footing.
        var resolved = GuardCommand.ResolveWorktreePath(target) ?? target;

        // Absolutize a still-relative resolved path BEFORE the exemption + off-limits checks, so
        // this lane sees the SAME absolute path shape the guard's Read tool lane sees (Claude Code
        // delivers absolute paths). Without this, a bare filename like ".env" or "dydo.json"
        // (no separator) falls through GuardCommand.IsBootstrapFile as a "root-level bootstrap
        // file", trips ShouldBypassOffLimits, and skips the whole off-limits check — leaking the
        // secret. OffLimitsService.RelativizeToProjectRoot handles the absolute form, so both the
        // exemption and off-limits verdicts stay correct. Print/registration below stay on the
        // ORIGINAL target (the worktree copy under the agent CWD).
        if (!Path.IsPathRooted(resolved))
            resolved = Path.GetFullPath(resolved);

        // Apply the guard's read-tier exemptions verbatim (bootstrap + mode files stay readable
        // even when a broad off-limits pattern would otherwise cover them).
        if (!GuardCommand.ShouldBypassOffLimits(resolved, agent))
        {
            var offLimitsService = new OffLimitsService();
            offLimitsService.LoadPatterns();
            // Reuse the guard's single BLOCKED emitter (GuardCommand.BlockIfPathOffLimits) rather
            // than duplicating the 4-line literal, so the block message + exit code cannot drift
            // between lanes. The emitter re-checks IsPathOffLimits and returns the exit code (or
            // null when the path is clear).
            var block = GuardCommand.BlockIfPathOffLimits(
                resolved, toolName: "read", sessionId, offLimitsService, registry);
            if (block != null)
                return block.Value;
        }

        if (File.Exists(target))
        {
            // Read/print/register on the ORIGINAL target so the worktree COPY under the agent's CWD
            // is what gets emitted and acked, not the normalized main-project path.
            Console.WriteLine(File.ReadAllText(target));
            ReadTrackingService.TrackReadCompletion(agent, target, sessionId, registry);
            return ExitCodes.Success;
        }

        ConsoleOutput.WriteError(
            $"'{target}' is neither an inbox message id nor an existing file path. " +
            "Pass an inbox item id (see 'dydo inbox show') or a path to a file.");
        return ExitCodes.ToolError;
    }
}
