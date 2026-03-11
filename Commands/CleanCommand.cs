namespace DynaDocs.Commands;

using System.CommandLine;
using DynaDocs.Services;

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
            return WorkspaceCleaner.Execute(agent, all, force, task);
        });

        return command;
    }
}
