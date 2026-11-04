namespace DynaDocs.Services.Survey;

using System.Text.RegularExpressions;
using DynaDocs.Services.Map;
using DynaDocs.Services.Map.Contract;

/// <summary>
/// The survey's finding rules over one Project read. Closed means a <c>completed</c>, <c>canceled</c> or
/// <c>duplicate</c> state; every other state type, backlog and triage included, is open. A blocker is a
/// <c>blocks</c> relation in the graph, so a linked issue's blockers outside the Project are not seen.
/// </summary>
internal static partial class SurveyRules
{
    private static readonly HashSet<string> ClosedTypes = ["completed", "canceled", "duplicate"];

    public static List<SurveyFinding> Find(LinearProjectRead read)
    {
        var graph = read.Graph;
        var byId = graph.Issues.Concat(graph.External).ToDictionary(issue => issue.Id);
        var blockers = graph.Relations.Where(relation => relation.Type == "blocks")
            .ToLookup(relation => relation.To, relation => byId[relation.From]);
        var ledger = Ledger.Parse(read.Content);

        var unblocked = byId.Values
            .Where(issue => IsOpen(issue) && issue.Assignee is null && blockers[issue.Id].Any() && blockers[issue.Id].All(IsClosed))
            .ToList();
        var open = graph.Issues.Where(IsOpen).ToList();

        IEnumerable<SurveyFinding> findings =
        [
            .. unblocked.Select(issue => Finding("unblocked", issue,
                "every blocker is closed: " + string.Join(", ", blockers[issue.Id].Select(blocker => blocker.Identifier)))),
            .. open.Where(issue => issue.State.Type == "unstarted" && issue.Assignee is null
                    && blockers[issue.Id].All(IsClosed) && !unblocked.Contains(issue))
                .Select(issue => Finding("pickable", issue, "unstarted, unassigned, no open blocker")),
            .. open.Where(issue => issue.State.Type == "started" && issue.Assignee is null)
                .Select(issue => Finding("started-unassigned", issue, $"{issue.State.Name}, with no assignee")),
            .. open.Where(issue => read.Priorities[issue.Id] == 0
                    && issue.Labels.Any(label => label.Name.Equals("Question", StringComparison.OrdinalIgnoreCase)))
                .Select(issue => Finding("question-no-priority", issue, "an open Question with no priority")),
            .. ledger is null ? NoLedger(graph, open) : LedgerFindings(ledger, open, byId),
        ];

        return [.. findings
            .OrderBy(finding => finding.Kind, StringComparer.Ordinal)
            .ThenBy(finding => KeyPrefix(finding.Key), StringComparer.Ordinal)
            .ThenBy(finding => KeyNumber(finding.Key))];
    }

    private static IEnumerable<SurveyFinding> NoLedger(MapGraph graph, List<MapIssue> open) =>
    [
        new("no-ledger", "-", graph.Project.Name, "the Project body has no ## heading containing \"ledger\""),
        .. open.Select(issue => Finding("unlaned", issue, "open, and the Project has no ledger")),
    ];

    private static IEnumerable<SurveyFinding> LedgerFindings(
        List<LedgerRow> ledger, List<MapIssue> open, Dictionary<string, MapIssue> byId)
    {
        var named = ledger.SelectMany(row => row.Keys).ToHashSet();
        var byKey = byId.Values.ToDictionary(issue => issue.Identifier);

        bool Laned(MapIssue issue) =>
            named.Contains(issue.Identifier)
            || issue.ParentId is { } parentId && byId.TryGetValue(parentId, out var parent) && named.Contains(parent.Identifier);

        return
        [
            .. open.Where(issue => !Laned(issue))
                .Select(issue => Finding("unlaned", issue, "open, and no ledger row names it or its parent")),
            .. ledger.Where(row => !ReadsAsDone().IsMatch(row.Text)
                    && row.Keys.All(key => byKey.TryGetValue(key, out var issue) && IsClosed(issue)))
                .Select(row => Finding("stale-row", byKey[row.Keys[0]], "the row names only closed issues: "
                    + string.Join(", ", row.Keys.Select(key => $"{key} {byKey[key].State.Name}")))),
        ];
    }

    private static SurveyFinding Finding(string kind, MapIssue issue, string reason) =>
        new(kind, issue.Identifier, issue.Title, reason);

    private static bool IsClosed(MapIssue issue) => ClosedTypes.Contains(issue.State.Type);

    private static bool IsOpen(MapIssue issue) => !IsClosed(issue);

    private static string KeyPrefix(string key) => key[..(key.LastIndexOf('-') + 1)];

    private static int KeyNumber(string key) => int.TryParse(key[(key.LastIndexOf('-') + 1)..], out var number) ? number : 0;

    [GeneratedRegex(@"\b(done|merged|closed|completed|canceled|cancelled|shipped|landed)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ReadsAsDone();
}
