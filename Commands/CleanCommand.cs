namespace DynaDocs.Commands;

using System.CommandLine;
using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

public static class CleanCommand
{
    public static Command Create()
    {
        var agentArgument = new Argument<string?>("agent")
        {
            DefaultValueFactory = _ => null,
            Description = "Agent name or letter to clean"
        };

        var allOption = new Option<bool>("--all")
        {
            Description = "Clean all agent workspaces"
        };

        var forceOption = new Option<bool>("--force")
        {
            Description = "Force clean even if agents are working"
        };

        var taskOption = new Option<string?>("--task")
        {
            Description = "Clean workspaces associated with a task"
        };

        var command = new Command("clean", "Clean agent workspaces");
        command.Arguments.Add(agentArgument);
        command.Options.Add(allOption);
        command.Options.Add(forceOption);
        command.Options.Add(taskOption);

        command.SetAction(parseResult =>
        {
            var agent = parseResult.GetValue(agentArgument);
            var all = parseResult.GetValue(allOption);
            var force = parseResult.GetValue(forceOption);
            var task = parseResult.GetValue(taskOption);
            return Execute(agent, all, force, task);
        });

        return command;
    }

    private static int Execute(string? agentNameOrLetter, bool all, bool force, string? task)
    {
        var registry = new AgentRegistry();

        if (!string.IsNullOrEmpty(task))
        {
            return CleanByTask(registry, task, force);
        }

        if (all)
        {
            return CleanAll(registry, force);
        }

        if (!string.IsNullOrEmpty(agentNameOrLetter))
        {
            return CleanAgent(registry, agentNameOrLetter, force);
        }

        ConsoleOutput.WriteError("Specify an agent name, --all, or --task <name>");
        return ExitCodes.ToolError;
    }

    private static int CleanAgent(AgentRegistry registry, string nameOrLetter, bool force)
    {
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

        if (!registry.IsValidAgentName(name))
        {
            ConsoleOutput.WriteError($"Unknown agent: {name}");
            return ExitCodes.ToolError;
        }

        var state = registry.GetAgentState(name);
        if (state != null && state.Status != AgentStatus.Free && !force)
        {
            ConsoleOutput.WriteError($"Agent {name} is currently {state.Status}. Use --force to clean anyway.");
            return ExitCodes.ToolError;
        }

        var workspace = registry.GetAgentWorkspace(name);
        if (!Directory.Exists(workspace))
        {
            Console.WriteLine($"Workspace for {name} is already clean");
            return ExitCodes.Success;
        }

        CleanWorkspace(workspace);
        Console.WriteLine($"Cleaned workspace for {name}");

        return ExitCodes.Success;
    }

    private static int CleanAll(AgentRegistry registry, bool force)
    {
        if (!force)
        {
            // Check for any working agents
            var workingAgents = registry.GetAllAgentStates()
                .Where(a => a.Status != AgentStatus.Free)
                .ToList();

            if (workingAgents.Count > 0)
            {
                ConsoleOutput.WriteError($"Cannot clean: {workingAgents.Count} agent(s) are working:");
                foreach (var agent in workingAgents)
                {
                    Console.WriteLine($"  - {agent.Name}: {agent.Status} ({agent.Task ?? "no task"})");
                }
                Console.WriteLine("Use --force to clean anyway.");
                return ExitCodes.ToolError;
            }
        }

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
        return ExitCodes.Success;
    }

    private static int CleanByTask(AgentRegistry registry, string taskName, bool force)
    {
        var cleaned = 0;

        foreach (var name in registry.AgentNames)
        {
            var state = registry.GetAgentState(name);
            if (state == null) continue;

            // Clean if agent's task matches
            if (state.Task != null && state.Task.Contains(taskName, StringComparison.OrdinalIgnoreCase))
            {
                if (state.Status != AgentStatus.Free && !force)
                {
                    Console.WriteLine($"Skipping {name} (currently {state.Status}). Use --force to include.");
                    continue;
                }

                var workspace = registry.GetAgentWorkspace(name);
                if (Directory.Exists(workspace))
                {
                    CleanWorkspace(workspace);
                    Console.WriteLine($"Cleaned {name} (was working on {state.Task})");
                    cleaned++;
                }
            }
        }

        if (cleaned == 0)
        {
            Console.WriteLine($"No workspaces found for task: {taskName}");
        }
        else
        {
            Console.WriteLine($"Cleaned {cleaned} workspace(s) for task: {taskName}");
        }

        return ExitCodes.Success;
    }

    private static void CleanWorkspace(string workspace)
    {
        // Clean specific files/folders, preserve the directory structure
        var filesToDelete = new[] { "state.md", ".session", "plan.md", "notes.md" };

        foreach (var file in filesToDelete)
        {
            var path = Path.Combine(workspace, file);
            if (File.Exists(path))
                File.Delete(path);
        }

        // Clean inbox
        var inboxPath = Path.Combine(workspace, "inbox");
        if (Directory.Exists(inboxPath))
        {
            foreach (var file in Directory.GetFiles(inboxPath))
            {
                File.Delete(file);
            }
        }
    }
}
