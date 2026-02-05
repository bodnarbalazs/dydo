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

        var briefOption = new Option<string>("--brief")
        {
            Description = "Brief description of the work",
            Required = true
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

        var toOption = new Option<string?>("--to")
        {
            Description = "Send dispatch to specific agent (skips auto-selection)"
        };

        var escalateOption = new Option<bool>("--escalate")
        {
            Description = "Mark dispatch as escalated after repeated failures"
        };

        var command = new Command("dispatch", "Dispatch work to another agent");
        command.Options.Add(roleOption);
        command.Options.Add(taskOption);
        command.Options.Add(briefOption);
        command.Options.Add(filesOption);
        command.Options.Add(contextOption);
        command.Options.Add(noLaunchOption);
        command.Options.Add(toOption);
        command.Options.Add(escalateOption);

        command.SetAction(parseResult =>
        {
            var role = parseResult.GetValue(roleOption)!;
            var task = parseResult.GetValue(taskOption)!;
            var brief = parseResult.GetValue(briefOption)!;
            var files = parseResult.GetValue(filesOption);
            var contextFile = parseResult.GetValue(contextOption);
            var noLaunch = parseResult.GetValue(noLaunchOption);
            var to = parseResult.GetValue(toOption);
            var escalate = parseResult.GetValue(escalateOption);

            return Execute(role, task, brief, files, contextFile, noLaunch, to, escalate);
        });

        return command;
    }

    private static int Execute(string role, string task, string brief, string? files, string? contextFile, bool noLaunch, string? to, bool escalate)
    {
        var registry = new AgentRegistry();
        var sessionId = registry.GetSessionContext();
        var currentHuman = registry.GetCurrentHuman();

        // Get sender info
        var sender = registry.GetCurrentAgent(sessionId);
        var senderName = sender?.Name ?? "Unknown";

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

            // Must be free
            var agentState = registry.GetAgentState(to);
            if (agentState?.Status != AgentStatus.Free)
            {
                ConsoleOutput.WriteError($"Agent '{to}' is not free (status: {agentState?.Status}).");
                return ExitCodes.ToolError;
            }

            targetAgentName = to;
        }
        else
        {
            // Auto-select: Find first free agent assigned to the current human
            var freeAgents = string.IsNullOrEmpty(currentHuman)
                ? registry.GetFreeAgents()
                : registry.GetFreeAgentsForHuman(currentHuman);

            if (freeAgents.Count == 0)
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

            // Pick first alphabetically
            targetAgentName = freeAgents.OrderBy(a => a.Name).First().Name;
        }

        // Create inbox item
        var inboxItem = new InboxItem
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            From = senderName,
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

        var itemPath = Path.Combine(inboxPath, $"{inboxItem.Id}-{task}.md");
        WriteInboxItem(itemPath, inboxItem);

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
            TerminalLauncher.LaunchNewTerminal(targetAgentName);
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
            received: {item.Received:o}{escalationYaml}
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
}
