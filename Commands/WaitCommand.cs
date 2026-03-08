namespace DynaDocs.Commands;

using System.CommandLine;
using System.Text.RegularExpressions;
using DynaDocs.Services;
using DynaDocs.Utils;

public static class WaitCommand
{
    public static Command Create()
    {
        var taskOption = new Option<string?>("--task")
        {
            Description = "Only wake on messages with this subject"
        };

        var cancelOption = new Option<bool>("--cancel")
        {
            Description = "Cancel an active wait (remove wait marker)"
        };

        var command = new Command("wait", "Wait for an incoming message");
        command.Options.Add(taskOption);
        command.Options.Add(cancelOption);

        command.SetAction(parseResult =>
        {
            var task = parseResult.GetValue(taskOption);
            var cancel = parseResult.GetValue(cancelOption);
            return Execute(task, cancel);
        });

        return command;
    }

    private static int Execute(string? task, bool cancel)
    {
        var registry = new AgentRegistry();
        var sessionId = registry.GetSessionContext();

        var agent = registry.GetCurrentAgent(sessionId);
        if (agent == null)
        {
            ConsoleOutput.WriteError("No agent identity assigned to this process.");
            return ExitCodes.ToolError;
        }

        // Handle --cancel
        if (cancel)
        {
            if (task != null)
            {
                registry.RemoveWaitMarker(agent.Name, task);
                Console.WriteLine($"Wait cancelled for task '{task}'.");
            }
            else
            {
                registry.ClearAllWaitMarkers(agent.Name);
                Console.WriteLine("All wait markers cleared.");
            }
            return ExitCodes.Success;
        }

        var inboxPath = Path.Combine(registry.GetAgentWorkspace(agent.Name), "inbox");

        if (string.IsNullOrEmpty(task))
        {
            // General wait: skip messages whose subject matches an active wait marker
            var markers = registry.GetWaitMarkers(agent.Name);
            var claimedTasks = new HashSet<string>(
                markers.Select(m => m.Task),
                StringComparer.OrdinalIgnoreCase);

            Console.WriteLine("Waiting for message...");

            while (true)
            {
                var message = FindMessage(inboxPath, null, claimedTasks);
                if (message != null)
                {
                    Console.WriteLine($"Message received from {message.From}:");
                    Console.WriteLine($"  Subject: {message.Subject ?? "(none)"}");
                    Console.WriteLine($"  Body: {message.Body}");
                    Console.WriteLine();
                    Console.WriteLine($"  Message file: {message.FilePath}");
                    Console.WriteLine("  Run 'dydo inbox show' for full details.");
                    return ExitCodes.Success;
                }

                Thread.Sleep(10_000);
            }
        }
        else
        {
            // Task-specific wait
            Console.WriteLine($"Waiting for message about '{task}'...");

            while (true)
            {
                var message = FindMessage(inboxPath, task);
                if (message != null)
                {
                    Console.WriteLine($"Message received from {message.From}:");
                    Console.WriteLine($"  Subject: {message.Subject ?? "(none)"}");
                    Console.WriteLine($"  Body: {message.Body}");
                    Console.WriteLine();
                    Console.WriteLine($"  Message file: {message.FilePath}");
                    Console.WriteLine("  Run 'dydo inbox show' for full details.");

                    // Clean up wait marker if one exists for this task
                    registry.RemoveWaitMarker(agent.Name, task);

                    return ExitCodes.Success;
                }

                Thread.Sleep(10_000);
            }
        }
    }

    internal static MessageInfo? FindMessage(string inboxPath, string? taskFilter, HashSet<string>? excludeSubjects = null)
    {
        if (!Directory.Exists(inboxPath))
            return null;

        var files = Directory.GetFiles(inboxPath, "*-msg-*.md")
            .OrderBy(f => File.GetCreationTimeUtc(f))
            .ToArray();

        foreach (var file in files)
        {
            var info = ParseMessageFile(file);
            if (info == null) continue;

            if (!string.IsNullOrEmpty(taskFilter) &&
                !string.Equals(info.Subject, taskFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            // Channel isolation: skip messages whose subject is claimed by a wait marker
            if (excludeSubjects != null &&
                !string.IsNullOrEmpty(info.Subject) &&
                excludeSubjects.Contains(info.Subject))
                continue;

            return info;
        }

        return null;
    }

    private static MessageInfo? ParseMessageFile(string filePath)
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
            string? from = null, subject = null, type = null;

            foreach (var line in yaml.Split('\n'))
            {
                var colonIndex = line.IndexOf(':');
                if (colonIndex < 0) continue;

                var key = line[..colonIndex].Trim();
                var value = line[(colonIndex + 1)..].Trim();

                switch (key)
                {
                    case "type": type = value; break;
                    case "from": from = value; break;
                    case "subject": subject = value; break;
                }
            }

            if (type != "message" || from == null)
                return null;

            // Extract body from ## Body section
            var bodyMatch = Regex.Match(content, @"## Body\s+(.+?)(?=\n#|$)", RegexOptions.Singleline);
            var body = bodyMatch.Success ? bodyMatch.Groups[1].Value.Trim() : "";

            return new MessageInfo
            {
                From = from,
                Subject = subject,
                Body = body,
                FilePath = filePath
            };
        }
        catch
        {
            return null;
        }
    }

    internal sealed class MessageInfo
    {
        public required string From { get; init; }
        public string? Subject { get; init; }
        public required string Body { get; init; }
        public required string FilePath { get; init; }
    }
}
