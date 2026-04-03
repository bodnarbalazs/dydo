namespace DynaDocs.Services;

using System.Text.RegularExpressions;
using DynaDocs.Utils;

public static class MessageFinder
{
    public static MessageInfo? FindMessage(string inboxPath, string? taskFilter, HashSet<string>? excludeSubjects = null)
    {
        if (!Directory.Exists(inboxPath))
            return null;

        var files = Directory.GetFiles(inboxPath, "*-msg-*.md");
        var parsed = new List<MessageInfo>();

        foreach (var file in files)
        {
            var info = ParseMessageFile(file);
            if (info == null) continue;

            if (!string.IsNullOrEmpty(taskFilter) &&
                !string.Equals(info.Subject, taskFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            if (excludeSubjects != null &&
                !string.IsNullOrEmpty(info.Subject) &&
                excludeSubjects.Contains(info.Subject))
                continue;

            parsed.Add(info);
        }

        // Sort by received timestamp from frontmatter, falling back to file creation time
        return parsed
            .OrderBy(m => m.Received ?? File.GetCreationTimeUtc(m.FilePath))
            .FirstOrDefault();
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
