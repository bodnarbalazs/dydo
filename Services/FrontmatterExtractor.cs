namespace DynaDocs.Services;

using DynaDocs.Models;
using DynaDocs.Utils;

internal static class FrontmatterExtractor
{
    public static Frontmatter? Extract(string content)
    {
        var fields = FrontmatterParser.ParseFields(content);
        if (fields == null) return null;

        var frontmatter = new Frontmatter();

        foreach (var (key, value) in fields)
        {
            switch (key.ToLowerInvariant())
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

    public static string RemoveFrontmatter(string content) =>
        FrontmatterParser.StripFrontmatter(content);
}
