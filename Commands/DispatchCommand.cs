namespace DynaDocs.Commands;

using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Runtime.InteropServices;
using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

public static class DispatchCommand
{
    public static Command Create()
    {
        var roleOption = new Option<string>("--role", "Role for the target agent")
        {
            IsRequired = true
        };

        var taskOption = new Option<string>("--task", "Task name")
        {
            IsRequired = true
        };

        var briefOption = new Option<string>("--brief", "Brief description of the work")
        {
            IsRequired = true
        };

        var filesOption = new Option<string?>("--files", "File pattern to include (e.g., 'src/Auth/**')");

        var contextOption = new Option<string?>("--context-file", "Path to context file");

        var noLaunchOption = new Option<bool>("--no-launch", "Don't launch new terminal, just write to inbox");

        var toOption = new Option<string?>("--to", "Send dispatch to specific agent (skips auto-selection)");

        var escalateOption = new Option<bool>("--escalate", "Mark dispatch as escalated after repeated failures");

        var command = new Command("dispatch", "Dispatch work to another agent")
        {
            roleOption,
            taskOption,
            briefOption,
            filesOption,
            contextOption,
            noLaunchOption,
            toOption,
            escalateOption
        };

        command.SetHandler((InvocationContext ctx) =>
        {
            var role = ctx.ParseResult.GetValueForOption(roleOption)!;
            var task = ctx.ParseResult.GetValueForOption(taskOption)!;
            var brief = ctx.ParseResult.GetValueForOption(briefOption)!;
            var files = ctx.ParseResult.GetValueForOption(filesOption);
            var contextFile = ctx.ParseResult.GetValueForOption(contextOption);
            var noLaunch = ctx.ParseResult.GetValueForOption(noLaunchOption);
            var to = ctx.ParseResult.GetValueForOption(toOption);
            var escalate = ctx.ParseResult.GetValueForOption(escalateOption);

            ctx.ExitCode = Execute(role, task, brief, files, contextFile, noLaunch, to, escalate);
        });

        return command;
    }

    private static int Execute(string role, string task, string brief, string? files, string? contextFile, bool noLaunch, string? to, bool escalate)
    {
        var registry = new AgentRegistry();
        var currentHuman = registry.GetCurrentHuman();

        // Get sender info
        var sender = registry.GetCurrentAgent();
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
            var letter = targetAgentName[0];
            LaunchNewTerminal(letter);
            Console.WriteLine($"  Terminal launched with --inbox {letter}");
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

    private record TerminalConfig(string FileName, Func<char, string> GetArguments);

    private static readonly TerminalConfig[] LinuxTerminals =
    [
        // Modern terminals (most common on current distros)
        new("gnome-terminal", a => $"-- bash -c \"claude --inbox {a}; exec bash\""),
        new("konsole", a => $"-e bash -c \"claude --inbox {a}; exec bash\""),
        new("xfce4-terminal", a => $"-e \"bash -c 'claude --inbox {a}; exec bash'\""),

        // Popular third-party terminals
        new("alacritty", a => $"-e bash -c \"claude --inbox {a}; exec bash\""),
        new("kitty", a => $"bash -c \"claude --inbox {a}; exec bash\""),
        new("wezterm", a => $"start -- bash -c \"claude --inbox {a}; exec bash\""),
        new("tilix", a => $"-e \"bash -c 'claude --inbox {a}; exec bash'\""),

        // Wayland-native
        new("foot", a => $"bash -c \"claude --inbox {a}; exec bash\""),

        // Fallback (usually available)
        new("xterm", a => $"-e bash -c \"claude --inbox {a}; exec bash\""),
    ];

    private static readonly TerminalConfig[] MacTerminals =
    [
        // Terminal.app is always present on macOS
        new("osascript", a => $"-e 'tell app \"Terminal\" to do script \"claude --inbox {a}\"'"),
    ];

    private static void LaunchNewTerminal(char agentLetter)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-Command \"claude --inbox {agentLetter}\"",
                    UseShellExecute = true
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                if (!TryLaunchTerminal(MacTerminals, agentLetter))
                {
                    throw new InvalidOperationException("No terminal found");
                }
            }
            else
            {
                if (!TryLaunchTerminal(LinuxTerminals, agentLetter))
                {
                    throw new InvalidOperationException("No terminal found");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WARN: Could not launch terminal: {ex.Message}");
            Console.WriteLine($"Please manually run: claude --inbox {agentLetter}");
        }
    }

    private static bool TryLaunchTerminal(TerminalConfig[] terminals, char agentLetter)
    {
        foreach (var terminal in terminals)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = terminal.FileName,
                    Arguments = terminal.GetArguments(agentLetter),
                    UseShellExecute = true
                });
                return true;
            }
            catch
            {
                // Try next terminal
            }
        }
        return false;
    }
}
