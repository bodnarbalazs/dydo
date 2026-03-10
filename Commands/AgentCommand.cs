namespace DynaDocs.Commands;

using System.CommandLine;
using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

public static class AgentCommand
{
    public static Command Create()
    {
        var command = new Command("agent", "Manage agent identity and roles");

        command.Subcommands.Add(CreateClaimCommand());
        command.Subcommands.Add(CreateReleaseCommand());
        command.Subcommands.Add(CreateStatusCommand());
        command.Subcommands.Add(CreateListCommand());
        command.Subcommands.Add(CreateRoleCommand());
        command.Subcommands.Add(CreateNewCommand());
        command.Subcommands.Add(CreateRenameCommand());
        command.Subcommands.Add(CreateRemoveCommand());
        command.Subcommands.Add(CreateReassignCommand());
        command.Subcommands.Add(CreateTreeCommand());

        return command;
    }

    private static Command CreateClaimCommand()
    {
        var nameArgument = new Argument<string>("name")
        {
            Description = "Agent name or letter (e.g., 'Adele' or 'A')"
        };

        var command = new Command("claim", "Claim an agent for this terminal");
        command.Arguments.Add(nameArgument);

        command.SetAction(parseResult =>
        {
            var name = parseResult.GetValue(nameArgument)!;
            return ExecuteClaim(name);
        });

        return command;
    }

    private static Command CreateReleaseCommand()
    {
        var command = new Command("release", "Release the current agent");

        command.SetAction(_ => ExecuteRelease());

        return command;
    }

    private static Command CreateStatusCommand()
    {
        var nameArgument = new Argument<string?>("name")
        {
            DefaultValueFactory = _ => null,
            Description = "Agent name (optional, defaults to current)"
        };

        var command = new Command("status", "Show agent status");
        command.Arguments.Add(nameArgument);

        command.SetAction(parseResult =>
        {
            var name = parseResult.GetValue(nameArgument);
            return ExecuteStatus(name);
        });

        return command;
    }

    private static Command CreateListCommand()
    {
        var freeOption = new Option<bool>("--free")
        {
            Description = "Show only free agents"
        };

        var allOption = new Option<bool>("--all")
        {
            Description = "Show all agents across all humans"
        };

        var command = new Command("list", "List agents");
        command.Options.Add(freeOption);
        command.Options.Add(allOption);

        command.SetAction(parseResult =>
        {
            var freeOnly = parseResult.GetValue(freeOption);
            var all = parseResult.GetValue(allOption);
            return ExecuteList(freeOnly, all);
        });

        return command;
    }

    private static Command CreateRoleCommand()
    {
        var roleArgument = new Argument<string>("role")
        {
            Description = "Role to set (code-writer, reviewer, co-thinker, docs-writer, planner, test-writer)"
        };

        var taskOption = new Option<string?>("--task")
        {
            Description = "Task name to associate with this role"
        };

        var command = new Command("role", "Set the current agent's role");
        command.Arguments.Add(roleArgument);
        command.Options.Add(taskOption);

        command.SetAction(parseResult =>
        {
            var role = parseResult.GetValue(roleArgument)!;
            var task = parseResult.GetValue(taskOption);
            return ExecuteRole(role, task);
        });

        return command;
    }

    private static int ExecuteClaim(string nameOrLetter)
    {
        var registry = new AgentRegistry();

        // Handle "auto" - claim first available agent for the current human
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

        // Resolve letter to name if needed
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

    private static int ExecuteRelease()
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

        // Check for auto-close marker
        var workspace = registry.GetAgentWorkspace(current.Name);
        var autoCloseMarker = Path.Combine(workspace, ".auto-close");
        if (File.Exists(autoCloseMarker))
        {
            File.Delete(autoCloseMarker);
            Console.WriteLine("  Auto-close: session will close shortly.");
            TerminalCloser.ScheduleClaudeTermination();
        }

        return ExitCodes.Success;
    }

    private static int ExecuteStatus(string? name)
    {
        var registry = new AgentRegistry();
        var sessionId = registry.GetSessionContext();

        AgentState? state;
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

    private static int ExecuteList(bool freeOnly, bool all)
    {
        var registry = new AgentRegistry();
        var human = registry.GetCurrentHuman();

        if (all)
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

        // Default: show only current human's agents with Task column
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

        var freeCount2 = filteredAgents.Count(a => a.Status == AgentStatus.Free);
        var dispatchedCount2 = filteredAgents.Count(a => a.Status == AgentStatus.Dispatched);
        var workingCount2 = filteredAgents.Count(a => a.Status == AgentStatus.Working);
        Console.WriteLine();
        Console.WriteLine($"Total: {filteredAgents.Count} agents ({freeCount2} free, {dispatchedCount2} dispatched, {workingCount2} working)");

        return ExitCodes.Success;
    }

    private static int ExecuteRole(string role, string? task)
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

        // Show permissions
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

    private static Command CreateTreeCommand()
    {
        var command = new Command("tree", "Show dispatch hierarchy of active agents");

        command.SetAction(_ => ExecuteTree());

        return command;
    }

    internal static int ExecuteTree()
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

        // Build parent->children map
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

        // Sort children alphabetically within each parent
        foreach (var list in children.Values)
            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        roots.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        // Collect all wait markers once
        var waitMarkers = new Dictionary<string, List<Models.WaitMarker>>(StringComparer.OrdinalIgnoreCase);
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
        Dictionary<string, List<Models.WaitMarker>> waitMarkers)
    {
        // Line 1: tree prefix + agent name
        if (isRoot)
        {
            Console.WriteLine($"{new string(' ', nameStartCol)}{agent.Name}");
        }
        else
        {
            Console.WriteLine($"{treePrefix}{agent.Name}");
        }

        // Line 2: centered role + dashes + task
        var role = agent.Role ?? "unknown";
        var roleText = $"[{role}]";
        var task = agent.Task ?? "-";

        // Build wait annotation
        var waitText = "";
        if (waitMarkers.TryGetValue(agent.Name, out var markers))
        {
            var targets = string.Join(", ", markers.Select(m => m.Target));
            waitText = $" waiting \u2192 {targets}";
        }

        var roleInfo = $"{roleText}{waitText} ------ {task}";

        // Center role text under the agent name
        var roleStartCol = nameStartCol + (agent.Name.Length / 2) - (roleInfo.Length / 2);
        if (roleStartCol < 0) roleStartCol = 0;

        Console.WriteLine($"{new string(' ', roleStartCol)}{roleInfo}");

        // Render children
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

    private static Command CreateNewCommand()
    {
        var nameArgument = new Argument<string>("name")
        {
            Description = "New agent name (e.g., 'William')"
        };

        var humanArgument = new Argument<string>("human")
        {
            Description = "Human to assign the agent to"
        };

        var command = new Command("new", "Create a new agent and assign to a human");
        command.Arguments.Add(nameArgument);
        command.Arguments.Add(humanArgument);

        command.SetAction(parseResult =>
        {
            var name = parseResult.GetValue(nameArgument)!;
            var human = parseResult.GetValue(humanArgument)!;
            return ExecuteNew(name, human);
        });

        return command;
    }

    private static Command CreateRenameCommand()
    {
        var oldNameArgument = new Argument<string>("old-name")
        {
            Description = "Current agent name"
        };

        var newNameArgument = new Argument<string>("new-name")
        {
            Description = "New agent name"
        };

        var command = new Command("rename", "Rename an agent");
        command.Arguments.Add(oldNameArgument);
        command.Arguments.Add(newNameArgument);

        command.SetAction(parseResult =>
        {
            var oldName = parseResult.GetValue(oldNameArgument)!;
            var newName = parseResult.GetValue(newNameArgument)!;
            return ExecuteRename(oldName, newName);
        });

        return command;
    }

    private static Command CreateRemoveCommand()
    {
        var nameArgument = new Argument<string>("name")
        {
            Description = "Agent name to remove"
        };

        var forceOption = new Option<bool>("--force")
        {
            Description = "Skip confirmation"
        };

        var command = new Command("remove", "Remove an agent from the pool");
        command.Arguments.Add(nameArgument);
        command.Options.Add(forceOption);

        command.SetAction(parseResult =>
        {
            var name = parseResult.GetValue(nameArgument)!;
            var force = parseResult.GetValue(forceOption);
            return ExecuteRemove(name, force);
        });

        return command;
    }

    private static Command CreateReassignCommand()
    {
        var nameArgument = new Argument<string>("name")
        {
            Description = "Agent name to reassign"
        };

        var humanArgument = new Argument<string>("human")
        {
            Description = "New human to assign the agent to"
        };

        var command = new Command("reassign", "Reassign an agent to a different human");
        command.Arguments.Add(nameArgument);
        command.Arguments.Add(humanArgument);

        command.SetAction(parseResult =>
        {
            var name = parseResult.GetValue(nameArgument)!;
            var human = parseResult.GetValue(humanArgument)!;
            return ExecuteReassign(name, human);
        });

        return command;
    }

    private static int ExecuteNew(string name, string human)
    {
        var registry = new AgentRegistry();

        if (!registry.CreateAgent(name, human, out var error))
        {
            ConsoleOutput.WriteError(error);
            return ExitCodes.ToolError;
        }

        // Normalize name for display (PascalCase)
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

    private static int ExecuteRename(string oldName, string newName)
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

    private static int ExecuteRemove(string name, bool force)
    {
        var registry = new AgentRegistry();

        // Check if agent exists first
        if (!registry.IsValidAgentName(name))
        {
            ConsoleOutput.WriteError($"Agent '{name}' does not exist in the pool.");
            return ExitCodes.ToolError;
        }

        // Confirm unless --force
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

    private static int ExecuteReassign(string name, string human)
    {
        var registry = new AgentRegistry();

        // Get current human for display
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
