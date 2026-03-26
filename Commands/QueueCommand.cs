namespace DynaDocs.Commands;

using System.CommandLine;
using DynaDocs.Services;
using DynaDocs.Utils;

public static class QueueCommand
{
    public static Command Create()
    {
        var command = new Command("queue", "Manage dispatch queues");

        command.Subcommands.Add(CreateCreateCommand());
        command.Subcommands.Add(CreateShowCommand());
        command.Subcommands.Add(CreateCancelCommand());
        command.Subcommands.Add(CreateClearCommand());

        return command;
    }

    private static Command CreateCreateCommand()
    {
        var nameArgument = new Argument<string>("name")
        {
            Description = "Queue name to create"
        };

        var command = new Command("create", "Create a transient queue");
        command.Arguments.Add(nameArgument);

        command.SetAction(parseResult =>
        {
            var name = parseResult.GetValue(nameArgument)!;
            var service = new QueueService();

            if (!service.CreateQueue(name, out var error))
            {
                ConsoleOutput.WriteError(error);
                return ExitCodes.ToolError;
            }

            Console.WriteLine($"Queue '{name}' created (transient — auto-deleted when empty).");
            return ExitCodes.Success;
        });

        return command;
    }

    private static Command CreateShowCommand()
    {
        var nameArgument = new Argument<string?>("name")
        {
            DefaultValueFactory = _ => null,
            Description = "Queue name (optional, shows all if omitted)"
        };

        var command = new Command("show", "Show queue state");
        command.Arguments.Add(nameArgument);

        command.SetAction(parseResult =>
        {
            var name = parseResult.GetValue(nameArgument);
            var service = new QueueService();

            if (!string.IsNullOrEmpty(name))
            {
                if (!service.QueueExists(name))
                {
                    var available = string.Join(", ", service.ListQueues());
                    ConsoleOutput.WriteError($"No queue '{name}'. Available: {available}");
                    return ExitCodes.ToolError;
                }

                ShowQueue(service, name);
                return ExitCodes.Success;
            }

            var queues = service.ListQueues();
            if (queues.Count == 0)
            {
                Console.WriteLine("No queues configured.");
                return ExitCodes.Success;
            }

            foreach (var q in queues)
                ShowQueue(service, q);

            return ExitCodes.Success;
        });

        return command;
    }

    private static void ShowQueue(QueueService service, string name)
    {
        var type = service.IsPersistent(name) ? "persistent" : "transient";
        Console.WriteLine($"Queue: {name} ({type})");

        var active = service.GetActive(name);
        if (active != null)
        {
            var running = ProcessUtils.IsProcessRunning(active.Pid) ? "running" : "stale";
            Console.WriteLine($"  Active: {active.Agent} ({active.Task}) PID={active.Pid} [{running}]");
        }
        else
        {
            Console.WriteLine("  Active: (none)");
        }

        var pending = service.GetPending(name);
        if (pending.Count > 0)
        {
            Console.WriteLine($"  Pending: {pending.Count}");
            foreach (var (fileName, entry) in pending)
            {
                var seq = fileName.Split('-')[0];
                Console.WriteLine($"    [{seq}] {entry.Agent} ({entry.Task})");
            }
        }
        else
        {
            Console.WriteLine("  Pending: (none)");
        }

        Console.WriteLine();
    }

    private static Command CreateCancelCommand()
    {
        var queueArgument = new Argument<string>("queue")
        {
            Description = "Queue name"
        };

        var idArgument = new Argument<string>("id")
        {
            Description = "Entry sequence number (e.g., 0001)"
        };

        var command = new Command("cancel", "Remove a pending (not active) queue entry");
        command.Arguments.Add(queueArgument);
        command.Arguments.Add(idArgument);

        command.SetAction(parseResult =>
        {
            var queue = parseResult.GetValue(queueArgument)!;
            var id = parseResult.GetValue(idArgument)!;
            var service = new QueueService();

            if (!service.CancelEntry(queue, id, out var error))
            {
                ConsoleOutput.WriteError(error);
                return ExitCodes.ToolError;
            }

            Console.WriteLine($"Entry {id} removed from queue '{queue}'.");
            return ExitCodes.Success;
        });

        return command;
    }

    private static Command CreateClearCommand()
    {
        var nameArgument = new Argument<string>("name")
        {
            Description = "Queue name to clear"
        };

        var command = new Command("clear", "Clear all entries from a queue");
        command.Arguments.Add(nameArgument);

        command.SetAction(parseResult =>
        {
            var name = parseResult.GetValue(nameArgument)!;
            var service = new QueueService();

            if (!service.ClearQueue(name, out var error))
            {
                ConsoleOutput.WriteError(error);
                return ExitCodes.ToolError;
            }

            Console.WriteLine($"Queue '{name}' cleared.");
            return ExitCodes.Success;
        });

        return command;
    }
}
