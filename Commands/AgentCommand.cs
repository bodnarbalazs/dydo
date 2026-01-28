namespace DynaDocs.Commands;

using System.CommandLine;
using System.CommandLine.Invocation;
using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

public static class AgentCommand
{
    public static Command Create()
    {
        var command = new Command("agent", "Manage agent identity and roles");

        command.AddCommand(CreateClaimCommand());
        command.AddCommand(CreateReleaseCommand());
        command.AddCommand(CreateStatusCommand());
        command.AddCommand(CreateListCommand());
        command.AddCommand(CreateRoleCommand());

        return command;
    }

    private static Command CreateClaimCommand()
    {
        var nameArgument = new Argument<string>("name", "Agent name or letter (e.g., 'Adele' or 'A')");

        var command = new Command("claim", "Claim an agent for this terminal")
        {
            nameArgument
        };

        command.SetHandler((InvocationContext ctx) =>
        {
            var name = ctx.ParseResult.GetValueForArgument(nameArgument);
            ctx.ExitCode = ExecuteClaim(name);
        });

        return command;
    }

    private static Command CreateReleaseCommand()
    {
        var command = new Command("release", "Release the current agent");

        command.SetHandler((InvocationContext ctx) =>
        {
            ctx.ExitCode = ExecuteRelease();
        });

        return command;
    }

    private static Command CreateStatusCommand()
    {
        var nameArgument = new Argument<string?>("name", () => null, "Agent name (optional, defaults to current)");

        var command = new Command("status", "Show agent status")
        {
            nameArgument
        };

        command.SetHandler((InvocationContext ctx) =>
        {
            var name = ctx.ParseResult.GetValueForArgument(nameArgument);
            ctx.ExitCode = ExecuteStatus(name);
        });

        return command;
    }

    private static Command CreateListCommand()
    {
        var freeOption = new Option<bool>("--free", "Show only free agents");

        var command = new Command("list", "List all agents")
        {
            freeOption
        };

        command.SetHandler((InvocationContext ctx) =>
        {
            var freeOnly = ctx.ParseResult.GetValueForOption(freeOption);
            ctx.ExitCode = ExecuteList(freeOnly);
        });

        return command;
    }

    private static Command CreateRoleCommand()
    {
        var roleArgument = new Argument<string>("role", "Role to set (code-writer, reviewer, docs-writer, interviewer, planner)");
        var taskOption = new Option<string?>("--task", "Task name to associate with this role");

        var command = new Command("role", "Set the current agent's role")
        {
            roleArgument,
            taskOption
        };

        command.SetHandler((InvocationContext ctx) =>
        {
            var role = ctx.ParseResult.GetValueForArgument(roleArgument);
            var task = ctx.ParseResult.GetValueForOption(taskOption);
            ctx.ExitCode = ExecuteRole(role, task);
        });

        return command;
    }

    private static int ExecuteClaim(string nameOrLetter)
    {
        var registry = new AgentRegistry();

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

        Console.WriteLine($"Claimed agent {name}");
        Console.WriteLine($"Workspace: {registry.GetAgentWorkspace(name)}");

        var (terminalPid, claudePid) = ProcessUtils.GetProcessAncestors();
        Console.WriteLine($"Terminal PID: {terminalPid}, Claude PID: {claudePid}");

        return ExitCodes.Success;
    }

    private static int ExecuteRelease()
    {
        var registry = new AgentRegistry();

        var current = registry.GetCurrentAgent();
        if (current == null)
        {
            ConsoleOutput.WriteError("No agent claimed for this terminal");
            return ExitCodes.ToolError;
        }

        if (!registry.ReleaseAgent(out var error))
        {
            ConsoleOutput.WriteError(error);
            return ExitCodes.ToolError;
        }

        Console.WriteLine($"Released agent {current.Name}");
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
                ConsoleOutput.WriteError("No agent claimed for this terminal");
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
        Console.WriteLine($"Status: {state.Status}");
        Console.WriteLine($"Role: {state.Role ?? "(none)"}");
        Console.WriteLine($"Task: {state.Task ?? "(none)"}");

        if (state.Since.HasValue)
            Console.WriteLine($"Since: {state.Since.Value:yyyy-MM-dd HH:mm:ss}");

        if (state.AllowedPaths.Count > 0)
            Console.WriteLine($"Allowed: {string.Join(", ", state.AllowedPaths)}");

        if (state.DeniedPaths.Count > 0)
            Console.WriteLine($"Denied: {string.Join(", ", state.DeniedPaths)}");

        var session = registry.GetSession(state.Name);
        if (session != null)
        {
            Console.WriteLine();
            Console.WriteLine($"Terminal PID: {session.TerminalPid}");
            Console.WriteLine($"Claude PID: {session.ClaudePid}");
            Console.WriteLine($"Claimed: {session.Claimed:yyyy-MM-dd HH:mm:ss}");
        }

        return ExitCodes.Success;
    }

    private static int ExecuteList(bool freeOnly)
    {
        var registry = new AgentRegistry();

        var agents = freeOnly ? registry.GetFreeAgents() : registry.GetAllAgentStates();

        if (agents.Count == 0)
        {
            Console.WriteLine(freeOnly ? "No free agents" : "No agents found");
            return ExitCodes.Success;
        }

        Console.WriteLine($"{"Agent",-10} {"Status",-10} {"Role",-15} {"Task",-20}");
        Console.WriteLine(new string('-', 60));

        foreach (var agent in agents)
        {
            var status = agent.Status.ToString().ToLowerInvariant();
            var role = agent.Role ?? "-";
            var task = agent.Task ?? "-";

            if (task.Length > 18)
                task = task[..18] + "..";

            Console.WriteLine($"{agent.Name,-10} {status,-10} {role,-15} {task,-20}");
        }

        if (!freeOnly)
        {
            var freeCount = agents.Count(a => a.Status == AgentStatus.Free);
            var workingCount = agents.Count(a => a.Status == AgentStatus.Working);
            Console.WriteLine();
            Console.WriteLine($"{freeCount} free, {workingCount} working");
        }

        return ExitCodes.Success;
    }

    private static int ExecuteRole(string role, string? task)
    {
        var registry = new AgentRegistry();

        var current = registry.GetCurrentAgent();
        if (current == null)
        {
            ConsoleOutput.WriteError("No agent claimed for this terminal. Run 'dydo agent claim <name>' first.");
            return ExitCodes.ToolError;
        }

        if (!registry.SetRole(role, task, out var error))
        {
            ConsoleOutput.WriteError(error);
            return ExitCodes.ToolError;
        }

        Console.WriteLine($"Agent {current.Name} role set to: {role}");
        if (!string.IsNullOrEmpty(task))
            Console.WriteLine($"Task: {task}");

        // Show permissions
        var state = registry.GetAgentState(current.Name);
        if (state != null)
        {
            Console.WriteLine($"Allowed paths: {string.Join(", ", state.AllowedPaths)}");
            if (state.DeniedPaths.Count > 0 && state.DeniedPaths[0] != "**")
                Console.WriteLine($"Denied paths: {string.Join(", ", state.DeniedPaths)}");
        }

        return ExitCodes.Success;
    }
}
