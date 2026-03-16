namespace DynaDocs.Services;

using System.Diagnostics;
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

        // Baton-passing: if sender has a reply-pending marker for this task, inherit it
        var inheritReply = sender != null && registry.GetReplyPendingMarkers(senderName)
            .Any(m => string.Equals(m.Task, task, StringComparison.OrdinalIgnoreCase));

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
            files, escalate, noLaunch, useTab, useNewWindow, autoClose, wait, worktree, inheritReply);

        return CompleteDispatch(registry, sender, senderName, targetAgentName, role, task, inheritReply, wait);
    }

    private static int CompleteDispatch(AgentRegistry registry, AgentState? sender, string senderName,
        string targetAgentName, string role, string task, bool inheritReply, bool wait)
    {
        // Clear sender's reply-pending marker after successful baton pass
        if (inheritReply)
            registry.RemoveReplyPendingMarker(senderName, task);

        // Review enforcement: create marker when code-writer dispatches reviewer for same task
        if (sender != null
            && string.Equals(sender.Role, "code-writer", StringComparison.OrdinalIgnoreCase)
            && string.Equals(role, "reviewer", StringComparison.OrdinalIgnoreCase)
            && string.Equals(sender.Task, task, StringComparison.OrdinalIgnoreCase))
        {
            registry.CreateReviewDispatchedMarker(senderName, task, targetAgentName);
        }

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
        bool noLaunch, bool useTab, bool useNewWindow, bool autoClose, bool wait, bool worktree, bool inheritReply = false)
    {
        var itemPath = WriteInboxItemToAgent(registry, targetAgentName, senderName, origin, role, task, brief, files, escalate, wait, inheritReply);

        HandleReviewerTransition(role, task, brief);
        PrintDispatchSummary(targetAgentName, role, task, itemPath, escalate);

        var effectiveAutoClose = autoClose || (registry.Config?.Dispatch?.AutoClose ?? false);

        string? worktreeId;
        string? workingDirOverride = null;
        var senderWorktreeId = GetSenderWorktreeId(registry, senderName);
        var needsMerge = HasNeedsMergeMarker(registry, senderName);

        string? cleanupWorktreeId = null;
        string? mainProjectRoot = null;

        if (senderWorktreeId != null && !needsMerge)
        {
            if (worktree)
                Console.WriteLine("  Warning: --worktree ignored — inheriting parent's worktree instead.");

            InheritWorktree(registry, targetAgentName, senderName, senderWorktreeId);
            workingDirOverride = GetWorktreePath(registry, senderName);
            worktreeId = null;
            cleanupWorktreeId = senderWorktreeId;
            mainProjectRoot = PathUtils.FindProjectRoot();
        }
        else if (senderWorktreeId != null && needsMerge)
        {
            // Merge dispatch — child launches in main repo to do the merge
            CopyWorktreeMetadataForMerger(registry, targetAgentName, senderName, senderWorktreeId);
            ClearNeedsMerge(registry, senderName);
            worktreeId = null;
        }
        else
        {
            worktreeId = SetupWorktree(registry, targetAgentName, worktree);
        }

        var (windowName, launchInTab) = ConfigureWindowSettings(registry, useTab, useNewWindow);

        registry.SetDispatchMetadata(targetAgentName, windowName, effectiveAutoClose);
        LaunchTerminalIfNeeded(targetAgentName, noLaunch, launchInTab, effectiveAutoClose, worktreeId, windowName, workingDirOverride, cleanupWorktreeId, mainProjectRoot);

        if (effectiveAutoClose)
            WatchdogService.EnsureRunning();
    }

    private static string WriteInboxItemToAgent(AgentRegistry registry, string targetAgentName, string senderName,
        string origin, string role, string task, string brief, string? files, bool escalate, bool wait, bool inheritReply = false)
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
            ReplyRequired = wait || inheritReply
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
        var workspace = registry.GetAgentWorkspace(targetAgentName);
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(workspace, ".worktree"), worktreeId);

        var projectRoot = PathUtils.FindProjectRoot()!;
        var worktreePath = Path.GetFullPath(Path.Combine(projectRoot, "dydo", "_system", ".local", "worktrees", worktreeId));
        File.WriteAllText(Path.Combine(workspace, ".worktree-path"), worktreePath);

        var baseBranch = GetCurrentGitBranch() ?? "master";
        File.WriteAllText(Path.Combine(workspace, ".worktree-base"), baseBranch);

        return worktreeId;
    }

    internal static string? GetCurrentGitBranch()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "rev-parse --abbrev-ref HEAD",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            var process = Process.Start(psi);
            if (process == null) return null;
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return process.ExitCode == 0 && !string.IsNullOrEmpty(output) ? output : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? GetSenderWorktreeId(AgentRegistry registry, string senderName)
    {
        var marker = Path.Combine(registry.GetAgentWorkspace(senderName), ".worktree");
        if (!File.Exists(marker)) return null;
        return File.ReadAllText(marker).Trim();
    }

    private static string? GetWorktreePath(AgentRegistry registry, string agentName)
    {
        var marker = Path.Combine(registry.GetAgentWorkspace(agentName), ".worktree-path");
        if (!File.Exists(marker)) return null;
        return File.ReadAllText(marker).Trim();
    }

    private static void InheritWorktree(AgentRegistry registry, string targetAgentName, string senderName, string worktreeId)
    {
        var senderWorkspace = registry.GetAgentWorkspace(senderName);
        var targetWorkspace = registry.GetAgentWorkspace(targetAgentName);
        Directory.CreateDirectory(targetWorkspace);
        File.WriteAllText(Path.Combine(targetWorkspace, ".worktree"), worktreeId);

        foreach (var marker in new[] { ".worktree-path", ".worktree-base" })
        {
            var src = Path.Combine(senderWorkspace, marker);
            if (File.Exists(src))
                File.Copy(src, Path.Combine(targetWorkspace, marker), overwrite: true);
        }
    }

    private static bool HasNeedsMergeMarker(AgentRegistry registry, string agentName)
    {
        var marker = Path.Combine(registry.GetAgentWorkspace(agentName), ".needs-merge");
        return File.Exists(marker);
    }

    private static void CopyWorktreeMetadataForMerger(AgentRegistry registry, string targetAgentName, string senderName, string senderWorktreeId)
    {
        var senderWorkspace = registry.GetAgentWorkspace(senderName);
        var targetWorkspace = registry.GetAgentWorkspace(targetAgentName);
        Directory.CreateDirectory(targetWorkspace);

        var baseSrc = Path.Combine(senderWorkspace, ".worktree-base");
        if (File.Exists(baseSrc))
            File.Copy(baseSrc, Path.Combine(targetWorkspace, ".worktree-base"), overwrite: true);

        File.WriteAllText(Path.Combine(targetWorkspace, ".merge-source"), $"worktree/{senderWorktreeId}");
        File.WriteAllText(Path.Combine(targetWorkspace, ".worktree-hold"), senderWorktreeId);
    }

    private static void ClearNeedsMerge(AgentRegistry registry, string agentName)
    {
        var marker = Path.Combine(registry.GetAgentWorkspace(agentName), ".needs-merge");
        if (File.Exists(marker))
            File.Delete(marker);
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

    private static void LaunchTerminalIfNeeded(string targetAgentName, bool noLaunch, bool launchInTab,
        bool effectiveAutoClose, string? worktreeId, string? windowName, string? workingDirOverride = null,
        string? cleanupWorktreeId = null, string? mainProjectRoot = null)
    {
        if (noLaunch)
            return;

        var projectRoot = workingDirOverride ?? PathUtils.FindProjectRoot();
        TerminalLauncher.LaunchNewTerminal(targetAgentName, projectRoot, launchInTab, effectiveAutoClose, worktreeId, windowName, cleanupWorktreeId, mainProjectRoot);
        Console.WriteLine($"  Terminal launched with --inbox {targetAgentName}");
    }

    private static string? CheckNoLaunchNudge(AgentRegistry registry, string? sessionId, string task)
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
            return $"The --wait flag is reserved for oversight roles (orchestrator, inquisitor, judge). Your role '{sender.Role}' should use --no-wait.";

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
