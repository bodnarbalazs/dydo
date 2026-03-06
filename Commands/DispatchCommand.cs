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

        var contextOption = new Option<string?>("--context-file")
        {
            Description = "Path to context file"
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

        var command = new Command("dispatch", "Dispatch work to another agent");
        command.Options.Add(roleOption);
        command.Options.Add(taskOption);
        command.Options.Add(briefOption);
        command.Options.Add(briefFileOption);
        command.Options.Add(filesOption);
        command.Options.Add(contextOption);
        command.Options.Add(noLaunchOption);
        command.Options.Add(toOption);
        command.Options.Add(escalateOption);
        command.Options.Add(tabOption);
        command.Options.Add(newWindowOption);

        command.SetAction(parseResult =>
        {
            var role = parseResult.GetValue(roleOption)!;
            var task = parseResult.GetValue(taskOption)!;
            var brief = parseResult.GetValue(briefOption);
            var briefFile = parseResult.GetValue(briefFileOption);
            var files = parseResult.GetValue(filesOption);
            var contextFile = parseResult.GetValue(contextOption);
            var noLaunch = parseResult.GetValue(noLaunchOption);
            var to = parseResult.GetValue(toOption);
            var escalate = parseResult.GetValue(escalateOption);
            var useTab = parseResult.GetValue(tabOption);
            var useNewWindow = parseResult.GetValue(newWindowOption);

            if (!string.IsNullOrEmpty(briefFile))
            {
                if (!File.Exists(briefFile))
                {
                    ConsoleOutput.WriteError($"Brief file not found: {briefFile}");
                    return ExitCodes.ToolError;
                }
                brief = File.ReadAllText(briefFile).Trim();
            }

            if (string.IsNullOrEmpty(brief))
            {
                ConsoleOutput.WriteError("Provide --brief or --brief-file.");
                return ExitCodes.ToolError;
            }

            return Execute(role, task, brief, files, contextFile, noLaunch, to, escalate, useTab, useNewWindow);
        });

        return command;
    }

    private static int Execute(string role, string task, string brief, string? files, string? contextFile, bool noLaunch, string? to, bool escalate, bool useTab, bool useNewWindow)
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
            ContextFile = contextFile,
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

        // Launch new terminal if requested
        if (!noLaunch)
        {
            var launchInTab = useTab || (!useNewWindow && (registry.Config?.Dispatch?.LaunchInTab ?? false));
            var projectRoot = PathUtils.FindProjectRoot();
            TerminalLauncher.LaunchNewTerminal(targetAgentName, projectRoot, launchInTab);
            Console.WriteLine($"  Terminal launched with --inbox {targetAgentName}");
        }

        return ExitCodes.Success;
    }

    private static void WriteInboxItem(string path, InboxItem item)
    {
        var filesSection = item.Files.Count > 0
            ? $"\n## Files\n\n{string.Join("\n", item.Files.Select(f => $"- {f}"))}"
            : "";

        var contextSection = !string.IsNullOrEmpty(item.ContextFile)
            ? $"\n## Context\n\nSee: [{item.ContextFile}]({item.ContextFile})"
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
            {contextSection}
            """;

        File.WriteAllText(path, content);
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
