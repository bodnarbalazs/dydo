namespace DynaDocs.Commands;

using System.CommandLine;
using DynaDocs.Models;
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

        var explicitTarget = agentName != null;
        agentName ??= registry.GetCurrentAgent(registry.GetSessionContext())?.Name;
        if (string.IsNullOrEmpty(agentName))
        {
            // Defensive: escalation writers call this best-effort; never fail their pipeline.
            Console.Error.WriteLine("NOTICE: dydo hand — no current agent to flag; nothing to do.");
            return ExitCodes.Success;
        }

        // Validate a caller-supplied --agent BEFORE any filesystem touch. An unknown or traversal name
        // ('..\\..\\x') must not fabricate a state file outside the agents tree, nor silently create a
        // ghost agent and report success. Same registry check every other agent-targeting entry uses;
        // SetNeedsHuman re-validates as defence in depth. (The self-resolved name is trusted — it came
        // from the registry — so this only gates the explicit-target path.)
        if (explicitTarget && !registry.IsValidAgentName(agentName))
        {
            Console.Error.WriteLine($"ERROR: dydo hand — '{agentName}' is not a known agent.");
            return ExitCodes.ValidationErrors;
        }

        // A raise records an EXPLICIT flag (sticky — not swept, not cleared by the next tool call); a
        // lower clears whatever is set, derived or explicit. The source arg is ignored when clearing.
        if (!registry.SetNeedsHuman(agentName, value, NeedsHumanSource.Explicit))
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
