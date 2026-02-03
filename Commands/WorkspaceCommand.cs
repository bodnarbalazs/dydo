namespace DynaDocs.Commands;

using System.CommandLine;
using DynaDocs.Services;
using DynaDocs.Utils;

public static class WorkspaceCommand
{
    public static Command Create()
    {
        var command = new Command("workspace", "Manage agent workspace");

        command.Subcommands.Add(CreateInitCommand());
        command.Subcommands.Add(CreateCheckCommand());

        return command;
    }

    private static Command CreateInitCommand()
    {
        var pathOption = new Option<string?>("--path")
        {
            Description = "Base path (defaults to current directory)"
        };

        var command = new Command("init", "Initialize agent workspaces");
        command.Options.Add(pathOption);

        command.SetAction(parseResult =>
        {
            var path = parseResult.GetValue(pathOption);
            return ExecuteInit(path);
        });

        return command;
    }

    private static Command CreateCheckCommand()
    {
        var command = new Command("check", "Verify workflow requirements before session end");

        command.SetAction(_ => ExecuteCheck());

        return command;
    }

    private static int ExecuteInit(string? basePath)
    {
        basePath ??= Environment.CurrentDirectory;
        var registry = new AgentRegistry(basePath);

        var workspacePath = registry.WorkspacePath;
        Directory.CreateDirectory(workspacePath);

        Console.WriteLine($"Initializing agent workspaces at {workspacePath}...");
        Console.WriteLine();

        var created = 0;

        // Create agent-states.md
        var statesPath = Path.Combine(workspacePath, "agent-states.md");
        if (!File.Exists(statesPath))
        {
            WriteAgentStatesFile(statesPath, registry);
            Console.WriteLine("Created agent-states.md");
            created++;
        }

        // Create each agent's workspace
        foreach (var name in registry.AgentNames)
        {
            var agentPath = registry.GetAgentWorkspace(name);
            if (Directory.Exists(agentPath))
            {
                continue;
            }

            Directory.CreateDirectory(agentPath);
            Directory.CreateDirectory(Path.Combine(agentPath, "inbox"));

            // Create workflow.md
            var workflowPath = Path.Combine(agentPath, "workflow.md");
            WriteAgentWorkflow(workflowPath, name);

            Console.WriteLine($"  Created workspace for {name}");
            created++;
        }

        Console.WriteLine();

        if (created == 0)
        {
            Console.WriteLine("All workspaces already exist.");
        }
        else
        {
            Console.WriteLine($"Created {created} workspace(s).");
        }

        Console.WriteLine();
        Console.WriteLine("Agent workspaces initialized.");
        Console.WriteLine();
        Console.WriteLine("Next steps:");
        Console.WriteLine("  1. Set environment variable: export DYDO_HUMAN=your_name");
        Console.WriteLine("  2. Claim an agent: dydo agent claim auto");
        Console.WriteLine("  3. Set a role: dydo agent role code-writer");

        return ExitCodes.Success;
    }

    private static int ExecuteCheck()
    {
        var registry = new AgentRegistry();
        var configService = new ConfigService();
        var agent = registry.GetCurrentAgent();

        if (agent == null)
        {
            Console.WriteLine("No agent identity assigned to this process. Skipping workflow check.");
            return ExitCodes.Success;
        }

        var issues = new List<string>();

        // Check if agent has an active task
        if (!string.IsNullOrEmpty(agent.Task))
        {
            // Check if task is properly handed off or completed
            var tasksPath = configService.GetTasksPath();
            var taskPath = Path.Combine(tasksPath, $"{agent.Task}.md");

            if (File.Exists(taskPath))
            {
                var content = File.ReadAllText(taskPath);
                if (content.Contains("status: active") || content.Contains("status: pending"))
                {
                    issues.Add($"Task '{agent.Task}' is still active. Mark ready-for-review or complete it.");
                }
            }
        }

        // Check for unprocessed inbox items
        var inboxPath = Path.Combine(registry.GetAgentWorkspace(agent.Name), "inbox");
        if (Directory.Exists(inboxPath))
        {
            var inboxItems = Directory.GetFiles(inboxPath, "*.md").Length;
            if (inboxItems > 0)
            {
                issues.Add($"Agent {agent.Name} has {inboxItems} unprocessed inbox item(s).");
            }
        }

        if (issues.Count > 0)
        {
            Console.WriteLine($"Workflow check for agent {agent.Name}:");
            Console.WriteLine();
            foreach (var issue in issues)
            {
                Console.WriteLine($"  ! {issue}");
            }
            Console.WriteLine();
            Console.WriteLine("Consider addressing these items before ending the session.");
            return ExitCodes.ValidationErrors;
        }

        Console.WriteLine($"Workflow check passed for agent {agent.Name}.");
        return ExitCodes.Success;
    }

    private static void WriteAgentStatesFile(string path, AgentRegistry registry)
    {
        var content = TemplateGenerator.GenerateAgentStatesMd(registry.AgentNames);
        File.WriteAllText(path, content);
    }

    private static void WriteAgentWorkflow(string path, string agentName)
    {
        var content = $"""
            # Workflow — {agentName}

            This is agent **{agentName}**'s workspace.

            ---

            ## Agent Identity

            Agent name: **{agentName}**

            Commands for this agent:

            ```bash
            dydo agent claim {agentName}
            dydo agent role code-writer --task my-task
            dydo agent release
            ```

            Workspace location: `dydo/agents/{agentName}/`

            ---

            ## Getting Started

            1. **Claim agent identity:**
               ```bash
               dydo agent claim {agentName}
               # Or use auto-claim:
               dydo agent claim auto
               ```

            2. **Set role based on task:**
               ```bash
               dydo agent role <role> --task <task-name>
               ```

            3. **Check inbox** (if dispatched work):
               ```bash
               dydo inbox show
               ```

            ---

            ## Workspace Structure

            ```
            dydo/agents/{agentName}/
            ├── workflow.md      # This file
            ├── state.md         # Current state (managed by dydo)
            ├── .session         # Session info (managed by dydo)
            ├── inbox/           # Messages from other agents
            ├── plan.md          # Planning notes (optional)
            └── notes.md         # Scratch space (optional)
            ```

            ---

            ## Role Permissions

            The current role determines file access:

            | Role | Can Edit | Cannot Edit |
            |------|----------|-------------|
            | `code-writer` | `src/**`, `tests/**` | `dydo/**`, `project/**` |
            | `reviewer` | (read-only) | (all files) |
            | `co-thinker` | `dydo/agents/{agentName}/**`, `dydo/project/decisions/**` | `src/**`, `tests/**` |
            | `docs-writer` | `dydo/**` | `dydo/agents/**`, `src/**`, `tests/**` |
            | `interviewer` | `dydo/agents/{agentName}/**` | Everything else |
            | `planner` | `dydo/agents/{agentName}/**`, `dydo/project/tasks/**` | `src/**` |

            The guard command enforces these permissions. If blocked, change role or dispatch to another agent.

            ---

            ## Completing Work

            1. Mark task ready for review (if applicable):
               ```bash
               dydo task ready-for-review my-task --summary "Implemented X. Tests pass."
               ```

            2. Dispatch if handoff needed:
               ```bash
               dydo dispatch --role reviewer --task my-task --brief "..."
               ```

            3. Release agent identity:
               ```bash
               dydo agent release
               ```

            ---

            *Agent {agentName} workspace. Managed by dydo.*
            """;

        File.WriteAllText(path, content);
    }
}
