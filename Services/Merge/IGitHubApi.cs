namespace DynaDocs.Services.Merge;

using System.Text.Json.Nodes;

/// <summary>
/// One GitHub REST call on the repository's host. <paramref name="path"/> is the full API path, such
/// as <c>repos/OWNER/NAME/pulls/5</c>; nothing fills it in from the environment. A reply with status
/// <c>0</c> never reached GitHub: gh is missing or signed out, or the network failed.
/// </summary>
internal interface IGitHubApi
{
    GitHubReply Send(string method, string path, JsonObject? body = null);
}
