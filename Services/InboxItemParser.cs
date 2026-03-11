namespace DynaDocs.Services;

using System.Text.RegularExpressions;
using DynaDocs.Models;

public static class InboxItemParser
{
    public static List<InboxItem> GetInboxItems(string workspace)
    {
        var inboxPath = Path.Combine(workspace, "inbox");
        if (!Directory.Exists(inboxPath))
            return [];

        var items = new List<InboxItem>();

        foreach (var file in Directory.GetFiles(inboxPath, "*.md"))
        {
            var item = ParseInboxItem(file);
            if (item != null)
                items.Add(item);
        }

        return items;
    }

    public static InboxItem? ParseInboxItem(string filePath)
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
            var fields = ParseYamlFields(yaml);

            if (fields.Id == null || fields.From == null)
                return null;

            // Messages don't require role/task
            if (fields.Type != "message" && (fields.Role == null || fields.Task == null))
                return null;

            // Extract brief from content
            var briefMatch = Regex.Match(content, @"## Brief\s+(.+?)(?=\n#|$)", RegexOptions.Singleline);
            var brief = briefMatch.Success ? briefMatch.Groups[1].Value.Trim() : "";

            // Extract body from content (for messages)
            var bodyMatch = Regex.Match(content, @"## Body\s+(.+?)(?=\n#|$)", RegexOptions.Singleline);
            var body = bodyMatch.Success ? bodyMatch.Groups[1].Value.Trim() : null;

            // Extract files from content
            var files = ExtractFiles(content);

            return new InboxItem
            {
                Id = fields.Id,
                From = fields.From,
                Origin = fields.Origin,
                Role = fields.Role ?? "",
                Task = fields.Task ?? "",
                Received = fields.Received,
                Brief = brief,
                Body = body,
                Files = files,
                Escalated = fields.Escalated,
                EscalatedAt = fields.EscalatedAt,
                Type = fields.Type,
                Subject = fields.Subject,
                ReplyRequired = fields.ReplyRequired
            };
        }
        catch
        {
            return null;
        }
    }

    private record YamlFields
    {
        public string? Id { get; init; }
        public string? From { get; init; }
        public string? Origin { get; init; }
        public string? Role { get; init; }
        public string? Task { get; init; }
        public string? Type { get; init; }
        public string? Subject { get; init; }
        public DateTime Received { get; init; } = DateTime.UtcNow;
        public bool Escalated { get; init; }
        public DateTime? EscalatedAt { get; init; }
        public bool ReplyRequired { get; init; }
    }

    private static YamlFields ParseYamlFields(string yaml)
    {
        string? id = null, from = null, role = null, task = null, origin = null;
        string? type = null, subject = null;
        DateTime received = DateTime.UtcNow;
        bool escalated = false;
        DateTime? escalatedAt = null;
        bool replyRequired = false;

        foreach (var line in yaml.Split('\n'))
        {
            var colonIndex = line.IndexOf(':');
            if (colonIndex < 0) continue;

            var key = line[..colonIndex].Trim();
            var value = line[(colonIndex + 1)..].Trim();

            switch (key)
            {
                case "id": id = value; break;
                case "from": from = value; break;
                case "origin": origin = value; break;
                case "role": role = value; break;
                case "task": task = value; break;
                case "type": type = value; break;
                case "subject": subject = value; break;
                case "received":
                    if (DateTime.TryParse(value, out var dt))
                        received = dt;
                    break;
                case "escalated":
                    escalated = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                    break;
                case "escalated_at":
                    if (DateTime.TryParse(value, out var escDt))
                        escalatedAt = escDt;
                    break;
                case "reply_required":
                    replyRequired = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                    break;
            }
        }

        return new YamlFields
        {
            Id = id, From = from, Origin = origin, Role = role, Task = task,
            Type = type, Subject = subject, Received = received,
            Escalated = escalated, EscalatedAt = escalatedAt, ReplyRequired = replyRequired
        };
    }

    private static List<string> ExtractFiles(string content)
    {
        var filesMatch = Regex.Match(content, @"## Files\s+((?:- .+\n?)+)");
        if (!filesMatch.Success)
            return [];

        return Regex.Matches(filesMatch.Groups[1].Value, @"- (.+)")
            .Select(m => m.Groups[1].Value.Trim())
            .ToList();
    }
}
