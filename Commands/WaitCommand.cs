namespace DynaDocs.Commands;

using System.CommandLine;
using DynaDocs.Services;
using DynaDocs.Utils;

public static class WaitCommand
{
    public static Command Create()
    {
        var taskOption = new Option<string?>("--task")
        {
            Description = "Only wake on messages with this subject"
        };

        var cancelOption = new Option<bool>("--cancel")
        {
            Description = "Cancel an active wait (remove wait marker)"
        };

        var command = new Command("wait", "Wait for an incoming message");
        command.Options.Add(taskOption);
        command.Options.Add(cancelOption);

        command.SetAction(parseResult =>
        {
            var task = parseResult.GetValue(taskOption);
            var cancel = parseResult.GetValue(cancelOption);
            return Execute(task, cancel);
        });

        return command;
    }

    private static int Execute(string? task, bool cancel)
    {
        var registry = new AgentRegistry();
        var sessionId = registry.GetSessionContext();

        var agent = registry.GetCurrentAgent(sessionId);
        if (agent == null)
        {
            ConsoleOutput.WriteError("No agent identity assigned to this process.");
            return ExitCodes.ToolError;
        }

        if (cancel)
            return HandleCancel(registry, agent.Name, task);

        var inboxPath = Path.Combine(registry.GetAgentWorkspace(agent.Name), "inbox");

        return string.IsNullOrEmpty(task)
            ? WaitGeneral(registry, agent.Name, inboxPath)
            : WaitForTask(registry, agent.Name, inboxPath, task);
    }

    private static int HandleCancel(AgentRegistry registry, string agentName, string? task)
    {
        if (task != null)
        {
            registry.RemoveWaitMarker(agentName, task);
            Console.WriteLine($"Wait cancelled for task '{task}'.");
        }
        else
        {
            registry.ClearAllWaitMarkers(agentName);
            Console.WriteLine("All wait markers cleared.");
        }
        return ExitCodes.Success;
    }

    private const string GeneralWaitMarker = "_general-wait";

    private static int WaitGeneral(AgentRegistry registry, string agentName, string inboxPath)
    {
        var parentPid = ProcessUtils.GetParentPid(Environment.ProcessId);
        var cancelled = false;
        Console.CancelKeyPress += (_, e) => { cancelled = true; e.Cancel = true; };

        var markers = registry.GetWaitMarkers(agentName);
        var claimedTasks = new HashSet<string>(
            markers.Select(m => m.Task),
            StringComparer.OrdinalIgnoreCase);

        // Record PID for visibility (after reading markers so it doesn't self-exclude)
        registry.CreateWaitMarker(agentName, GeneralWaitMarker, agentName);
        registry.UpdateWaitMarkerListening(agentName, GeneralWaitMarker, Environment.ProcessId);

        Console.WriteLine("Waiting for message...");

        try
        {
            while (!cancelled)
            {
                var message = MessageFinder.FindMessage(inboxPath, null, claimedTasks);
                if (message != null)
                {
                    PrintMessage(message);
                    return ExitCodes.Success;
                }

                if (parentPid.HasValue && !ProcessUtils.IsProcessRunning(parentPid.Value))
                    return ExitCodes.ToolError;

                Thread.Sleep(10_000);
            }
            return ExitCodes.ToolError;
        }
        finally
        {
            registry.RemoveWaitMarker(agentName, GeneralWaitMarker);
        }
    }

    private static int WaitForTask(AgentRegistry registry, string agentName, string inboxPath, string task)
    {
        var parentPid = ProcessUtils.GetParentPid(Environment.ProcessId);
        var cancelled = false;
        Console.CancelKeyPress += (_, e) => { cancelled = true; e.Cancel = true; };

        registry.UpdateWaitMarkerListening(agentName, task, Environment.ProcessId);

        Console.WriteLine($"Waiting for message about '{task}'...");

        try
        {
            while (!cancelled)
            {
                var message = MessageFinder.FindMessage(inboxPath, task);
                if (message != null)
                {
                    PrintMessage(message);
                    registry.RemoveWaitMarker(agentName, task);
                    return ExitCodes.Success;
                }

                if (parentPid.HasValue && !ProcessUtils.IsProcessRunning(parentPid.Value))
                    return ExitCodes.ToolError;

                Thread.Sleep(10_000);
            }
            return ExitCodes.ToolError;
        }
        finally
        {
            registry.ResetWaitMarkerListening(agentName, task);
        }
    }

    private static void PrintMessage(MessageFinder.MessageInfo message)
    {
        Console.WriteLine($"Message received from {message.From}:");
        Console.WriteLine($"  Subject: {message.Subject ?? "(none)"}");
        Console.WriteLine($"  Body: {message.Body}");
        Console.WriteLine();
        Console.WriteLine($"  Message file: {message.FilePath}");
        Console.WriteLine("  Run 'dydo inbox show' for full details.");
    }

    internal static MessageFinder.MessageInfo? FindMessage(string inboxPath, string? taskFilter, HashSet<string>? excludeSubjects = null)
        => MessageFinder.FindMessage(inboxPath, taskFilter, excludeSubjects);
}
