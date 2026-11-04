namespace DynaDocs.Services.Survey;

using System.Text.RegularExpressions;
using DynaDocs.Services.Map;
using DynaDocs.Services.Map.Contract;

/// <summary>
/// The survey's finding rules over one Project read. Closed means a <c>completed</c>, <c>canceled</c> or
/// <c>duplicate</c> state; every other state type, backlog and triage included, is open. A blocker is a
/// <c>blocks</c> relation in the graph, so a linked issue's blockers outside the Project are not seen.
/// A ledger row is judged on the identifiers it names that are in the graph, and on the text it shows.
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
            .. unblocked.Select(issue => Finding("unblocked", issue, UnblockedReason(issue, graph, blockers[issue.Id]))),
            .. open.Where(issue => issue.State.Type == "unstarted" && issue.Assignee is null
                    && blockers[issue.Id].All(IsClosed) && !unblocked.Contains(issue))
                .Select(issue => Finding("pickable", issue, "unstarted, unassigned, no open blocker")),
            .. open.Where(issue => issue.State.Type == "backlog" && issue.Assignee is null && blockers[issue.Id].All(IsClosed))
                .Select(issue => Finding("uncontracted", issue, "Backlog, unassigned, no open blocker")),
            .. open.Where(issue => issue.State.Type == "started" && issue.Assignee is null)
                .Select(issue => Finding("started-unassigned", issue, $"{issue.State.Name}, with no assignee")),
            .. open.Where(issue => read.Priorities[issue.Id] == 0
                    && issue.Labels.Any(label => label.Name.Equals("Question", StringComparison.OrdinalIgnoreCase)))
                .Select(issue => Finding("question-no-priority", issue, "an open Question with no priority")),
            .. ledger is null ? NoLedger(graph, open) : LedgerFindings(ledger, open, byId, read.Parents),
        ];

        return [.. findings
            .OrderBy(finding => finding.Kind, StringComparer.Ordinal)
            .ThenBy(finding => KeyPrefix(finding.Key), StringComparer.Ordinal)
            .ThenBy(finding => KeyNumber(finding.Key))];
    }

    // A linked issue's blockers outside the Project are not in the graph, so its reason says so.
    private static string UnblockedReason(MapIssue issue, MapGraph graph, IEnumerable<MapIssue> blockers) =>
        (graph.Issues.Contains(issue) ? "every blocker is closed: " : "every blocker in the Project's graph is closed: ")
        + string.Join(", ", blockers.Select(blocker => blocker.Identifier).Distinct()
            .OrderBy(KeyPrefix, StringComparer.Ordinal).ThenBy(KeyNumber));

    private static IEnumerable<SurveyFinding> NoLedger(MapGraph graph, List<MapIssue> open) =>
    [
        new("no-ledger", "-", graph.Project.Name, "the Project body has no ## heading containing \"ledger\""),
        .. open.Select(issue => Finding("unlaned", issue, "open, and the Project has no ledger")),
    ];

    private static IEnumerable<SurveyFinding> LedgerFindings(
        List<LedgerRow> ledger, List<MapIssue> open, Dictionary<string, MapIssue> byId,
        IReadOnlyDictionary<string, string> parents)
    {
        var named = ledger.SelectMany(row => row.Keys).ToHashSet();
        var byKey = byId.Values.ToDictionary(issue => issue.Identifier);

        bool Laned(MapIssue issue) =>
            named.Contains(issue.Identifier) || parents.TryGetValue(issue.Id, out var parent) && named.Contains(parent);

        return
        [
            .. open.Where(issue => !Laned(issue))
                .Select(issue => Finding("unlaned", issue, "open, and no ledger row names it or its parent")),
            .. ledger.Where(row => !ReadsAsDone().IsMatch(row.Text))
                .Select(row => row.Keys.Where(byKey.ContainsKey).Select(key => byKey[key]).ToList())
                .Where(issues => issues.Count > 0 && issues.All(IsClosed))
                .Select(issues => Finding("stale-row", issues[0], "the row names only closed issues: "
                    + string.Join(", ", issues.Select(issue => $"{issue.Identifier} {issue.State.Name}")))),
        ];
    }

    private static SurveyFinding Finding(string kind, MapIssue issue, string reason) =>
        new(kind, issue.Identifier, issue.Title, reason);

    private static bool IsClosed(MapIssue issue) => ClosedTypes.Contains(issue.State.Type);

    private static bool IsOpen(MapIssue issue) => !IsClosed(issue);

    private static string KeyPrefix(string key) => key[..(key.LastIndexOf('-') + 1)];

    private static int KeyNumber(string key) => int.TryParse(key[(key.LastIndexOf('-') + 1)..], out var number) ? number : 0;

    // "not done", "isn't merged", "hasn't been merged" and "never landed" do not read as done.
    [GeneratedRegex(@"(?<!(?:\bnot|\bnever|n['’]t)\s+(?:(?:yet|been)\s+)*)\b(done|merged|closed|completed|canceled|cancelled|shipped|landed)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex ReadsAsDone();
}
