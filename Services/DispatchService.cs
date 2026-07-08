namespace DynaDocs.Services;

using DynaDocs.Commands;
using DynaDocs.Models;
using DynaDocs.Utils;

public static class DispatchService
{
    public static int Execute(DispatchOptions opts)
    {
        if (opts.UseTab && opts.UseNewWindow)
        {
            ConsoleOutput.WriteError("Cannot specify both --tab and --new-window.");
            return ExitCodes.ToolError;
        }

        var registry = new AgentRegistry();
        var sessionId = registry.GetSessionContext();
        var currentHuman = registry.GetCurrentHuman();

        if (!registry.TryGetCurrentOwnedAgent(sessionId, out var sender, out var ownershipError))
        {
            ConsoleOutput.WriteError(ownershipError!);
            return ExitCodes.ToolError;
        }

        var nudgeError = CheckDispatchNudges(registry, sender, opts);
        if (nudgeError != null)
        {
            ConsoleOutput.WriteError(nudgeError);
            return ExitCodes.ToolError;
        }

        var senderName = sender?.Name ?? "Unknown";
        var origin = GetOriginForTask(registry, sender, opts.Task) ?? senderName;
        var launchHost = ResolveLaunchHost(registry, sender, opts.HostOverride);
        if (!opts.NoLaunch && !CanResolveLaunchExecutable(launchHost, out var launchError))
        {
            ConsoleOutput.WriteError(launchError!);
            return ExitCodes.ToolError;
        }

        var (targetAgentName, targetError) = SelectTargetAgent(registry, opts, currentHuman, senderName, origin);
        if (targetError != null)
        {
            ConsoleOutput.WriteError(targetError);
            return ExitCodes.ToolError;
        }

        WriteAndLaunch(registry, targetAgentName!, senderName, origin, opts, launchHost);

        PrintReleaseHint(registry, sender, senderName);
        return ExitCodes.Success;
    }

    // Both nudges are block-once soft-blocks: they return a message on the first bare dispatch
    // and null on the re-run. --no-launch launches nothing, so it only gets the no-launch nudge;
    // a launched dispatch with no effective auto-close leaves the target's terminal open after
    // it releases, so it gets the auto-close nudge instead.
    private static string? CheckDispatchNudges(AgentRegistry registry, AgentState? sender, DispatchOptions opts)
    {
        if (opts.NoLaunch)
            return CheckNoLaunchNudge(registry, sender, opts.Task);

        if (!opts.AutoClose && !(registry.Config?.Dispatch?.AutoClose ?? false))
            return CheckAutoCloseNudge(registry, sender, opts.Task);

        return null;
    }

    internal static string ResolveLaunchHost(AgentRegistry registry, AgentState? sender, string? hostOverride)
    {
        var normalizedOverride = AgentSession.NormalizeHost(hostOverride);
        if (normalizedOverride is "claude" or "codex")
            return normalizedOverride;

        var currentHost = sender != null
            ? AgentSession.NormalizeHost(registry.GetSession(sender.Name)?.Host)
            : AgentSession.UnknownHost;

        return currentHost is "claude" or "codex" ? currentHost : "claude";
    }

    private static bool CanResolveLaunchExecutable(string launchHost, out string? error)
    {
        error = null;
        try
        {
            _ = TerminalLauncher.GetLaunchExecutable(launchHost);
            return true;
        }
        catch (Exception ex)
        {
            error = $"Cannot launch {launchHost}: {ex.Message}";
            return false;
        }
    }

    private static (string? agentName, string? error) SelectTargetAgent(
        AgentRegistry registry, DispatchOptions opts, string? currentHuman, string senderName, string origin)
    {
        var existing = FindAgentWorkingOnTask(registry, opts.Task, senderName);
        if (existing != null)
            return (null, $"{existing} is already working on task '{opts.Task}'. If you need to re-dispatch, have them release first.");

        var (selection, selectionError) = !string.IsNullOrEmpty(opts.To)
            ? AgentSelector.SelectExplicit(registry, opts.To, currentHuman, opts.Role, opts.Task, senderName)
            : AgentSelector.SelectAutomatic(registry, currentHuman, opts.Role, opts.Task, senderName, origin);

        if (selection == null)
            return (null, selectionError!);

        return (selection.AgentName, null);
    }

    private static void WriteAndLaunch(AgentRegistry registry, string targetAgentName, string senderName,
        string origin, DispatchOptions opts, string launchHost)
    {
        var itemPath = WriteInboxItemToAgent(registry, targetAgentName, senderName, origin,
            opts.Role, opts.Task, opts.Brief, opts.Files, opts.Escalate);

        InjectBriefIntoTaskFile(opts.Task, opts.Brief);
        HandleReviewerTransition(opts.Role, opts.Task, opts.Brief);
        PrintDispatchSummary(targetAgentName, opts.Role, opts.Task, itemPath, opts.Escalate);

        var effectiveAutoClose = opts.AutoClose || (registry.Config?.Dispatch?.AutoClose ?? false);
        var (windowName, launchInTab) = ConfigureWindowSettings(registry, opts.UseTab, opts.UseNewWindow);

        if (!registry.SetDispatchMetadata(targetAgentName, windowName, effectiveAutoClose))
            // Non-fatal (finding 6): the dispatch proceeds, but the window/auto-close metadata could not be
            // written under persistent lock contention. Warn so the operator knows any stale --auto-close from a
            // prior lifecycle was not overwritten and can re-dispatch, rather than the terminal silently
            // mis-closing.
            Console.WriteLine($"  Warning: could not record dispatch window/auto-close metadata for {targetAgentName} (lock contended); re-dispatch if --auto-close behaves unexpectedly.");

        LaunchTerminalIfNeeded(targetAgentName, opts.NoLaunch, launchInTab, effectiveAutoClose, windowName, launchHost);

        if (effectiveAutoClose)
            WatchdogService.EnsureRunning();
    }

    private static string WriteInboxItemToAgent(AgentRegistry registry, string targetAgentName, string senderName,
        string origin, string role, string task, string brief, string? files, bool escalate)
    {
        var senderState = registry.GetAgentState(senderName);
        var inboxItem = new InboxItem
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            From = senderName,
            FromRole = senderState?.Role,
            Origin = origin,
            Role = role,
            Task = task,
            Received = DateTime.UtcNow,
            Brief = brief,
            Files = string.IsNullOrEmpty(files) ? [] : [files],
            Escalated = escalate,
            EscalatedAt = escalate ? DateTime.UtcNow : null
        };

        var inboxPath = Path.Combine(registry.GetAgentWorkspace(targetAgentName), "inbox");
        Directory.CreateDirectory(inboxPath);

        var sanitizedTask = PathUtils.SanitizeForFilename(task);
        if (sanitizedTask != task)
        {
            Console.WriteLine($"  Warning: Task name sanitized for filesystem safety.");
            Console.WriteLine($"    Original: \"{task}\"");
            Console.WriteLine($"    Filename: \"{sanitizedTask}\"");
        }

        var itemPath = Path.Combine(inboxPath, $"{inboxItem.Id}-{sanitizedTask}.md");
        WriteInboxItem(itemPath, inboxItem);

        return itemPath;
    }

    internal static void InjectBriefIntoTaskFile(string task, string brief)
    {
        if (string.IsNullOrWhiteSpace(brief)) return;

        try
        {
            var configService = new ConfigService();
            var tasksPath = configService.GetTasksPath();
            var taskFilePath = Path.Combine(tasksPath, $"{Utils.PathUtils.SanitizeForFilename(task)}.md");

            if (!File.Exists(taskFilePath)) return;

            var content = File.ReadAllText(taskFilePath);
            if (!content.Contains("(No description)")) return;

            content = content.Replace("(No description)", brief);
            File.WriteAllText(taskFilePath, content);
        }
        catch
        {
            // Non-blocking: brief injection is a convenience side-effect
        }
    }

    private static void HandleReviewerTransition(string role, string task, string brief)
    {
        if (role.Equals("reviewer", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(task))
        {
            if (Commands.TaskCommand.TransitionToReviewPending(task, brief))
                Console.WriteLine($"  Task state: marked ready for review");
        }
    }

    private static void PrintDispatchSummary(string targetAgentName, string role, string task, string itemPath, bool escalate)
    {
        var escalatedIndicator = escalate ? " [ESCALATED]" : "";
        Console.WriteLine($"Work dispatched to agent {targetAgentName}.{escalatedIndicator}");
        Console.WriteLine($"  Role: {role}");
        Console.WriteLine($"  Task: {task}");
        Console.WriteLine($"  Inbox: {itemPath}");
        if (escalate)
            Console.WriteLine($"  Escalated: yes");
    }

    private static (string? windowName, bool launchInTab) ConfigureWindowSettings(AgentRegistry registry, bool useTab, bool useNewWindow)
    {
        // Two-tier window targeting:
        // Root dispatches (no DYDO_WINDOW, no --new-window): null → launcher uses -w 0 (MRU)
        // Child dispatches: inherit DYDO_WINDOW → launcher uses -w {guid}
        // --new-window dispatches: fresh GUID → launcher uses --window {guid}
        var windowName = Environment.GetEnvironmentVariable("DYDO_WINDOW");
        if (string.IsNullOrEmpty(windowName))
            windowName = useNewWindow ? Guid.NewGuid().ToString("N")[..8] : null;

        var launchInTab = useTab || (!useNewWindow && (registry.Config?.Dispatch?.LaunchInTab ?? true));

        return (windowName, launchInTab);
    }

    private static int LaunchTerminalIfNeeded(string targetAgentName, bool noLaunch, bool launchInTab,
        bool effectiveAutoClose, string? windowName, string launchHost)
    {
        if (noLaunch)
            return 0;

        var projectRoot = PathUtils.FindProjectRoot();
        var pid = TerminalLauncher.LaunchNewTerminal(targetAgentName, projectRoot, launchInTab, effectiveAutoClose, null, windowName, host: launchHost);
        Console.WriteLine($"  Terminal launched with --inbox {targetAgentName} ({launchHost})");
        return pid;
    }

    private static string? CheckNoLaunchNudge(AgentRegistry registry, AgentState? sender, string task)
    {
        if (sender == null) return null;

        var senderWorkspace = registry.GetAgentWorkspace(sender.Name);
        var nudgeKey = PathUtils.SanitizeForFilename(task);
        var markerPath = Path.Combine(senderWorkspace, $".no-launch-nudge-{nudgeKey}");

        if (!File.Exists(markerPath))
        {
            Directory.CreateDirectory(senderWorkspace);
            File.WriteAllText(markerPath, DateTime.UtcNow.ToString("o"));
            return "You dispatched with the --no-launch flag, it means that the agent you dispatched to will not be activated " +
                   "and the user will have to call them manually. Unless the user was explicit about using no-launch or there is a " +
                   "good reason for it you shouldn't use this flag. If you insist you may run it again and it will pass.";
        }
        File.Delete(markerPath);
        return null;
    }

    /// <summary>
    /// Slice B of #222: soft-block a launched dispatch that omits --auto-close, since the
    /// target's terminal then lingers open after it releases. Mirrors CheckNoLaunchNudge's
    /// block-once toggle — the first bare dispatch writes a per-task marker and blocks; the
    /// next run of the same command clears it and proceeds, preserving the deliberate
    /// keep-terminal-open case.
    /// </summary>
    private static string? CheckAutoCloseNudge(AgentRegistry registry, AgentState? sender, string task)
    {
        if (sender == null) return null;

        var senderWorkspace = registry.GetAgentWorkspace(sender.Name);
        var nudgeKey = PathUtils.SanitizeForFilename(task);
        var markerPath = Path.Combine(senderWorkspace, $".auto-close-nudge-{nudgeKey}");

        if (!File.Exists(markerPath))
        {
            Directory.CreateDirectory(senderWorkspace);
            File.WriteAllText(markerPath, DateTime.UtcNow.ToString("o"));
            return "dydo dispatch without --auto-close leaves the agent terminal open after it releases; " +
                   "add --auto-close, or re-run to proceed if you deliberately want the terminal left open to inspect.";
        }
        File.Delete(markerPath);
        return null;
    }

    private static void PrintReleaseHint(AgentRegistry registry, AgentState? sender, string senderName)
    {
        if (sender == null || string.Equals(sender.Role, "co-thinker", StringComparison.OrdinalIgnoreCase))
            return;

        var senderInbox = Path.Combine(registry.GetAgentWorkspace(senderName), "inbox");
        var hasInboxItems = Directory.Exists(senderInbox) && Directory.GetFiles(senderInbox, "*.md").Length > 0;
        // Sentinel waits (_general-wait) are infrastructure, not outstanding work — exclude
        // them so the hint still surfaces when only the universal general wait is active.
        var hasTaskWaits = registry.GetWaitMarkers(senderName).Any(m => !m.Task.StartsWith('_'));

        if (!hasInboxItems && !hasTaskWaits)
            Console.WriteLine("  Nothing left? Don't forget: dydo agent release");
    }

    internal static string? FindAgentWorkingOnTask(AgentRegistry registry, string task, string senderName)
    {
        foreach (var agent in registry.GetAllAgentStates())
        {
            if (string.Equals(agent.Name, senderName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (agent.Status is AgentStatus.Working or AgentStatus.Reviewing)
            {
                if (string.Equals(agent.Task, task, StringComparison.OrdinalIgnoreCase))
                    return agent.Name;
            }

            if (agent.Status == AgentStatus.Dispatched)
            {
                var agentInbox = Path.Combine(registry.GetAgentWorkspace(agent.Name), "inbox");
                if (Directory.Exists(agentInbox))
                {
                    var sanitized = PathUtils.SanitizeForFilename(task);
                    var matching = Directory.GetFiles(agentInbox, $"*-{sanitized}.md");
                    if (matching.Length > 0)
                        return agent.Name;
                }
            }
        }

        return null;
    }

    internal static void WriteInboxItem(string path, InboxItem item)
    {
        var filesSection = item.Files.Count > 0
            ? $"\n## Files\n\n{string.Join("\n", item.Files.Select(f => $"- {f}"))}"
            : "";

        var fromRoleYaml = !string.IsNullOrEmpty(item.FromRole)
            ? $"\nfrom_role: {item.FromRole}"
            : "";

        var originYaml = !string.IsNullOrEmpty(item.Origin)
            ? $"\norigin: {item.Origin}"
            : "";

        var escalationYaml = item.Escalated
            ? $"\nescalated: true\nescalated_at: {item.EscalatedAt:o}"
            : "";

        var escalationHeader = item.Escalated ? "ESCALATED " : "";

        var content = $"""
            ---
            id: {item.Id}
            from: {item.From}{fromRoleYaml}
            role: {item.Role}
            task: {item.Task}
            received: {item.Received:o}{originYaml}{escalationYaml}
            ---

            # {escalationHeader}{item.Role.ToUpperInvariant()} Request: {item.Task}

            ## From

            {item.From}

            ## Brief

            {item.Brief}
            {filesSection}
            """;

        File.WriteAllText(path, content);
    }

    internal static string? GetOriginForTask(AgentRegistry registry, AgentState? sender, string task)
    {
        if (sender == null) return null;
        var workspace = registry.GetAgentWorkspace(sender.Name);
        var inboxPath = Path.Combine(workspace, "inbox");
        var archivePath = Path.Combine(workspace, "archive", "inbox");

        foreach (var dir in new[] { inboxPath, archivePath })
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.GetFiles(dir, $"*-{PathUtils.SanitizeForFilename(task)}.md"))
            {
                var (origin, from) = ParseInboxItemOrigin(file);
                if (!string.IsNullOrEmpty(origin)) return origin;
                if (!string.IsNullOrEmpty(from)) return from;
            }
        }
        return null;
    }

    private static (string? origin, string? from) ParseInboxItemOrigin(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath);
            var fields = FrontmatterParser.ParseFields(content);
            if (fields == null) return (null, null);

            fields.TryGetValue("origin", out var origin);
            fields.TryGetValue("from", out var from);

            return (origin, from);
        }
        catch
        {
            return (null, null);
        }
    }
}
