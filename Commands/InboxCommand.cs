namespace DynaDocs.Commands;

using System.CommandLine;
using System.Text.RegularExpressions;
using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

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

        command.SetAction(_ => ExecuteList());

        return command;
    }

    private static Command CreateShowCommand()
    {
        var command = new Command("show", "Show current agent's inbox");

        command.SetAction(_ => ExecuteShow());

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
            return ExecuteClear(all, id);
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
            ConsoleOutput.WriteError("No agent identity assigned to this process.");
            return ExitCodes.ToolError;
        }

        var items = GetInboxItems(registry.GetAgentWorkspace(agent.Name));

        if (items.Count == 0)
        {
            Console.WriteLine($"Agent {agent.Name} inbox: empty");
            return ExitCodes.Success;
        }

        Console.WriteLine($"Agent {agent.Name} inbox: {items.Count} item(s)");
        Console.WriteLine();

        foreach (var item in items.OrderBy(i => i.Received))
        {
            var escalatedPrefix = item.Escalated ? "[ESCALATED] " : "";
            Console.WriteLine($"{escalatedPrefix}[{item.Id}] {item.Role.ToUpperInvariant()}: {item.Task}");
            Console.WriteLine($"  From: {item.From}");
            Console.WriteLine($"  Received: {item.Received:yyyy-MM-dd HH:mm} UTC");
            if (item.Escalated && item.EscalatedAt.HasValue)
                Console.WriteLine($"  Escalated: {item.EscalatedAt:yyyy-MM-dd HH:mm} UTC");
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
            ConsoleOutput.WriteError("No agent identity assigned to this process.");
            return ExitCodes.ToolError;
        }

        var inboxPath = Path.Combine(registry.GetAgentWorkspace(agent.Name), "inbox");
        if (!Directory.Exists(inboxPath))
        {
            Console.WriteLine("Inbox already empty.");
            return ExitCodes.Success;
        }

        var archivePath = Path.Combine(inboxPath, "archive");
        Directory.CreateDirectory(archivePath);

        if (all)
        {
            var files = Directory.GetFiles(inboxPath, "*.md");
            foreach (var file in files)
            {
                var destPath = Path.Combine(archivePath, Path.GetFileName(file));
                if (File.Exists(destPath))
                    File.Delete(destPath);  // Overwrite if exists
                File.Move(file, destPath);
            }
            Console.WriteLine($"Archived {files.Length} item(s) to inbox/archive/");
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
                var destPath = Path.Combine(archivePath, Path.GetFileName(file));
                if (File.Exists(destPath))
                    File.Delete(destPath);
                File.Move(file, destPath);
            }
            Console.WriteLine($"Archived item {id} to inbox/archive/");
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
            bool escalated = false;
            DateTime? escalatedAt = null;

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
                    case "escalated":
                        escalated = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                        break;
                    case "escalated_at":
                        if (DateTime.TryParse(value, out var escDt))
                            escalatedAt = escDt;
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
                Files = files,
                Escalated = escalated,
                EscalatedAt = escalatedAt
            };
        }
        catch
        {
            return null;
        }
    }
}
