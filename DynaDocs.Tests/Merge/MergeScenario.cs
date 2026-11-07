namespace DynaDocs.Tests.Merge;

using System.Text.Json.Nodes;
using DynaDocs.Services;
using DynaDocs.Services.Merge;
using static DynaDocs.Tests.Integration.PushedRepository;
using static FakeGitHub;

/// <summary>
/// A dydo project checkout whose origin is <c>github.com/acme/widgets</c>, and a fake GitHub where
/// PR #7 into <c>feature/x</c>, head <see cref="Sha"/>, holds a code PASS for that head and green CI;
/// <c>feature/x</c>'s ref, its PR metadata and the default pin all name <see cref="Tip"/>, and a merge
/// GitHub writes has <see cref="Tip"/> as its first parent. A test changes the one answer it is about,
/// then runs <c>dydo merge</c>.
/// </summary>
internal sealed class MergeScenario : IDisposable
{
    public const string Sha = "0123456789abcdef0123456789abcdef01234567";
    public const string Tip = "1111111111111111111111111111111111111111";
    public const string ReviewBase = "2222222222222222222222222222222222222222";
    public const string MergeSha = "3333333333333333333333333333333333333333";
    /// <summary>A target tip from before the ref advanced, such as a PR's stale <c>base.sha</c>.</summary>
    public const string Stale = "6666666666666666666666666666666666666666";
    /// <summary>A target tip another merge moved the ref to during the run.</summary>
    public const string Moved = "7777777777777777777777777777777777777777";
    public const string Repo = "repos/acme/widgets";
    /// <summary>The repository's GraphQL node id, as <c>GET repos/acme/widgets</c> names it.</summary>
    public const string RepositoryId = "R_kgDOAcmeWidgets";
    public const string Target = "feature/x";
    public const int Number = 7;

    public const string Pulls = Repo + "/pulls/7";
    public const string Branch = Repo + "/branches/feature/x";
    public const string Rules = Repo + "/rules/branches/feature/x?per_page=100&page=1";
    public const string Suites = Repo + "/commits/" + Sha + "/check-suites?per_page=100&page=1";
    public const string Runs = Repo + "/commits/" + Sha + "/check-runs?filter=latest&per_page=100&page=1";
    public const string Statuses = Repo + "/commits/" + Sha + "/status?per_page=100&page=1";
    public const string Merges = Repo + "/merges";
    public const string MergeCommit = Repo + "/git/commits/" + MergeSha;

    public MergeScenario(string origin = "https://github.com/acme/widgets.git")
    {
        Root = Path.Combine(Path.GetTempPath(), "dydo-merge-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Root);
        Git(Root, "init", "-b", "master");
        File.WriteAllText(Path.Combine(Root, "dydo.json"), "{}\n");
        Git(Root, "remote", "add", "origin", origin);

        PullJson = Pull(Body(PassLine()));
        GitHub.On("GET", Pulls, Reply(200, PullJson), Reply(200, MergedPull()));
        GitHub.On("GET", Repo, Reply(200, $$"""{ "full_name": "acme/widgets", "default_branch": "master", "node_id": "{{RepositoryId}}" }"""));
        GitHub.On("GET", Branch, Reply(200, BranchJson));
        GitHub.On("GET", Rules, Reply(200, "[]"));
        GitHub.On("GET", Compare(ReviewBase, Tip), Reply(200, """{ "status": "ahead" }"""));
        GitHub.On("GET", Compare(Tip, Sha), Reply(200, """{ "status": "diverged" }"""));
        GitHub.On("GET", Suites, Reply(200, SuitePage(Suite("GitHub Actions", "completed", "success"))));
        GitHub.On("GET", Runs, Reply(200, RunPage(Run("build", "completed", "success"))));
        GitHub.On("GET", Statuses, Reply(200, """{ "state": "pending", "total_count": 0, "statuses": [] }"""));
        GitHub.On("POST", Merges, Reply(201, $$"""{ "sha": "{{MergeSha}}" }"""));
        GitHub.On("GET", MergeCommit, Reply(200, MergeCommitJson(Tip)));
    }

    public string Root { get; }

    public FakeGitHub GitHub { get; } = new();

    /// <summary>The open PR's first answer; a test edits it before <see cref="Merge"/>.</summary>
    public JsonObject PullJson { get; }

    public JsonObject BranchJson { get; } = BranchAt(Tip);

    /// <summary>The host <c>dydo merge</c> asked to talk to.</summary>
    public string? Host { get; private set; }

    /// <summary>The pauses <c>dydo merge</c> asked for between rereads; no test waits for real.</summary>
    public List<TimeSpan> Waits { get; } = [];

    public void Dispose() => LinkSafeDelete.DeleteTree(Root);

    public (int Exit, string Stdout, string Stderr) Merge(string? sha = Sha, bool dryRun = false, string? directory = null, string? pin = Tip) =>
        ConsoleCapture.All(() => PullRequestMerge.Run(directory ?? Root, Number, sha, pin, dryRun, host =>
        {
            Host = host;
            return GitHub;
        }, Waits.Add));

    public static string Compare(string baseRef, string head) => $"{Repo}/compare/{baseRef}...{head}?per_page=1";

    /// <summary><c>GET branches/feature/x</c>'s answer while the ref is at <paramref name="tip"/>.</summary>
    public static JsonObject BranchAt(string tip) => JsonNode.Parse($$"""
        {
          "name": "feature/x",
          "commit": { "sha": "{{tip}}" },
          "protected": false,
          "protection": { "enabled": false, "required_status_checks": { "enforcement_level": "off", "contexts": [], "checks": [] } }
        }
        """)!.AsObject();

    /// <summary>The merge commit GitHub wrote, onto <paramref name="firstParent"/>.</summary>
    public static JsonObject MergeCommitJson(string firstParent) => JsonNode.Parse($$"""
        { "sha": "{{MergeSha}}", "parents": [ { "sha": "{{firstParent}}" }, { "sha": "{{Sha}}" } ] }
        """)!.AsObject();

    public static string PassLine(string candidate = Sha, string rubric = "code", string? reviewBase = ReviewBase) =>
        $"PASS - rubric/type: {rubric}; contract DYD-1 description as of 2026-10-29T10:00:00.000Z; candidate {candidate}; "
        + (reviewBase == null ? "" : $"base {reviewBase}; ")
        + "reviewer/model: claude-opus-5-5; applicable tests 12/12, gates 3/3. Checks: the whole diff holds. Evidence: link.";

    public static string Body(params string[] reviewLines) =>
        "Closes DYD-1.\n\n## Summary\n\nA frame.\n\n## Independent review\n\n" + string.Join("\n\n", reviewLines) + "\n";

    public static JsonObject Pull(string body) => JsonNode.Parse($$"""
        {
          "number": 7,
          "state": "open",
          "merged": false,
          "draft": false,
          "title": "Widgets get a frame",
          "body": {{JsonValue.Create(body).ToJsonString()}},
          "base": { "ref": "feature/x", "sha": "{{Tip}}", "repo": { "full_name": "acme/widgets" } },
          "head": {
            "sha": "{{Sha}}",
            "ref": "tauntphoenix/dyd-1-frame",
            "label": "acme:tauntphoenix/dyd-1-frame",
            "repo": { "full_name": "acme/widgets" }
          }
        }
        """)!.AsObject();

    public static JsonObject MergedPull()
    {
        var pull = Pull(Body(PassLine()));
        pull["state"] = "closed";
        pull["merged"] = true;
        return pull;
    }

    public static JsonObject Suite(string app, string status, string? conclusion, int runs = 1, string? slug = null) => new()
    {
        ["status"] = status,
        ["conclusion"] = conclusion,
        ["latest_check_runs_count"] = runs,
        ["app"] = new JsonObject { ["name"] = app, ["slug"] = slug }
    };

    public static JsonObject Run(string name, string status, string? conclusion) => new()
    {
        ["name"] = name,
        ["status"] = status,
        ["conclusion"] = conclusion
    };

    public static JsonObject Status(string context, string state) => new() { ["context"] = context, ["state"] = state };

    public static JsonObject SuitePage(params JsonObject[] suites) => Page("check_suites", suites);

    public static JsonObject RunPage(params JsonObject[] runs) => Page("check_runs", runs);

    public static JsonObject StatusPage(params JsonObject[] statuses) => Page("statuses", statuses);

    private static JsonObject Page(string name, JsonObject[] items) => new()
    {
        ["total_count"] = items.Length,
        [name] = new JsonArray([.. items])
    };
}
