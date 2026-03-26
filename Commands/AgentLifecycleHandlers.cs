namespace DynaDocs.Commands;

using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

internal static class AgentLifecycleHandlers
{
    public static int ExecuteClaim(string nameOrLetter)
    {
        var registry = new AgentRegistry();

        if (nameOrLetter.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            if (!registry.ClaimAuto(out var claimedAgent, out var autoError))
            {
                ConsoleOutput.WriteError(autoError);
                return ExitCodes.ToolError;
            }

            Console.WriteLine($"Agent identity assigned to this process: {claimedAgent}");
            Console.WriteLine($"  Workspace: {registry.GetAgentWorkspace(claimedAgent)}");

            var human = registry.GetCurrentHuman();
            if (!string.IsNullOrEmpty(human))
                Console.WriteLine($"  Assigned human: {human}");

            return ExitCodes.Success;
        }

        var name = nameOrLetter;
        if (nameOrLetter.Length == 1 && char.IsLetter(nameOrLetter[0]))
        {
            var resolved = registry.GetAgentNameFromLetter(nameOrLetter[0]);
            if (resolved == null)
            {
                ConsoleOutput.WriteError($"Unknown agent letter: {nameOrLetter}");
                return ExitCodes.ToolError;
            }
            name = resolved;
        }

        if (!registry.ClaimAgent(name, out var error))
        {
            ConsoleOutput.WriteError(error);
            return ExitCodes.ToolError;
        }

        Console.WriteLine($"Agent identity assigned to this process: {name}");
        Console.WriteLine($"  Workspace: {registry.GetAgentWorkspace(name)}");

        var currentHuman = registry.GetCurrentHuman();
        if (!string.IsNullOrEmpty(currentHuman))
            Console.WriteLine($"  Assigned human: {currentHuman}");

        return ExitCodes.Success;
    }

    public static int ExecuteRelease()
    {
        var registry = new AgentRegistry();
        var sessionId = registry.GetSessionContext();

        var current = registry.GetCurrentAgent(sessionId);
        if (current == null)
        {
            ConsoleOutput.WriteError("No agent identity assigned to this process.");
            return ExitCodes.ToolError;
        }

        if (!registry.ReleaseAgent(sessionId, out var error))
        {
            ConsoleOutput.WriteError(error);
            return ExitCodes.ToolError;
        }

        Console.WriteLine($"Agent identity released: {current.Name}");
        Console.WriteLine("  Status: free");

        DequeueIfActive(current.Name, registry.Config);

        return ExitCodes.Success;
    }

    internal static void DequeueIfActive(string agentName, DydoConfig? config)
    {
        var queueService = new QueueService(null, config);
        var activeQueues = queueService.FindQueuesWithActiveAgent(agentName);

        foreach (var queueName in activeQueues)
        {
            queueService.ClearActive(queueName);
            var next = queueService.DequeueNext(queueName);
            if (next != null)
            {
                queueService.ClearQueuedMarker(next.Agent);
                var projectRoot = next.WorkingDirOverride ?? next.MainProjectRoot ?? PathUtils.FindProjectRoot();
                var pid = TerminalLauncher.LaunchNewTerminal(next.Agent, projectRoot, next.LaunchInTab,
                    next.AutoClose, next.WorktreeId, next.WindowName, next.CleanupWorktreeId, next.MainProjectRoot);
                queueService.SetActive(queueName, next.Agent, next.Task, pid);
                Console.WriteLine($"  Dequeued {next.Agent} from '{queueName}' — terminal launched.");
            }
            else
            {
                queueService.CleanupIfEmptyTransient(queueName);
            }
        }
    }

    public static int ExecuteStatus(string? name)
    {
        var registry = new AgentRegistry();
        var sessionId = registry.GetSessionContext();

        DynaDocs.Models.AgentState? state;
        if (string.IsNullOrEmpty(name))
        {
            state = registry.GetCurrentAgent(sessionId);
            if (state == null)
            {
                ConsoleOutput.WriteError("No agent identity assigned to this process.");
                return ExitCodes.ToolError;
            }
        }
        else
        {
            state = registry.GetAgentState(name);
            if (state == null)
            {
                ConsoleOutput.WriteError($"Unknown agent: {name}");
                return ExitCodes.ToolError;
            }
        }

        Console.WriteLine($"Agent: {state.Name}");
        Console.WriteLine($"  Status: {state.Status.ToString().ToLowerInvariant()}");
        Console.WriteLine($"  Assigned human: {state.AssignedHuman ?? registry.GetHumanForAgent(state.Name) ?? "(unassigned)"}");
        Console.WriteLine($"  Role: {state.Role ?? "(none)"}");

        if (!string.IsNullOrEmpty(state.Task))
        {
            Console.WriteLine($"  Task: {state.Task}");
            var configService = new ConfigService();
            var taskFilePath = Path.Combine(configService.GetTasksPath(), $"{state.Task}.md");
            if (File.Exists(taskFilePath))
                Console.WriteLine($"  Task file: {taskFilePath}");
        }

        if (state.Since.HasValue)
            Console.WriteLine($"  Since: {state.Since.Value:yyyy-MM-dd HH:mm:ss} UTC");

        if (state.WritablePaths.Count > 0)
            Console.WriteLine($"  Writable paths: {string.Join(", ", state.WritablePaths)}");

        if (state.ReadOnlyPaths.Count > 0 && state.ReadOnlyPaths[0] != "**")
            Console.WriteLine($"  Read-only paths: {string.Join(", ", state.ReadOnlyPaths)}");

        var session = registry.GetSession(state.Name);
        if (session != null)
        {
            Console.WriteLine();
            Console.WriteLine("Session:");
            Console.WriteLine($"  Session ID: {session.SessionId}");
            Console.WriteLine($"  Claimed: {session.Claimed:yyyy-MM-dd HH:mm:ss} UTC");
        }

        return ExitCodes.Success;
    }

    public static int ExecuteRole(string role, string? task)
    {
        var registry = new AgentRegistry();
        var sessionId = registry.GetSessionContext();

        var current = registry.GetCurrentAgent(sessionId);
        if (current == null)
        {
            ConsoleOutput.WriteError("No agent identity assigned to this process. Run 'dydo agent claim auto' first.");
            return ExitCodes.ToolError;
        }

        if (!registry.SetRole(sessionId, role, task, out var error))
        {
            ConsoleOutput.WriteError(error);
            return ExitCodes.ToolError;
        }

        Console.WriteLine($"Agent {current.Name} role updated.");
        Console.WriteLine($"  Role: {role}");

        if (!string.IsNullOrEmpty(task))
        {
            Console.WriteLine($"  Task: {task}");
            var configService = new ConfigService();
            var taskFilePath = Path.Combine(configService.GetTasksPath(), $"{task}.md");
            if (File.Exists(taskFilePath))
                Console.WriteLine($"  Task file: {taskFilePath}");
        }

        var state = registry.GetAgentState(current.Name);
        if (state != null)
        {
            Console.WriteLine($"  Writable paths: {string.Join(", ", state.WritablePaths)}");
            if (state.ReadOnlyPaths.Count > 0 && state.ReadOnlyPaths[0] != "**")
                Console.WriteLine($"  Read-only paths: {string.Join(", ", state.ReadOnlyPaths)}");
            else if (state.WritablePaths.Count == 0)
                Console.WriteLine("  Note: This role has no write permissions.");
        }

        return ExitCodes.Success;
    }
}
