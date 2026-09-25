namespace DynaDocs.Services.Map;

using System.Text.Json;

/// <summary>
/// Reads teams, a team's projects, and one Project's work graph from Linear, fresh on every call,
/// and normalizes them into the map API's shapes. The query set is DYD-261's: every connection
/// passes `first` so one page stays under Linear's 10k complexity cap, and a nested relation list
/// that overflows its first page is followed with `IssueRelationsPage`.
/// </summary>
internal sealed class LinearReader(LinearGraphQL linear)
{
    private const string IssueFields = """
        fragment IssueFields on Issue {
          id identifier title url archivedAt
          state { name type color }
          assignee { name }
          parent { id }
          team { id key }
          project { id name }
        }
        """;

    private const string TeamsQuery = """
        query Teams($after: String) {
          teams(first: 100, after: $after) {
            nodes { id key name }
            pageInfo { hasNextPage endCursor }
          }
        }
        """;

    private const string TeamProjectsQuery = """
        query TeamProjects($teamId: String!, $after: String) {
          team(id: $teamId) {
            projects(first: 100, after: $after) {
              nodes { id name url status { name type } }
              pageInfo { hasNextPage endCursor }
            }
          }
        }
        """;

    private const string ProjectIssuesQuery = """
        query ProjectIssues($projectId: String!, $after: String) {
          project(id: $projectId) {
            id name url
            issues(first: 25, after: $after) {
              pageInfo { hasNextPage endCursor }
              nodes {
                ...IssueFields
                relations(first: 10) {
                  pageInfo { hasNextPage endCursor }
                  nodes { id type relatedIssue { ...IssueFields } }
                }
                inverseRelations(first: 10) {
                  pageInfo { hasNextPage endCursor }
                  nodes { id type issue { ...IssueFields } }
                }
              }
            }
          }
        }
        """ + IssueFields;

    private const string IssueRelationsPageQuery = """
        query IssueRelationsPage($id: String!, $after: String, $invAfter: String) {
          issue(id: $id) {
            relations(first: 50, after: $after) {
              pageInfo { hasNextPage endCursor }
              nodes { id type relatedIssue { ...IssueFields } }
            }
            inverseRelations(first: 50, after: $invAfter) {
              pageInfo { hasNextPage endCursor }
              nodes { id type issue { ...IssueFields } }
            }
          }
        }
        """ + IssueFields;

    public async Task<List<MapTeam>> GetTeamsAsync(CancellationToken ct)
    {
        var teams = new List<MapTeam>();
        await PageAsync(TeamsQuery, new(), data => data.GetProperty("teams"),
            node => teams.Add(new MapTeam(Text(node, "id"), Text(node, "key"), Text(node, "name"))), ct);
        return [.. teams.OrderBy(team => team.Name, StringComparer.OrdinalIgnoreCase)];
    }

    public async Task<List<MapProject>> GetProjectsAsync(string teamId, CancellationToken ct)
    {
        var projects = new List<MapProject>();
        await PageAsync(TeamProjectsQuery, new() { ["teamId"] = teamId },
            data => Found(data, "team", $"Linear has no team {teamId}.").GetProperty("projects"),
            node => projects.Add(ToProject(node)), ct);
        return [.. projects.OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase)];
    }

    public async Task<MapGraph> GetGraphAsync(string projectId, CancellationToken ct)
    {
        JsonElement project = default;
        var nodes = new List<JsonElement>();
        await PageAsync(ProjectIssuesQuery, new() { ["projectId"] = projectId },
            data =>
            {
                project = Found(data, "project", $"Linear has no project {projectId}.");
                return project.GetProperty("issues");
            },
            nodes.Add, ct);

        var kept = nodes.Where(node => !IsArchived(node)).ToList();
        var sightings = new List<Sighting>();
        foreach (var node in kept)
            await CollectRelationsAsync(node, sightings, ct);

        return Normalize(project, kept, sightings);
    }

    private static MapGraph Normalize(JsonElement project, List<JsonElement> nodes, List<Sighting> sightings)
    {
        var issues = nodes.Select(ToIssue).ToList();
        var present = issues.Select(issue => issue.Id).ToHashSet();
        var external = new List<MapIssue>();
        var relations = new List<MapRelation>();
        var seen = new HashSet<string>();

        foreach (var (id, type, from, to, farEnd) in sightings)
        {
            // An archived far end drops the relation with it; the same-Project relation is seen from
            // both ends, so the relation id decides.
            if (type is not ("blocks" or "related") || IsArchived(farEnd) || !seen.Add(id))
                continue;

            var far = ToIssue(farEnd);
            if (present.Add(far.Id))
                external.Add(far);
            relations.Add(new MapRelation(id, type, from, to));
        }

        return new MapGraph(
            new MapGraphProject(Text(project, "id"), Text(project, "name"), Text(project, "url")),
            issues, external, relations);
    }

    private async Task CollectRelationsAsync(JsonElement node, List<Sighting> sightings, CancellationToken ct)
    {
        var issueId = Text(node, "id");
        var outgoing = node.GetProperty("relations");
        var incoming = node.GetProperty("inverseRelations");
        AddOutgoing(issueId, outgoing, sightings);
        AddIncoming(issueId, incoming, sightings);

        var (moreOut, outCursor) = PageState(outgoing);
        var (moreIn, inCursor) = PageState(incoming);
        while (moreOut || moreIn)
        {
            var issue = Found(
                await linear.QueryAsync(IssueRelationsPageQuery,
                    new() { ["id"] = issueId, ["after"] = outCursor, ["invAfter"] = inCursor }, ct),
                "issue", $"Linear has no issue {issueId}.");

            // A finished connection keeps its last cursor, so it answers an empty page; skip it.
            if (moreOut)
            {
                outgoing = issue.GetProperty("relations");
                AddOutgoing(issueId, outgoing, sightings);
                (moreOut, outCursor) = PageState(outgoing);
            }

            if (moreIn)
            {
                incoming = issue.GetProperty("inverseRelations");
                AddIncoming(issueId, incoming, sightings);
                (moreIn, inCursor) = PageState(incoming);
            }
        }
    }

    // `relations` lists the issue as the source: {type: blocks, relatedIssue: B} means it blocks B.
    private static void AddOutgoing(string issueId, JsonElement connection, List<Sighting> sightings)
    {
        foreach (var relation in connection.GetProperty("nodes").EnumerateArray())
        {
            var far = relation.GetProperty("relatedIssue");
            sightings.Add(new(Text(relation, "id"), Text(relation, "type"), issueId, Text(far, "id"), far));
        }
    }

    // `inverseRelations` lists the issue as the target: {type: blocks, issue: A} means A blocks it.
    private static void AddIncoming(string issueId, JsonElement connection, List<Sighting> sightings)
    {
        foreach (var relation in connection.GetProperty("nodes").EnumerateArray())
        {
            var far = relation.GetProperty("issue");
            sightings.Add(new(Text(relation, "id"), Text(relation, "type"), Text(far, "id"), issueId, far));
        }
    }

    private async Task PageAsync(
        string query,
        Dictionary<string, string?> variables,
        Func<JsonElement, JsonElement> connectionOf,
        Action<JsonElement> onNode,
        CancellationToken ct)
    {
        var more = true;
        while (more)
        {
            var connection = connectionOf(await linear.QueryAsync(query, variables, ct));
            foreach (var node in connection.GetProperty("nodes").EnumerateArray())
                onNode(node);
            (more, variables["after"]) = PageState(connection);
        }
    }

    private static (bool More, string? Cursor) PageState(JsonElement connection)
    {
        var pageInfo = connection.GetProperty("pageInfo");
        return (pageInfo.GetProperty("hasNextPage").GetBoolean(), OptionalText(pageInfo, "endCursor"));
    }

    private static JsonElement Found(JsonElement data, string field, string notFound) =>
        Optional(data, field) ?? throw MapApiException.NotFound(notFound);

    private static MapProject ToProject(JsonElement node)
    {
        var status = node.GetProperty("status");
        return new MapProject(Text(node, "id"), Text(node, "name"), Text(node, "url"),
            new MapProjectStatus(Text(status, "name"), Text(status, "type")));
    }

    private static MapIssue ToIssue(JsonElement node)
    {
        var state = node.GetProperty("state");
        var team = node.GetProperty("team");
        return new MapIssue(
            Text(node, "id"), Text(node, "identifier"), Text(node, "title"), Text(node, "url"),
            new MapIssueState(Text(state, "name"), Text(state, "type"), Text(state, "color")),
            Optional(node, "assignee") is { } assignee ? Text(assignee, "name") : null,
            Optional(node, "parent") is { } parent ? Text(parent, "id") : null,
            new MapIssueTeam(Text(team, "id"), Text(team, "key")),
            Optional(node, "project") is { } project ? new MapIssueProject(Text(project, "id"), Text(project, "name")) : null);
    }

    private static bool IsArchived(JsonElement issue) => OptionalText(issue, "archivedAt") != null;

    private static JsonElement? Optional(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value : null;

    private static string? OptionalText(JsonElement element, string name) => Optional(element, name)?.GetString();

    private static string Text(JsonElement element, string name) =>
        element.GetProperty(name).GetString() ?? throw new InvalidOperationException($"Linear sent null {name}.");

    private readonly record struct Sighting(string Id, string Type, string From, string To, JsonElement FarEnd);
}
