namespace DynaDocs.Services.Map;

using DynaDocs.Services.Map.Contract;

/// <summary>
/// One Project read for <c>dydo survey</c>: the map's graph, the Project body's markdown, each graph
/// issue's Linear priority by issue id (0 is no priority), and each Project issue's parent identifier by
/// issue id, for parents inside the graph or not. It never reaches the viewer.
/// </summary>
internal sealed record LinearProjectRead(
    MapGraph Graph,
    string Content,
    IReadOnlyDictionary<string, double> Priorities,
    IReadOnlyDictionary<string, string> Parents);
