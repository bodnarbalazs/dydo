namespace DynaDocs.Commands;

using System.CommandLine;
using DynaDocs.Services;
using DynaDocs.Utils;

/// <summary>
/// <c>dydo mode</c>: show and set this machine's mode flags. An agent's <c>dydo mode set</c> meets
/// the guard's built-in mode-set nudge (GuardCommand.ModeSet.cs); a human's terminal does not.
/// </summary>
public static class ModeCommand
{
    public static Command Create() => Create(new ModeState());

    internal static Command Create(ModeState state)
    {
        var show = new Command("show", "List every mode flag with its value");
        show.SetAction(_ =>
        {
            Print(state);
            return ExitCodes.Success;
        });

        var flagArgument = new Argument<string>("flag") { Description = "Mode flag (overnight)" };
        var valueArgument = new Argument<string>("value") { Description = "New value (overnight: on, off)" };
        var set = new Command("set", "Set one mode flag");
        set.Arguments.Add(flagArgument);
        set.Arguments.Add(valueArgument);
        set.SetAction(parseResult =>
            Set(state, parseResult.GetValue(flagArgument)!, parseResult.GetValue(valueArgument)!));

        var command = new Command("mode", "Show or set this machine's mode flags (overnight)");
        command.Subcommands.Add(show);
        command.Subcommands.Add(set);
        return command;
    }

    private static int Set(ModeState state, string flag, string value)
    {
        if (!ModeState.Flags.TryGetValue(flag, out var values))
        {
            Console.Error.WriteLine($"Unknown mode flag '{flag}'. Flags: {string.Join(", ", ModeState.Flags.Keys)}.");
            return ExitCodes.ToolError;
        }

        if (!values.Contains(value))
        {
            Console.Error.WriteLine($"Invalid value '{value}' for {flag}. Values: {string.Join(", ", values)}.");
            return ExitCodes.ToolError;
        }

        state.Set(flag, value);
        Print(state);
        return ExitCodes.Success;
    }

    private static void Print(ModeState state)
    {
        foreach (var (flag, value) in state.Read())
            Console.WriteLine($"{flag}: {value}");
    }
}
