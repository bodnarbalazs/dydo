namespace DynaDocs.Services.Merge;

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DynaDocs.Utils;
using static CommitSha;
using static GitHubJson;

/// <summary>
/// <c>dydo merge</c>: merge a reviewed PR into its <c>feature/*</c> base with a merge commit, or refuse.
/// In order: the repository is origin's; a closed PR is reconciled first (its SHA already in a feature
/// base is success, so a rerun after a lost answer is safe); the base is a feature branch and not the
/// default; the head is the reviewed SHA; the PR body holds that SHA's PASS; its checks are green. The
/// merge names the validated base and the SHA, so a retarget or a push after the checks cannot change
/// where or what lands. Nothing writes before every check has passed, and a dry run writes nothing.
/// </summary>
internal sealed partial class PullRequestMerge
{
    private static readonly HashSet<string> MergeRubrics = ["code", "docs", "merge"];

    private readonly MergeCheckout _checkout;
    private readonly IGitHubApi _github;
    private readonly int _number;
    private readonly string _sha;
    private readonly string _repo;
    private readonly Dictionary<string, string?> _resolved = [];

    private PullRequestMerge(MergeCheckout checkout, IGitHubApi github, int number, string sha)
    {
        _checkout = checkout;
        _github = github;
        _number = number;
        _sha = sha;
        _repo = checkout.Origin.ApiPath;
    }

    /// <param name="connect">Opens GitHub on origin's host; production passes <see cref="GhApi"/>.</param>
    public static int Run(string currentDirectory, int number, string? sha, bool dryRun, Func<string, IGitHubApi> connect)
    {
        try
        {
            sha = sha?.ToLowerInvariant();
            if (sha == null || !FullShaRegex().IsMatch(sha))
                throw MergeStop.Refused("--match-head-commit takes the full 40-character SHA the reviewer passed");
            var checkout = MergeCheckout.Locate(currentDirectory);
            return new PullRequestMerge(checkout, connect(checkout.Origin.Host), number, sha).Merge(dryRun);
        }
        catch (MergeStop stop)
        {
            foreach (var line in stop.Lines)
                Console.Error.WriteLine(line);
            return stop.ExitCode;
        }
    }

    private int Merge(bool dryRun)
    {
        var pull = ReadPull();
        if (pull.Closed)
            return Reconcile(pull);

        var defaultBranch = ReadDefaultBranch();
        CheckPull(pull, defaultBranch);
        var branch = ReadBranch(pull.Base) ?? throw Moved(pull.Base);
        var tip = Text(branch, "commit", "sha") ?? throw MergeStop.ToolError($"GitHub named no tip for {pull.Base}");
        var pass = ReviewPass(pull, tip);
        if (CommitChecks.Refusal(_github, _repo, _sha, pull.Base, branch) is { } checks)
            throw MergeStop.Refused(checks);

        if (dryRun)
        {
            Console.WriteLine($"would merge {Short(_sha)} into {pull.Base} (PR #{_number})");
            return ExitCodes.Success;
        }

        var message = CommitMessage(pull, pass);
        var merge = Compare(tip, _sha) == "ahead" ? WriteTwoParentMerge(pull.Base, tip, message) : PostMerge(pull.Base, message);
        if (merge == null)
            Console.WriteLine(AlreadyIntegrated(pull));
        else
            Report(pull, merge, defaultBranch);
        return ExitCodes.Success;
    }

    private PullRequest ReadPull()
    {
        var reply = _github.Send("GET", $"{_repo}/pulls/{_number}");
        var pull = reply.Status == 404 ? null : PullRequest.From(Expect(reply, 200, $"could not read PR #{_number} from {_checkout.Origin}"));
        return pull != null && string.Equals(pull.BaseRepo, _checkout.Origin.FullName, StringComparison.OrdinalIgnoreCase)
            ? pull
            : throw MergeStop.Refused($"PR #{_number} is not a pull request in {_checkout.Origin}");
    }

    private int Reconcile(PullRequest pull)
    {
        var tip = IsFeatureBase(pull.Base, ReadDefaultBranch()) ? Text(ReadBranch(pull.Base), "commit", "sha") : null;
        if (tip == null || !Contains(tip, _sha))
            throw MergeStop.Refused($"PR #{_number} is closed");
        Console.WriteLine(AlreadyIntegrated(pull));
        return ExitCodes.Success;
    }

    private string ReadDefaultBranch()
    {
        var reply = _github.Send("GET", _repo);
        return reply.Status == 200 && Text(reply.Json, "default_branch") is { Length: > 0 } name
            ? name
            : throw MergeStop.Refused($"could not read {_checkout.Origin.FullName}'s default branch from GitHub; nothing merged");
    }

    private void CheckPull(PullRequest pull, string defaultBranch)
    {
        if (pull.Draft)
            throw MergeStop.Refused($"PR #{_number} is a draft");
        if (pull.HeadRepo == null || !string.Equals(pull.HeadRepo, pull.BaseRepo, StringComparison.OrdinalIgnoreCase))
            throw MergeStop.Refused($"PR #{_number} comes from {pull.HeadRepo ?? pull.HeadLabel}; dydo merge merges same-repository PRs only");
        if (DefaultNamed(pull.Base, defaultBranch) is { } named)
            throw MergeStop.Refused($"{pull.Base} is this repository's default branch ({named}); that merge is the user's click");
        if (!FeatureBranch.IsValid(pull.Base, _checkout.Root))
            throw MergeStop.Refused($"PR #{_number} targets {pull.Base}; dydo merge merges only into feature/* branches - "
                + "master, main and the default branch are the user's click");
        if (pull.HeadSha != _sha)
            throw MergeStop.Refused($"PR #{_number} head is {Short(pull.HeadSha)}, not the reviewed {Short(_sha)}; "
                + $"review {Short(pull.HeadSha)} or merge what was reviewed");
    }

    private bool IsFeatureBase(string name, string defaultBranch) =>
        DefaultNamed(name, defaultBranch) == null && FeatureBranch.IsValid(name, _checkout.Root);

    // GitHub's default read fresh, or the local origin/HEAD's name: either refuses, whatever its case.
    private string? DefaultNamed(string name, string defaultBranch) =>
        new[] { defaultBranch, _checkout.OriginHead }.FirstOrDefault(named => string.Equals(named, name, StringComparison.OrdinalIgnoreCase));

    private ReviewVerdict ReviewPass(PullRequest pull, string tip)
    {
        var verdicts = IndependentReview.Parse(pull.Body);
        var passes = verdicts.Where(verdict => verdict.Pass && Names(verdict.Candidate)).ToList();
        if (passes.Count == 0)
            throw MergeStop.Refused($"PR #{_number}'s Independent review holds no PASS for candidate {Short(_sha)}",
                verdicts.Where(verdict => verdict is { Pass: true, Candidate: not null })
                    .Select(verdict => $"found PASS for {Short(verdict.Candidate!)}").Distinct());
        if (verdicts.Any(verdict => !verdict.Pass && Names(verdict.Candidate)))
            throw MergeStop.Refused($"the review section also holds a FAIL for {Short(_sha)}");

        var pass = passes.FirstOrDefault(verdict => MergeRubrics.Contains(verdict.Rubric))
            ?? throw MergeStop.Refused($"the PASS for {Short(_sha)} is a {passes[0].Rubric} review; a merge needs a code, docs or merge PASS");
        if (pass.Base == null)
            Console.WriteLine($"note: the PASS for {Short(_sha)} names no base; the base check is skipped");
        else if (!Contains(tip, pass.Base))
            throw MergeStop.Refused($"the PASS for {Short(_sha)} reviewed against {Short(pass.Base)}, which {pull.Base} does not contain");
        return pass;
    }

    // A full SHA must be the SHA; an abbreviated one counts only when GitHub resolves it to exactly the
    // SHA, and an ambiguous prefix (422) resolves to nothing.
    private bool Names(string? candidate)
    {
        if (candidate == null || !_sha.StartsWith(candidate, StringComparison.Ordinal))
            return false;
        if (candidate.Length == _sha.Length)
            return true;
        if (!_resolved.TryGetValue(candidate, out var resolved))
        {
            var reply = _github.Send("GET", $"{_repo}/commits/{candidate}");
            resolved = reply.Status is 404 or 422 ? null : Text(Expect(reply, 200, $"could not resolve candidate {candidate}"), "sha");
            _resolved[candidate] = resolved;
        }
        return resolved == _sha;
    }

    private JsonNode? ReadBranch(string name)
    {
        var reply = _github.Send("GET", $"{_repo}/branches/{name}");
        return reply.Status == 404 ? null : Expect(reply, 200, $"could not read {name}");
    }

    private bool Contains(string tip, string commit) => Compare(commit, tip) is "ahead" or "identical";

    // How head relates to base: ahead, behind, identical or diverged; null when either is unknown.
    private string? Compare(string baseRef, string head)
    {
        var reply = _github.Send("GET", $"{_repo}/compare/{baseRef}...{head}?per_page=1");
        return reply.Status == 404 ? null : Text(Expect(reply, 200, $"could not compare {Short(baseRef)} with {Short(head)}"), "status");
    }

    /// <summary>The merge commit's SHA, or null when the SHA was already in the base (204).</summary>
    private string? PostMerge(string target, string message)
    {
        var reply = _github.Send("POST", $"{_repo}/merges",
            new JsonObject { ["base"] = target, ["head"] = _sha, ["commit_message"] = message });
        return reply.Status switch
        {
            201 => Text(reply.Json, "sha") ?? throw MergeStop.ToolError($"GitHub merged PR #{_number} but named no merge commit"),
            204 => null,
            409 => throw MergeStop.Refused($"{Short(_sha)} conflicts with {target}; a Merge code-writer resolves it (code-writer merge resource)"),
            404 => throw Moved(target),
            _ => throw Failed($"GitHub refused the merge of PR #{_number}", reply)
        };
    }

    // The base tip is an ancestor of the SHA, so GitHub would fast-forward and leave no merge commit:
    // write the two-parent commit by hand. force=false makes a tip that moved meanwhile fail the update.
    private string WriteTwoParentMerge(string target, string tip, string message)
    {
        var commit = Expect(_github.Send("GET", $"{_repo}/git/commits/{_sha}"), 200, $"could not read {Short(_sha)}");
        var created = Expect(_github.Send("POST", $"{_repo}/git/commits", new JsonObject
        {
            ["message"] = message,
            ["tree"] = Text(commit, "tree", "sha"),
            ["parents"] = new JsonArray(tip, _sha)
        }), 201, "could not write the merge commit");
        var merge = Text(created, "sha") ?? throw MergeStop.ToolError("GitHub named no merge commit");

        var update = _github.Send("PATCH", $"{_repo}/git/refs/heads/{target}", new JsonObject { ["sha"] = merge, ["force"] = false });
        if (update.Status == 200)
            return merge;
        if (update.Status == 404 || update.Status == 422 && update.Message.Contains("fast forward", StringComparison.OrdinalIgnoreCase))
            throw Moved(target);
        throw Failed($"GitHub refused to move {target} to the merge commit", update);
    }

    private void Report(PullRequest pull, string merge, string defaultBranch)
    {
        var reread = _github.Send("GET", $"{_repo}/pulls/{_number}");
        var marked = (reread.Status, Flag(reread.Json, "merged")) switch
        {
            (200, true) => "yes",
            (200, false) => "no",
            _ => "unverified"
        };
        Console.WriteLine($"merged PR #{_number} ({Short(_sha)}) into {pull.Base} as {Short(merge)}; PR marked merged: {marked}");

        var now = _github.Send("GET", _repo);
        if (now.Status == 200 && Text(now.Json, "default_branch") is { Length: > 0 } current && current != defaultBranch)
            Console.WriteLine($"warning: {_checkout.Origin.FullName}'s default branch changed during the run, from {defaultBranch} to {current}");
    }

    private string CommitMessage(PullRequest pull, ReviewVerdict pass) =>
        $"Merge pull request #{_number} from {_checkout.Origin.Owner}/{pull.HeadRef}\n\n{pull.Title}\n\n"
        + $"Dydo-Merge: dydo {DydoVersion.Current}\nReviewed-Candidate: {_sha}\nReview: {(pass.Line.Length > 200 ? pass.Line[..200] : pass.Line)}";

    private string AlreadyIntegrated(PullRequest pull) =>
        $"already integrated (no new merge commit): PR #{_number} ({Short(_sha)}) is in {pull.Base}";

    private MergeStop Moved(string target) =>
        MergeStop.Refused($"{target} moved or no longer exists on {_checkout.Origin.FullName}; nothing merged - rerun");

    private static JsonNode? Expect(GitHubReply reply, int status, string what) =>
        reply.Status == status ? reply.Json : throw Failed(what, reply);

    private static MergeStop Failed(string what, GitHubReply reply) =>
        MergeStop.ToolError(reply.Status == 0 ? $"{what}: {reply.Error}" : $"{what}: {reply.Message} (HTTP {reply.Status})");

    [GeneratedRegex(@"\A[0-9a-f]{40}\z")]
    private static partial Regex FullShaRegex();

    private sealed record PullRequest(
        bool Closed, bool Draft, string Title, string Body, string Base, string? BaseRepo,
        string HeadSha, string HeadRef, string? HeadRepo, string HeadLabel)
    {
        public static PullRequest From(JsonNode? pull) => new(
            Text(pull, "state") == "closed", Flag(pull, "draft"), Text(pull, "title") ?? "", Text(pull, "body") ?? "",
            Text(pull, "base", "ref") ?? "", Text(pull, "base", "repo", "full_name"),
            Text(pull, "head", "sha") ?? "", Text(pull, "head", "ref") ?? "", Text(pull, "head", "repo", "full_name"),
            Text(pull, "head", "label") ?? "a deleted fork");
    }
}
