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

        var forceOption = new Option<bool>("--force")
        {
            Description = "Archive a specific orphaned inbox file"
        };

        var fileOption = new Option<string?>("--file")
        {
            Description = "Orphaned inbox file to archive with --force"
        };

        var command = new Command("clear", "Clear processed inbox items");
        command.Options.Add(allOption);
        command.Options.Add(idOption);
        command.Options.Add(forceOption);
        command.Options.Add(fileOption);

        command.SetAction(parseResult =>
        {
            var all = parseResult.GetValue(allOption);
            var id = parseResult.GetValue(idOption);
            var force = parseResult.GetValue(forceOption);
            var file = parseResult.GetValue(fileOption);
            return InboxService.ExecuteClear(all, id, force, file);
        });

        return command;
    }
}
