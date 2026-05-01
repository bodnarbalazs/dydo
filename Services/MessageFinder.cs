namespace DynaDocs.Services;

using System.Text.RegularExpressions;
using DynaDocs.Utils;

public static class MessageFinder
{
    private static readonly Regex MessageIdRegex = new(
        @"^([a-f0-9]+)-msg-",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static HashSet<string> GetInboxMessageIds(string inboxPath)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(inboxPath))
            return ids;
        foreach (var file in Directory.GetFiles(inboxPath, "*-msg-*.md"))
        {
            var match = MessageIdRegex.Match(Path.GetFileName(file));
            if (match.Success)
                ids.Add(match.Groups[1].Value);
        }
        return ids;
    }

    public static MessageInfo? FindMessage(
        string inboxPath,
        string? taskFilter,
        HashSet<string>? excludeSubjects = null,
        HashSet<string>? excludeIds = null,
        HashSet<string>? includeIds = null)
    {
        if (!Directory.Exists(inboxPath))
            return null;

        var parsed = new List<MessageInfo>();
        foreach (var file in Directory.GetFiles(inboxPath, "*-msg-*.md"))
        {
            var info = ParseMessageFile(file);
            if (info == null) continue;
            if (!MatchesSubject(info.Subject, taskFilter, excludeSubjects)) continue;
            if (!MatchesIdFilter(file, excludeIds, includeIds)) continue;
            parsed.Add(info);
        }

        // Sort by received timestamp from frontmatter, falling back to file creation time
        return parsed
            .OrderBy(m => m.Received ?? File.GetCreationTimeUtc(m.FilePath))
            .FirstOrDefault();
    }

    private static bool MatchesSubject(string? subject, string? taskFilter, HashSet<string>? excludeSubjects)
    {
        if (!string.IsNullOrEmpty(taskFilter) &&
            !string.Equals(subject, taskFilter, StringComparison.OrdinalIgnoreCase))
            return false;
        if (excludeSubjects != null &&
            !string.IsNullOrEmpty(subject) &&
            excludeSubjects.Contains(subject))
            return false;
        return true;
    }

    private static bool MatchesIdFilter(string filePath, HashSet<string>? excludeIds, HashSet<string>? includeIds)
    {
        if (includeIds == null && (excludeIds == null || excludeIds.Count == 0))
            return true;
        var idMatch = MessageIdRegex.Match(Path.GetFileName(filePath));
        var id = idMatch.Success ? idMatch.Groups[1].Value : null;
        if (excludeIds != null && id != null && excludeIds.Contains(id))
            return false;
        if (includeIds != null && (id == null || !includeIds.Contains(id)))
            return false;
        return true;
    }

    private static MessageInfo? ParseMessageFile(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath);
            var fields = FrontmatterParser.ParseFields(content);
            if (fields == null) return null;

            fields.TryGetValue("type", out var type);
            fields.TryGetValue("from", out var from);
            fields.TryGetValue("subject", out var subject);

            DateTime? received = null;
            if (fields.TryGetValue("received", out var receivedStr) &&
                DateTime.TryParse(receivedStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                received = dt.ToUniversalTime();

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
                FilePath = filePath,
                Received = received
            };
        }
        catch
        {
            return null;
        }
    }

    public sealed class MessageInfo
    {
        public required string From { get; init; }
        public string? Subject { get; init; }
        public required string Body { get; init; }
        public required string FilePath { get; init; }
        public DateTime? Received { get; init; }
    }
}
