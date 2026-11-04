namespace DynaDocs.Tests.Survey;

using DynaDocs.Services.Map;
using DynaDocs.Services.Map.Contract;
using DynaDocs.Services.Survey;

public class SurveyRulesTests
{
    private static readonly Dictionary<string, string> StateNames = new()
    {
        ["triage"] = "Triage", ["backlog"] = "Backlog", ["unstarted"] = "Todo", ["started"] = "In Progress",
        ["completed"] = "Done", ["canceled"] = "Canceled", ["duplicate"] = "Duplicate",
    };

    private static MapIssue Issue(
        int number, string type = "unstarted", string? assignee = null, int? parent = null,
        bool external = false, params string[] labels) => new(
        $"id-{number}", $"DYD-{number}", $"Issue {number}", $"https://linear.app/x/issue/DYD-{number}",
        new MapIssueState(StateNames[type], type, "#000000"), assignee, parent is { } p ? $"id-{p}" : null,
        new MapIssueTeam("t-dyd", "DYD"),
        external ? new MapIssueProject("p-other", "Other") : new MapIssueProject("p-map", "Map"),
        [.. labels.Select(label => new MapLabel(label, "#000000"))]);

    private static MapRelation Blocks(int from, int to) => new($"r-{from}-{to}", "blocks", $"id-{from}", $"id-{to}");

    private static MapRelation Related(int from, int to) => new($"r-{from}-{to}", "related", $"id-{from}", $"id-{to}");

    // A ledger naming every open Project issue, so tests about other kinds see no unlaned findings.
    private static string LedgerOf(IEnumerable<MapIssue> issues) =>
        "## Lane ledger\n" + string.Concat(issues
            .Where(issue => issue.State.Type is not ("completed" or "canceled" or "duplicate"))
            .Select(issue => $"- {issue.Identifier} lane, in progress\n"));

    private static List<SurveyFinding> Survey(
        List<MapIssue> issues, List<MapIssue>? external = null, List<MapRelation>? relations = null,
        string? content = null, Dictionary<string, double>? priorities = null)
    {
        external ??= [];
        var graph = new MapGraph(new MapGraphProject("p-map", "Map", "https://linear.app/x/project/map"),
            issues, external, relations ?? []);
        priorities ??= [];
        foreach (var issue in issues.Concat(external))
            priorities.TryAdd(issue.Id, 0);
        return SurveyRules.Find(new LinearProjectRead(graph, content ?? LedgerOf(issues), priorities));
    }

    private static List<string> Lines(List<SurveyFinding> findings) => [.. findings.Select(finding => finding.ToString())];

    private static List<string> Kinds(List<SurveyFinding> findings, string kind) =>
        [.. findings.Where(finding => finding.Kind == kind).Select(finding => finding.Key)];

    [Theory]
    [InlineData("triage")]
    [InlineData("backlog")]
    [InlineData("unstarted")]
    [InlineData("started")]
    public void Unblocked_AnOpenUnassignedIssue_WhoseEveryBlockerIsClosed(string type)
    {
        var findings = Survey(
            [Issue(1, type), Issue(2, "completed"), Issue(3, "canceled")],
            relations: [Blocks(2, 1), Blocks(3, 1)]);

        Assert.Contains("unblocked DYD-1 Issue 1 — every blocker is closed: DYD-2, DYD-3", Lines(findings));
    }

    [Fact]
    public void Unblocked_IncludesAnIssueLinkedFromTheProject()
    {
        var findings = Survey([Issue(1, "duplicate")], external: [Issue(9, "backlog", external: true)],
            relations: [Blocks(1, 9)]);

        Assert.Equal(["DYD-9"], Kinds(findings, "unblocked"));
    }

    [Fact]
    public void Unblocked_NotWhileABlockerIsOpen_NorWhenAssigned_Closed_OrOnlyRelated()
    {
        var findings = Survey(
            [Issue(1), Issue(2, "completed"), Issue(3, "backlog"), Issue(4, assignee: "Ada"), Issue(5, "canceled"), Issue(6)],
            relations: [Blocks(2, 1), Blocks(3, 1), Blocks(2, 4), Blocks(2, 5), Related(2, 6)]);

        Assert.Empty(Kinds(findings, "unblocked"));
    }

    [Fact]
    public void Pickable_IsTheViewersRule_MinusIssuesAlreadyUnblocked()
    {
        var findings = Survey(
            [Issue(1), Issue(2), Issue(3, "completed"), Issue(4, "backlog"), Issue(5), Issue(6, "started"), Issue(7, assignee: "Ada")],
            external: [Issue(9, external: true)],
            relations: [Blocks(3, 2), Blocks(4, 5)]);

        Assert.Equal(["DYD-1"], Kinds(findings, "pickable"));
        Assert.Equal(["DYD-2"], Kinds(findings, "unblocked"));
        Assert.Contains("pickable DYD-1 Issue 1 — unstarted, unassigned, no open blocker", Lines(findings));
    }

    [Fact]
    public void Unlaned_AnOpenProjectIssueNoLedgerRowNames_UnlessItsParentIsNamed()
    {
        var findings = Survey(
            [Issue(1, "started", "Ada"), Issue(2, "backlog", "Ada"), Issue(3, "unstarted", "Ada", parent: 1),
                Issue(4, "unstarted", "Ada", parent: 2), Issue(5, "completed")],
            external: [Issue(9, "started", "Ada", external: true)],
            content: "## Lane ledger\n| Lane | Ticket |\n| -- | -- |\n| Core | [DYD-1](https://linear.app/x/issue/DYD-1/core) |\n");

        Assert.Equal(["DYD-2", "DYD-4"], Kinds(findings, "unlaned"));
        Assert.Contains("unlaned DYD-2 Issue 2 — open, and no ledger row names it or its parent", Lines(findings));
    }

    [Fact]
    public void StaleRow_NamesOnlyClosedIssues_AndDoesNotReadAsDone()
    {
        var findings = Survey(
            [Issue(1, "completed"), Issue(2, "canceled"), Issue(3, "completed"), Issue(4, "completed"), Issue(5, "started", "Ada")],
            content: """
                ## Lane ledger
                | Lane | DYD-1, DYD-2 | waiting on review |
                - DYD-3 lane: Done
                - DYD-4 and DYD-5, still running
                - DYD-4 and LC-77, waiting on another team
                """);

        Assert.Equal(
            ["stale-row DYD-1 Issue 1 — the row names only closed issues: DYD-1 Done, DYD-2 Canceled"],
            Lines(findings).Where(line => line.StartsWith("stale-row", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("merged")]
    [InlineData("Closed")]
    [InlineData("completed")]
    [InlineData("canceled")]
    [InlineData("cancelled")]
    public void StaleRow_ARowThatReadsAsDone_IsNotStale(string word)
    {
        var findings = Survey([Issue(1, "completed")], content: $"## Lane ledger\n- DYD-1 lane: {word}\n");

        Assert.Empty(Kinds(findings, "stale-row"));
    }

    [Fact]
    public void StartedUnassigned_AStartedIssueWithNoAssignee()
    {
        var findings = Survey([Issue(1, "started"), Issue(2, "started", "Ada"), Issue(3)]);

        Assert.Equal(["started-unassigned DYD-1 Issue 1 — In Progress, with no assignee"],
            Lines(findings).Where(line => line.StartsWith("started-unassigned", StringComparison.Ordinal)));
    }

    [Fact]
    public void QuestionNoPriority_AnOpenQuestionWithNoPriority()
    {
        var findings = Survey(
            [Issue(1, "backlog", "Ada", labels: "Question"), Issue(2, "backlog", "Ada", labels: "Question"),
                Issue(3, "completed", labels: "Question"), Issue(4, "backlog", "Ada", labels: "Feature")],
            priorities: new() { ["id-2"] = 3 });

        Assert.Equal(["question-no-priority DYD-1 Issue 1 — an open Question with no priority"],
            Lines(findings).Where(line => line.StartsWith("question-no-priority", StringComparison.Ordinal)));
    }

    [Fact]
    public void NoLedger_IsOneFinding_AndEveryOpenProjectIssueIsUnlaned()
    {
        var findings = Survey([Issue(1, "started", "Ada"), Issue(2, "completed"), Issue(3, "backlog", "Ada")],
            content: "## Lanes\n- DYD-1 lane\n");

        Assert.Equal(
            [
                "no-ledger - Map — the Project body has no ## heading containing \"ledger\"",
                "unlaned DYD-1 Issue 1 — open, and the Project has no ledger",
                "unlaned DYD-3 Issue 3 — open, and the Project has no ledger",
            ],
            Lines(findings));
    }

    [Fact]
    public void Findings_AreSortedByKind_ThenByKeyNumber()
    {
        var findings = Survey(
            [Issue(10, "started"), Issue(9, "started"), Issue(2), Issue(1, "started", "Ada")],
            content: "## Ledger\n- DYD-1 lane\n");

        Assert.Equal(
            ["pickable DYD-2", "started-unassigned DYD-9", "started-unassigned DYD-10",
                "unlaned DYD-2", "unlaned DYD-9", "unlaned DYD-10"],
            findings.Select(finding => $"{finding.Kind} {finding.Key}"));
    }

    [Fact]
    public void AQuietProject_HasNoFindings()
    {
        Assert.Empty(Survey([Issue(1, "started", "Ada"), Issue(2, "completed")]));
    }
}
