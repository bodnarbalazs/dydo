namespace DynaDocs.Services.Map.Contract;

internal sealed record GraphQLRequest(string Query, Dictionary<string, string?> Variables);
