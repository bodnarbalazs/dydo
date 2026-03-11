namespace DynaDocs.Commands;

using System.CommandLine;
using DynaDocs.Services;

public static class InboxCommand
{
    public static Command Create()
    {
        var command = new Command("inbox", "Manage agent inbox");

        command.Subcommands.Add(CreateListCommand());
        command.Subcommands.Add(CreateShowCommand());
        command.Subcommands.Add(CreateClearCommand());

        return command;
    }

    private static Command CreateListCommand()
    {
        var command = new Command("list", "List agents with pending inbox items");

        command.SetAction(_ => InboxService.ExecuteList());

        return command;
    }

    private static Command CreateShowCommand()
    {
        var command = new Command("show", "Show current agent's inbox");

        command.SetAction(_ => InboxService.ExecuteShow());

        return command;
    }

    private static Command CreateClearCommand()
    {
        var allOption = new Option<bool>("--all")
        {
            Description = "Clear all items"
        };

        var idOption = new Option<string?>("--id")
        {
            Description = "Clear specific item by ID"
        };

        var command = new Command("clear", "Clear processed inbox items");
        command.Options.Add(allOption);
        command.Options.Add(idOption);

        command.SetAction(parseResult =>
        {
            var all = parseResult.GetValue(allOption);
            var id = parseResult.GetValue(idOption);
            return InboxService.ExecuteClear(all, id);
        });

        return command;
    }
}
