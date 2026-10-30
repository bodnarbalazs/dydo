namespace DynaDocs.Services.Merge;

using System.Text.Json.Nodes;

/// <summary>One GitHub answer: its HTTP status (<c>0</c> when none came back), its JSON body, and gh's error text.</summary>
internal sealed record GitHubReply(int Status, JsonNode? Json, string Error)
{
    /// <summary>GitHub's own message for the reply, else gh's error text.</summary>
    public string Message => GitHubJson.Text(Json, "message") is { Length: > 0 } message ? message : Error;
}
