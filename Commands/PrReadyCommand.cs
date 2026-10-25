namespace DynaDocs.Commands;

using System.CommandLine;
using DynaDocs.Services;

/// <summary>
/// <c>dydo pr-ready</c>: check a tree is fit to open or ready a PR from. The rules live in
/// <see cref="PrReadyCheck"/>, which the guard shares; the command only parses.
/// </summary>
public static class PrReadyCommand
{
    public static Command Create()
    {
        var pathArgument = new Argument<string?>("path")
        {
            Arity = ArgumentArity.ZeroOrOne,
            Description = "The tree to check (default: the current directory)"
        };
        var bodyOption = new Option<string?>("--body")
        {
            Description = "PR body file that must name its ticket and hold the reviewer's PASS block"
        };

        var command = new Command("pr-ready",
            "Check a PR can be opened: clean tree, HEAD equal to its upstream, and with --body a ticket key and PASS review");
        command.Arguments.Add(pathArgument);
        command.Options.Add(bodyOption);
        command.SetAction(parseResult => PrReadyCheck.Run(
            Path.GetFullPath(parseResult.GetValue(pathArgument) ?? Environment.CurrentDirectory),
            parseResult.GetValue(bodyOption)));
        return command;
    }
}
