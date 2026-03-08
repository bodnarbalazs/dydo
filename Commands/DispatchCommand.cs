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

            return Execute(role, task, brief, files, noLaunch, to, escalate, useTab, useNewWindow, autoClose, wait);
        });

        return command;
    }

    private static int Execute(string role, string task, string brief, string? files, bool noLaunch, string? to, bool escalate, bool useTab, bool useNewWindow, bool autoClose, bool wait)
    {
        if (useTab && useNewWindow)
        {
            ConsoleOutput.WriteError("Cannot specify both --tab and --new-window.");
            return ExitCodes.ToolError;
        }

        var registry = new AgentRegistry();
        var sessionId = registry.GetSessionContext();
        var currentHuman = registry.GetCurrentHuman();

        // Get sender info
        var sender = registry.GetCurrentAgent(sessionId);
        var senderName = sender?.Name ?? "Unknown";

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
                    .OrderBy(a => a.Name)
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
            EscalatedAt = escalate ? DateTime.UtcNow : null
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

        // Create .auto-close marker in target agent workspace
        if (effectiveAutoClose)
        {
            var autoCloseMarker = Path.Combine(registry.GetAgentWorkspace(targetAgentName), ".auto-close");
            File.WriteAllText(autoCloseMarker, "");
        }

        // Launch new terminal if requested
        if (!noLaunch)
        {
            var launchInTab = useTab || (!useNewWindow && (registry.Config?.Dispatch?.LaunchInTab ?? false));
            var projectRoot = PathUtils.FindProjectRoot();
            TerminalLauncher.LaunchNewTerminal(targetAgentName, projectRoot, launchInTab, effectiveAutoClose);
            Console.WriteLine($"  Terminal launched with --inbox {targetAgentName}");
        }

        // --wait: create marker and enter poll loop
        if (wait)
        {
            registry.CreateWaitMarker(senderName, task, targetAgentName);
            Console.WriteLine($"Waiting for response on '{task}'...");
            Console.WriteLine($"  If this times out, resume with: dydo wait --task {task}");

            var senderInboxPath = Path.Combine(registry.GetAgentWorkspace(senderName), "inbox");
            while (true)
            {
                var message = WaitCommand.FindMessage(senderInboxPath, task);
                if (message != null)
                {
                    Console.WriteLine($"Message received from {message.From}:");
                    Console.WriteLine($"  Subject: {message.Subject ?? "(none)"}");
                    Console.WriteLine($"  Body: {message.Body}");
                    registry.RemoveWaitMarker(senderName, task);
                    return ExitCodes.Success;
                }

                Thread.Sleep(10_000);
            }
        }

        // --no-wait: show release hint when appropriate
        if (origin != senderName &&
            !string.Equals(sender?.Role, "co-thinker", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("  Nothing left? Don't forget: dydo agent release");
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

        var escalationHeader = item.Escalated ? "ESCALATED " : "";

        var content = $"""
            ---
            id: {item.Id}
            from: {item.From}
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
