namespace DynaDocs.Services.Map.Contract;

/// <summary>The last successfully fetched graph and the UTC time of that fetch.</summary>
internal sealed record MapSnapshot(MapGraph Graph, DateTimeOffset FetchedAt);
