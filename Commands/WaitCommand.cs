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

    // Test hook — production stays at 10s; tests that exercise the polling loop
    // shorten this so they don't hang for a real interval per iteration.
    internal static int PollIntervalMs { get; set; } = 10_000;

    private static int WaitGeneral(AgentRegistry registry, string agentName, string inboxPath)
    {
        // Idempotency: a live general-wait already does the job. Refuse to register
        // a duplicate with a NONZERO exit + stderr so wrappers/agents notice and
        // don't grow a defensive habit of re-registering before every tool block.
        var existing = registry.GetWaitMarkers(agentName)
            .FirstOrDefault(m => m.Task == GeneralWaitMarker);
        if (existing is { Listening: true, Pid: { } activePid }
            && ProcessUtils.IsProcessRunning(activePid))
        {
            Console.Error.WriteLine(
                $"A general wait is already active for {agentName} (PID {activePid}). Refusing to register a duplicate.");
            return ExitCodes.ToolError;
        }

        var parentPid = ProcessUtils.GetParentPid(Environment.ProcessId);
        var claudePid = ProcessUtils.FindAncestorProcess("claude");
        var cancelled = false;
        Console.CancelKeyPress += (_, e) => { cancelled = true; e.Cancel = true; };

        // Atomic create — Listening=true and Pid set in a single file write so the
        // bash that launched this wait can return to the parent without leaving a
        // window where guard checks observe Listening=false. (#0133)
        registry.CreateListeningWaitMarker(agentName, GeneralWaitMarker, agentName, Environment.ProcessId);

        // Wait fires on inbox-files ∩ state.md.UnreadMessages, re-read each poll. The
        // canonical "not yet delivered" set: writer adds via MessageService.DeliverInboxMessage
        // → AddUnreadMessage; Read removes via GuardCommand.TrackReadCompletion → MarkMessageRead.
        // No registration-time snapshot — eliminates the W1-exit → W2-register race (#0147)
        // and cannot re-introduce the #0141 deadlock because already-read ids are no
        // longer in the set. (#0141 / #0147)
        Console.WriteLine("Waiting for message...");

        try
        {
            while (!cancelled)
            {
                // Re-read each poll so subjects claimed by task waits registered after this
                // wait started are excluded — task-channel waits have priority over the general
                // fallback, regardless of registration order.
                var claimedTasks = GetActiveTaskWaitSubjects(registry, agentName);
                var unreadIds = registry.GetAgentState(agentName)?.UnreadMessages is { } u
                    ? new HashSet<string>(u, StringComparer.OrdinalIgnoreCase)
                    : null;
                var message = (unreadIds is null || unreadIds.Count == 0)
                    ? null
                    : MessageFinder.FindMessage(inboxPath, null, claimedTasks, excludeIds: null, includeIds: unreadIds);
                if (message != null)
                {
                    PrintMessage(message);
                    return ExitCodes.Success;
                }

                if (parentPid.HasValue && !ProcessUtils.IsProcessRunning(parentPid.Value))
                    return ExitCodes.ToolError;

                // Background bash survives Claude exit on Windows — check Claude ancestor too
                if (claudePid.HasValue && !ProcessUtils.IsProcessRunning(claudePid.Value))
                    return ExitCodes.ToolError;

                Thread.Sleep(PollIntervalMs);
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
        var claudePid = ProcessUtils.FindAncestorProcess("claude");
        var cancelled = false;
        Console.CancelKeyPress += (_, e) => { cancelled = true; e.Cancel = true; };

        // Atomic upsert — same race fix as WaitGeneral. The dispatcher-pre-created
        // marker's Target and Since are preserved by CreateListeningWaitMarker.
        registry.CreateListeningWaitMarker(agentName, task, agentName, Environment.ProcessId);

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

                if (claudePid.HasValue && !ProcessUtils.IsProcessRunning(claudePid.Value))
                    return ExitCodes.ToolError;

                Thread.Sleep(PollIntervalMs);
            }
            return ExitCodes.ToolError;
        }
        finally
        {
            registry.ResetWaitMarkerListening(agentName, task);
        }
    }

    internal static HashSet<string> GetActiveTaskWaitSubjects(AgentRegistry registry, string agentName)
    {
        // Skip "_"-prefix markers (general-wait sentinels) — only task-channel subjects exclude.
        return new HashSet<string>(
            registry.GetWaitMarkers(agentName)
                .Where(m => !m.Task.StartsWith('_'))
                .Select(m => m.Task),
            StringComparer.OrdinalIgnoreCase);
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
