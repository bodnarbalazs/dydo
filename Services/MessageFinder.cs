namespace DynaDocs.Services;

using System.Text.RegularExpressions;

public static class MessageFinder
{
    public static MessageInfo? FindMessage(string inboxPath, string? taskFilter, HashSet<string>? excludeSubjects = null)
    {
        if (!Directory.Exists(inboxPath))
            return null;

        var files = Directory.GetFiles(inboxPath, "*-msg-*.md")
            .OrderBy(f => File.GetCreationTimeUtc(f))
            .ToArray();

        foreach (var file in files)
        {
            var info = ParseMessageFile(file);
            if (info == null) continue;

            if (!string.IsNullOrEmpty(taskFilter) &&
                !string.Equals(info.Subject, taskFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            // Channel isolation: skip messages whose subject is claimed by a wait marker
            if (excludeSubjects != null &&
                !string.IsNullOrEmpty(info.Subject) &&
                excludeSubjects.Contains(info.Subject))
                continue;

            return info;
        }

        return null;
    }

    private static MessageInfo? ParseMessageFile(string filePath)
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
            string? from = null, subject = null, type = null;

            foreach (var line in yaml.Split('\n'))
            {
                var colonIndex = line.IndexOf(':');
                if (colonIndex < 0) continue;

                var key = line[..colonIndex].Trim();
                var value = line[(colonIndex + 1)..].Trim();

                switch (key)
                {
                    case "type": type = value; break;
                    case "from": from = value; break;
                    case "subject": subject = value; break;
                }
            }

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
                FilePath = filePath
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
    }
}
