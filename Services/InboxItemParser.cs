namespace DynaDocs.Services;

using System.Text.RegularExpressions;
using DynaDocs.Models;
using DynaDocs.Utils;

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
            var rawFields = FrontmatterParser.ParseFields(content);
            if (rawFields == null)
                return null;

            var fields = ParseYamlFields(rawFields);

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
                FilePath = filePath,
                From = fields.From,
                FromRole = fields.FromRole,
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
        public string? FromRole { get; init; }
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

    private static YamlFields ParseYamlFields(Dictionary<string, string> rawFields)
    {
        var state = new YamlParseState();

        foreach (var (key, value) in rawFields)
            ApplyYamlField(state, key, value);

        return new YamlFields
        {
            Id = state.Id, From = state.From, FromRole = state.FromRole, Origin = state.Origin,
            Role = state.Role, Task = state.Task, Type = state.Type, Subject = state.Subject,
            Received = state.Received, Escalated = state.Escalated, EscalatedAt = state.EscalatedAt,
            ReplyRequired = state.ReplyRequired
        };
    }

    private static void ApplyYamlField(YamlParseState state, string key, string value)
    {
        if (StringFieldSetters.TryGetValue(key, out var setter))
            setter(state, value);
        else
            ApplyDateOrBoolField(state, key, value);
    }

    private static readonly Dictionary<string, Action<YamlParseState, string>> StringFieldSetters = new()
    {
        ["id"] = (s, v) => s.Id = v,
        ["from"] = (s, v) => s.From = v,
        ["origin"] = (s, v) => s.Origin = v,
        ["role"] = (s, v) => s.Role = v,
        ["task"] = (s, v) => s.Task = v,
        ["type"] = (s, v) => s.Type = v,
        ["subject"] = (s, v) => s.Subject = v,
        ["from_role"] = (s, v) => s.FromRole = v,
    };

    private static void ApplyDateOrBoolField(YamlParseState state, string key, string value)
    {
        ApplyDateField(state, key, value);
        ApplyBoolField(state, key, value);
    }

    private static void ApplyDateField(YamlParseState state, string key, string value)
    {
        switch (key)
        {
            case "received":
                if (DateTime.TryParse(value, out var dt))
                    state.Received = dt;
                break;
            case "escalated_at":
                if (DateTime.TryParse(value, out var escDt))
                    state.EscalatedAt = escDt;
                break;
        }
    }

    private static void ApplyBoolField(YamlParseState state, string key, string value)
    {
        switch (key)
        {
            case "escalated":
                state.Escalated = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                break;
            case "reply_required":
                state.ReplyRequired = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                break;
        }
    }

    private sealed class YamlParseState
    {
        public string? Id { get; set; }
        public string? From { get; set; }
        public string? FromRole { get; set; }
        public string? Origin { get; set; }
        public string? Role { get; set; }
        public string? Task { get; set; }
        public string? Type { get; set; }
        public string? Subject { get; set; }
        public DateTime Received { get; set; } = DateTime.UtcNow;
        public bool Escalated { get; set; }
        public DateTime? EscalatedAt { get; set; }
        public bool ReplyRequired { get; set; }
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
