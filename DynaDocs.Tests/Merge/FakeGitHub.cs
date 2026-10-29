namespace DynaDocs.Tests.Merge;

using System.Text.Json.Nodes;
using DynaDocs.Services.Merge;

/// <summary>
/// A canned GitHub for <c>dydo merge</c>: each method and path answers with its registered replies in
/// order, the last one repeating. A call nothing was registered for fails the test.
/// </summary>
internal sealed class FakeGitHub : IGitHubApi
{
    private readonly Dictionary<string, List<GitHubReply>> _routes = [];
    private readonly Dictionary<string, int> _served = [];

    public List<(string Method, string Path, JsonObject? Body)> Calls { get; } = [];

    public IEnumerable<(string Method, string Path, JsonObject? Body)> Writes => Calls.Where(call => call.Method != "GET");

    public void On(string method, string path, params GitHubReply[] replies) => _routes[Key(method, path)] = [.. replies];

    public bool Has(string method, string path) => _routes.ContainsKey(Key(method, path));

    public static GitHubReply Reply(int status, JsonNode? json = null) => new(status, json, status == 0 ? "network down" : "");

    public static GitHubReply Reply(int status, string json) => Reply(status, JsonNode.Parse(json));

    public GitHubReply Send(string method, string path, JsonObject? body = null)
    {
        Calls.Add((method, path, body?.DeepClone().AsObject()));
        var key = Key(method, path);
        if (!_routes.TryGetValue(key, out var replies))
            throw new InvalidOperationException($"unexpected GitHub call: {key}");
        var served = _served.GetValueOrDefault(key);
        _served[key] = served + 1;
        // Through text, as gh's output arrives, so values read as they do from GitHub.
        var reply = replies[Math.Min(served, replies.Count - 1)];
        return reply with { Json = reply.Json == null ? null : JsonNode.Parse(reply.Json.ToJsonString()) };
    }

    private static string Key(string method, string path) => $"{method} {path}";
}
