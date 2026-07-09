namespace DynaDocs.Services;

using System.Text.RegularExpressions;
using DynaDocs.Models;
using DynaDocs.Utils;

/// <summary>
/// Registers read-completion for an agent: marks a matching must-read complete and a matching
/// inbox message read from a file path. Shared by the guard's observed-Read tracking and the
/// host-agnostic <c>dydo read</c> verb so shell-based hosts (codex) register reads identically to
/// Claude's hook-observed Reads.
/// </summary>
public static partial class ReadTrackingService
{
    public static void TrackReadCompletion(AgentState? agent, string? filePath, string? sessionId, AgentRegistry registry)
    {
        if (agent == null || string.IsNullOrEmpty(filePath))
            return;

        // Track must-read completion
        if (agent.UnreadMustReads.Count > 0)
        {
            var relPath = NormalizeForMustReadComparison(filePath);
            if (agent.UnreadMustReads.Any(p => p.Equals(relPath, StringComparison.OrdinalIgnoreCase)))
                registry.MarkMustReadComplete(sessionId, relPath);
        }

        // Track message reads
        if (agent.UnreadMessages.Count > 0)
        {
            var messageId = ExtractMessageIdFromPath(filePath);
            if (messageId != null && agent.UnreadMessages.Contains(messageId))
                registry.MarkMessageRead(sessionId, messageId);
        }
    }

    /// <summary>
    /// Normalizes a file path for must-read comparison by extracting the project-relative
    /// portion starting from "dydo/".
    /// </summary>
    public static string NormalizeForMustReadComparison(string filePath)
    {
        var normalized = PathUtils.NormalizeWorktreePath(filePath)?.Replace('\\', '/') ?? filePath.Replace('\\', '/');
        var dydoIndex = normalized.IndexOf("dydo/", StringComparison.OrdinalIgnoreCase);
        return dydoIndex >= 0 ? normalized[dydoIndex..] : normalized;
    }

    /// <summary>
    /// Extracts a message ID from an inbox message file path.
    /// Matches paths like */inbox/{id}-msg-*.md and returns the {id} portion.
    /// </summary>
    public static string? ExtractMessageIdFromPath(string filePath)
    {
        var normalized = filePath.Replace('\\', '/');
        var match = InboxMessageIdRegex().Match(normalized);
        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex(@"/inbox/([a-f0-9]+)-msg-[^/]+\.md$", RegexOptions.IgnoreCase)]
    private static partial Regex InboxMessageIdRegex();
}
