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

        if (File.Exists(target))
        {
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
