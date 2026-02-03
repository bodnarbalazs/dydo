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

        var command = new Command("list", "List all agents");
        command.Options.Add(freeOption);

        command.SetAction(parseResult =>
        {
            var freeOnly = parseResult.GetValue(freeOption);
            return ExecuteList(freeOnly);
        });

        return command;
    }

    private static Command CreateRoleCommand()
    {
        var roleArgument = new Argument<string>("role")
        {
            Description = "Role to set (code-writer, reviewer, co-thinker, docs-writer, interviewer, planner, tester)"
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

        var current = registry.GetCurrentAgent();
        if (current == null)
        {
            ConsoleOutput.WriteError("No agent identity assigned to this process.");
            return ExitCodes.ToolError;
        }

        if (!registry.ReleaseAgent(out var error))
        {
            ConsoleOutput.WriteError(error);
            return ExitCodes.ToolError;
        }

        Console.WriteLine($"Agent identity released: {current.Name}");
        Console.WriteLine("  Status: free");
        return ExitCodes.Success;
    }

    private static int ExecuteStatus(string? name)
    {
        var registry = new AgentRegistry();

        AgentState? state;
        if (string.IsNullOrEmpty(name))
        {
            state = registry.GetCurrentAgent();
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
            Console.WriteLine($"  Task: {state.Task}");

        if (state.Since.HasValue)
            Console.WriteLine($"  Since: {state.Since.Value:yyyy-MM-dd HH:mm:ss} UTC");

        if (state.AllowedPaths.Count > 0)
            Console.WriteLine($"  Allowed paths: {string.Join(", ", state.AllowedPaths)}");

        if (state.DeniedPaths.Count > 0 && state.DeniedPaths[0] != "**")
            Console.WriteLine($"  Denied paths: {string.Join(", ", state.DeniedPaths)}");

        var session = registry.GetSession(state.Name);
        if (session != null)
        {
            Console.WriteLine();
            Console.WriteLine("Session:");
            Console.WriteLine($"  Terminal PID: {session.TerminalPid}");
            Console.WriteLine($"  Claude PID: {session.ClaudePid}");
            Console.WriteLine($"  Claimed: {session.Claimed:yyyy-MM-dd HH:mm:ss} UTC");
        }

        return ExitCodes.Success;
    }

    private static int ExecuteList(bool freeOnly)
    {
        var registry = new AgentRegistry();
        var human = registry.GetCurrentHuman();

        var agents = freeOnly ? registry.GetFreeAgents() : registry.GetAllAgentStates();

        if (agents.Count == 0)
        {
            Console.WriteLine(freeOnly ? "No free agents in pool." : "No agents found in pool.");
            return ExitCodes.Success;
        }

        Console.WriteLine($"{"Agent",-10} {"Status",-10} {"Human",-12} {"Role",-15}");
        Console.WriteLine(new string('-', 52));

        foreach (var agent in agents)
        {
            var status = agent.Status.ToString().ToLowerInvariant();
            var assignedHuman = agent.AssignedHuman ?? registry.GetHumanForAgent(agent.Name) ?? "-";
            var role = agent.Role ?? "-";

            Console.WriteLine($"{agent.Name,-10} {status,-10} {assignedHuman,-12} {role,-15}");
        }

        var freeCount = agents.Count(a => a.Status == AgentStatus.Free);
        var workingCount = agents.Count(a => a.Status == AgentStatus.Working);
        Console.WriteLine();
        Console.WriteLine($"Total: {agents.Count} agents ({freeCount} free, {workingCount} working)");

        if (!string.IsNullOrEmpty(human))
        {
            var humanAgents = registry.GetAgentsForHuman(human);
            var humanFree = registry.GetFreeAgentsForHuman(human);
            Console.WriteLine($"Agents assigned to human '{human}': {humanAgents.Count} ({humanFree.Count} free)");
        }

        return ExitCodes.Success;
    }

    private static int ExecuteRole(string role, string? task)
    {
        var registry = new AgentRegistry();

        var current = registry.GetCurrentAgent();
        if (current == null)
        {
            ConsoleOutput.WriteError("No agent identity assigned to this process. Run 'dydo agent claim auto' first.");
            return ExitCodes.ToolError;
        }

        if (!registry.SetRole(role, task, out var error))
        {
            ConsoleOutput.WriteError(error);
            return ExitCodes.ToolError;
        }

        Console.WriteLine($"Agent {current.Name} role updated.");
        Console.WriteLine($"  Role: {role}");

        if (!string.IsNullOrEmpty(task))
            Console.WriteLine($"  Task: {task}");

        // Show permissions
        var state = registry.GetAgentState(current.Name);
        if (state != null)
        {
            Console.WriteLine($"  Allowed paths: {string.Join(", ", state.AllowedPaths)}");
            if (state.DeniedPaths.Count > 0 && state.DeniedPaths[0] != "**")
                Console.WriteLine($"  Denied paths: {string.Join(", ", state.DeniedPaths)}");
            else if (state.AllowedPaths.Count == 0)
                Console.WriteLine("  Note: This role has no write permissions.");
        }

        return ExitCodes.Success;
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
