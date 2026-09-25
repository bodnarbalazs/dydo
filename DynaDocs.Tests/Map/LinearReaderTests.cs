namespace DynaDocs.Tests.Map;

using DynaDocs.Services.Map;
using DynaDocs.Services.Map.Contract;
using static LinearJson;

public class LinearReaderTests
{
    private const string Done = """{"name":"Done","type":"completed","color":"#5e6ad2"}""";
    private const string Duplicate = """{"name":"Duplicate","type":"duplicate","color":"#95a2b3"}""";

    private readonly FakeLinear _linear = new();

    private Task<MapGraph> Graph() => _linear.Reader().GetGraphAsync("p-map", CancellationToken.None);

    [Fact]
    public async Task Teams_FollowEveryPage_SortedByName()
    {
        _linear.Serve(call => call.Var("after") == null
            ? """{"data":{"teams":{"nodes":[{"id":"t2","key":"ZED","name":"zeta"},{"id":"t1","key":"DYD","name":"Dydo"}],"pageInfo":{"hasNextPage":true,"endCursor":"c1"}}}}"""
            : """{"data":{"teams":{"nodes":[{"id":"t3","key":"ALP","name":"Alpha"}],"pageInfo":{"hasNextPage":false,"endCursor":"c2"}}}}""");

        var teams = await _linear.Reader().GetTeamsAsync(CancellationToken.None);

        Assert.Equal(["Alpha", "Dydo", "zeta"], teams.Select(team => team.Name));
        Assert.Equal(new MapTeam("t1", "DYD", "Dydo"), teams[1]);
        Assert.Equal([null, "c1"], _linear.Calls.Select(call => call.Var("after")));
    }

    [Fact]
    public async Task Projects_OfOneTeam_SortedByName_WithStatus()
    {
        _linear.Serve(_ => """
            {"data":{"team":{"projects":{"nodes":[
              {"id":"p2","name":"Visual map","url":"https://linear.app/x/project/p2","status":{"name":"In Progress","type":"started"}},
              {"id":"p1","name":"Consolidate","url":"https://linear.app/x/project/p1","status":{"name":"Completed","type":"completed"}}
            ],"pageInfo":{"hasNextPage":false,"endCursor":"c"}}}}}
            """);

        var projects = await _linear.Reader().GetProjectsAsync("t1", CancellationToken.None);

        Assert.Equal(["Consolidate", "Visual map"], projects.Select(project => project.Name));
        Assert.Equal(new MapProjectStatus("In Progress", "started"), projects[1].Status);
        Assert.Equal("https://linear.app/x/project/p2", projects[1].Url);
        Assert.Equal("t1", _linear.Calls.Single().Var("teamId"));
    }

    [Fact]
    public async Task Projects_OfUnknownTeam_AreNotFound()
    {
        _linear.Serve(_ => """{"data":{"team":null}}""");

        var error = await Assert.ThrowsAsync<MapApiException>(
            () => _linear.Reader().GetProjectsAsync("nope", CancellationToken.None));

        Assert.Equal(("not_found", 404), (error.Code, error.Status));
    }

    [Fact]
    public async Task Graph_OfUnknownProject_IsNotFound()
    {
        _linear.Serve(_ => """{"data":{"project":null}}""");

        var error = await Assert.ThrowsAsync<MapApiException>(Graph);

        Assert.Equal(("not_found", 404), (error.Code, error.Status));
    }

    [Fact]
    public async Task Graph_FollowsFull25IssuePages_UntilTheLastPage()
    {
        static string Page(int from, int count) =>
            string.Join(",", Enumerable.Range(from, count).Select(n => Node(Issue(n.ToString()))));
        _linear.Serve(call => call.Var("after") switch
        {
            null => IssuesPage(Page(1, 25), more: true, cursor: "p1"),
            "p1" => IssuesPage(Page(26, 25), more: true, cursor: "p2"),
            _ => IssuesPage(Page(51, 3)),
        });

        var graph = await Graph();

        Assert.Equal(Enumerable.Range(1, 53).Select(n => n.ToString()), graph.Issues.Select(issue => issue.Id));
        Assert.Equal([null, "p1", "p2"], _linear.Calls.Select(call => call.Var("after")));
    }

    [Fact]
    public async Task Graph_FollowsIssuePages_AndMapsIssueFields()
    {
        _linear.Serve(call => call.Var("after") == null
            ? IssuesPage(Node(Issue("1", assignee: """{"name":"Balazs"}""")), more: true, cursor: "page1")
            : IssuesPage(Node(Issue("2", state: Duplicate, parent: """{"id":"1"}"""))));

        var graph = await Graph();

        Assert.Equal(new MapGraphProject("p-map", "Map", "https://linear.app/x/project/map"), graph.Project);
        Assert.Equal(["1", "2"], graph.Issues.Select(issue => issue.Id));
        Assert.Equal(new MapIssue("1", "DYD-1", "Issue 1", "https://linear.app/x/issue/DYD-1",
            new MapIssueState("Todo", "unstarted", "#e2e2e2"), "Balazs", null,
            new MapIssueTeam("t-dyd", "DYD"), new MapIssueProject("p-map", "Map")), graph.Issues[0]);
        Assert.Equal("duplicate", graph.Issues[1].State.Type);
        Assert.Equal("1", graph.Issues[1].ParentId);
        Assert.Equal([null, "page1"], _linear.Calls.Select(call => call.Var("after")));
        Assert.All(_linear.Calls, call => Assert.Equal("p-map", call.Var("projectId")));
        Assert.Contains("issues(first: 25", _linear.Calls[0].Query);
        Assert.Contains("relations(first: 10", _linear.Calls[0].Query);
    }

    [Fact]
    public async Task Graph_SameProjectRelation_SeenFromBothEnds_IsKeptOnce()
    {
        _linear.Serve(_ => IssuesPage(
            Node(Issue("1"), relations: Connection(Outgoing("r1", "blocks", Issue("2")))) + "," +
            Node(Issue("2"), inverseRelations: Connection(Incoming("r1", "blocks", Issue("1"))))));

        var graph = await Graph();

        Assert.Equal([new MapRelation("r1", "blocks", "1", "2")], graph.Relations);
        Assert.Empty(graph.External);
    }

    [Fact]
    public async Task Graph_KeepsBlocksAndRelated_DropsDuplicateAndSimilar()
    {
        _linear.Serve(_ => IssuesPage(
            Node(Issue("1"), relations: Connection(string.Join(",",
                Outgoing("r-blocks", "blocks", Issue("2")),
                Outgoing("r-related", "related", Issue("2")),
                Outgoing("r-dup", "duplicate", Issue("2")),
                Outgoing("r-sim", "similar", Issue("2"))))) + "," +
            Node(Issue("2"))));

        var graph = await Graph();

        Assert.Equal(["r-blocks", "r-related"], graph.Relations.Select(relation => relation.Id));
    }

    [Fact]
    public async Task Graph_FarEndsOutsideTheProject_AreExternal_WithOrWithoutAProject()
    {
        _linear.Serve(_ => IssuesPage(
            Node(Issue("1"),
                relations: Connection(Outgoing("r-other", "related", Issue("9", project: InOther))),
                inverseRelations: Connection(string.Join(",",
                    Incoming("r-none", "blocks", Issue("8", project: "null")),
                    Incoming("r-other-2", "blocks", Issue("9", project: InOther)))))));

        var graph = await Graph();

        Assert.Equal(["1"], graph.Issues.Select(issue => issue.Id));
        Assert.Equal(["9", "8"], graph.External.Select(issue => issue.Id));
        Assert.Equal(new MapIssueProject("p-other", "Other"), graph.External[0].Project);
        Assert.Null(graph.External[1].Project);
        Assert.Equal(
            [new MapRelation("r-other", "related", "1", "9"), new MapRelation("r-none", "blocks", "8", "1"),
                new MapRelation("r-other-2", "blocks", "9", "1")],
            graph.Relations);
    }

    [Fact]
    public async Task Graph_DropsArchivedIssues_AndEveryRelationTouchingOne()
    {
        const string archived = "\"2026-09-01T00:00:00.000Z\"";
        _linear.Serve(_ => IssuesPage(
            Node(Issue("1"),
                relations: Connection(Outgoing("r-to-archived", "blocks", Issue("9", project: InOther, archivedAt: archived))),
                inverseRelations: Connection(Incoming("r-from-archived-member", "blocks", Issue("3", archivedAt: archived)))) + "," +
            Node(Issue("3", archivedAt: archived),
                relations: Connection(Outgoing("r-from-archived-member", "blocks", Issue("1"))))));

        var graph = await Graph();

        Assert.Equal(["1"], graph.Issues.Select(issue => issue.Id));
        Assert.Empty(graph.External);
        Assert.Empty(graph.Relations);
    }

    [Fact]
    public async Task Graph_ResolvedBlocker_StaysABlocksRelation_WithItsClosedState()
    {
        _linear.Serve(_ => IssuesPage(
            Node(Issue("1"), inverseRelations: Connection(Incoming("r1", "blocks", Issue("2", state: Done)))) + "," +
            Node(Issue("2", state: Done), relations: Connection(Outgoing("r1", "blocks", Issue("1"))))));

        var graph = await Graph();

        Assert.Equal([new MapRelation("r1", "blocks", "2", "1")], graph.Relations);
        Assert.Equal("completed", graph.Issues.Single(issue => issue.Id == "2").State.Type);
    }

    [Fact]
    public async Task Graph_NestedOverflow_FollowsIssueRelationsPage_UntilBothListsEnd()
    {
        _linear.Serve(call => (call.Operation, call.Var("after"), call.Var("invAfter")) switch
        {
            ("ProjectIssues", _, _) => IssuesPage(Node(Issue("1"),
                relations: Connection(Outgoing("r-a", "blocks", Issue("9", project: InOther)), more: true, cursor: "out1"),
                inverseRelations: Connection(Incoming("r-b", "related", Issue("8", project: InOther)), more: true, cursor: "in1"))),
            ("IssueRelationsPage", "out1", "in1") => $$"""
                {"data":{"issue":{
                  "relations":{{Connection(Outgoing("r-c", "blocks", Issue("7", project: InOther)), cursor: "out2")}},
                  "inverseRelations":{{Connection(Incoming("r-d", "blocks", Issue("6", project: InOther)), more: true, cursor: "in2")}} } } }
                """,
            // The finished outgoing list answers an empty page after its last cursor.
            ("IssueRelationsPage", "out2", "in2") => $$"""
                {"data":{"issue":{
                  "relations":{{Connection("", cursor: null)}},
                  "inverseRelations":{{Connection(Incoming("r-e", "blocks", Issue("5", project: "null")), cursor: "in3")}} } } }
                """,
            _ => throw new InvalidOperationException($"unexpected {call.Operation} {call.Var("after")} {call.Var("invAfter")}")
        });

        var graph = await Graph();

        Assert.Equal(["r-a", "r-b", "r-c", "r-d", "r-e"], graph.Relations.Select(relation => relation.Id));
        Assert.Equal(["9", "8", "7", "6", "5"], graph.External.Select(issue => issue.Id));
        Assert.Equal(2, _linear.CallsTo("IssueRelationsPage"));
        Assert.All(_linear.Calls.Where(call => call.Operation == "IssueRelationsPage"),
            call => Assert.Equal("1", call.Var("id")));
    }

    [Fact]
    public async Task Graph_OverflowOnAnIssueLinearNoLongerHas_IsNotFound()
    {
        _linear.Serve(call => call.Operation == "ProjectIssues"
            ? IssuesPage(Node(Issue("1"), relations: Connection("", more: true, cursor: "c")))
            : """{"data":{"issue":null}}""");

        var error = await Assert.ThrowsAsync<MapApiException>(Graph);

        Assert.Equal("not_found", error.Code);
    }
}
