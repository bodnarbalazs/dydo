namespace DynaDocs.Commands;

using System.CommandLine;

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
        command.Subcommands.Add(CleanCommand.Create());

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
            return AgentLifecycleHandlers.ExecuteClaim(name);
        });

        return command;
    }

    private static Command CreateReleaseCommand()
    {
        var command = new Command("release", "Release the current agent");

        command.SetAction(_ => AgentLifecycleHandlers.ExecuteRelease());

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
            return AgentLifecycleHandlers.ExecuteStatus(name);
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
            return AgentListHandler.ExecuteList(freeOnly, all);
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
            return AgentLifecycleHandlers.ExecuteRole(role, task);
        });

        return command;
    }

    private static Command CreateTreeCommand()
    {
        var command = new Command("tree", "Show dispatch hierarchy of active agents");

        command.SetAction(_ => AgentTreeHandler.ExecuteTree());

        return command;
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
            return AgentManagementHandlers.ExecuteNew(name, human);
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
            return AgentManagementHandlers.ExecuteRename(oldName, newName);
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
            return AgentManagementHandlers.ExecuteRemove(name, force);
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
            return AgentManagementHandlers.ExecuteReassign(name, human);
        });

        return command;
    }
}
