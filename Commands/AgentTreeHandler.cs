namespace DynaDocs.Commands;

using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

internal static class AgentTreeHandler
{
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
        foreach (var agent in active)
        {
            var markers = registry.GetWaitMarkers(agent.Name);
            if (markers.Count > 0)
                waitMarkers[agent.Name] = markers;
        }

        for (var i = 0; i < roots.Count; i++)
        {
            if (i > 0)
                Console.WriteLine();
            RenderAgent(roots[i], 5, "", true, children, waitMarkers);
        }

        return ExitCodes.Success;
    }

    private static void RenderAgent(
        AgentState agent, int nameStartCol, string treePrefix, bool isRoot,
        Dictionary<string, List<AgentState>> children,
        Dictionary<string, List<WaitMarker>> waitMarkers)
    {
        if (isRoot)
        {
            Console.WriteLine($"{new string(' ', nameStartCol)}{agent.Name}");
        }
        else
        {
            Console.WriteLine($"{treePrefix}{agent.Name}");
        }

        var role = agent.Role ?? "unknown";
        var roleText = $"[{role}]";
        var task = agent.Task ?? "-";

        var waitText = "";
        if (waitMarkers.TryGetValue(agent.Name, out var markers))
        {
            var targets = string.Join(", ", markers.Select(m => m.Target));
            waitText = $" waiting \u2192 {targets}";
        }

        var roleInfo = $"{roleText}{waitText} ------ {task}";

        var roleStartCol = nameStartCol + (agent.Name.Length / 2) - (roleInfo.Length / 2);
        if (roleStartCol < 0) roleStartCol = 0;

        Console.WriteLine($"{new string(' ', roleStartCol)}{roleInfo}");

        if (!children.TryGetValue(agent.Name, out var kids)) return;

        var stemCol = nameStartCol + 3;
        var childNameCol = stemCol + 4;

        for (var i = 0; i < kids.Count; i++)
        {
            var isLast = i == kids.Count - 1;
            var branch = isLast ? "\u2514\u2500\u2500 " : "\u251C\u2500\u2500 ";
            var continuation = isLast ? "   " : "\u2502  ";

            var prefix = new string(' ', stemCol) + branch;
            var childPrefix = new string(' ', stemCol) + continuation;

            RenderAgent(kids[i], childNameCol, prefix, false, children, waitMarkers);
        }
    }
}
