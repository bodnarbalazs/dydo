namespace DynaDocs.Tests.Merge;

using System.Text.Json.Nodes;
using DynaDocs.Services.Merge;

/// <summary>
/// A canned GitHub for <c>dydo merge</c>: each method and path answers with its registered replies in
/// order, the last one repeating, or from the request body when a route answers from state. A call
/// nothing was registered for fails the test.
/// </summary>
internal sealed class FakeGitHub : IGitHubApi
{
    private readonly Dictionary<string, Func<JsonObject?, GitHubReply>> _routes = [];

    public List<(string Method, string Path, JsonObject? Body)> Calls { get; } = [];

    public IEnumerable<(string Method, string Path, JsonObject? Body)> Writes => Calls.Where(call => call.Method != "GET");

    public void On(string method, string path, params GitHubReply[] replies)
    {
        var served = 0;
        On(method, path, _ => replies[Math.Min(served++, replies.Length - 1)]);
    }

    public void On(string method, string path, Func<JsonObject?, GitHubReply> answer) => _routes[Key(method, path)] = answer;

    public static GitHubReply Reply(int status, JsonNode? json = null) => new(status, json, status == 0 ? "network down" : "");

    public static GitHubReply Reply(int status, string json) => Reply(status, JsonNode.Parse(json));

    public GitHubReply Send(string method, string path, JsonObject? body = null)
    {
        Calls.Add((method, path, body?.DeepClone().AsObject()));
        var key = Key(method, path);
        if (!_routes.TryGetValue(key, out var answer))
            throw new InvalidOperationException($"unexpected GitHub call: {key}");
        // Through text, as gh's output arrives, so values read as they do from GitHub.
        var reply = answer(body?.DeepClone().AsObject());
        return reply with { Json = reply.Json == null ? null : JsonNode.Parse(reply.Json.ToJsonString()) };
    }

    private static string Key(string method, string path) => $"{method} {path}";
}
