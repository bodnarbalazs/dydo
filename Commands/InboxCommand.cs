namespace DynaDocs.Commands;

using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.RegularExpressions;
using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

public static class InboxCommand
{
    public static Command Create()
    {
        var command = new Command("inbox", "Manage agent inbox");

        command.AddCommand(CreateListCommand());
        command.AddCommand(CreateShowCommand());
        command.AddCommand(CreateClearCommand());

        return command;
    }

    private static Command CreateListCommand()
    {
        var command = new Command("list", "List agents with pending inbox items");

        command.SetHandler((InvocationContext ctx) =>
        {
            ctx.ExitCode = ExecuteList();
        });

        return command;
    }

    private static Command CreateShowCommand()
    {
        var command = new Command("show", "Show current agent's inbox");

        command.SetHandler((InvocationContext ctx) =>
        {
            ctx.ExitCode = ExecuteShow();
        });

        return command;
    }

    private static Command CreateClearCommand()
    {
        var allOption = new Option<bool>("--all", "Clear all items");
        var idOption = new Option<string?>("--id", "Clear specific item by ID");

        var command = new Command("clear", "Clear processed inbox items")
        {
            allOption,
            idOption
        };

        command.SetHandler((InvocationContext ctx) =>
        {
            var all = ctx.ParseResult.GetValueForOption(allOption);
            var id = ctx.ParseResult.GetValueForOption(idOption);
            ctx.ExitCode = ExecuteClear(all, id);
        });

        return command;
    }

    private static int ExecuteList()
    {
        var registry = new AgentRegistry();

        Console.WriteLine($"{"Agent",-10} {"Items",-6} {"Oldest",-20}");
        Console.WriteLine(new string('-', 40));

        var hasItems = false;

        foreach (var name in registry.AgentNames)
        {
            var items = GetInboxItems(registry.GetAgentWorkspace(name));
            if (items.Count == 0) continue;

            hasItems = true;
            var oldest = items.Min(i => i.Received);
            Console.WriteLine($"{name,-10} {items.Count,-6} {oldest:yyyy-MM-dd HH:mm}");
        }

        if (!hasItems)
        {
            Console.WriteLine("(No pending inbox items)");
        }

        return ExitCodes.Success;
    }

    private static int ExecuteShow()
    {
        var registry = new AgentRegistry();

        var agent = registry.GetCurrentAgent();
        if (agent == null)
        {
            ConsoleOutput.WriteError("No agent claimed for this terminal");
            return ExitCodes.ToolError;
        }

        var items = GetInboxItems(registry.GetAgentWorkspace(agent.Name));

        if (items.Count == 0)
        {
            Console.WriteLine($"Inbox for {agent.Name}: empty");
            return ExitCodes.Success;
        }

        Console.WriteLine($"Inbox for {agent.Name}: {items.Count} item(s)");
        Console.WriteLine();

        foreach (var item in items.OrderBy(i => i.Received))
        {
            Console.WriteLine($"[{item.Id}] {item.Role.ToUpperInvariant()}: {item.Task}");
            Console.WriteLine($"  From: {item.From}");
            Console.WriteLine($"  Received: {item.Received:yyyy-MM-dd HH:mm}");
            Console.WriteLine($"  Brief: {item.Brief}");

            if (item.Files.Count > 0)
                Console.WriteLine($"  Files: {string.Join(", ", item.Files)}");

            Console.WriteLine();
        }

        return ExitCodes.Success;
    }

    private static int ExecuteClear(bool all, string? id)
    {
        var registry = new AgentRegistry();

        var agent = registry.GetCurrentAgent();
        if (agent == null)
        {
            ConsoleOutput.WriteError("No agent claimed for this terminal");
            return ExitCodes.ToolError;
        }

        var inboxPath = Path.Combine(registry.GetAgentWorkspace(agent.Name), "inbox");
        if (!Directory.Exists(inboxPath))
        {
            Console.WriteLine("Inbox already empty");
            return ExitCodes.Success;
        }

        if (all)
        {
            var files = Directory.GetFiles(inboxPath, "*.md");
            foreach (var file in files)
            {
                File.Delete(file);
            }
            Console.WriteLine($"Cleared {files.Length} item(s)");
        }
        else if (!string.IsNullOrEmpty(id))
        {
            var files = Directory.GetFiles(inboxPath, $"{id}*.md");
            if (files.Length == 0)
            {
                ConsoleOutput.WriteError($"No inbox item with ID: {id}");
                return ExitCodes.ToolError;
            }

            foreach (var file in files)
            {
                File.Delete(file);
            }
            Console.WriteLine($"Cleared item {id}");
        }
        else
        {
            ConsoleOutput.WriteError("Specify --all or --id <id>");
            return ExitCodes.ToolError;
        }

        return ExitCodes.Success;
    }

    private static List<InboxItem> GetInboxItems(string workspace)
    {
        var inboxPath = Path.Combine(workspace, "inbox");
        if (!Directory.Exists(inboxPath))
            return [];

        var items = new List<InboxItem>();

        foreach (var file in Directory.GetFiles(inboxPath, "*.md"))
        {
            var item = ParseInboxItem(file);
            if (item != null)
                items.Add(item);
        }

        return items;
    }

    private static InboxItem? ParseInboxItem(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath);
            if (!content.StartsWith("---"))
                return null;

            var endIndex = content.IndexOf("---", 3);
            if (endIndex < 0)
                return null;

            var yaml = content[3..endIndex].Trim();

            string? id = null, from = null, role = null, task = null;
            DateTime received = DateTime.UtcNow;

            foreach (var line in yaml.Split('\n'))
            {
                var colonIndex = line.IndexOf(':');
                if (colonIndex < 0) continue;

                var key = line[..colonIndex].Trim();
                var value = line[(colonIndex + 1)..].Trim();

                switch (key)
                {
                    case "id": id = value; break;
                    case "from": from = value; break;
                    case "role": role = value; break;
                    case "task": task = value; break;
                    case "received":
                        if (DateTime.TryParse(value, out var dt))
                            received = dt;
                        break;
                }
            }

            if (id == null || from == null || role == null || task == null)
                return null;

            // Extract brief from content
            var briefMatch = Regex.Match(content, @"## Brief\s+(.+?)(?=\n#|$)", RegexOptions.Singleline);
            var brief = briefMatch.Success ? briefMatch.Groups[1].Value.Trim() : "";

            // Extract files from content
            var files = new List<string>();
            var filesMatch = Regex.Match(content, @"## Files\s+((?:- .+\n?)+)");
            if (filesMatch.Success)
            {
                files = Regex.Matches(filesMatch.Groups[1].Value, @"- (.+)")
                    .Select(m => m.Groups[1].Value.Trim())
                    .ToList();
            }

            return new InboxItem
            {
                Id = id,
                From = from,
                Role = role,
                Task = task,
                Received = received,
                Brief = brief,
                Files = files
            };
        }
        catch
        {
            return null;
        }
    }
}
