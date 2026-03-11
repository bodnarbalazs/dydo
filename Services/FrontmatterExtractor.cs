namespace DynaDocs.Services;

using DynaDocs.Models;

internal static class FrontmatterExtractor
{
    public static Frontmatter? Extract(string content)
    {
        if (!content.StartsWith("---")) return null;

        var endIndex = content.IndexOf("---", 3);
        if (endIndex == -1) return null;

        var yaml = content.Substring(3, endIndex - 3);
        var frontmatter = new Frontmatter();

        foreach (var line in yaml.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var colonIndex = line.IndexOf(':');
            if (colonIndex == -1) continue;

            var key = line[..colonIndex].Trim().ToLowerInvariant();
            var value = line[(colonIndex + 1)..].Trim();

            switch (key)
            {
                case "area": frontmatter.Area = value; break;
                case "type": frontmatter.Type = value; break;
                case "status": frontmatter.Status = value; break;
                case "date": frontmatter.Date = value; break;
                case "must-read":
                    frontmatter.MustRead = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                    break;
            }
        }

        return frontmatter;
    }

    public static string RemoveFrontmatter(string content)
    {
        if (!content.StartsWith("---")) return content;
        var endIndex = content.IndexOf("---", 3);
        return endIndex == -1 ? content : content[(endIndex + 3)..].TrimStart();
    }
}
