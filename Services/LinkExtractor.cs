namespace DynaDocs.Services;

using System.Text.RegularExpressions;
using DynaDocs.Models;

internal static partial class LinkExtractor
{
    public static List<LinkInfo> Extract(string content)
    {
        var links = new List<LinkInfo>();
        var lines = content.Split('\n');
        var inCodeBlock = false;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var lineNumber = i + 1;

            if (line.TrimStart().StartsWith("```"))
            {
                inCodeBlock = !inCodeBlock;
                continue;
            }

            if (inCodeBlock)
                continue;

            foreach (Match match in MarkdownLinkRegex().Matches(line))
            {
                if (IsInsideInlineCode(line, match.Index))
                    continue;

                var target = match.Groups[2].Value;
                var (path, anchor) = SplitAnchor(target);

                var linkType = target.StartsWith("http://") || target.StartsWith("https://")
                    ? LinkType.External
                    : LinkType.Markdown;

                links.Add(new LinkInfo(
                    RawText: match.Value,
                    DisplayText: match.Groups[1].Value,
                    Target: path,
                    Anchor: anchor,
                    Type: linkType,
                    LineNumber: lineNumber
                ));
            }

            foreach (Match match in WikilinkRegex().Matches(line))
            {
                var path = match.Groups[1].Value;
                var (targetPath, anchor) = SplitAnchor(path);

                links.Add(new LinkInfo(
                    RawText: match.Value,
                    DisplayText: match.Groups[2].Success ? match.Groups[2].Value : path,
                    Target: targetPath,
                    Anchor: anchor,
                    Type: LinkType.Wikilink,
                    LineNumber: lineNumber
                ));
            }
        }

        return links;
    }

    private static (string path, string? anchor) SplitAnchor(string target)
    {
        var anchorIndex = target.IndexOf('#');
        if (anchorIndex == -1) return (target, null);
        return (target[..anchorIndex], target[(anchorIndex + 1)..]);
    }

    private static bool IsInsideInlineCode(string line, int position)
    {
        var backtickCount = 0;
        for (int i = 0; i < position && i < line.Length; i++)
        {
            if (line[i] == '`')
                backtickCount++;
        }
        return backtickCount % 2 == 1;
    }

    [GeneratedRegex(@"\[([^\]]+)\]\(([^)]+)\)")]
    private static partial Regex MarkdownLinkRegex();

    [GeneratedRegex(@"\[\[([^\]|]+)(?:\|([^\]]+))?\]\]")]
    private static partial Regex WikilinkRegex();
}
