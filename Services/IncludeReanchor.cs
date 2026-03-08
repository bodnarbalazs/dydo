namespace DynaDocs.Services;

using System.Text.RegularExpressions;

public static class IncludeReanchor
{
    public record IncludeTag(string Tag, string? UpperAnchor, string? LowerAnchor);
    public record ReanchorResult(string Content, List<string> Placed, List<string> Unplaced);

    private static readonly Regex IncludePattern = new(@"\{\{include:([a-zA-Z0-9_-]+)\}\}");

    public static List<IncludeTag> ExtractUserIncludes(string stockContent, string userContent)
    {
        var stockTags = new HashSet<string>(
            IncludePattern.Matches(stockContent).Select(m => m.Value));

        var userLines = userContent.Split('\n');
        var result = new List<IncludeTag>();

        for (var i = 0; i < userLines.Length; i++)
        {
            var line = userLines[i].Trim();
            var match = IncludePattern.Match(line);
            if (!match.Success || line != match.Value) continue;
            if (stockTags.Contains(match.Value)) continue;

            var upper = FindNearestNonBlank(userLines, i, -1);
            var lower = FindNearestNonBlank(userLines, i, 1);
            result.Add(new IncludeTag(match.Value, upper, lower));
        }

        return result;
    }

    public static ReanchorResult Reanchor(string newContent, List<IncludeTag> userIncludes)
    {
        var placed = new List<string>();
        var unplaced = new List<string>();
        var lines = newContent.Split('\n').ToList();

        foreach (var include in userIncludes)
        {
            var upperIdx = include.UpperAnchor != null
                ? FindLineIndex(lines, include.UpperAnchor) : -1;
            var lowerIdx = include.LowerAnchor != null
                ? FindLineIndex(lines, include.LowerAnchor) : -1;

            int insertAt;
            if (upperIdx >= 0)
                insertAt = upperIdx + 1;
            else if (lowerIdx >= 0)
                insertAt = lowerIdx;
            else
            {
                unplaced.Add(include.Tag);
                continue;
            }

            lines.Insert(insertAt, include.Tag);
            placed.Add(include.Tag);
        }

        return new ReanchorResult(string.Join('\n', lines), placed, unplaced);
    }

    private static string? FindNearestNonBlank(string[] lines, int from, int direction)
    {
        var i = from + direction;
        while (i >= 0 && i < lines.Length)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]))
                return lines[i].Trim();
            i += direction;
        }
        return null;
    }

    private static int FindLineIndex(List<string> lines, string anchor)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].Trim() == anchor)
                return i;
        }
        return -1;
    }
}
