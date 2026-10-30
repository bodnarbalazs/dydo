namespace DynaDocs.Services.Merge;

using System.Text.RegularExpressions;

/// <summary>
/// The reviewer verdicts in a PR body's <c>## Independent review</c> section, where doctrine puts them
/// verbatim: every line starting <c>PASS</c> or <c>FAIL</c>, a hyphen, en dash or em dash, then
/// <c>rubric/type:</c>, fenced or not. Of the fields after the rubric only <c>candidate</c> and
/// <c>base</c> are read, each the first of its kind, possibly backticked; any may be missing.
/// </summary>
internal static partial class IndependentReview
{
    public static List<ReviewVerdict> Parse(string body)
    {
        var section = SectionRegex().Match(body);
        if (!section.Success)
            return [];

        return [.. VerdictRegex().Matches(section.Groups["section"].Value).Select(verdict =>
        {
            var fields = verdict.Groups["fields"].Value;
            return new ReviewVerdict(verdict.Groups["verdict"].Value == "PASS",
                verdict.Groups["rubric"].Value.Trim().ToLowerInvariant(),
                Sha(CandidateRegex().Match(fields)), Sha(BaseRegex().Match(fields)), verdict.Value.Trim());
        })];
    }

    private static string? Sha(Match field) => field.Success ? field.Groups["sha"].Value.ToLowerInvariant() : null;

    // The section runs to the next level-one or level-two heading, as dydo pr-ready reads it.
    [GeneratedRegex(@"^##[ \t]+Independent review[ \t]*\r?$(?<section>[\s\S]*?)(?=^#{1,2}[ \t]|\z)",
        RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex SectionRegex();

    [GeneratedRegex(@"^[ \t]*(?<verdict>PASS|FAIL)[ \t]*[-–—][ \t]*rubric/type:(?<rubric>[^;\r\n]*)(?<fields>;[^\r\n]*)",
        RegexOptions.Multiline)]
    private static partial Regex VerdictRegex();

    // A field starts after a semicolon, so prose in Checks naming a "candidate" is not one.
    [GeneratedRegex(@";[ \t]*candidate[ \t]+`?(?<sha>[0-9A-Fa-f]{7,40})\b")]
    private static partial Regex CandidateRegex();

    [GeneratedRegex(@";[ \t]*base[ \t]+`?(?<sha>[0-9A-Fa-f]{7,40})\b")]
    private static partial Regex BaseRegex();
}
