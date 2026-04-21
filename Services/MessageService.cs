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
        var messageId = DeliverInboxMessage(registry, sender!.Name, to, body, subject);

        if (!string.IsNullOrEmpty(subject))
        {
            if (registry.RemoveReplyPendingMarker(sender.Name, subject))
                Console.WriteLine($"  Reply obligation fulfilled for '{subject}'.");
        }
        else
        {
            var markers = registry.GetReplyPendingMarkers(sender.Name);
            foreach (var marker in markers.Where(m => m.To.Equals(to, StringComparison.OrdinalIgnoreCase)))
            {
                registry.RemoveReplyPendingMarker(sender.Name, marker.Task);
                Console.WriteLine($"  Reply obligation fulfilled for '{marker.Task}'.");
            }
        }

        WarnOnSubjectMismatch(registry, to, subject, targetState);

        Console.WriteLine($"Message sent to {to}.");
        Console.WriteLine($"  Subject: {subject ?? "(none)"}");
        return ExitCodes.Success;
    }

    /// <summary>
    /// Writes an inbox message file for programmatic delivery (bypasses ownership/active checks).
    /// Used by system-initiated sends like reviewer verdict auto-routing.
    /// </summary>
    public static string DeliverInboxMessage(AgentRegistry registry, string fromName, string toName,
        string body, string? subject)
    {
        var messageId = Guid.NewGuid().ToString("N")[..8];
        var sanitizedSubject = PathUtils.SanitizeForFilename(subject ?? "general");

        var inboxPath = Path.Combine(registry.GetAgentWorkspace(toName), "inbox");
        Directory.CreateDirectory(inboxPath);

        var filePath = Path.Combine(inboxPath, $"{messageId}-msg-{sanitizedSubject}.md");
        var subjectYaml = !string.IsNullOrEmpty(subject) ? $"\nsubject: {subject}" : "";
        var content = $"""
            ---
            id: {messageId}
            type: message
            from: {fromName}{subjectYaml}
            received: {DateTime.UtcNow:o}
            ---

            # Message from {fromName}

            ## Subject

            {subject ?? "(none)"}

            ## Body

            {body}
            """;

        File.WriteAllText(filePath, content);

        var targetState = registry.GetAgentState(toName);
        if (targetState != null && targetState.Status == Models.AgentStatus.Working)
            registry.AddUnreadMessage(toName, messageId);

        return messageId;
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

        // Allow sends that fulfill a reply-pending obligation, even to inactive targets
        var replyMarkers = registry.GetReplyPendingMarkers(senderName);
        var hasReplyPending = replyMarkers.Any(m =>
            m.To.Equals(to, StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrEmpty(subject) || m.Task.Equals(subject, StringComparison.OrdinalIgnoreCase)));

        if (hasReplyPending)
            return null;

        return BuildInactiveTargetMessage(registry, to, subject, targetState, activeAgents, oversightAgents);
    }

    private static string BuildInactiveTargetMessage(AgentRegistry registry, string to, string? subject,
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

        if (!string.IsNullOrEmpty(subject))
        {
            var waiters = GetAgentsWaitingForSubject(registry, subject)
                .Where(w => !w.Equals(to, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (waiters.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"  Agents waiting on subject '{subject}':");
                foreach (var waiter in waiters)
                    sb.AppendLine($"    {waiter}");
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

    private static List<string> GetAgentsWaitingForSubject(AgentRegistry registry, string subject)
    {
        var waiters = new List<string>();
        foreach (var name in registry.AgentNames)
        {
            if (registry.GetWaitMarkers(name).Any(m => m.Task.Equals(subject, StringComparison.OrdinalIgnoreCase)))
                waiters.Add(name);
        }
        return waiters;
    }

    private static void WarnOnSubjectMismatch(AgentRegistry registry, string to, string? subject,
        Models.AgentState? targetState)
    {
        if (string.IsNullOrEmpty(subject)) return;
        if (targetState?.Status != Models.AgentStatus.Working) return;

        var waits = registry.GetWaitMarkers(to);
        var specificWaits = waits.Where(m => !m.Task.StartsWith("_")).ToList();
        if (specificWaits.Count == 0) return;

        var hasGeneralWait = waits.Any(m => m.Task.StartsWith("_"));
        if (hasGeneralWait) return;

        if (specificWaits.Any(m => m.Task.Equals(subject, StringComparison.OrdinalIgnoreCase)))
            return;

        var waitList = string.Join(", ", specificWaits.Select(m => $"'{m.Task}'"));
        Console.Error.WriteLine(
            $"Warning: Recipient {to} is waiting on {waitList}, not '{subject}'. " +
            $"Message delivered, but their wait won't fire on this subject.");
    }
}
