namespace DynaDocs.Services;

using DynaDocs.Commands;
using DynaDocs.Utils;

public static class MessageService
{
    public static int Execute(string to, string body, string? subject, bool force)
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

        // Clear reply-pending marker if this message fulfills a reply obligation
        if (!string.IsNullOrEmpty(subject))
        {
            if (registry.RemoveReplyPendingMarker(sender.Name, subject))
                Console.WriteLine($"  Reply obligation fulfilled for '{subject}'.");
        }

        if (targetState != null && targetState.Status == Models.AgentStatus.Working)
        {
            registry.AddUnreadMessage(to, messageId);
        }

        Console.WriteLine($"Message sent to {to}.");
        Console.WriteLine($"  Subject: {subject ?? "(none)"}");

        return ExitCodes.Success;
    }
}
