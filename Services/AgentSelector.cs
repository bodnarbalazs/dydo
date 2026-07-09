namespace DynaDocs.Services;

using DynaDocs.Utils;

public static class AgentSelector
{
    public record SelectionResult(string AgentName);

    public static (SelectionResult? result, string? error) SelectExplicit(
        AgentRegistry registry, string to, string? currentHuman, string role, string task,
        string senderName)
    {
        if (string.Equals(to, senderName, StringComparison.OrdinalIgnoreCase))
            return (null, "Cannot dispatch to yourself; this would orphan your session. Pick a different agent or omit --to.");

        if (!registry.IsValidAgentName(to))
            return (null, $"Agent '{to}' does not exist.");

        var assignedHuman = registry.GetHumanForAgent(to);
        if (!string.IsNullOrEmpty(currentHuman) && assignedHuman != currentHuman)
            return (null, $"Agent '{to}' is not assigned to you (assigned to: {assignedHuman ?? "nobody"}).");

        var dispatcherRole = registry.GetAgentState(senderName)?.Role;
        if (!registry.CanTakeRole(to, role, task, out var roleError, dispatcherRole))
            return (null, roleError);

        if (!registry.ReserveAgent(to, out var reserveError))
            return (null, reserveError);

        return (new SelectionResult(to), null);
    }

    public static (SelectionResult? result, string? error) SelectAutomatic(
        AgentRegistry registry, string? currentHuman, string role, string task,
        string senderName, string? origin)
    {
        // Try auto-return to origin agent first
        var reserved = TryReserveOrigin(registry, currentHuman, role, task, senderName, origin);

        if (reserved == null)
        {
            reserved = TryReserveFromPool(registry, currentHuman, role, task, senderName);

            if (reserved == null)
                return (null, FormatNoAgentsError(currentHuman));
        }

        return (new SelectionResult(reserved), null);
    }

    private static string? TryReserveOrigin(
        AgentRegistry registry, string? currentHuman, string role, string task,
        string senderName, string? origin)
    {
        if (string.IsNullOrEmpty(origin) || origin == senderName)
            return null;

        if (!registry.IsValidAgentName(origin))
            return null;

        var dispatcherRole = registry.GetAgentState(senderName)?.Role;
        if (!registry.CanTakeRole(origin, role, task, out _, dispatcherRole))
            return null;

        var originHuman = registry.GetHumanForAgent(origin);
        if (!string.IsNullOrEmpty(currentHuman) && originHuman != currentHuman)
            return null;

        return registry.ReserveAgent(origin, out _) ? origin : null;
    }

    private static string? TryReserveFromPool(
        AgentRegistry registry, string? currentHuman, string role, string task, string senderName)
    {
        var freeAgents = string.IsNullOrEmpty(currentHuman)
            ? registry.GetFreeAgents()
            : registry.GetFreeAgentsForHuman(currentHuman);

        var dispatcherRole = registry.GetAgentState(senderName)?.Role;
        var eligible = freeAgents
            .Where(a => !string.Equals(a.Name, senderName, StringComparison.OrdinalIgnoreCase))
            .Where(a => registry.CanTakeRole(a.Name, role, task, out _, dispatcherRole))
            .OrderBy(a => registry.HasPendingInbox(a.Name) ? 1 : 0)
            .ThenBy(a => a.Name)
            .ToList();

        if (eligible.Count == 0)
            return null;

        foreach (var candidate in eligible)
        {
            if (registry.ReserveAgent(candidate.Name, out _))
                return candidate.Name;
        }

        return null;
    }

    private static string FormatNoAgentsError(string? currentHuman)
    {
        return !string.IsNullOrEmpty(currentHuman)
            ? $"No free agents available for human '{currentHuman}'."
            : "No free agents available.";
    }
}
