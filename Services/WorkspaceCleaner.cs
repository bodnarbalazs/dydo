namespace DynaDocs.Services;

using DynaDocs.Models;
using DynaDocs.Utils;

public static class WorkspaceCleaner
{
    private static readonly string[] FilesToClean =
        ["state.md", ".session", "plan.md", "notes.md", ".auto-close"];

    private static readonly string[] DirsToRemove =
        ["modes", ".waiting", ".reply-pending", ".dispatch-markers", ".review-dispatched"];

    public static int Execute(string? agentNameOrLetter, bool all, bool force, string? task)
    {
        var registry = new AgentRegistry();

        if (!string.IsNullOrEmpty(task))
            return CleanByTask(registry, task, force);

        if (all)
            return CleanAll(registry, force);

        if (!string.IsNullOrEmpty(agentNameOrLetter))
            return CleanAgent(registry, agentNameOrLetter, force);

        ConsoleOutput.WriteError("Specify an agent name, --all, or --task <name>");
        return ExitCodes.ToolError;
    }

    private static int CleanAgent(AgentRegistry registry, string nameOrLetter, bool force)
    {
        var name = nameOrLetter;
        if (nameOrLetter.Length == 1 && char.IsLetter(nameOrLetter[0]))
        {
            name = registry.GetAgentNameFromLetter(nameOrLetter[0]);
            if (name == null)
            {
                ConsoleOutput.WriteError($"Unknown agent letter: {nameOrLetter}");
                return ExitCodes.ToolError;
            }
        }

        if (!registry.IsValidAgentName(name))
        {
            ConsoleOutput.WriteError($"Unknown agent: {name}");
            return ExitCodes.ToolError;
        }

        var state = registry.GetAgentState(name);
        if (state is { Status: not AgentStatus.Free } && !force)
        {
            ConsoleOutput.WriteError(
                $"Agent {name} is currently {state.Status}. Use --force to clean anyway.");
            return ExitCodes.ToolError;
        }

        var workspace = registry.GetAgentWorkspace(name);
        if (!Directory.Exists(workspace))
        {
            Console.WriteLine($"Workspace for {name} is already clean");
            return ExitCodes.Success;
        }

        var markersBefore = CountWaitMarkersInWorkspace(workspace);
        CleanWorkspace(workspace);
        Console.WriteLine($"Cleaned workspace for {name}");
        if (markersBefore > 0)
            Console.WriteLine($"Audit: found {markersBefore} stale wait marker(s), cleaned {markersBefore}");

        return ExitCodes.Success;
    }

    private static int CleanAll(AgentRegistry registry, bool force)
    {
        if (!force)
        {
            var workingAgents = registry.GetAllAgentStates()
                .Where(a => a.Status != AgentStatus.Free)
                .ToList();

            if (workingAgents.Count > 0)
            {
                ConsoleOutput.WriteError(
                    $"Cannot clean: {workingAgents.Count} agent(s) are working:");
                foreach (var agent in workingAgents)
                    Console.WriteLine($"  - {agent.Name}: {agent.Status} ({agent.Task ?? "no task"})");
                Console.WriteLine("Use --force to clean anyway.");
                return ExitCodes.ToolError;
            }
        }

        var markersBefore = CountWaitMarkers(registry);

        var cleaned = 0;
        foreach (var name in registry.AgentNames)
        {
            var workspace = registry.GetAgentWorkspace(name);
            if (Directory.Exists(workspace))
            {
                CleanWorkspace(workspace);
                cleaned++;
            }
        }

        Console.WriteLine($"Cleaned {cleaned} workspace(s)");
        ReportWaitMarkerAudit(registry, markersBefore);
        return ExitCodes.Success;
    }

    private static int CleanByTask(AgentRegistry registry, string taskName, bool force)
    {
        var cleaned = 0;

        foreach (var name in registry.AgentNames)
        {
            var state = registry.GetAgentState(name);
            if (state?.Task == null ||
                !state.Task.Contains(taskName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (state.Status != AgentStatus.Free && !force)
            {
                Console.WriteLine(
                    $"Skipping {name} (currently {state.Status}). Use --force to include.");
                continue;
            }

            var workspace = registry.GetAgentWorkspace(name);
            if (!Directory.Exists(workspace)) continue;

            CleanWorkspace(workspace);
            Console.WriteLine($"Cleaned {name} (was working on {state.Task})");
            cleaned++;
        }

        Console.WriteLine(cleaned == 0
            ? $"No workspaces found for task: {taskName}"
            : $"Cleaned {cleaned} workspace(s) for task: {taskName}");

        return ExitCodes.Success;
    }

    private static int CountWaitMarkersInWorkspace(string workspace)
    {
        var waitingDir = Path.Combine(workspace, ".waiting");
        if (!Directory.Exists(waitingDir))
            return 0;
        return Directory.GetFiles(waitingDir, "*.json").Length;
    }

    private static int CountWaitMarkers(AgentRegistry registry)
    {
        var count = 0;
        foreach (var name in registry.AgentNames)
        {
            var waitingDir = Path.Combine(registry.GetAgentWorkspace(name), ".waiting");
            if (Directory.Exists(waitingDir))
                count += Directory.GetFiles(waitingDir, "*.json").Length;
        }
        return count;
    }

    private static void ReportWaitMarkerAudit(AgentRegistry registry, int beforeCount)
    {
        if (beforeCount == 0) return;

        var afterCount = CountWaitMarkers(registry);
        var cleaned = beforeCount - afterCount;
        Console.WriteLine($"Audit: found {beforeCount} stale wait marker(s), cleaned {cleaned}");
    }

    public static void CleanWorkspace(string workspace)
    {
        foreach (var file in FilesToClean)
        {
            var path = Path.Combine(workspace, file);
            if (File.Exists(path))
                File.Delete(path);
        }

        var inboxPath = Path.Combine(workspace, "inbox");
        if (Directory.Exists(inboxPath))
        {
            foreach (var file in Directory.GetFiles(inboxPath))
                File.Delete(file);
        }

        foreach (var dir in DirsToRemove)
        {
            var path = Path.Combine(workspace, dir);
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
    }
}
