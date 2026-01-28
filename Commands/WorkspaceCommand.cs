namespace DynaDocs.Commands;

using System.CommandLine;
using System.CommandLine.Invocation;
using DynaDocs.Services;
using DynaDocs.Utils;

public static class WorkspaceCommand
{
    public static Command Create()
    {
        var command = new Command("workspace", "Manage agent workspace");

        command.AddCommand(CreateInitCommand());
        command.AddCommand(CreateCheckCommand());

        return command;
    }

    private static Command CreateInitCommand()
    {
        var pathOption = new Option<string?>("--path", "Base path (defaults to current directory)");

        var command = new Command("init", "Initialize agent workspaces for all 26 agents")
        {
            pathOption
        };

        command.SetHandler((InvocationContext ctx) =>
        {
            var path = ctx.ParseResult.GetValueForOption(pathOption);
            ctx.ExitCode = ExecuteInit(path);
        });

        return command;
    }

    private static Command CreateCheckCommand()
    {
        var command = new Command("check", "Verify workflow requirements before session end");

        command.SetHandler((InvocationContext ctx) =>
        {
            ctx.ExitCode = ExecuteCheck();
        });

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
        Console.WriteLine("Agents are ready. Start a workflow with:");
        Console.WriteLine("  claude --feature A    (full workflow as Adele)");
        Console.WriteLine("  claude --task B       (standard workflow as Brian)");
        Console.WriteLine("  claude --quick C      (quick task as Charlie)");

        return ExitCodes.Success;
    }

    private static int ExecuteCheck()
    {
        var registry = new AgentRegistry();
        var agent = registry.GetCurrentAgent();

        if (agent == null)
        {
            Console.WriteLine("No agent claimed - skipping workflow check");
            return ExitCodes.Success;
        }

        var issues = new List<string>();

        // Check if agent has an active task
        if (!string.IsNullOrEmpty(agent.Task))
        {
            // Check if task is properly handed off or completed
            var tasksPath = Path.Combine(Environment.CurrentDirectory, "project", "tasks");
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

        // Check for uncommitted inbox items
        var inboxPath = Path.Combine(registry.GetAgentWorkspace(agent.Name), "inbox");
        if (Directory.Exists(inboxPath))
        {
            var inboxItems = Directory.GetFiles(inboxPath, "*.md").Length;
            if (inboxItems > 0)
            {
                issues.Add($"You have {inboxItems} unprocessed inbox item(s).");
            }
        }

        if (issues.Count > 0)
        {
            Console.WriteLine($"Workflow check for {agent.Name}:");
            Console.WriteLine();
            foreach (var issue in issues)
            {
                Console.WriteLine($"  ! {issue}");
            }
            Console.WriteLine();
            Console.WriteLine("Consider addressing these before ending session.");
            return ExitCodes.ValidationErrors;
        }

        Console.WriteLine($"Workflow check passed for {agent.Name}");
        return ExitCodes.Success;
    }

    private static void WriteAgentStatesFile(string path, AgentRegistry registry)
    {
        var rows = string.Join("\n", registry.AgentNames.Select(name =>
            $"| {name} | free | - | - | - |"));

        var content = $"""
            ---
            last-updated: {DateTime.UtcNow:o}
            ---

            # Agent States

            | Agent | Status | Role | Task | Since |
            |-------|--------|------|------|-------|
            {rows}

            ## Pending Inbox

            | Agent | Items | Oldest |
            |-------|-------|--------|
            | (none) | - | - |

            ---

            <!--
            This file is updated by dydo commands.
            Check agent status: dydo agent list
            -->
            """;

        File.WriteAllText(path, content);
    }

    private static void WriteAgentWorkflow(string path, string agentName)
    {
        var content = $"""
            # Workflow — {agentName}

            You are **{agentName}**. This is your workspace.

            ---

            ## Your Identity

            Your name is **{agentName}**. Use this name in all dydo commands:

            ```bash
            dydo agent claim {agentName}
            dydo agent role code-writer --task my-task
            dydo agent release
            ```

            Your workspace is `.workspace/{agentName}/`.

            ---

            ## First Steps

            1. **Claim your identity:**
               ```bash
               dydo agent claim {agentName}
               ```

            2. **Set your role based on your task:**
               ```bash
               dydo agent role <role> --task <task-name>
               ```

            3. **Check your inbox** (if started with `--inbox`):
               ```bash
               dydo inbox show
               ```

            ---

            ## Your Workspace

            ```
            .workspace/{agentName}/
            ├── workflow.md      # This file
            ├── state.md         # Your current state (managed by dydo)
            ├── .session         # Session info (managed by dydo)
            ├── inbox/           # Messages from other agents
            ├── plan.md          # Your current plan (optional)
            └── notes.md         # Scratch space (optional)
            ```

            Use `plan.md` and `notes.md` freely for your work.

            ---

            ## Role Permissions

            Your current role determines what you can edit:

            | Role | Can Edit | Cannot Edit |
            |------|----------|-------------|
            | `code-writer` | `src/**`, `tests/**` | `docs/**`, `project/**` |
            | `reviewer` | (nothing) | (everything) |
            | `docs-writer` | `docs/**` | `src/**`, `tests/**` |
            | `interviewer` | `.workspace/{agentName}/**` | Everything else |
            | `planner` | `.workspace/{agentName}/**`, `project/tasks/**` | `src/**`, `docs/**` |

            The guard enforces this. If blocked, change your role or dispatch to another agent.

            ---

            ## When You're Done

            1. Mark task ready for review (if applicable):
               ```bash
               dydo task ready-for-review my-task --summary "Implemented X. Tests pass."
               ```

            2. Dispatch if handoff needed:
               ```bash
               dydo dispatch --role reviewer --task my-task --brief "..."
               ```

            3. Release your claim:
               ```bash
               dydo agent release
               ```

            ---

            *Remember: You are {agentName}. Work in your workspace. Respect others.*
            """;

        File.WriteAllText(path, content);
    }
}
