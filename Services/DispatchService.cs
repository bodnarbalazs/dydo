namespace DynaDocs.Services;

using DynaDocs.Models;
using DynaDocs.Utils;

public static class DispatchService
{
    public static int Execute(string role, string task, string brief, string? files, bool noLaunch,
        string? to, bool escalate, bool useTab, bool useNewWindow, bool autoClose, bool wait, bool worktree)
    {
        if (useTab && useNewWindow)
        {
            ConsoleOutput.WriteError("Cannot specify both --tab and --new-window.");
            return ExitCodes.ToolError;
        }

        var registry = new AgentRegistry();
        var sessionId = registry.GetSessionContext();
        var currentHuman = registry.GetCurrentHuman();

        if (noLaunch)
        {
            var nudgeError = CheckNoLaunchNudge(registry, sessionId, task);
            if (nudgeError != null)
            {
                ConsoleOutput.WriteError(nudgeError);
                return ExitCodes.ToolError;
            }
        }

        var sender = registry.GetCurrentAgent(sessionId);
        var senderName = sender?.Name ?? "Unknown";

        var waitError = CheckWaitPrivilege(wait, sender);
        if (waitError != null)
        {
            ConsoleOutput.WriteError(waitError);
            return ExitCodes.ToolError;
        }

        var origin = GetOriginForTask(registry, sender, task) ?? senderName;

        var existing = FindAgentWorkingOnTask(registry, task, senderName);
        if (existing != null)
        {
            ConsoleOutput.WriteError($"{existing} is already working on task '{task}'. If you need to re-dispatch, have them release first.");
            return ExitCodes.ToolError;
        }

        // Select target agent
        var (selection, selectionError) = !string.IsNullOrEmpty(to)
            ? AgentSelector.SelectExplicit(registry, to, currentHuman, role, task)
            : AgentSelector.SelectAutomatic(registry, currentHuman, role, task, senderName, origin);

        if (selection == null)
        {
            ConsoleOutput.WriteError(selectionError!);
            return ExitCodes.ToolError;
        }

        var targetAgentName = selection.AgentName;

        WriteAndLaunch(registry, targetAgentName, senderName, origin, role, task, brief,
            files, escalate, noLaunch, useTab, useNewWindow, autoClose, wait, worktree);

        if (wait)
        {
            registry.CreateWaitMarker(senderName, task, targetAgentName);
            Console.WriteLine($"Wait registered for '{task}'. Listen for the response with:");
            Console.WriteLine($"  dydo wait --task {task}");
            Console.WriteLine("  (run in background to avoid blocking)");
            return ExitCodes.Success;
        }

        PrintReleaseHint(registry, sender, senderName);
        return ExitCodes.Success;
    }

    private static void WriteAndLaunch(AgentRegistry registry, string targetAgentName, string senderName,
        string origin, string role, string task, string brief, string? files, bool escalate,
        bool noLaunch, bool useTab, bool useNewWindow, bool autoClose, bool wait, bool worktree)
    {
        var itemPath = WriteInboxItemToAgent(registry, targetAgentName, senderName, origin, role, task, brief, files, escalate, wait);

        HandleReviewerTransition(role, task, brief);
        PrintDispatchSummary(targetAgentName, role, task, itemPath, escalate);

        var effectiveAutoClose = autoClose || (registry.Config?.Dispatch?.AutoClose ?? false);
        var worktreeId = SetupWorktree(registry, targetAgentName, worktree);
        var (windowName, launchInTab) = ConfigureWindowSettings(registry, useTab, useNewWindow);

        registry.SetDispatchMetadata(targetAgentName, windowName, effectiveAutoClose);
        LaunchTerminalIfNeeded(targetAgentName, noLaunch, launchInTab, effectiveAutoClose, worktreeId, windowName);

        if (effectiveAutoClose)
            WatchdogService.EnsureRunning();
    }

    private static string WriteInboxItemToAgent(AgentRegistry registry, string targetAgentName, string senderName,
        string origin, string role, string task, string brief, string? files, bool escalate, bool wait)
    {
        var inboxItem = new InboxItem
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            From = senderName,
            Origin = origin,
            Role = role,
            Task = task,
            Received = DateTime.UtcNow,
            Brief = brief,
            Files = string.IsNullOrEmpty(files) ? [] : [files],
            Escalated = escalate,
            EscalatedAt = escalate ? DateTime.UtcNow : null,
            ReplyRequired = wait
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

    private static string? SetupWorktree(AgentRegistry registry, string targetAgentName, bool worktree)
    {
        if (!worktree)
            return null;

        var worktreeId = TerminalLauncher.GenerateWorktreeId(targetAgentName);
        var worktreeMarker = Path.Combine(registry.GetAgentWorkspace(targetAgentName), ".worktree");
        File.WriteAllText(worktreeMarker, worktreeId);
        return worktreeId;
    }

    private static (string? windowName, bool launchInTab) ConfigureWindowSettings(AgentRegistry registry, bool useTab, bool useNewWindow)
    {
        // Inherit window GUID from parent, or generate a fresh one.
        // First dispatch (no DYDO_WINDOW): creates a named window via wt -w {guid}.
        // Child dispatches: inherit DYDO_WINDOW, targeting the parent's window.
        var windowName = Environment.GetEnvironmentVariable("DYDO_WINDOW");
        if (string.IsNullOrEmpty(windowName))
            windowName = Guid.NewGuid().ToString("N")[..8];

        var launchInTab = useTab || (!useNewWindow && (registry.Config?.Dispatch?.LaunchInTab ?? true));

        return (windowName, launchInTab);
    }

    private static void LaunchTerminalIfNeeded(string targetAgentName, bool noLaunch, bool launchInTab,
        bool effectiveAutoClose, string? worktreeId, string? windowName)
    {
        if (noLaunch)
            return;

        var projectRoot = PathUtils.FindProjectRoot();
        TerminalLauncher.LaunchNewTerminal(targetAgentName, projectRoot, launchInTab, effectiveAutoClose, worktreeId, windowName);
        Console.WriteLine($"  Terminal launched with --inbox {targetAgentName}");
    }

    private static string? CheckNoLaunchNudge(AgentRegistry registry, string sessionId, string task)
    {
        var sender = registry.GetCurrentAgent(sessionId);
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

    private static string? CheckWaitPrivilege(bool wait, AgentState? sender)
    {
        if (!wait || sender == null) return null;

        var waitPrivilegedRoles = new[] { "orchestrator", "inquisitor", "judge" };
        if (!waitPrivilegedRoles.Contains(sender.Role, StringComparer.OrdinalIgnoreCase))
            return $"The --wait flag is reserved for oversight roles (orchestrator, inquisitor, judge). Your role '{sender.Role}' should use --no-wait. See decision 005.";

        return null;
    }

    private static void PrintReleaseHint(AgentRegistry registry, AgentState? sender, string senderName)
    {
        if (sender == null || string.Equals(sender.Role, "co-thinker", StringComparison.OrdinalIgnoreCase))
            return;

        var senderInbox = Path.Combine(registry.GetAgentWorkspace(senderName), "inbox");
        var hasInboxItems = Directory.Exists(senderInbox) && Directory.GetFiles(senderInbox, "*.md").Length > 0;
        var hasWaitMarkers = registry.GetWaitMarkers(senderName).Count > 0;

        if (!hasInboxItems && !hasWaitMarkers)
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

        var originYaml = !string.IsNullOrEmpty(item.Origin)
            ? $"\norigin: {item.Origin}"
            : "";

        var escalationYaml = item.Escalated
            ? $"\nescalated: true\nescalated_at: {item.EscalatedAt:o}"
            : "";

        var replyRequiredYaml = item.ReplyRequired ? "\nreply_required: true" : "";

        var escalationHeader = item.Escalated ? "ESCALATED " : "";

        var content = $"""
            ---
            id: {item.Id}
            from: {item.From}
            role: {item.Role}
            task: {item.Task}
            received: {item.Received:o}{originYaml}{escalationYaml}{replyRequiredYaml}
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
            if (!content.StartsWith("---")) return (null, null);

            var endIndex = content.IndexOf("---", 3);
            if (endIndex < 0) return (null, null);

            var yaml = content[3..endIndex];
            string? origin = null, from = null;

            foreach (var line in yaml.Split('\n'))
            {
                var colonIndex = line.IndexOf(':');
                if (colonIndex < 0) continue;

                var key = line[..colonIndex].Trim();
                var value = line[(colonIndex + 1)..].Trim();

                switch (key)
                {
                    case "origin": origin = value; break;
                    case "from": from = value; break;
                }
            }

            return (origin, from);
        }
        catch
        {
            return (null, null);
        }
    }
}
