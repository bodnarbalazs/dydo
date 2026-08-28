namespace DynaDocs.Rules;

using System.Text.RegularExpressions;
using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;
using Markdig;
using Markdig.Syntax;

public sealed class FutureFeatureRule : RuleBase
{
    private static readonly HashSet<string> DeliveryFields = new(
        ["assigned", "assignee", "priority", "blocked-by", "blocks", "dependency", "dependencies",
         "project", "initiative", "cycle", "milestone", "sprint", "campaign", "slice", "task", "issue",
         "workflow", "state", "due-date", "estimate", "labels", "parent", "sub-issue", "team"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly Regex LinearReference = new(
        @"^https://linear\.app/[A-Za-z0-9._~-]+/(?:issue/[A-Z]+-[0-9]+(?:/[a-z0-9-]+)?|project/[a-z0-9-]+-[0-9a-f]{12}|initiative/[a-z0-9-]+-[0-9a-f]{12})$",
        RegexOptions.CultureInvariant);

    public override string Name => "FutureFeature";
    public override string Description => "FutureFeatures are unpromoted ideas or terminal promotion provenance";

    public override IEnumerable<Violation> Validate(DocFile doc, List<DocFile> allDocs, string basePath)
    {
        if (!IsFutureFeature(doc))
            yield break;

        var fields = FrontmatterParser.ParseFields(doc.Content) ?? [];
        var area = Value(fields, "area");
        var type = Value(fields, "type");
        var status = Value(fields, "status");

        if (!string.Equals(area, "project", StringComparison.Ordinal))
            yield return CreateError(doc, "FutureFeatures require area: project");
        if (!string.Equals(type, "concept", StringComparison.Ordinal))
            yield return CreateError(doc, "FutureFeatures require type: concept");
        if (status is not ("idea" or "promoted"))
            yield return CreateError(doc, "FutureFeatures require status: idea or status: promoted");

        var linearReferences = Values(doc.Content, "linear-reference");
        if (status == "idea" && linearReferences.Count != 0)
            yield return CreateError(doc, "Idea FutureFeatures must not have a linear-reference");
        if (status == "promoted" && (linearReferences.Count != 1 || !LinearReference.IsMatch(linearReferences[0])))
            yield return CreateError(doc, "Promoted FutureFeatures require exactly one valid linear-reference URL");

        foreach (var field in fields.Keys.Where(DeliveryFields.Contains))
            yield return CreateError(doc, $"FutureFeatures must not have delivery field: {field}");

        if (!HasContentSection(doc.Content, "Rationale"))
            yield return CreateError(doc, "FutureFeatures require a non-empty ## Rationale section");

        var relatedSection = GetSection(doc.Content, "Related");
        if (relatedSection == null)
        {
            yield return CreateError(doc, "FutureFeatures require a ## Related section with a resolving repository link");
            yield break;
        }

        var resolver = new LinkResolver();
        var hasRelatedLink = doc.Links.Any(link =>
            link.Type == LinkType.Markdown &&
            link.LineNumber > relatedSection.Value.StartLine &&
            link.LineNumber < relatedSection.Value.EndLine &&
            resolver.ResolveLink(doc, link, allDocs, basePath));
        if (!hasRelatedLink)
            yield return CreateError(doc, "FutureFeatures require a ## Related section with a resolving repository link");
    }

    private static bool IsFutureFeature(DocFile doc)
    {
        const string folder = "project/future-features/";
        var path = PathUtils.NormalizeForKey(PathUtils.CollapseRelativeSegments(doc.RelativePath));
        if (!path.StartsWith(folder, StringComparison.Ordinal) || doc.FileName.StartsWith("_", StringComparison.Ordinal))
            return false;

        return !path[folder.Length..].Contains('/');
    }

    private static string? Value(Dictionary<string, string> fields, string key)
    {
        return fields.FirstOrDefault(pair => pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value;
    }

    private static List<string> Values(string content, string key)
    {
        var yaml = FrontmatterParser.ExtractYamlBlock(content);
        if (yaml == null)
            return [];

        return yaml.Split('\n')
            .Select(line => line.Split(':', 2))
            .Where(parts => parts.Length == 2 && parts[0].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
            .Select(parts => parts[1].Trim())
            .ToList();
    }

    private static bool HasContentSection(string content, string title)
    {
        var section = GetSection(content, title);
        if (section == null)
            return false;

        return content.Split('\n')
            .Skip(section.Value.StartLine)
            .Take(section.Value.EndLine - section.Value.StartLine - 1)
            .Any(line => !string.IsNullOrWhiteSpace(line));
    }

    private static (int StartLine, int EndLine)? GetSection(string content, string title)
    {
        var lines = content.Split('\n');
        var headings = GetHeadings(Markdown.Parse(content))
            .Where(heading => heading.Level <= 2)
            .OrderBy(heading => heading.Line)
            .ToList();
        var start = headings.FindIndex(heading => heading.Level == 2 &&
            HeadingTitle(lines[heading.Line]).Equals(title, StringComparison.Ordinal));
        if (start == -1)
            return null;

        var startLine = headings[start].Line + 1;
        var endLine = start + 1 < headings.Count ? headings[start + 1].Line + 1 : lines.Length + 1;
        return (startLine, endLine);
    }

    private static IEnumerable<HeadingBlock> GetHeadings(ContainerBlock container)
    {
        foreach (var block in container)
        {
            if (block is HeadingBlock heading)
                yield return heading;
            if (block is ContainerBlock child)
                foreach (var nestedHeading in GetHeadings(child))
                    yield return nestedHeading;
        }
    }

    private static string HeadingTitle(string line)
    {
        return line.TrimStart()[2..].Trim().TrimEnd('#').TrimEnd();
    }
}
