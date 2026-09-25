namespace DynaDocs.Tests.Map;

internal sealed record LinearCall(
    string Operation,
    string Query,
    Dictionary<string, string?> Variables,
    string? Authorization,
    HttpMethod Method,
    string? MediaType)
{
    public string? Var(string name) => Variables.GetValueOrDefault(name);
}
