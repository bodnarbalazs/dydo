namespace DynaDocs.Services.Map;

/// <summary>
/// One Project's work graph: its non-archived issues, the non-archived far ends outside it, and the
/// relations between them, deduplicated by id.
/// </summary>
internal sealed record MapGraph(
    MapGraphProject Project,
    List<MapIssue> Issues,
    List<MapIssue> External,
    List<MapRelation> Relations);
