namespace DynaDocs.Commands;

using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

internal static class AgentListHandler
{
    public static int ExecuteList(bool freeOnly, bool all)
    {
        var registry = new AgentRegistry();
        var human = registry.GetCurrentHuman();

        if (all)
            return ShowAllAgents(registry, freeOnly, human);

        return ShowHumanAgents(registry, freeOnly, human);
    }

    private static int ShowAllAgents(AgentRegistry registry, bool freeOnly, string? human)
    {
        var agents = freeOnly ? registry.GetFreeAgents() : registry.GetAllAgentStates();

        if (agents.Count == 0)
        {
            Console.WriteLine(freeOnly ? "No free agents in pool." : "No agents found in pool.");
            return ExitCodes.Success;
        }

        Console.WriteLine($"{"Agent",-10} {"Status",-10} {"Human",-12} {"Waiting For",-14} {"Role",-15}");
        Console.WriteLine(new string('-', 66));

        var allWithInbox = new HashSet<string>(
            agents.Select(a => a.Name).Where(registry.HasPendingInbox),
            StringComparer.OrdinalIgnoreCase);

        foreach (var agent in agents)
        {
            var displayName = allWithInbox.Contains(agent.Name) ? agent.Name + "*" : agent.Name;
            var status = agent.Status.ToString().ToLowerInvariant();
            var assignedHuman = agent.AssignedHuman ?? registry.GetHumanForAgent(agent.Name) ?? "-";
            var role = agent.Role ?? "-";
            var waitTargets = registry.GetWaitMarkers(agent.Name);
            var waitingFor = waitTargets.Count > 0
                ? string.Join(", ", waitTargets.Select(m => m.Target))
                : "-";

            Console.WriteLine($"{displayName,-10} {status,-10} {assignedHuman,-12} {waitingFor,-14} {role,-15}");
        }

        var freeCount = agents.Count(a => a.Status == AgentStatus.Free);
        var dispatchedCount = agents.Count(a => a.Status == AgentStatus.Dispatched);
        var workingCount = agents.Count(a => a.Status == AgentStatus.Working);
        Console.WriteLine();
        Console.WriteLine($"Total: {agents.Count} agents ({freeCount} free, {dispatchedCount} dispatched, {workingCount} working)");

        if (!string.IsNullOrEmpty(human))
        {
            var humanAgents = registry.GetAgentsForHuman(human);
            var humanFree = registry.GetFreeAgentsForHuman(human);
            Console.WriteLine($"Agents assigned to human '{human}': {humanAgents.Count} ({humanFree.Count} free)");
        }

        return ExitCodes.Success;
    }

    private static int ShowHumanAgents(AgentRegistry registry, bool freeOnly, string? human)
    {
        if (string.IsNullOrEmpty(human))
        {
            ConsoleOutput.WriteError("No human identity set. Run 'dydo init' to configure, or use 'dydo agent list --all' to see all agents.");
            return ExitCodes.ToolError;
        }

        List<AgentState> filteredAgents;

        if (freeOnly)
        {
            filteredAgents = registry.GetFreeAgentsForHuman(human);
        }
        else
        {
            var humanAgentNames = registry.GetAgentsForHuman(human);
            filteredAgents = registry.GetAllAgentStates()
                .Where(a => humanAgentNames.Contains(a.Name, StringComparer.OrdinalIgnoreCase))
                .ToList();
        }

        if (filteredAgents.Count == 0)
        {
            Console.WriteLine(freeOnly ? "No free agents in pool." : "No agents found in pool.");
            return ExitCodes.Success;
        }

        Console.WriteLine($"{"Agent",-10} {"Status",-10} {"Role",-15} {"Waiting For",-14} {"Task"}");
        Console.WriteLine(new string('-', 66));

        var agentsWithInbox = new HashSet<string>(
            filteredAgents.Select(a => a.Name).Where(registry.HasPendingInbox),
            StringComparer.OrdinalIgnoreCase);

        foreach (var agent in filteredAgents)
        {
            var displayName = agentsWithInbox.Contains(agent.Name) ? agent.Name + "*" : agent.Name;
            var status = agent.Status.ToString().ToLowerInvariant();
            var role = agent.Role ?? "-";
            var task = agent.Task ?? "-";
            var waitTargets = registry.GetWaitMarkers(agent.Name);
            var waitingFor = waitTargets.Count > 0
                ? string.Join(", ", waitTargets.Select(m => m.Target))
                : "-";

            Console.WriteLine($"{displayName,-10} {status,-10} {role,-15} {waitingFor,-14} {task}");
        }

        var freeCount = filteredAgents.Count(a => a.Status == AgentStatus.Free);
        var dispatchedCount = filteredAgents.Count(a => a.Status == AgentStatus.Dispatched);
        var workingCount = filteredAgents.Count(a => a.Status == AgentStatus.Working);
        Console.WriteLine();
        Console.WriteLine($"Total: {filteredAgents.Count} agents ({freeCount} free, {dispatchedCount} dispatched, {workingCount} working)");

        return ExitCodes.Success;
    }
}
