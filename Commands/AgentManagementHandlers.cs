namespace DynaDocs.Commands;

using DynaDocs.Services;
using DynaDocs.Utils;

internal static class AgentManagementHandlers
{
    public static int ExecuteNew(string name, string human)
    {
        var registry = new AgentRegistry();

        if (!registry.CreateAgent(name, human, out var error))
        {
            ConsoleOutput.WriteError(error);
            return ExitCodes.ToolError;
        }

        var displayName = name.Length > 1
            ? char.ToUpperInvariant(name[0]) + name[1..].ToLowerInvariant()
            : name.ToUpperInvariant();

        Console.WriteLine($"Agent created: {displayName}");
        Console.WriteLine($"  Assigned to: {human}");
        Console.WriteLine($"  Workspace: {registry.GetAgentWorkspace(displayName)}");

        var workflowPath = Path.Combine(
            new ConfigService().GetDydoRoot(),
            "workflows",
            $"{displayName.ToLowerInvariant()}.md");
        Console.WriteLine($"  Workflow: {workflowPath}");

        return ExitCodes.Success;
    }

    public static int ExecuteRename(string oldName, string newName)
    {
        var registry = new AgentRegistry();

        if (!registry.RenameAgent(oldName, newName, out var error))
        {
            ConsoleOutput.WriteError(error);
            return ExitCodes.ToolError;
        }

        var displayNewName = newName.Length > 1
            ? char.ToUpperInvariant(newName[0]) + newName[1..].ToLowerInvariant()
            : newName.ToUpperInvariant();

        Console.WriteLine($"Agent renamed: {oldName} → {displayNewName}");
        Console.WriteLine($"  Updated: dydo.json, workspace, workflow file");

        return ExitCodes.Success;
    }

    public static int ExecuteRemove(string name, bool force)
    {
        var registry = new AgentRegistry();

        if (!registry.IsValidAgentName(name))
        {
            ConsoleOutput.WriteError($"Agent '{name}' does not exist in the pool.");
            return ExitCodes.ToolError;
        }

        if (!force)
        {
            Console.Write($"Remove agent '{name}'? This will delete workspace and workflow file. [y/N] ");
            var response = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (response != "y" && response != "yes")
            {
                Console.WriteLine("Cancelled.");
                return ExitCodes.Success;
            }
        }

        if (!registry.RemoveAgent(name, out var error))
        {
            ConsoleOutput.WriteError(error);
            return ExitCodes.ToolError;
        }

        Console.WriteLine($"Agent removed: {name}");
        Console.WriteLine("  Deleted: dydo.json entry, workspace folder, workflow file");

        return ExitCodes.Success;
    }

    public static int ExecuteReassign(string name, string human)
    {
        var registry = new AgentRegistry();

        var currentHuman = registry.GetHumanForAgent(name);

        if (!registry.ReassignAgent(name, human, out var error))
        {
            ConsoleOutput.WriteError(error);
            return ExitCodes.ToolError;
        }

        Console.WriteLine($"Agent reassigned: {name}");
        Console.WriteLine($"  From: {currentHuman ?? "(unassigned)"}");
        Console.WriteLine($"  To: {human}");

        return ExitCodes.Success;
    }
}
