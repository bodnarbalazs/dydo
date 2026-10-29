namespace DynaDocs.Tests.Merge;

using System.Text.Json.Nodes;
using DynaDocs.Services;
using DynaDocs.Utils;
using static DynaDocs.Tests.Integration.PushedRepository;
using static FakeGitHub;
using static MergeScenario;

/// <summary>
/// <c>dydo merge</c> end to end against the fake GitHub (DYD-424): each of design §2's refusals, the
/// reconcile-first rerun, GitHub's answers to the merge, the two-parent path and the report.
/// </summary>
[Collection("Integration")]
public sealed class PullRequestMergeTests : IDisposable
{
    private readonly MergeScenario _s = new();

    public void Dispose() => _s.Dispose();

    private void AssertRefused((int Exit, string Stdout, string Stderr) result, string reason)
    {
        Assert.Equal(1, result.Exit);
        Assert.Equal($"refused: {reason}", Lines(result.Stderr)[0]);
        Assert.Empty(_s.GitHub.Writes);
    }

    private static string[] Lines(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string Field(JsonObject? body, string name) => body![name]!.GetValue<string>();

    #region The merge

    [Fact]
    public void ReviewedGreenPr_MergesOnceWithAMergeCommitThatRecordsTheReview()
    {
        var (exit, stdout, stderr) = _s.Merge();

        Assert.True(exit == 0, stderr);
        Assert.Contains("merged PR #7 (0123456789ab) into feature/x as 333333333333; PR marked merged: yes", Lines(stdout));
        Assert.Equal("github.com", _s.Host);
        var post = Assert.Single(_s.GitHub.Writes);
        Assert.Equal(("POST", Merges), (post.Method, post.Path));
        Assert.Equal(["base", "head", "commit_message"], post.Body!.Select(member => member.Key));
        Assert.Equal(Target, Field(post.Body, "base"));
        Assert.Equal(Sha, Field(post.Body, "head"));
        Assert.Equal("Merge pull request #7 from acme/tauntphoenix/dyd-1-frame\n\nWidgets get a frame\n\n"
            + $"Dydo-Merge: dydo {DydoVersion.Current}\nReviewed-Candidate: {Sha}\nReview: {PassLine()[..200]}",
            Field(post.Body, "commit_message"));
    }

    [Fact]
    public void AnUpperCaseSha_IsTheSameCommit()
    {
        var (exit, _, stderr) = _s.Merge(Sha.ToUpperInvariant());

        Assert.True(exit == 0, stderr);
        Assert.Equal(Sha, Field(Assert.Single(_s.GitHub.Writes).Body, "head"));
    }

    [Fact]
    public void DryRun_ReadsAndChecksEverythingAndWritesNothing()
    {
        var (exit, stdout, stderr) = _s.Merge(dryRun: true);

        Assert.True(exit == 0, stderr);
        Assert.Contains("would merge 0123456789ab into feature/x (PR #7)", Lines(stdout));
        Assert.Empty(_s.GitHub.Writes);
        Assert.Contains(_s.GitHub.Calls, call => call.Path == Runs);
        Assert.Contains(_s.GitHub.Calls, call => call.Path == Rules);
    }

    [Fact]
    public void AlreadyMergedAnswer204_IsAlreadyIntegrated()
    {
        _s.GitHub.On("POST", Merges, Reply(204));

        var (exit, stdout, stderr) = _s.Merge();

        Assert.True(exit == 0, stderr);
        Assert.StartsWith("already integrated (no new merge commit)", Lines(stdout)[^1]);
    }

    [Theory]
    [InlineData(403, "Resource not accessible by integration")]
    [InlineData(422, "Validation Failed")]
    [InlineData(422, "Repository rule violations found")]
    public void AnUnexpectedMergeAnswer_IsAToolErrorWithGitHubsMessage(int status, string message)
    {
        _s.GitHub.On("POST", Merges, Reply(status, new JsonObject { ["message"] = message }));

        var (exit, _, stderr) = _s.Merge();

        Assert.Equal(ExitCodes.ToolError, exit);
        Assert.StartsWith("error: ", stderr);
        Assert.Contains($"{message} (HTTP {status})", stderr);
    }

    [Fact]
    public void GhThatCannotReachGitHub_IsAToolError()
    {
        _s.GitHub.On("GET", Pulls, Reply(0));

        var (exit, _, stderr) = _s.Merge();

        Assert.Equal(ExitCodes.ToolError, exit);
        Assert.Equal("error: could not read PR #7 from github.com/acme/widgets: network down", Lines(stderr)[0]);
    }

    #endregion

    #region Fast-forward eligible: the two-parent Git Data path

    private void FastForwardEligible()
    {
        _s.GitHub.On("GET", Compare(Tip, Sha), Reply(200, """{ "status": "ahead" }"""));
        _s.GitHub.On("GET", $"{Repo}/git/commits/{Sha}", Reply(200, $$"""{ "sha": "{{Sha}}", "tree": { "sha": "4444444444444444444444444444444444444444" } }"""));
        _s.GitHub.On("POST", $"{Repo}/git/commits", Reply(201, $$"""{ "sha": "{{MergeSha}}" }"""));
        _s.GitHub.On("PATCH", $"{Repo}/git/refs/heads/feature/x", Reply(200, $$"""{ "object": { "sha": "{{MergeSha}}" } }"""));
    }

    [Fact]
    public void BaseTipAncestorOfTheSha_WritesATwoParentCommitAndMovesTheBaseWithoutForce()
    {
        FastForwardEligible();

        var (exit, stdout, stderr) = _s.Merge();

        Assert.True(exit == 0, stderr);
        Assert.Contains("merged PR #7 (0123456789ab) into feature/x as 333333333333; PR marked merged: yes", Lines(stdout));
        var writes = _s.GitHub.Writes.ToList();
        Assert.Equal([("POST", $"{Repo}/git/commits"), ("PATCH", $"{Repo}/git/refs/heads/feature/x")],
            writes.Select(write => (write.Method, write.Path)));
        var commit = writes[0].Body!;
        Assert.Equal("4444444444444444444444444444444444444444", Field(commit, "tree"));
        Assert.Equal([Tip, Sha], commit["parents"]!.AsArray().Select(parent => parent!.GetValue<string>()));
        Assert.StartsWith("Merge pull request #7 from acme/tauntphoenix/dyd-1-frame", Field(commit, "message"));
        Assert.Equal(MergeSha, Field(writes[1].Body, "sha"));
        Assert.False(writes[1].Body!["force"]!.GetValue<bool>());
    }

    [Fact]
    public void BaseTipThatMovedBeforeTheRefUpdate_Refused17()
    {
        FastForwardEligible();
        _s.GitHub.On("PATCH", $"{Repo}/git/refs/heads/feature/x", Reply(422, """{ "message": "Update is not a fast forward" }"""));

        var (exit, _, stderr) = _s.Merge();

        Assert.Equal(1, exit);
        Assert.Equal("refused: feature/x moved or no longer exists on acme/widgets; nothing merged - rerun", Lines(stderr)[0]);
    }

    [Fact]
    public void ARefUpdateARulesetRejects_IsAToolError()
    {
        FastForwardEligible();
        _s.GitHub.On("PATCH", $"{Repo}/git/refs/heads/feature/x", Reply(422, """{ "message": "Repository rule violations found" }"""));

        var (exit, _, stderr) = _s.Merge();

        Assert.Equal(ExitCodes.ToolError, exit);
        Assert.Contains("Repository rule violations found (HTTP 422)", stderr);
    }

    #endregion

    #region Report: post-verify and the default branch

    [Fact]
    public void PrStillOpenOnTheReread_ReportsMarkedMergedNo()
    {
        _s.GitHub.On("GET", Pulls, Reply(200, _s.PullJson));

        var (exit, stdout, _) = _s.Merge();

        Assert.Equal(0, exit);
        Assert.EndsWith("PR marked merged: no", Lines(stdout)[^1]);
    }

    [Fact]
    public void AFailedRereadAfter201_StillExitsZeroUnverified()
    {
        _s.GitHub.On("GET", Pulls, Reply(200, _s.PullJson), Reply(0));

        var (exit, stdout, _) = _s.Merge();

        Assert.Equal(0, exit);
        Assert.Contains("merged PR #7 (0123456789ab) into feature/x as 333333333333; PR marked merged: unverified", Lines(stdout));
    }

    [Fact]
    public void DefaultBranchChangedDuringTheRun_Warns()
    {
        _s.GitHub.On("GET", Repo,
            Reply(200, """{ "full_name": "acme/widgets", "default_branch": "master" }"""),
            Reply(200, """{ "full_name": "acme/widgets", "default_branch": "feature/x" }"""));

        var (exit, stdout, _) = _s.Merge();

        Assert.Equal(0, exit);
        Assert.Contains("warning: acme/widgets's default branch changed during the run, from master to feature/x", Lines(stdout));
    }

    #endregion

    #region Reconcile first

    [Fact]
    public void ClosedAndIntegratedIntoAFeatureBase_ExitsZeroWithoutAnyCheck()
    {
        _s.PullJson["state"] = "closed";
        _s.PullJson["merged"] = true;
        _s.GitHub.On("GET", Compare(Sha, Tip), Reply(200, """{ "status": "ahead" }"""));

        var (exit, stdout, stderr) = _s.Merge();

        Assert.True(exit == 0, stderr);
        Assert.StartsWith("already integrated (no new merge commit)", Lines(stdout)[0]);
        Assert.Empty(_s.GitHub.Writes);
        Assert.DoesNotContain(_s.GitHub.Calls, call => call.Path == Suites);
    }

    [Fact]
    public void ClosedAndNotIntegrated_Refused5()
    {
        _s.PullJson["state"] = "closed";
        _s.GitHub.On("GET", Compare(Sha, Tip), Reply(200, """{ "status": "diverged" }"""));

        AssertRefused(_s.Merge(), "PR #7 is closed");
    }

    [Fact]
    public void ClosedIntoMaster_IsNotIntegratedIntoAFeatureBase()
    {
        _s.PullJson["state"] = "closed";
        _s.PullJson["merged"] = true;
        _s.PullJson["base"]!["ref"] = "master";

        AssertRefused(_s.Merge(), "PR #7 is closed");
    }

    [Fact]
    public void ClosedWithItsBaseDeleted_Refused5()
    {
        _s.PullJson["state"] = "closed";
        _s.GitHub.On("GET", Branch, Reply(404, """{ "message": "Branch not found" }"""));

        AssertRefused(_s.Merge(), "PR #7 is closed");
    }

    [Fact]
    public void ARerunAfterALostMergeAnswer_FindsTheMergeAndExitsZero()
    {
        _s.GitHub.On("POST", Merges, Reply(0));
        _s.GitHub.On("GET", Compare(Sha, Tip), Reply(200, """{ "status": "ahead" }"""));

        var lost = _s.Merge();
        var rerun = _s.Merge();

        Assert.Equal(ExitCodes.ToolError, lost.Exit);
        Assert.Equal(0, rerun.Exit);
        Assert.StartsWith("already integrated (no new merge commit)", Lines(rerun.Stdout)[0]);
        Assert.Single(_s.GitHub.Writes);
    }

    [Fact]
    public void ARerunWhilstThePrStillShowsOpen_Gets204AndExitsZero()
    {
        _s.GitHub.On("GET", Pulls, Reply(200, _s.PullJson));
        _s.GitHub.On("POST", Merges, Reply(0), Reply(204));

        var lost = _s.Merge();
        var rerun = _s.Merge();

        Assert.Equal(ExitCodes.ToolError, lost.Exit);
        Assert.Equal(0, rerun.Exit);
        Assert.StartsWith("already integrated (no new merge commit)", Lines(rerun.Stdout)[^1]);
    }

    #endregion

    #region Refusals 1-17

    [Fact]
    public void Refusal1_NoDydoProjectAboveTheDirectory()
    {
        var bare = Path.Combine(Path.GetTempPath(), "dydo-merge-none-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(bare);
        try
        {
            Git(bare, "init");

            AssertRefused(_s.Merge(directory: bare), "no dydo project here; run dydo merge from a checkout of the project's repository");
            Assert.Null(_s.Host);
        }
        finally
        {
            LinkSafeDelete.DeleteTree(bare);
        }
    }

    [Fact]
    public void Refusal1_TheGitTopLevelIsNotTheProjectRoot()
    {
        var sub = Path.Combine(_s.Root, "sub");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "dydo.json"), "{}\n");

        AssertRefused(_s.Merge(directory: sub), "no dydo project here; run dydo merge from a checkout of the project's repository");
    }

    [Fact]
    public void Refusal2_NoOriginRemote()
    {
        Git(_s.Root, "remote", "remove", "origin");

        AssertRefused(_s.Merge(), $"{_s.Root} has no origin remote naming HOST/OWNER/REPO");
    }

    [Fact]
    public void Refusal2_AnOriginThatNamesNoOwnerAndRepository()
    {
        Git(_s.Root, "remote", "set-url", "origin", "C:/repositories/widgets.git");

        AssertRefused(_s.Merge(), $"{_s.Root} has no origin remote naming HOST/OWNER/REPO");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0123456")]
    [InlineData("0123456789abcdef0123456789abcdef0123456")]
    [InlineData("0123456789abcdef0123456789abcdef012345678")]
    [InlineData("g123456789abcdef0123456789abcdef01234567")]
    public void Refusal3_AnythingButAFullSha(string? sha)
    {
        AssertRefused(_s.Merge(sha), "--match-head-commit takes the full 40-character SHA the reviewer passed");
        Assert.Empty(_s.GitHub.Calls);
    }

    [Fact]
    public void Refusal4_NoSuchPullRequest()
    {
        _s.GitHub.On("GET", Pulls, Reply(404, """{ "message": "Not Found" }"""));

        AssertRefused(_s.Merge(), "PR #7 is not a pull request in github.com/acme/widgets");
    }

    [Fact]
    public void Refusal4_APullRequestGitHubPlacesInAnotherRepository()
    {
        _s.PullJson["base"]!["repo"]!["full_name"] = "acme/gadgets";

        AssertRefused(_s.Merge(), "PR #7 is not a pull request in github.com/acme/widgets");
    }

    [Fact]
    public void Refusal5_ADraft()
    {
        _s.PullJson["draft"] = true;

        AssertRefused(_s.Merge(), "PR #7 is a draft");
    }

    [Fact]
    public void Refusal6_AForkPr()
    {
        _s.PullJson["head"]!["repo"]!["full_name"] = "mallory/widgets";

        AssertRefused(_s.Merge(), "PR #7 comes from mallory/widgets; dydo merge merges same-repository PRs only");
    }

    [Fact]
    public void Refusal6_AForkThatWasDeleted()
    {
        _s.PullJson["head"]!["repo"] = null;
        _s.PullJson["head"]!["label"] = "mallory:frame";

        AssertRefused(_s.Merge(), "PR #7 comes from mallory:frame; dydo merge merges same-repository PRs only");
    }

    [Theory]
    [InlineData("develop")]
    [InlineData("main")]
    [InlineData("feature/x.lock")]
    public void Refusal7_ABaseOutsideTheFeaturePattern(string baseRef)
    {
        _s.PullJson["base"]!["ref"] = baseRef;

        AssertRefused(_s.Merge(),
            $"PR #7 targets {baseRef}; dydo merge merges only into feature/* branches - master, main and the default branch are the user's click");
    }

    [Theory]
    [InlineData("master", "master")]
    [InlineData("feature/x", "feature/x")]
    [InlineData("feature/x", "Feature/X")]
    public void Refusal8_TheDefaultBranch(string baseRef, string defaultBranch)
    {
        _s.PullJson["base"]!["ref"] = baseRef;
        _s.GitHub.On("GET", Repo, Reply(200, new JsonObject { ["full_name"] = "acme/widgets", ["default_branch"] = defaultBranch }));

        AssertRefused(_s.Merge(), $"{baseRef} is this repository's default branch ({defaultBranch}); that merge is the user's click");
    }

    [Fact]
    public void Refusal8_TheBranchTheLocalOriginHeadNames()
    {
        Git(_s.Root, "symbolic-ref", "refs/remotes/origin/HEAD", "refs/remotes/origin/feature/x");

        AssertRefused(_s.Merge(), "feature/x is this repository's default branch (feature/x); that merge is the user's click");
    }

    [Theory]
    [InlineData(403, """{ "message": "Forbidden" }""")]
    [InlineData(200, """{ "full_name": "acme/widgets" }""")]
    [InlineData(200, """{ "full_name": "acme/widgets", "default_branch": "" }""")]
    public void Refusal9_AnUnreadableDefaultBranch(int status, string json)
    {
        _s.GitHub.On("GET", Repo, Reply(status, json));

        AssertRefused(_s.Merge(), "could not read acme/widgets's default branch from GitHub; nothing merged");
    }

    [Fact]
    public void Refusal10_AHeadThatMovedPastTheReview()
    {
        _s.PullJson["head"]!["sha"] = "5555555555555555555555555555555555555555";

        AssertRefused(_s.Merge(), "PR #7 head is 555555555555, not the reviewed 0123456789ab; review 555555555555 or merge what was reviewed");
    }

    [Fact]
    public void Refusal11_NoPassForTheSha_NamesThePassesItFound()
    {
        _s.PullJson["body"] = Body(PassLine("5555555555555555555555555555555555555555"), PassLine("abcdef1"));

        var result = _s.Merge();

        AssertRefused(result, "PR #7's Independent review holds no PASS for candidate 0123456789ab");
        Assert.Equal(["found PASS for 555555555555", "found PASS for abcdef1"], Lines(result.Stderr)[1..]);
    }

    [Fact]
    public void Refusal11_NoIndependentReviewSection()
    {
        _s.PullJson["body"] = "Closes DYD-1.\n\n" + PassLine() + "\n";

        AssertRefused(_s.Merge(), "PR #7's Independent review holds no PASS for candidate 0123456789ab");
    }

    [Fact]
    public void Refusal12_AFailForTheSameCandidate()
    {
        _s.PullJson["body"] = Body(PassLine(), "FAIL - rubric/type: code; contract DYD-1; candidate " + Sha + "; base " + ReviewBase + "; reviewer/model: r; gates 2/3.");

        AssertRefused(_s.Merge(), "the review section also holds a FAIL for 0123456789ab");
    }

    [Theory]
    [InlineData("spec")]
    [InlineData("project-plan")]
    public void Refusal13_ARubricAMergeCannotRestOn(string rubric)
    {
        _s.PullJson["body"] = Body(PassLine(rubric: rubric));

        AssertRefused(_s.Merge(), $"the PASS for 0123456789ab is a {rubric} review; a merge needs a code, docs or merge PASS");
    }

    [Theory]
    [InlineData(200, """{ "status": "diverged" }""")]
    [InlineData(200, """{ "status": "behind" }""")]
    [InlineData(404, """{ "message": "Not Found" }""")]
    public void Refusal14_AReviewBaseTheTargetDoesNotContain(int status, string json)
    {
        _s.GitHub.On("GET", Compare(ReviewBase, Tip), Reply(status, json));

        AssertRefused(_s.Merge(), "the PASS for 0123456789ab reviewed against 222222222222, which feature/x does not contain");
    }

    [Fact]
    public void Refusal14_IsSkippedWithANoteWhenThePassNamesNoBase()
    {
        _s.PullJson["body"] = Body(PassLine(reviewBase: null));

        var (exit, stdout, stderr) = _s.Merge();

        Assert.True(exit == 0, stderr);
        Assert.Contains("note: the PASS for 0123456789ab names no base; the base check is skipped", Lines(stdout));
        Assert.DoesNotContain(_s.GitHub.Calls, call => call.Path == Compare(ReviewBase, Tip));
    }

    [Fact]
    public void Refusal15_NoChecksReported()
    {
        _s.GitHub.On("GET", Suites, Reply(200, SuitePage()));

        AssertRefused(_s.Merge(), "no checks reported on 0123456789ab; wait for CI, or this repo runs no CI for PRs into feature/x");
    }

    [Fact]
    public void Refusal15_ChecksThatAreNotGreen()
    {
        _s.GitHub.On("GET", Runs, Reply(200, RunPage(Run("build", "completed", "failure"))));

        AssertRefused(_s.Merge(), "checks on 0123456789ab are not green: build failure");
    }

    [Fact]
    public void Refusal16_AConflict()
    {
        _s.GitHub.On("POST", Merges, Reply(409, """{ "message": "Merge conflict" }"""));

        var (exit, _, stderr) = _s.Merge();

        Assert.Equal(1, exit);
        Assert.Equal("refused: 0123456789ab conflicts with feature/x; a Merge code-writer resolves it (code-writer merge resource)", Lines(stderr)[0]);
    }

    [Fact]
    public void Refusal17_ABaseGoneWhenTheMergeArrives()
    {
        _s.GitHub.On("POST", Merges, Reply(404, """{ "message": "Base does not exist" }"""));

        var (exit, _, stderr) = _s.Merge();

        Assert.Equal(1, exit);
        Assert.Equal("refused: feature/x moved or no longer exists on acme/widgets; nothing merged - rerun", Lines(stderr)[0]);
    }

    [Fact]
    public void Refusal17_ABaseGoneBeforeTheChecks()
    {
        _s.GitHub.On("GET", Branch, Reply(404, """{ "message": "Branch not found" }"""));

        AssertRefused(_s.Merge(), "feature/x moved or no longer exists on acme/widgets; nothing merged - rerun");
    }

    #endregion
}
