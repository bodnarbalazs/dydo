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

        var registerOption = new Option<bool>("--register")
        {
            Description = "Register a durable wait marker and return immediately (for hosts that cannot hold a foreground wait, e.g. dispatched codex sessions)"
        };

        var command = new Command("wait", "Wait for an incoming message");
        command.Options.Add(taskOption);
        command.Options.Add(cancelOption);
        command.Options.Add(registerOption);

        command.SetAction(parseResult =>
        {
            var task = parseResult.GetValue(taskOption);
            var cancel = parseResult.GetValue(cancelOption);
            var register = parseResult.GetValue(registerOption);
            return Execute(task, cancel, register);
        });

        return command;
    }

    private static int Execute(string? task, bool cancel, bool register)
    {
        var registry = new AgentRegistry();
        var sessionId = registry.GetSessionContext();

        var agent = registry.GetCurrentAgent(sessionId);
        if (agent == null)
        {
            ConsoleOutput.WriteError("No agent identity assigned to this process.");
            return ExitCodes.ToolError;
        }

        // #0195 (F11): refuse to register or cancel wait markers when the caller does not
        // actually own the resolved agent. Without this, an attacker who can set DYDO_AGENT
        // to X in a non-claude shell can hold X's general-wait slot indefinitely (or cancel
        // a legitimate wait), even with F1's env-path ownership gate in place — the fall-
        // through to .session-context can still resolve to a real claimed agent.
        if (!registry.VerifyCallerOwnsAgent(agent.Name))
        {
            ConsoleOutput.WriteError($"Caller does not own agent {agent.Name}. Refusing to register wait marker.");
            return ExitCodes.ToolError;
        }

        if (cancel)
            return HandleCancel(registry, agent.Name, task);

        // Durable registration (#0254): either requested explicitly with --register, or
        // auto-selected when the caller's session host cannot hold a foreground wait (a
        // dispatched codex session's runtime kills a blocking `dydo wait` at its tool timeout,
        // leaving no marker). The durable marker keys its liveness to the claimed host PID, so
        // it survives tool timeouts and satisfies the guard while the session lives.
        if (register || HostCannotHoldForegroundWait(registry, agent.Name))
            return RegisterDurableWait(registry, agent.Name, task);

        var inboxPath = Path.Combine(registry.GetAgentWorkspace(agent.Name), "inbox");

        return string.IsNullOrEmpty(task)
            ? WaitGeneral(registry, agent.Name, inboxPath)
            : WaitForTask(registry, agent.Name, inboxPath, task);
    }

    // A host whose runtime cannot hold a foreground wait — currently a dispatched codex session,
    // resolved from the claimed session's Host. Claude hosts return false, keeping their default
    // foreground-wait behavior unchanged.
    private static bool HostCannotHoldForegroundWait(AgentRegistry registry, string agentName)
    {
        var session = registry.GetSession(agentName);
        return session != null && session.Host == "codex";
    }

    // Write a durable general-wait (or task-wait) marker keyed to the claimed session's host
    // liveness PID and return immediately. Message delivery on such hosts is poll-based
    // (`dydo inbox show` / `dydo read`); this marker satisfies the guard's general-wait
    // obligation without a live foreground wait process.
    private static int RegisterDurableWait(AgentRegistry registry, string agentName, string? task)
    {
        var hostPid = ResolveHostLivenessPid(registry, agentName);
        if (hostPid == null)
        {
            ConsoleOutput.WriteError(
                $"Cannot register a durable wait for {agentName}: no host-liveness PID resolved for the claimed session.");
            return ExitCodes.ToolError;
        }

        var markerTask = string.IsNullOrEmpty(task) ? GeneralWaitMarker : task;
        registry.CreateDurableWaitMarker(agentName, markerTask, agentName, hostPid.Value);

        Console.WriteLine(string.IsNullOrEmpty(task)
            ? $"Durable general wait registered for {agentName} (host PID {hostPid.Value})."
            : $"Durable wait registered for {agentName} on '{task}' (host PID {hostPid.Value}).");
        Console.WriteLine("  It stays active while your session's host process lives; poll with 'dydo inbox show' / 'dydo read'.");
        return ExitCodes.Success;
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
        var hostPid = ResolveHostLivenessPid(registry, agentName);
        var cancelled = false;
        Console.CancelKeyPress += (_, e) => { cancelled = true; e.Cancel = true; };

        // Snapshot the unread set at registration. The poll loop excludes these ids,
        // so this wait fires only on arrivals that happen AFTER registration. The
        // agent drains the snapshot via `dydo inbox show` + Read (both unblocked while
        // this wait is alive). Captured BEFORE CreateListeningWaitMarker so deliveries
        // that race the marker write are NOT in the snapshot and DO fire the wait. (#0149)
        var unreadSnapshot = CaptureUnreadSnapshot(registry, agentName);

        // Atomic create — Listening=true and Pid set in a single file write so the
        // bash that launched this wait can return to the parent without leaving a
        // window where guard checks observe Listening=false. (#0133)
        registry.CreateListeningWaitMarker(agentName, GeneralWaitMarker, agentName, Environment.ProcessId);

        // Wait fires on inbox-files ∩ (UnreadMessages − snapshot-at-registration). The
        // canonical "not yet delivered" set: writer adds via MessageService.DeliverInboxMessage
        // → AddUnreadMessage; Read removes via GuardCommand.TrackReadCompletion → MarkMessageRead.
        // The registration-time snapshot prevents stacked unreads (rate-of-arrival
        // > drain) from re-firing this wait infinitely (#0149). It does NOT
        // re-introduce #0141 (post-Read ids are absent from UnreadMessages — and
        // would also be in the snapshot if still present at register), and it does
        // NOT re-introduce #0147 (post-registration arrivals are not in the snapshot
        // and pass through). (#0141 / #0147 / #0149)
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
                    : MessageFinder.FindMessage(inboxPath, null, claimedTasks, excludeIds: unreadSnapshot, includeIds: unreadIds);
                if (message != null)
                {
                    PrintMessage(message);
                    return ExitCodes.Success;
                }

                if (OwnerProcessExited(parentPid, hostPid))
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
        var hostPid = ResolveHostLivenessPid(registry, agentName);
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

                if (OwnerProcessExited(parentPid, hostPid))
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

    private static HashSet<string> CaptureUnreadSnapshot(AgentRegistry registry, string agentName)
    {
        var unread = registry.GetAgentState(agentName)?.UnreadMessages;
        return unread is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(unread, StringComparer.OrdinalIgnoreCase);
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

    // Host-tab liveness for a backgrounded wait keys off the PID captured and validated at
    // claim time (session.ClaimedPid — "PID whose liveness indicates the claiming tab is still
    // around"), NOT a fresh ancestry walk. `dydo wait` runs in a background shell whose process
    // tree does not reliably reach the durable host, and on Windows FindClaudeAncestor also
    // matches transient `node` wrappers — so re-walking here bound to a short-lived ancestor
    // and dropped the wait with a spurious exit-1 while the tab was still alive. Fall back to the
    // walk only for legacy sessions with no persisted ClaimedPid.
    internal static int? ResolveHostLivenessPid(AgentRegistry registry, string agentName)
    {
        var session = registry.GetSession(agentName);
        return session?.ClaimedPid ?? ProcessUtils.FindAgentHostAncestor(session?.Host);
    }

    private static bool OwnerProcessExited(int? parentPid, int? hostPid) =>
        IsDead(parentPid) || IsDead(hostPid);

    private static bool IsDead(int? pid) =>
        pid.HasValue && !ProcessUtils.IsProcessRunning(pid.Value);

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
