namespace DynaDocs.Commands;

using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

internal static class AgentTreeHandler
{
    private const int BoxWidth = 43;

    public static int ExecuteTree()
    {
        var registry = new AgentRegistry();
        var allStates = registry.GetAllAgentStates();
        var active = allStates.Where(a => a.Status != AgentStatus.Free).ToList();

        if (active.Count == 0)
        {
            Console.WriteLine("No active agents.");
            return ExitCodes.Success;
        }

        var activeNames = new HashSet<string>(active.Select(a => a.Name), StringComparer.OrdinalIgnoreCase);

        var children = new Dictionary<string, List<AgentState>>(StringComparer.OrdinalIgnoreCase);
        var roots = new List<AgentState>();

        foreach (var agent in active)
        {
            if (!string.IsNullOrEmpty(agent.DispatchedBy) && activeNames.Contains(agent.DispatchedBy))
            {
                if (!children.ContainsKey(agent.DispatchedBy))
                    children[agent.DispatchedBy] = [];
                children[agent.DispatchedBy].Add(agent);
            }
            else
            {
                roots.Add(agent);
            }
        }

        foreach (var list in children.Values)
            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        roots.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        var waitMarkers = new Dictionary<string, List<WaitMarker>>(StringComparer.OrdinalIgnoreCase);
        var worktreeGroups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var agent in active)
        {
            var markers = registry.GetWaitMarkers(agent.Name);
            if (markers.Count > 0)
                waitMarkers[agent.Name] = markers;

            var wtId = registry.GetWorktreeId(agent.Name);
            if (wtId != null)
                worktreeGroups[agent.Name] = wtId;
        }

        // Render roots
        var rootItems = GroupByWorktree(roots, worktreeGroups);
        var first = true;
        foreach (var item in rootItems)
        {
            if (!first) Console.WriteLine();
            first = false;

            if (item.WtId != null)
            {
                RenderBox(item.WtId, item.Agents, "  ", "  ",
                    registry, children, waitMarkers, worktreeGroups);
            }
            else
            {
                RenderAgent(item.Agents[0], 5, "", true,
                    children, waitMarkers, worktreeGroups, registry);
            }
        }

        // Legend
        var hasStale = worktreeGroups.Values.Distinct()
            .Any(id => registry.IsWorktreeStale(id));
        if (hasStale)
        {
            Console.WriteLine();
            Console.WriteLine("? = stale worktree");
        }

        return ExitCodes.Success;
    }

    private static List<(string? WtId, List<AgentState> Agents)> GroupByWorktree(
        List<AgentState> agents, Dictionary<string, string> worktreeGroups)
    {
        var buckets = new Dictionary<string, List<AgentState>>(StringComparer.OrdinalIgnoreCase);
        var items = new List<(string? WtId, List<AgentState> Agents)>();

        foreach (var agent in agents)
        {
            if (worktreeGroups.TryGetValue(agent.Name, out var wtId))
            {
                if (!buckets.ContainsKey(wtId))
                    buckets[wtId] = [];
                buckets[wtId].Add(agent);
            }
            else
            {
                items.Add((null, new List<AgentState> { agent }));
            }
        }

        foreach (var (wtId, wtAgents) in buckets)
            items.Add((wtId, wtAgents));

        items.Sort((a, b) => string.Compare(
            a.Agents[0].Name, b.Agents[0].Name, StringComparison.OrdinalIgnoreCase));
        return items;
    }

    private static void RenderBox(
        string wtId, List<AgentState> agents,
        string firstLinePrefix, string contLinePrefix,
        AgentRegistry registry,
        Dictionary<string, List<AgentState>> children,
        Dictionary<string, List<WaitMarker>> waitMarkers,
        Dictionary<string, string> worktreeGroups)
    {
        var stale = registry.IsWorktreeStale(wtId);
        var displayId = wtId + (stale ? "?" : "");
        var innerW = BoxWidth - 4;

        // Top border
        var dashes = Math.Max(1, BoxWidth - 4 - displayId.Length);
        Console.WriteLine($"{firstLinePrefix}\u250c {displayId} {new string('\u2500', dashes)}\u2510");

        // Agent content
        for (var i = 0; i < agents.Count; i++)
        {
            if (i > 0)
                Console.WriteLine($"{contLinePrefix}\u2502{new string(' ', BoxWidth - 2)}\u2502");

            var name = FitToWidth(agents[i].Name, innerW);
            var roleInfo = FitToWidth(BuildRoleInfo(agents[i], waitMarkers), innerW);
            Console.WriteLine($"{contLinePrefix}\u2502  {name}\u2502");
            Console.WriteLine($"{contLinePrefix}\u2502  {roleInfo}\u2502");
        }

        // Bottom border
        Console.WriteLine($"{contLinePrefix}\u2514{new string('\u2500', BoxWidth - 2)}\u2518");

        // Outside children (not in same worktree)
        var outside = new List<AgentState>();
        foreach (var agent in agents)
        {
            if (!children.TryGetValue(agent.Name, out var kids)) continue;
            foreach (var kid in kids)
            {
                if (worktreeGroups.TryGetValue(kid.Name, out var kidWt) && kidWt == wtId)
                    continue;
                outside.Add(kid);
            }
        }

        if (outside.Count > 0)
        {
            outside.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            var stemCol = contLinePrefix.Length + 1;
            RenderItems(GroupByWorktree(outside, worktreeGroups), stemCol,
                children, waitMarkers, worktreeGroups, registry);
        }
    }

    private static void RenderItems(
        List<(string? WtId, List<AgentState> Agents)> items, int stemCol,
        Dictionary<string, List<AgentState>> children,
        Dictionary<string, List<WaitMarker>> waitMarkers,
        Dictionary<string, string> worktreeGroups,
        AgentRegistry registry)
    {
        var childNameCol = stemCol + 4;

        for (var i = 0; i < items.Count; i++)
        {
            var isLast = i == items.Count - 1;
            var item = items[i];
            var branch = isLast ? "\u2514\u2500\u2500 " : "\u251c\u2500\u2500 ";
            var cont = isLast ? "    " : "\u2502   ";
            var spaces = new string(' ', stemCol);

            if (item.WtId != null)
            {
                RenderBox(item.WtId, item.Agents, spaces + branch, spaces + cont,
                    registry, children, waitMarkers, worktreeGroups);
            }
            else
            {
                RenderAgent(item.Agents[0], childNameCol, spaces + branch, false,
                    children, waitMarkers, worktreeGroups, registry);
            }
        }
    }

    private static void RenderAgent(
        AgentState agent, int nameStartCol, string treePrefix, bool isRoot,
        Dictionary<string, List<AgentState>> children,
        Dictionary<string, List<WaitMarker>> waitMarkers,
        Dictionary<string, string> worktreeGroups,
        AgentRegistry registry)
    {
        if (isRoot)
            Console.WriteLine($"{new string(' ', nameStartCol)}{agent.Name}");
        else
            Console.WriteLine($"{treePrefix}{agent.Name}");

        var roleInfo = BuildRoleInfo(agent, waitMarkers);
        var roleStartCol = nameStartCol + (agent.Name.Length / 2) - (roleInfo.Length / 2);
        if (roleStartCol < 0) roleStartCol = 0;
        Console.WriteLine($"{new string(' ', roleStartCol)}{roleInfo}");

        if (!children.TryGetValue(agent.Name, out var kids)) return;

        var stemCol = nameStartCol + 3;
        RenderItems(GroupByWorktree(kids, worktreeGroups), stemCol,
            children, waitMarkers, worktreeGroups, registry);
    }

    private static string BuildRoleInfo(AgentState agent, Dictionary<string, List<WaitMarker>> waitMarkers)
    {
        var role = agent.Role ?? "unknown";
        var task = agent.Task ?? "-";
        var waitText = "";
        if (waitMarkers.TryGetValue(agent.Name, out var markers))
        {
            var targets = string.Join(", ", markers.Select(m => m.Target));
            waitText = $" waiting \u2192 {targets}";
        }
        return $"[{role}]{waitText} ------ {task}";
    }

    private static string FitToWidth(string text, int width) =>
        text.Length <= width ? text.PadRight(width) : text[..width];
}
