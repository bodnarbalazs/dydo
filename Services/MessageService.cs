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
        var validationError = ValidateSendRequest(registry, sender, to, subject, currentHuman, force);
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
        string to, string? subject, string? currentHuman, bool force)
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

        return CheckTargetActive(registry, sender.Name, to, subject, force);
    }

    private static string? CheckOwnership(AgentRegistry registry, string to, string? currentHuman)
    {
        var targetHuman = registry.GetHumanForAgent(to);
        if (!string.IsNullOrEmpty(currentHuman) && targetHuman != currentHuman)
            return $"Agent '{to}' is not assigned to you (assigned to: {targetHuman ?? "nobody"}).";
        return null;
    }

    private static string? CheckTargetActive(AgentRegistry registry, string senderName, string to, string? subject, bool force)
    {
        var targetState = registry.GetAgentState(to);
        if (targetState == null || targetState.Status == Models.AgentStatus.Working)
            return null;

        if (force)
            return null;

        var activeAgents = registry.GetActiveAgents();
        var oversightAgents = registry.GetActiveOversightAgents();

        // Check if sender has a reply-pending marker targeting this agent
        var replyMarkers = registry.GetReplyPendingMarkers(senderName);
        var hasReplyPending = replyMarkers.Any(m =>
            m.To.Equals(to, StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrEmpty(subject) || m.Task.Equals(subject, StringComparison.OrdinalIgnoreCase)));

        if (hasReplyPending)
            return BuildReplyPendingMessage(to, oversightAgents);

        return BuildInactiveTargetMessage(registry, to, targetState, activeAgents, oversightAgents);
    }

    private static string BuildInactiveTargetMessage(AgentRegistry registry, string to,
        Models.AgentState? targetState, List<Models.AgentState> activeAgents, List<Models.AgentState> oversightAgents)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Agent {to} has been released and will not receive this message.");
        if (targetState?.DispatchedBy != null)
        {
            var dispatcher = registry.GetAgentState(targetState.DispatchedBy);
            if (dispatcher?.Status == Models.AgentStatus.Working)
            {
                sb.AppendLine();
                sb.AppendLine($"  {to} was dispatched by {dispatcher.Name} — try messaging them instead:");
                sb.AppendLine($"    dydo msg --to {dispatcher.Name} --body \"...\"");
            }
        }

        if (oversightAgents.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("  Active oversight agents:");
            sb.Append(FormatActiveAgentList(oversightAgents));
        }

        if (activeAgents.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("  All active agents:");
            sb.Append(FormatActiveAgentList(activeAgents));
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine("  No agents are currently active. Ask the human to intervene.");
        }

        return sb.ToString().TrimEnd();
    }

    private static string BuildReplyPendingMessage(string to, List<Models.AgentState> oversightAgents)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Agent {to} has been released, but you have a pending reply obligation to them.");

        if (oversightAgents.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("  Message an oversight agent to handle the situation:");
            sb.Append(FormatActiveAgentList(oversightAgents));
        }

        sb.AppendLine();
        sb.AppendLine($"  Or force-send (message will sit unread until {to} is claimed):");
        sb.AppendLine($"    dydo msg --to {to} --body \"...\" --force");

        return sb.ToString().TrimEnd();
    }

    private static string FormatActiveAgentList(List<Models.AgentState> agents)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var agent in agents)
        {
            var role = agent.Role ?? "—";
            var task = agent.Task != null ? $"task: {agent.Task}" : "";
            sb.AppendLine($"    {agent.Name.PadRight(10)} {role.PadRight(16)} {task}");
        }
        return sb.ToString();
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
