namespace DynaDocs.Commands;

using System.CommandLine;
using DynaDocs.Services;
using DynaDocs.Utils;

public static class MessageCommand
{
    public static Command Create()
    {
        var toOption = new Option<string>("--to")
        {
            Description = "Target agent name",
            Required = true
        };

        var bodyOption = new Option<string?>("--body")
        {
            Description = "Message content"
        };

        var bodyFileOption = new Option<string?>("--body-file")
        {
            Description = "Read body from file"
        };

        var subjectOption = new Option<string?>("--subject")
        {
            Description = "Topic/task identifier"
        };

        var forceOption = new Option<bool>("--force")
        {
            Description = "Allow sending to inactive agents"
        };

        var command = new Command("message", "Send a message to another agent");
        command.Aliases.Add("msg");
        command.Options.Add(toOption);
        command.Options.Add(bodyOption);
        command.Options.Add(bodyFileOption);
        command.Options.Add(subjectOption);
        command.Options.Add(forceOption);

        command.SetAction(parseResult =>
        {
            var to = parseResult.GetValue(toOption)!;
            var body = parseResult.GetValue(bodyOption);
            var bodyFile = parseResult.GetValue(bodyFileOption);
            var subject = parseResult.GetValue(subjectOption);
            var force = parseResult.GetValue(forceOption);

            var bodyFromFile = false;
            if (!string.IsNullOrEmpty(bodyFile))
            {
                if (!File.Exists(bodyFile))
                {
                    ConsoleOutput.WriteError($"Body file not found: {bodyFile}");
                    return ExitCodes.ToolError;
                }
                body = File.ReadAllText(bodyFile).Trim();
                bodyFromFile = true;
            }

            if (string.IsNullOrEmpty(body))
            {
                ConsoleOutput.WriteError("Provide --body or --body-file.");
                return ExitCodes.ToolError;
            }

            if (!bodyFromFile)
            {
                var shellMetaError = DispatchCommand.DetectShellMetacharacters(body);
                if (shellMetaError != null)
                {
                    ConsoleOutput.WriteError(shellMetaError);
                    return ExitCodes.ToolError;
                }
            }

            return Execute(to, body, subject, force);
        });

        return command;
    }

    private static int Execute(string to, string body, string? subject, bool force)
    {
        var registry = new AgentRegistry();
        var sessionId = registry.GetSessionContext();
        var currentHuman = registry.GetCurrentHuman();

        var sender = registry.GetCurrentAgent(sessionId);
        if (sender == null)
        {
            ConsoleOutput.WriteError("No agent identity assigned to this process.");
            return ExitCodes.ToolError;
        }

        if (!registry.IsValidAgentName(to))
        {
            ConsoleOutput.WriteError($"Agent '{to}' does not exist.");
            return ExitCodes.ToolError;
        }

        if (to.Equals(sender.Name, StringComparison.OrdinalIgnoreCase))
        {
            ConsoleOutput.WriteError("Cannot send a message to yourself.");
            return ExitCodes.ToolError;
        }

        var targetHuman = registry.GetHumanForAgent(to);
        if (!string.IsNullOrEmpty(currentHuman) && targetHuman != currentHuman)
        {
            ConsoleOutput.WriteError($"Agent '{to}' is not assigned to you (assigned to: {targetHuman ?? "nobody"}).");
            return ExitCodes.ToolError;
        }

        var targetState = registry.GetAgentState(to);
        if (targetState != null && targetState.Status != Models.AgentStatus.Working && !force)
        {
            ConsoleOutput.WriteError(
                $"Agent {to} is not currently active. The message will sit unread until {to} is claimed.\n" +
                $"Send anyway with: dydo msg --to {to} --body \"...\" --force");
            return ExitCodes.ToolError;
        }

        var messageId = Guid.NewGuid().ToString("N")[..8];
        var sanitizedSubject = PathUtils.SanitizeForFilename(subject ?? "general");

        var inboxPath = Path.Combine(registry.GetAgentWorkspace(to), "inbox");
        Directory.CreateDirectory(inboxPath);

        var filePath = Path.Combine(inboxPath, $"{messageId}-msg-{sanitizedSubject}.md");

        var subjectYaml = !string.IsNullOrEmpty(subject) ? $"\nsubject: {subject}" : "";
        var content = $"""
            ---
            id: {messageId}
            type: message
            from: {sender.Name}{subjectYaml}
            received: {DateTime.UtcNow:o}
            ---

            # Message from {sender.Name}

            ## Subject

            {subject ?? "(none)"}

            ## Body

            {body}
            """;

        File.WriteAllText(filePath, content);

        if (targetState != null && targetState.Status == Models.AgentStatus.Working)
        {
            registry.AddUnreadMessage(to, messageId);
        }

        Console.WriteLine($"Message sent to {to}.");
        Console.WriteLine($"  Subject: {subject ?? "(none)"}");

        return ExitCodes.Success;
    }
}
