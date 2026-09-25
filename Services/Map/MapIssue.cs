namespace DynaDocs.Services.Map;

/// <summary>One issue as the viewer sees it; <see cref="Assignee"/> is a display name.</summary>
internal sealed record MapIssue(
    string Id,
    string Identifier,
    string Title,
    string Url,
    MapIssueState State,
    string? Assignee,
    string? ParentId,
    MapIssueTeam Team,
    MapIssueProject? Project);
