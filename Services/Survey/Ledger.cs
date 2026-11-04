namespace DynaDocs.Services.Survey;

using System.Text.RegularExpressions;

/// <summary>
/// The lane ledger in a Project body: the section under the first <c>##</c> heading containing
/// "ledger" (any case), up to the next <c>##</c> heading. Its rows are the table rows and list items
/// that name at least one issue identifier, bare or inside a Linear link.
/// </summary>
internal static partial class Ledger
{
    /// <summary>The ledger's rows, or null when the body has no ledger section.</summary>
    public static List<LedgerRow>? Parse(string content)
    {
        var lines = content.ReplaceLineEndings("\n").Split('\n');
        var heading = Array.FindIndex(lines,
            line => Heading().IsMatch(line) && line.Contains("ledger", StringComparison.OrdinalIgnoreCase));
        if (heading < 0)
            return null;

        return [.. lines.Skip(heading + 1)
            .TakeWhile(line => !Heading().IsMatch(line))
            .Where(line => Row().IsMatch(line))
            .Select(line => new LedgerRow(line.Trim(), [.. Key().Matches(line).Select(key => key.Value).Distinct()]))
            .Where(row => row.Keys.Count > 0)];
    }

    [GeneratedRegex(@"^##\s")]
    private static partial Regex Heading();

    // A table row starts with a pipe; a list item with -, * or +, or a number and . or ).
    [GeneratedRegex(@"^\s*(\||[-*+]\s|\d+[.)]\s)")]
    private static partial Regex Row();

    [GeneratedRegex(@"\b[A-Z][A-Z0-9]*-[0-9]+\b")]
    private static partial Regex Key();
}
