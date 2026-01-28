namespace DynaDocs.Commands;

using System.CommandLine;
using System.CommandLine.Invocation;
using DynaDocs.Services;
using DynaDocs.Utils;

public static class GuardCommand
{
    public static Command Create()
    {
        var actionOption = new Option<string>("--action", "Action being attempted (edit, write, delete)")
        {
            IsRequired = true
        };

        var pathOption = new Option<string>("--path", "Path being accessed")
        {
            IsRequired = true
        };

        var command = new Command("guard", "Check if current agent can perform action (used by hooks)")
        {
            actionOption,
            pathOption
        };

        command.SetHandler((InvocationContext ctx) =>
        {
            var action = ctx.ParseResult.GetValueForOption(actionOption)!;
            var path = ctx.ParseResult.GetValueForOption(pathOption)!;
            ctx.ExitCode = Execute(action, path);
        });

        return command;
    }

    private static int Execute(string action, string path)
    {
        var registry = new AgentRegistry();

        var agent = registry.GetCurrentAgent();
        if (agent == null)
        {
            // No agent claimed - could be running outside workflow
            // In strict mode, this would be an error
            // For now, allow but warn
            Console.WriteLine("WARN: No agent claimed for this terminal");
            return ExitCodes.Success;
        }

        if (string.IsNullOrEmpty(agent.Role))
        {
            Console.WriteLine($"WARN: Agent {agent.Name} has no role set");
            return ExitCodes.Success;
        }

        if (!registry.IsPathAllowed(path, action, out var error))
        {
            Console.WriteLine($"BLOCKED: {error}");
            return ExitCodes.ValidationErrors;
        }

        Console.WriteLine($"ALLOWED: {agent.Name} ({agent.Role}) can {action} {path}");
        return ExitCodes.Success;
    }
}
