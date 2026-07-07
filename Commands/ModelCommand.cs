namespace DynaDocs.Commands;

using System.CommandLine;
using DynaDocs.Services;
using DynaDocs.Utils;

/// <summary>
/// The <c>dydo model</c> command group (issue #214). A time-boxed operational swap for a model
/// outage: <c>cap</c> rebinds every tier on an unavailable model to a fallback and re-syncs the
/// native agents; <c>uncap</c> restores it. The watchdog auto-restores once the cap's reset time
/// passes, so the swap is self-healing without a runtime failover interceptor.
/// </summary>
public static class ModelCommand
{
    public static Command Create()
    {
        var command = new Command("model", "Temporarily cap an unavailable model to a fallback tier");
        command.Subcommands.Add(CreateCapCommand());
        command.Subcommands.Add(CreateUncapCommand());
        return command;
    }

    private static Command CreateCapCommand()
    {
        var modelArg = new Argument<string>("model")
        {
            Description = "The unavailable model id to cap (e.g. claude-fable-5).",
        };
        var untilOption = new Option<string>("--until")
        {
            Description = "When the cap lifts, as stated in the limit error: [yyyy-]mm-dd hh:mm (local time).",
            Required = true,
        };
        var fallbackOption = new Option<string?>("--fallback")
        {
            Description = "Model to rebind the capped tiers to. Defaults to models.fallback in dydo.json.",
        };

        var command = new Command("cap", "Rebind every tier on MODEL to a fallback until a reset time, then re-sync");
        command.Arguments.Add(modelArg);
        command.Options.Add(untilOption);
        command.Options.Add(fallbackOption);

        command.SetAction(parse =>
        {
            var model = parse.GetValue(modelArg)!;
            var untilRaw = parse.GetValue(untilOption)!;
            var until = ModelCapService.ParseUntil(untilRaw);
            if (until == null)
            {
                Console.Error.WriteLine(
                    $"model cap: could not parse --until '{untilRaw}'. Expected [yyyy-]mm-dd hh:mm.");
                return ExitCodes.ValidationErrors;
            }
            return ModelCapService.Cap(model, until.Value, parse.GetValue(fallbackOption), Console.Out, Console.Error);
        });
        return command;
    }

    private static Command CreateUncapCommand()
    {
        var modelArg = new Argument<string>("model")
        {
            Description = "The capped model id to restore (e.g. claude-fable-5).",
        };

        var command = new Command("uncap", "Restore MODEL's tier bindings, clear the cap, and re-sync");
        command.Arguments.Add(modelArg);

        command.SetAction(parse =>
            ModelCapService.Uncap(parse.GetValue(modelArg)!, Console.Out, Console.Error));
        return command;
    }
}
