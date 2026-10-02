namespace DynaDocs.Services.Map.Contract;

/// <summary>
/// One issue as the viewer sees it; <see cref="Assignee"/> is a display name. <see cref="Labels"/> is
/// always present and empty when the issue has none.
/// </summary>
internal sealed record MapIssue(
    string Id,
    string Identifier,
    string Title,
    string Url,
    MapIssueState State,
    string? Assignee,
    string? ParentId,
    MapIssueTeam Team,
    MapIssueProject? Project,
    List<MapLabel> Labels);
