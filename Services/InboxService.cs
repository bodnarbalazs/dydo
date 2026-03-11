namespace DynaDocs.Services;

using DynaDocs.Models;
using DynaDocs.Utils;

public static class InboxService
{
    public static int ExecuteList()
    {
        var registry = new AgentRegistry();

        Console.WriteLine($"{"Agent",-10} {"Items",-6} {"Oldest",-20}");
        Console.WriteLine(new string('-', 40));

        var hasItems = false;

        foreach (var name in registry.AgentNames)
        {
            var items = InboxItemParser.GetInboxItems(registry.GetAgentWorkspace(name));
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

    public static int ExecuteShow()
    {
        var registry = new AgentRegistry();
        var sessionId = registry.GetSessionContext();

        var agent = registry.GetCurrentAgent(sessionId);
        if (agent == null)
        {
            ConsoleOutput.WriteError("No agent identity assigned to this process.");
            return ExitCodes.ToolError;
        }

        var items = InboxItemParser.GetInboxItems(registry.GetAgentWorkspace(agent.Name));

        if (items.Count == 0)
        {
            Console.WriteLine($"Agent {agent.Name} inbox: empty");
            return ExitCodes.Success;
        }

        Console.WriteLine($"Agent {agent.Name} inbox: {items.Count} item(s)");
        Console.WriteLine();

        foreach (var item in items.OrderBy(i => i.Received))
        {
            PrintInboxItem(item);
            Console.WriteLine();
        }

        return ExitCodes.Success;
    }

    public static int ExecuteClear(bool all, string? id)
    {
        var registry = new AgentRegistry();
        var sessionId = registry.GetSessionContext();

        var agent = registry.GetCurrentAgent(sessionId);
        if (agent == null)
        {
            ConsoleOutput.WriteError("No agent identity assigned to this process.");
            return ExitCodes.ToolError;
        }

        if (!all && string.IsNullOrEmpty(id))
        {
            ConsoleOutput.WriteError("Specify --all or --id <id>");
            return ExitCodes.ToolError;
        }

        var workspace = registry.GetAgentWorkspace(agent.Name);
        var inboxPath = Path.Combine(workspace, "inbox");
        if (!Directory.Exists(inboxPath))
        {
            Console.WriteLine("Inbox already empty.");
            return ExitCodes.Success;
        }

        var archivePath = Path.Combine(workspace, "archive", "inbox");
        Directory.CreateDirectory(archivePath);

        if (all)
            return ClearAll(registry, agent.Name, sessionId, inboxPath, archivePath);

        return ClearById(registry, agent.Name, sessionId, inboxPath, archivePath, id!);
    }

    private static int ClearAll(AgentRegistry registry, string agentName, string sessionId, string inboxPath, string archivePath)
    {
        var files = Directory.GetFiles(inboxPath, "*.md");
        foreach (var file in files)
        {
            TrackReplyPending(registry, agentName, file);
            var destPath = Path.Combine(archivePath, Path.GetFileName(file));
            File.Move(file, destPath, overwrite: true);
        }
        registry.ClearAllUnreadMessages(agentName);
        Console.WriteLine($"Archived {files.Length} item(s) to archive/inbox/");
        return ExitCodes.Success;
    }

    private static int ClearById(AgentRegistry registry, string agentName, string sessionId, string inboxPath, string archivePath, string id)
    {
        var files = Directory.GetFiles(inboxPath, $"{id}*.md");
        if (files.Length == 0)
        {
            ConsoleOutput.WriteError($"No inbox item with ID: {id}");
            return ExitCodes.ToolError;
        }

        foreach (var file in files)
        {
            TrackReplyPending(registry, agentName, file);
            var destPath = Path.Combine(archivePath, Path.GetFileName(file));
            File.Move(file, destPath, overwrite: true);
        }
        registry.MarkMessageRead(sessionId, id);
        Console.WriteLine($"Archived item {id} to archive/inbox/");
        return ExitCodes.Success;
    }

    private static void TrackReplyPending(AgentRegistry registry, string agentName, string file)
    {
        var item = InboxItemParser.ParseInboxItem(file);
        if (item != null && item.ReplyRequired && !string.IsNullOrEmpty(item.Task))
        {
            registry.CreateReplyPendingMarker(agentName, item.Task, item.From);
            Console.WriteLine($"  Reply required: message {item.From} about '{item.Task}' before releasing.");
        }
    }

    private static void PrintInboxItem(InboxItem item)
    {
        if (item.Type == "message")
        {
            Console.WriteLine($"[{item.Id}] MESSAGE: {item.Subject ?? "(no subject)"}");
            Console.WriteLine($"  From: {item.From}");
            Console.WriteLine($"  Received: {item.Received:yyyy-MM-dd HH:mm} UTC");
            var bodyPreview = item.Body ?? "";
            if (bodyPreview.Length > 200)
                bodyPreview = bodyPreview[..200] + "...";
            Console.WriteLine($"  Body: {bodyPreview}");
        }
        else
        {
            var escalatedPrefix = item.Escalated ? "[ESCALATED] " : "";
            Console.WriteLine($"{escalatedPrefix}[{item.Id}] {item.Role.ToUpperInvariant()}: {item.Task}");
            Console.WriteLine($"  From: {item.From}");
            if (!string.IsNullOrEmpty(item.Origin) && item.Origin != item.From)
                Console.WriteLine($"  Origin: {item.Origin}");
            Console.WriteLine($"  Received: {item.Received:yyyy-MM-dd HH:mm} UTC");
            if (item.Escalated && item.EscalatedAt.HasValue)
                Console.WriteLine($"  Escalated: {item.EscalatedAt:yyyy-MM-dd HH:mm} UTC");
            Console.WriteLine($"  Brief: {item.Brief}");

            if (item.ReplyRequired)
                Console.WriteLine($"  Reply required: yes (message {item.From} about '{item.Task}' before releasing)");

            if (item.Files.Count > 0)
                Console.WriteLine($"  Files: {string.Join(", ", item.Files)}");
        }
    }
}
