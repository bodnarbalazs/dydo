namespace DynaDocs.Commands;

using System.CommandLine;
using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

public static class GuardLiftCommand
{
    public static Command CreateLiftCommand()
    {
        var agentArg = new Argument<string>("agent")
        {
            Description = "Agent name to lift guard for"
        };

        var minutesArg = new Argument<int?>("minutes")
        {
            DefaultValueFactory = _ => null,
            Description = "Optional time limit in minutes"
        };

        var command = new Command("lift", "Temporarily lift RBAC restrictions for an agent");
        command.Arguments.Add(agentArg);
        command.Arguments.Add(minutesArg);

        command.SetAction(parseResult =>
        {
            var agent = parseResult.GetValue(agentArg)!;
            var minutes = parseResult.GetValue(minutesArg);
            return ExecuteLift(agent, minutes);
        });

        return command;
    }

    public static Command CreateRestoreCommand()
    {
        var agentArg = new Argument<string>("agent")
        {
            Description = "Agent name to restore guard for"
        };

        var command = new Command("restore", "Restore RBAC restrictions for an agent");
        command.Arguments.Add(agentArg);

        command.SetAction(parseResult =>
        {
            var agent = parseResult.GetValue(agentArg)!;
            return ExecuteRestore(agent);
        });

        return command;
    }

    private static int ExecuteLift(string agentName, int? minutes)
    {
        if (minutes.HasValue && minutes.Value <= 0)
        {
            Console.Error.WriteLine("Error: Minutes must be a positive number.");
            return ExitCodes.ToolError;
        }

        var registry = new AgentRegistry();

        if (!registry.IsValidAgentName(agentName))
        {
            Console.Error.WriteLine($"Error: Agent '{agentName}' does not exist.");
            return ExitCodes.ToolError;
        }

        var state = registry.GetAgentState(agentName);
        if (state == null || state.Status != AgentStatus.Working)
        {
            Console.Error.WriteLine($"Error: Agent '{agentName}' is not currently claimed.");
            return ExitCodes.ToolError;
        }

        var human = Environment.GetEnvironmentVariable("DYDO_HUMAN");
        if (string.IsNullOrEmpty(human))
        {
            Console.Error.WriteLine("Error: DYDO_HUMAN environment variable not set.");
            return ExitCodes.ToolError;
        }

        var liftService = new GuardLiftService();
        liftService.Lift(agentName, human, minutes);

        if (minutes.HasValue)
            Console.WriteLine($"Guard lifted for {agentName}. RBAC restrictions suspended for {minutes} minutes.");
        else
            Console.WriteLine($"Guard lifted for {agentName}. RBAC restrictions suspended.");

        return ExitCodes.Success;
    }

    private static int ExecuteRestore(string agentName)
    {
        var liftService = new GuardLiftService();
        if (!liftService.IsLifted(agentName))
        {
            Console.WriteLine($"No active lift for {agentName}.");
            return ExitCodes.Success;
        }

        liftService.Restore(agentName);

        Console.WriteLine($"Guard restored for {agentName}. RBAC restrictions resumed.");
        return ExitCodes.Success;
    }
}
