namespace DynaDocs.Services.Map;

/// <summary>A `blocks` or `related` link; for `blocks`, <see cref="From"/> blocks <see cref="To"/>.</summary>
internal sealed record MapRelation(string Id, string Type, string From, string To);
