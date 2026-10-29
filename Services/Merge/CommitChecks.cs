namespace DynaDocs.Services.Merge;

using System.Text.Json.Nodes;
using static CommitSha;
using static GitHubJson;

/// <summary>
/// The CI rule: every check suite, latest check run and legacy status on the SHA has finished green
/// (<c>success</c>, <c>neutral</c> or <c>skipped</c>; any other or unknown state is not), every check
/// the target's branch protection or rulesets require is among them, and at least one suite holds CI.
/// An inert app's suite that is unfinished and holds no run is not CI and is ignored. Every list is
/// read to its last page.
/// </summary>
internal static class CommitChecks
{
    private const int PageSize = 100;

    private static readonly HashSet<string> GreenConclusions = ["success", "neutral", "skipped"];

    /// <summary>
    /// Apps, by slug, that leave a suite on a commit and never run anything in it: blacksmith-sh leaves
    /// one queued with zero runs on every bodnarbalazs/dydo and LC commit. Adding an app is a code change
    /// with review; any other app's unfinished suite refuses.
    /// </summary>
    private static readonly HashSet<string> InertApps = ["blacksmith-sh"];

    /// <summary>Null when the checks are green, else the refusal reason.</summary>
    public static string? Refusal(IGitHubApi github, string repo, string sha, string target, JsonNode branch)
    {
        var commit = $"{repo}/commits/{sha}";
        var suites = Pages(github, $"{commit}/check-suites", sha, "check_suites").Where(suite => !IsInert(suite)).ToList();
        if (suites.Count == 0)
            return $"no checks reported on {Short(sha)}; wait for CI, or this repo runs no CI for PRs into {target}";

        var runs = Pages(github, $"{commit}/check-runs?filter=latest", sha, "check_runs").ToList();
        var statuses = Pages(github, $"{commit}/status", sha, "statuses").ToList();
        var present = runs.Select(run => Text(run, "name")).Concat(statuses.Select(status => Text(status, "context"))).ToHashSet();
        List<string> problems =
        [
            .. suites.Select(suite => CheckProblem(Text(suite, "app", "slug") ?? Text(suite, "app", "name"), suite)).OfType<string>(),
            .. runs.Select(run => CheckProblem(Text(run, "name"), run)).OfType<string>(),
            .. statuses.Where(status => Text(status, "state") != "success")
                .Select(status => $"{Text(status, "context")} {Text(status, "state") ?? "unknown"}"),
            .. Required(github, repo, sha, target, branch).Where(name => !present.Contains(name)).Select(name => $"{name} missing")
        ];
        return problems.Count == 0 ? null : $"checks on {Short(sha)} are not green: {string.Join("; ", problems)}";
    }

    // A listed app's suite is ignored only while it is unfinished and empty; once it holds a run it is judged.
    private static bool IsInert(JsonNode suite) =>
        Text(suite, "app", "slug") is { } slug && InertApps.Contains(slug)
        && Text(suite, "status") != "completed" && Number(suite, "latest_check_runs_count") == 0;

    private static string? CheckProblem(string? name, JsonNode check)
    {
        var status = Text(check, "status");
        if (status != "completed")
            return $"{name} {status ?? "unknown"}";
        var conclusion = Text(check, "conclusion");
        return conclusion != null && GreenConclusions.Contains(conclusion) ? null : $"{name} {conclusion ?? "no conclusion"}";
    }

    // Branch protection's contexts count only while it enforces them; rulesets list theirs per rule.
    private static IEnumerable<string> Required(IGitHubApi github, string repo, string sha, string target, JsonNode branch)
    {
        var protection = At(branch, "protection", "required_status_checks");
        IEnumerable<string?> fromProtection = Text(protection, "enforcement_level") is null or "off"
            ? []
            : Items(protection, "contexts").Select(context => Text(context));
        var fromRulesets = Pages(github, $"{repo}/rules/branches/{target}", sha, PlanHasNoRulesets)
            .Where(rule => Text(rule, "type") == "required_status_checks")
            .SelectMany(rule => Items(rule, "parameters", "required_status_checks").Select(check => Text(check, "context")));
        return fromProtection.Concat(fromRulesets).OfType<string>().Distinct();
    }

    // A private repository on GitHub's free plan cannot have rulesets, and GitHub answers 403 asking for
    // an upgrade; any other refusal to show them stays an error.
    private static bool PlanHasNoRulesets(GitHubReply reply) =>
        reply.Status == 403 && reply.Message.Contains("Upgrade to GitHub", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<JsonNode> Pages(IGitHubApi github, string path, string sha, params string[] items) =>
        Pages(github, path, sha, _ => false, items);

    // An empty path reads a page that is itself the array; a reply <paramref name="none"/> accepts lists nothing.
    private static IEnumerable<JsonNode> Pages(IGitHubApi github, string path, string sha, Func<GitHubReply, bool> none,
        params string[] items)
    {
        var separator = path.Contains('?') ? '&' : '?';
        for (var page = 1; ; page++)
        {
            var reply = github.Send("GET", $"{path}{separator}per_page={PageSize}&page={page}");
            if (none(reply))
                yield break;
            if (reply.Status != 200)
                throw MergeStop.ToolError($"could not read the checks on {Short(sha)}: "
                    + (reply.Status == 0 ? reply.Error : $"{reply.Message} (HTTP {reply.Status})"));
            var listed = Items(reply.Json, items).ToList();
            foreach (var item in listed)
                yield return item;
            if (listed.Count < PageSize)
                yield break;
        }
    }
}
