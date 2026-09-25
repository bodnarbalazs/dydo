namespace DynaDocs.Services.Map;

internal sealed record GraphQLRequest(string Query, Dictionary<string, string?> Variables);
