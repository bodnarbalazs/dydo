namespace DynaDocs.Tests.Merge;

using System.Text.Json.Nodes;
using DynaDocs.Services.Merge;
using static FakeGitHub;
using static MergeScenario;

/// <summary>
/// <c>feature/x</c>'s ref as GitHub keeps it on the two-parent path: a commit graph and a tip that moves
/// only as Git allows. An unforced <c>PATCH git/refs</c> fast-forwards whenever the tip is an ancestor of
/// the new commit; GraphQL <c>updateRefs</c> moves the ref only when its <c>beforeOid</c>, if given, is
/// the tip, and rejects atomically otherwise. Another merge may land on the ref between the merge
/// commit's write and the ref move.
/// </summary>
internal sealed class TargetBranch
{
    public const string RefName = "refs/heads/" + Target;

    private readonly Dictionary<string, string[]> _parents;
    private readonly string? _landsMeanwhile;

    /// <param name="parents">Each commit's parents; the merge commit's are added when it is written.</param>
    /// <param name="landsMeanwhile">The tip another merge moves the ref to once the merge commit is written.</param>
    public TargetBranch(FakeGitHub github, Dictionary<string, string[]> parents, string? landsMeanwhile = null)
    {
        _parents = parents;
        _landsMeanwhile = landsMeanwhile;
        github.On("GET", Branch, _ => Reply(200, BranchAt(Tip)));
        github.On("POST", $"{Repo}/git/commits", WriteCommit);
        github.On("PATCH", $"{Repo}/git/refs/heads/{Target}", Patch);
        github.On("POST", "graphql", UpdateRefs);
    }

    public string Tip { get; private set; } = MergeScenario.Tip;

    private GitHubReply WriteCommit(JsonObject? commit)
    {
        _parents[MergeSha] = [.. commit!["parents"]!.AsArray().Select(parent => parent!.GetValue<string>())];
        Tip = _landsMeanwhile ?? Tip;
        return Reply(201, new JsonObject { ["sha"] = MergeSha });
    }

    private GitHubReply Patch(JsonObject? update)
    {
        var after = update!["sha"]!.GetValue<string>();
        if (update["force"]?.GetValue<bool>() != true && !Reaches(after, Tip))
            return Reply(422, """{ "message": "Update is not a fast forward" }""");
        Tip = after;
        return Reply(200, new JsonObject { ["object"] = new JsonObject { ["sha"] = after } });
    }

    private GitHubReply UpdateRefs(JsonObject? request)
    {
        if (request!["variables"]?["input"]?["repositoryId"]?.GetValue<string>() != RepositoryId)
            return Reply(200, """{ "data": null, "errors": [ { "type": "NOT_FOUND", "message": "Could not resolve to a node" } ] }""");
        var update = request["variables"]!["input"]!["refUpdates"]!.AsArray().Single()!;
        var before = update["beforeOid"]?.GetValue<string>();
        var after = update["afterOid"]!.GetValue<string>();
        var fastForward = update["force"]?.GetValue<bool>() == true || Reaches(after, Tip);
        if (update["name"]!.GetValue<string>() != RefName || before != null && before != Tip || !fastForward)
            return Reply(200, """{ "data": { "updateRefs": null }, "errors": [ { "type": "UNPROCESSABLE", "message": "A ref update was rejected" } ] }""");
        Tip = after;
        return Reply(200, """{ "data": { "updateRefs": { "clientMutationId": null } } }""");
    }

    private bool Reaches(string commit, string ancestor) =>
        commit == ancestor || _parents.GetValueOrDefault(commit, []).Any(parent => Reaches(parent, ancestor));
}
