namespace DynaDocs.Tests;

using System.Text.Json.Nodes;

internal static class HookTestAssertions
{
    public static List<string> Commands(JsonNode? entry)
    {
        var entryObject = Assert.IsType<JsonObject>(entry);
        var hooks = Assert.IsType<JsonArray>(entryObject["hooks"]);
        return hooks
            .OfType<JsonObject>()
            .Select(hook => hook["command"]?.GetValue<string>())
            .Where(command => command != null)
            .Select(command => command!)
            .ToList();
    }

    public static int Count(JsonArray entries, string command) =>
        entries.Sum(entry => Commands(entry).Count(existing => existing == command));
}
