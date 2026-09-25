namespace DynaDocs.Tests.Map;

/// <summary>
/// Hand-written fragments of Linear's GraphQL answers, in the shape the map's queries select.
/// Project "p-map" / "Map" on team DYD is the Project under test.
/// </summary>
internal static class LinearJson
{
    public const string InMap = """{"id":"p-map","name":"Map"}""";
    public const string InOther = """{"id":"p-other","name":"Other"}""";

    public static string Issue(
        string id,
        string state = """{"name":"Todo","type":"unstarted","color":"#e2e2e2"}""",
        string project = InMap,
        string assignee = "null",
        string parent = "null",
        string archivedAt = "null") => $$"""
        "id":"{{id}}","identifier":"DYD-{{id}}","title":"Issue {{id}}","url":"https://linear.app/x/issue/DYD-{{id}}",
        "archivedAt":{{archivedAt}},"state":{{state}},"assignee":{{assignee}},"parent":{{parent}},
        "team":{"id":"t-dyd","key":"DYD"},"project":{{project}}
        """;

    public static string Connection(string nodes, bool more = false, string? cursor = null) => $$"""
        {"pageInfo":{"hasNextPage":{{(more ? "true" : "false")}},"endCursor":{{(cursor == null ? "null" : $"\"{cursor}\"")}}},"nodes":[{{nodes}}]}
        """;

    public static string Outgoing(string relationId, string type, string farEnd) =>
        $$"""{"id":"{{relationId}}","type":"{{type}}","relatedIssue":{{{farEnd}}} }""";

    public static string Incoming(string relationId, string type, string farEnd) =>
        $$"""{"id":"{{relationId}}","type":"{{type}}","issue":{{{farEnd}}} }""";

    public static string Node(string issue, string? relations = null, string? inverseRelations = null) =>
        $$"""{{{issue}},"relations":{{relations ?? Connection("")}},"inverseRelations":{{inverseRelations ?? Connection("")}}}""";

    public static string IssuesPage(string nodes, bool more = false, string? cursor = null) =>
        $$"""{"data":{"project":{"id":"p-map","name":"Map","url":"https://linear.app/x/project/map","issues":{{Connection(nodes, more, cursor)}} } } }""";
}
