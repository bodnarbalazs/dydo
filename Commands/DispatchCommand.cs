namespace DynaDocs.Commands;

using System.CommandLine;
using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

public static class DispatchCommand
{
    public static Command Create()
    {
        var roleOption = new Option<string>("--role")
        {
            Description = "Role for the target agent",
            Required = true
        };

        var taskOption = new Option<string>("--task")
        {
            Description = "Task name",
            Required = true
        };

        var briefOption = new Option<string?>("--brief")
        {
            Description = "Brief description of the work"
        };

        var briefFileOption = new Option<string?>("--brief-file")
        {
            Description = "Path to a file containing the brief"
        };

        var filesOption = new Option<string?>("--files")
        {
            Description = "File pattern to include (e.g., 'src/Auth/**')"
        };

        var noLaunchOption = new Option<bool>("--no-launch")
        {
            Description = "Don't launch new terminal, just write to inbox"
        };

        var tabOption = new Option<bool>("--tab")
        {
            Description = "Launch in a new tab (overrides config)"
        };

        var newWindowOption = new Option<bool>("--new-window")
        {
            Description = "Launch in a new window (overrides config)"
        };

        var toOption = new Option<string?>("--to")
        {
            Description = "Send dispatch to specific agent (skips auto-selection)"
        };
        toOption.Aliases.Add("--agent");

        var escalateOption = new Option<bool>("--escalate")
        {
            Description = "Mark dispatch as escalated after repeated failures"
        };

        var autoCloseOption = new Option<bool>("--auto-close")
        {
            Description = "Auto-close the dispatched agent's terminal after release"
        };

        var waitOption = new Option<bool>("--wait")
        {
            Description = "Wait for a response from the dispatched agent"
        };

        var noWaitOption = new Option<bool>("--no-wait")
        {
            Description = "Dispatch and return immediately (fire and forget)"
        };

        var worktreeOption = new Option<bool>("--worktree")
        {
            Description = "Run dispatched agent in a git worktree for isolated work"
        };

        var command = new Command("dispatch", "Dispatch work to another agent");
        command.Options.Add(roleOption);
        command.Options.Add(taskOption);
        command.Options.Add(briefOption);
        command.Options.Add(briefFileOption);
        command.Options.Add(filesOption);
        command.Options.Add(noLaunchOption);
        command.Options.Add(toOption);
        command.Options.Add(escalateOption);
        command.Options.Add(autoCloseOption);
        command.Options.Add(tabOption);
        command.Options.Add(newWindowOption);
        command.Options.Add(waitOption);
        command.Options.Add(noWaitOption);
        command.Options.Add(worktreeOption);

        command.SetAction(parseResult =>
        {
            var role = parseResult.GetValue(roleOption)!;
            var task = parseResult.GetValue(taskOption)!;
            var brief = parseResult.GetValue(briefOption);
            var briefFile = parseResult.GetValue(briefFileOption);
            var files = parseResult.GetValue(filesOption);
            var noLaunch = parseResult.GetValue(noLaunchOption);
            var to = parseResult.GetValue(toOption);
            var escalate = parseResult.GetValue(escalateOption);
            var useTab = parseResult.GetValue(tabOption);
            var useNewWindow = parseResult.GetValue(newWindowOption);
            var autoClose = parseResult.GetValue(autoCloseOption);
            var wait = parseResult.GetValue(waitOption);
            var noWait = parseResult.GetValue(noWaitOption);
            var worktree = parseResult.GetValue(worktreeOption);

            // Validate --worktree / --no-launch
            if (worktree && noLaunch)
            {
                ConsoleOutput.WriteError("Cannot specify both --worktree and --no-launch. Worktree lifecycle depends on terminal commands.");
                return ExitCodes.ToolError;
            }

            // Validate --wait / --no-wait
            if (wait && noWait)
            {
                ConsoleOutput.WriteError("Cannot specify both --wait and --no-wait.");
                return ExitCodes.ToolError;
            }

            if (!wait && !noWait)
            {
                ConsoleOutput.WriteError("Specify --wait or --no-wait to indicate whether you expect a response.");
                return ExitCodes.ToolError;
            }

            var briefFromFile = false;
            if (!string.IsNullOrEmpty(briefFile))
            {
                if (!File.Exists(briefFile))
                {
                    ConsoleOutput.WriteError($"Brief file not found: {briefFile}");
                    return ExitCodes.ToolError;
                }
                brief = File.ReadAllText(briefFile).Trim();
                briefFromFile = true;
            }

            if (string.IsNullOrEmpty(brief))
            {
                ConsoleOutput.WriteError("Provide --brief or --brief-file.");
                return ExitCodes.ToolError;
            }

            if (!briefFromFile)
            {
                var shellMetaError = DetectShellMetacharacters(brief);
                if (shellMetaError != null)
                {
                    ConsoleOutput.WriteError(shellMetaError);
                    return ExitCodes.ToolError;
                }
            }

            return Execute(role, task, brief, files, noLaunch, to, escalate, useTab, useNewWindow, autoClose, wait, worktree);
        });

        return command;
    }

    private static int Execute(string role, string task, string brief, string? files, bool noLaunch, string? to, bool escalate, bool useTab, bool useNewWindow, bool autoClose, bool wait, bool worktree)
    {
        if (useTab && useNewWindow)
        {
            ConsoleOutput.WriteError("Cannot specify both --tab and --new-window.");
            return ExitCodes.ToolError;
        }

        var registry = new AgentRegistry();
        var sessionId = registry.GetSessionContext();
        var currentHuman = registry.GetCurrentHuman();

        // --no-launch nudge: catch accidental use of --no-launch
        if (noLaunch)
        {
            var nudgeSender = registry.GetCurrentAgent(sessionId);
            if (nudgeSender != null)
            {
                var senderWorkspace = registry.GetAgentWorkspace(nudgeSender.Name);
                var nudgeKey = PathUtils.SanitizeForFilename(task);
                var markerPath = Path.Combine(senderWorkspace, $".no-launch-nudge-{nudgeKey}");

                if (!File.Exists(markerPath))
                {
                    Directory.CreateDirectory(senderWorkspace);
                    File.WriteAllText(markerPath, DateTime.UtcNow.ToString("o"));
                    ConsoleOutput.WriteError(
                        "You dispatched with the --no-launch flag, it means that the agent you dispatched to will not be activated " +
                        "and the user will have to call them manually. Unless the user was explicit about using no-launch or there is a " +
                        "good reason for it you shouldn't use this flag. If you insist you may run it again and it will pass.");
                    return ExitCodes.ToolError;
                }
                File.Delete(markerPath);
            }
        }

        // Get sender info
        var sender = registry.GetCurrentAgent(sessionId);
        var senderName = sender?.Name ?? "Unknown";

        // --wait privilege: only orchestrator, inquisitor, judge may use --wait
        if (wait && sender != null)
        {
            var waitPrivilegedRoles = new[] { "orchestrator", "inquisitor", "judge" };
            if (!waitPrivilegedRoles.Contains(sender.Role, StringComparer.OrdinalIgnoreCase))
            {
                ConsoleOutput.WriteError($"The --wait flag is reserved for oversight roles (orchestrator, inquisitor, judge). Your role '{sender.Role}' should use --no-wait. See decision 005.");
                return ExitCodes.ToolError;
            }
        }

        // Determine origin: inherit from inbox item if this is a send-back, else sender is origin
        var origin = GetOriginForTask(registry, sender, task) ?? senderName;

        // Double-dispatch protection
        var existing = FindAgentWorkingOnTask(registry, task, senderName);
        if (existing != null)
        {
            ConsoleOutput.WriteError($"{existing} is already working on task '{task}'. If you need to re-dispatch, have them release first.");
            return ExitCodes.ToolError;
        }

        // Determine target agent
        string targetAgentName;

        if (!string.IsNullOrEmpty(to))
        {
            // Explicit agent specified - validate it
            if (!registry.IsValidAgentName(to))
            {
                ConsoleOutput.WriteError($"Agent '{to}' does not exist.");
                return ExitCodes.ToolError;
            }

            // Must be assigned to current human (if human context exists)
            var assignedHuman = registry.GetHumanForAgent(to);
            if (!string.IsNullOrEmpty(currentHuman) && assignedHuman != currentHuman)
            {
                ConsoleOutput.WriteError($"Agent '{to}' is not assigned to you (assigned to: {assignedHuman ?? "nobody"}).");
                return ExitCodes.ToolError;
            }

            // Check role eligibility (e.g., prevent dispatching review to the code-writer)
            if (!registry.CanTakeRole(to, role, task, out var roleError))
            {
                ConsoleOutput.WriteError(roleError);
                return ExitCodes.ToolError;
            }

            // Atomically reserve the agent
            if (!registry.ReserveAgent(to, out var reserveError))
            {
                ConsoleOutput.WriteError(reserveError);
                return ExitCodes.ToolError;
            }

            targetAgentName = to;
        }
        else
        {
            // Try auto-return to origin agent first
            string? reserved = null;
            if (!string.IsNullOrEmpty(origin) && origin != senderName &&
                registry.IsValidAgentName(origin) &&
                registry.CanTakeRole(origin, role, task, out _))
            {
                var originHuman = registry.GetHumanForAgent(origin);
                if (string.IsNullOrEmpty(currentHuman) || originHuman == currentHuman)
                {
                    if (registry.ReserveAgent(origin, out _))
                    {
                        reserved = origin;
                    }
                }
            }

            if (reserved == null)
            {
                // Auto-select: Find first free agent assigned to the current human
                var freeAgents = string.IsNullOrEmpty(currentHuman)
                    ? registry.GetFreeAgents()
                    : registry.GetFreeAgentsForHuman(currentHuman);

                // Filter by role eligibility
                var eligible = freeAgents
                    .Where(a => registry.CanTakeRole(a.Name, role, task, out _))
                    .OrderBy(a => registry.HasPendingInbox(a.Name) ? 1 : 0)
                    .ThenBy(a => a.Name)
                    .ToList();

                if (eligible.Count == 0)
                {
                    if (!string.IsNullOrEmpty(currentHuman))
                    {
                        ConsoleOutput.WriteError($"No free agents available for human '{currentHuman}'.");
                    }
                    else
                    {
                        ConsoleOutput.WriteError("No free agents available.");
                    }
                    return ExitCodes.ToolError;
                }

                // Try to reserve each candidate in alphabetical order
                // Another dispatch may grab a candidate between query and lock
                foreach (var candidate in eligible)
                {
                    if (registry.ReserveAgent(candidate.Name, out _))
                    {
                        reserved = candidate.Name;
                        break;
                    }
                }

                if (reserved == null)
                {
                    ConsoleOutput.WriteError("No free agents could be reserved. All candidates were claimed concurrently.");
                    return ExitCodes.ToolError;
                }
            }

            targetAgentName = reserved;
        }

        // Create inbox item
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

        // Write to target agent's inbox
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

        // Auto-transition task to review-pending when dispatching to reviewer
        if (role.Equals("reviewer", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(task))
        {
            if (TaskCommand.TransitionToReviewPending(task, brief))
            {
                Console.WriteLine($"  Task state: marked ready for review");
            }
        }

        var escalatedIndicator = escalate ? " [ESCALATED]" : "";
        Console.WriteLine($"Work dispatched to agent {targetAgentName}.{escalatedIndicator}");
        Console.WriteLine($"  Role: {role}");
        Console.WriteLine($"  Task: {task}");
        Console.WriteLine($"  Inbox: {itemPath}");
        if (escalate)
        {
            Console.WriteLine($"  Escalated: yes");
        }

        // Resolve effective auto-close: CLI flag || config default
        var effectiveAutoClose = autoClose || (registry.Config?.Dispatch?.AutoClose ?? false);

        // Create .worktree marker so cleanup can find orphans
        string? worktreeId = null;
        if (worktree)
        {
            worktreeId = TerminalLauncher.GenerateWorktreeId(targetAgentName);
            var worktreeMarker = Path.Combine(registry.GetAgentWorkspace(targetAgentName), ".worktree");
            File.WriteAllText(worktreeMarker, worktreeId);
        }

        // Determine window routing context
        string? windowName = Environment.GetEnvironmentVariable("DYDO_WINDOW");
        var launchInTab = useTab || (!useNewWindow && (registry.Config?.Dispatch?.LaunchInTab ?? false));

        if (!launchInTab)
        {
            // New window: always generate a fresh GUID for routing
            windowName = Guid.NewGuid().ToString("N")[..8];
        }
        // For tabs: windowName inherits from DYDO_WINDOW (may be null — falls back to -w 0)

        // Store dispatch metadata in target agent state (survives release for watchdog)
        registry.SetDispatchMetadata(targetAgentName, windowName, effectiveAutoClose);

        // Launch new terminal if requested
        if (!noLaunch)
        {
            var projectRoot = PathUtils.FindProjectRoot();
            TerminalLauncher.LaunchNewTerminal(targetAgentName, projectRoot, launchInTab, effectiveAutoClose, worktreeId, windowName);
            Console.WriteLine($"  Terminal launched with --inbox {targetAgentName}");
        }

        // Start watchdog if auto-close is enabled
        if (effectiveAutoClose)
            WatchdogService.EnsureRunning();

        // --wait: create marker and return immediately
        if (wait)
        {
            registry.CreateWaitMarker(senderName, task, targetAgentName);
            Console.WriteLine($"Wait registered for '{task}'. Listen for the response with:");
            Console.WriteLine($"  dydo wait --task {task}");
            Console.WriteLine("  (run in background to avoid blocking)");
            return ExitCodes.Success;
        }

        // --no-wait: show release hint when agent has no remaining work
        if (sender != null &&
            !string.Equals(sender.Role, "co-thinker", StringComparison.OrdinalIgnoreCase))
        {
            var senderInbox = Path.Combine(registry.GetAgentWorkspace(senderName), "inbox");
            var hasInboxItems = Directory.Exists(senderInbox) && Directory.GetFiles(senderInbox, "*.md").Length > 0;
            var hasWaitMarkers = registry.GetWaitMarkers(senderName).Count > 0;

            if (!hasInboxItems && !hasWaitMarkers)
            {
                Console.WriteLine("  Nothing left? Don't forget: dydo agent release");
            }
        }

        return ExitCodes.Success;
    }

    /// <summary>
    /// Checks if any non-free agent (other than the sender) is already working on the given task.
    /// </summary>
    private static string? FindAgentWorkingOnTask(AgentRegistry registry, string task, string senderName)
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

    private static void WriteInboxItem(string path, InboxItem item)
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

    /// <summary>
    /// Checks a brief for shell metacharacters that indicate the Bash command was garbled
    /// (e.g. unquoted &&, ||, $()). Returns an error message if detected, null if clean.
    /// Briefs loaded via --brief-file skip this check since they bypass the shell.
    /// </summary>
    internal static string? DetectShellMetacharacters(string brief)
    {
        // Patterns that almost certainly indicate shell garbling, not intentional prose
        string[] shellPatterns = ["&&", "||", "$(", "${", "`"];

        foreach (var pattern in shellPatterns)
        {
            if (brief.Contains(pattern))
                return $"Brief contains shell metacharacter '{pattern}'. " +
                       "This usually means the --brief value was not properly quoted in the shell. " +
                       "Use --brief-file instead for complex content.";
        }

        return null;
    }

    /// <summary>
    /// Determines the origin agent for a task by checking the sender's inbox/archive for a matching item.
    /// Returns the origin from the inbox item, or the From field if no origin is set.
    /// Returns null if no matching inbox item is found (sender is the origin).
    /// </summary>
    private static string? GetOriginForTask(AgentRegistry registry, AgentState? sender, string task)
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

    /// <summary>
    /// Lightweight parse of an inbox item file to extract only origin and from fields.
    /// </summary>
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
