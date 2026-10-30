namespace DynaDocs.Services.Merge;

using System.Text.Json.Nodes;

/// <summary>
/// Reads GitHub's JSON without trusting its shape: a missing member, or one of another kind, reads as
/// absent, so a malformed answer fails a check instead of throwing.
/// </summary>
internal static class GitHubJson
{
    public static JsonNode? At(JsonNode? node, params string[] path)
    {
        foreach (var name in path)
            node = node is JsonObject members ? members[name] : null;
        return node;
    }

    public static string? Text(JsonNode? node, params string[] path) =>
        At(node, path) is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    public static bool Flag(JsonNode? node, params string[] path) =>
        At(node, path) is JsonValue value && value.TryGetValue<bool>(out var flag) && flag;

    public static IEnumerable<JsonNode> Items(JsonNode? node, params string[] path) =>
        At(node, path) is JsonArray items ? items.OfType<JsonNode>() : [];
}
