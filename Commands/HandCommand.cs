namespace DynaDocs.Commands;

using System.CommandLine;
using DynaDocs.Services;
using DynaDocs.Utils;

/// <summary>
/// <c>dydo hand raise</c> / <c>dydo hand lower</c> — the explicit half of the needs-human attention
/// signal (Decision 030 §1). The flag is normally machine-written and self-healing, but an agent can
/// raise it deliberately (e.g. an escalation path) and clear it itself once resolved, rather than
/// leaving that only to the human un-checking it in Notion. Targets the current agent by default, or a
/// named one via <c>--agent</c> so a workflow can flag the context it manages.
/// </summary>
public static class HandCommand
{
    public static Command Create()
    {
        var command = new Command("hand", "Raise or lower the needs-human attention flag");
        command.Subcommands.Add(CreateSetCommand("raise", true, "Flag that a human is needed"));
        command.Subcommands.Add(CreateSetCommand("lower", false, "Clear the needs-human flag"));
        return command;
    }

    private static Command CreateSetCommand(string verb, bool value, string description)
    {
        var agentOption = new Option<string?>("--agent")
        {
            Description = "Target agent (defaults to the current agent for this session)"
        };

        var command = new Command(verb, description);
        command.Options.Add(agentOption);
        command.SetAction(parseResult => Execute(parseResult.GetValue(agentOption), value));
        return command;
    }

    private static int Execute(string? agentName, bool value)
    {
        var registry = new AgentRegistry();

        agentName ??= registry.GetCurrentAgent(registry.GetSessionContext())?.Name;
        if (string.IsNullOrEmpty(agentName))
        {
            // Defensive: escalation writers call this best-effort; never fail their pipeline.
            Console.Error.WriteLine("NOTICE: dydo hand — no current agent to flag; nothing to do.");
            return ExitCodes.Success;
        }

        if (!registry.SetNeedsHuman(agentName, value))
        {
            Console.Error.WriteLine($"NOTICE: dydo hand — {agentName}'s state is locked; try again.");
            return ExitCodes.Success;
        }

        Console.WriteLine(value
            ? $"Raised the needs-human flag on {agentName}."
            : $"Lowered the needs-human flag on {agentName}.");
        return ExitCodes.Success;
    }
}
