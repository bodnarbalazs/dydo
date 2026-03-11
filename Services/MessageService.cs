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
        var validationError = ValidateSendRequest(registry, sender, to, currentHuman, force);
        if (validationError != null)
        {
            ConsoleOutput.WriteError(validationError);
            return ExitCodes.ToolError;
        }

        var targetState = registry.GetAgentState(to);
        WriteMessage(registry, sender!.Name, to, body, subject, targetState);
        return ExitCodes.Success;
    }

    private static string? ValidateSendRequest(AgentRegistry registry, Models.AgentState? sender,
        string to, string? currentHuman, bool force)
    {
        if (sender == null)
            return "No agent identity assigned to this process.";

        if (!registry.IsValidAgentName(to))
            return $"Agent '{to}' does not exist.";

        if (to.Equals(sender.Name, StringComparison.OrdinalIgnoreCase))
            return "Cannot send a message to yourself.";

        var ownershipError = CheckOwnership(registry, to, currentHuman);
        if (ownershipError != null)
            return ownershipError;

        return CheckTargetActive(registry, to, force);
    }

    private static string? CheckOwnership(AgentRegistry registry, string to, string? currentHuman)
    {
        var targetHuman = registry.GetHumanForAgent(to);
        if (!string.IsNullOrEmpty(currentHuman) && targetHuman != currentHuman)
            return $"Agent '{to}' is not assigned to you (assigned to: {targetHuman ?? "nobody"}).";
        return null;
    }

    private static string? CheckTargetActive(AgentRegistry registry, string to, bool force)
    {
        var targetState = registry.GetAgentState(to);
        if (targetState != null && targetState.Status != Models.AgentStatus.Working && !force)
        {
            return $"Agent {to} is not currently active. The message will sit unread until {to} is claimed.\n" +
                   $"Send anyway with: dydo msg --to {to} --body \"...\" --force";
        }
        return null;
    }

    private static void WriteMessage(AgentRegistry registry, string senderName, string to,
        string body, string? subject, Models.AgentState? targetState)
    {
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
            from: {senderName}{subjectYaml}
            received: {DateTime.UtcNow:o}
            ---

            # Message from {senderName}

            ## Subject

            {subject ?? "(none)"}

            ## Body

            {body}
            """;

        File.WriteAllText(filePath, content);

        if (!string.IsNullOrEmpty(subject))
        {
            if (registry.RemoveReplyPendingMarker(senderName, subject))
                Console.WriteLine($"  Reply obligation fulfilled for '{subject}'.");
        }

        if (targetState != null && targetState.Status == Models.AgentStatus.Working)
            registry.AddUnreadMessage(to, messageId);

        Console.WriteLine($"Message sent to {to}.");
        Console.WriteLine($"  Subject: {subject ?? "(none)"}");
    }
}
