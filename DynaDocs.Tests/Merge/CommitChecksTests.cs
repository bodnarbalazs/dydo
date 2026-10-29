namespace DynaDocs.Tests.Merge;

using System.Text.Json.Nodes;
using DynaDocs.Services.Merge;
using static FakeGitHub;
using static MergeScenario;

/// <summary>
/// The CI table (design §2, CI rule): every check suite, check run and legacy status on the SHA must
/// have finished green, every required check must be among them, and some suite must hold CI.
/// </summary>
[Collection("Integration")]
public sealed class CommitChecksTests : IDisposable
{
    private const string NotGreen = "checks on 0123456789ab are not green: ";

    private readonly MergeScenario _s = new();

    public void Dispose() => _s.Dispose();

    private string? Refusal() => CommitChecks.Refusal(_s.GitHub, Repo, Sha, Target, _s.BranchJson);

    private void RunsAre(params JsonObject[] runs) => _s.GitHub.On("GET", Runs, Reply(200, RunPage(runs)));

    private void SuitesAre(params JsonObject[] suites) => _s.GitHub.On("GET", Suites, Reply(200, SuitePage(suites)));

    [Fact]
    public void GreenSuiteAndRunWithNoLegacyStatuses_IsGreen()
    {
        Assert.Null(Refusal());
    }

    [Theory]
    [InlineData("queued")]
    [InlineData("in_progress")]
    [InlineData("pending")]
    [InlineData("requested")]
    [InlineData("waiting")]
    [InlineData("mystery")]
    public void ARunThatHasNotCompleted_IsNotGreen(string status)
    {
        RunsAre(Run("build", status, null));

        Assert.Equal(NotGreen + $"build {status}", Refusal());
    }

    [Theory]
    [InlineData("queued")]
    [InlineData("in_progress")]
    [InlineData("pending")]
    [InlineData("requested")]
    [InlineData("waiting")]
    [InlineData("mystery")]
    public void ASuiteWithRunsThatHasNotCompleted_IsNotGreen(string status)
    {
        SuitesAre(Suite("GitHub Actions", status, null, runs: 2));

        Assert.Equal(NotGreen + $"GitHub Actions {status}", Refusal());
    }

    [Theory]
    [InlineData("success")]
    [InlineData("neutral")]
    [InlineData("skipped")]
    public void AGreenConclusion_Passes(string conclusion)
    {
        SuitesAre(Suite("GitHub Actions", "completed", conclusion));
        RunsAre(Run("build", "completed", conclusion));

        Assert.Null(Refusal());
    }

    [Theory]
    [InlineData("failure")]
    [InlineData("cancelled")]
    [InlineData("timed_out")]
    [InlineData("action_required")]
    [InlineData("stale")]
    [InlineData("startup_failure")]
    [InlineData("exploded")]
    public void AnyOtherConclusion_IsNotGreen(string conclusion)
    {
        RunsAre(Run("build", "completed", conclusion));

        Assert.Equal(NotGreen + $"build {conclusion}", Refusal());
    }

    [Fact]
    public void ACompletedRunWithoutAConclusion_IsNotGreen()
    {
        RunsAre(Run("build", "completed", null));

        Assert.Equal(NotGreen + "build no conclusion", Refusal());
    }

    [Fact]
    public void EveryProblemIsNamed()
    {
        RunsAre(Run("build", "completed", "failure"), Run("lint", "completed", "success"), Run("e2e", "in_progress", null));

        Assert.Equal(NotGreen + "build failure; e2e in_progress", Refusal());
    }

    [Fact]
    public void AFailureBeyondPageOne_IsRead()
    {
        var green = Enumerable.Range(0, 100).Select(i => Run($"shard {i}", "completed", "success")).ToArray();
        var page1 = RunPage(green);
        page1["total_count"] = 101;
        _s.GitHub.On("GET", Runs, Reply(200, page1));
        _s.GitHub.On("GET", Runs.Replace("&page=1", "&page=2"), Reply(200, RunPage(Run("shard 100", "completed", "failure"))));

        Assert.Equal(NotGreen + "shard 100 failure", Refusal());
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("failure")]
    [InlineData("error")]
    [InlineData("mystery")]
    public void ALegacyStatusThatIsNotSuccess_IsNotGreen(string state)
    {
        _s.GitHub.On("GET", Statuses, Reply(200, StatusPage(Status("ci/legacy", "success"), Status("ci/deploy", state))));

        Assert.Equal(NotGreen + $"ci/deploy {state}", Refusal());
    }

    [Fact]
    public void ARequiredCheckMissing_IsNotGreen()
    {
        var required = _s.BranchJson["protection"]!["required_status_checks"]!;
        required["enforcement_level"] = "everyone";
        required["contexts"] = new JsonArray("build", "ci/legacy", "deploy-preview");
        _s.GitHub.On("GET", Statuses, Reply(200, StatusPage(Status("ci/legacy", "success"))));

        Assert.Equal(NotGreen + "deploy-preview missing", Refusal());
    }

    [Fact]
    public void RequiredChecksOfProtectionThatIsOff_AreNotRequired()
    {
        _s.BranchJson["protection"]!["required_status_checks"]!["contexts"] = new JsonArray("deploy-preview");

        Assert.Null(Refusal());
    }

    [Fact]
    public void ARulesetsRequiredCheckMissing_IsNotGreen()
    {
        _s.GitHub.On("GET", Rules, Reply(200, """
            [
              { "type": "deletion" },
              { "type": "required_status_checks", "parameters": { "required_status_checks": [ { "context": "build" }, { "context": "lint" } ] } }
            ]
            """));

        Assert.Equal(NotGreen + "lint missing", Refusal());
    }

    // bodnarbalazs/LC is private on GitHub's free plan, which has no rulesets and answers 403 for them.
    [Fact]
    public void ARepositoryWhosePlanHasNoRulesets_RequiresNoRulesetChecks()
    {
        _s.GitHub.On("GET", Rules, Reply(403, """{ "message": "Upgrade to GitHub Pro or make this repository public to enable this feature.", "status": "403" }"""));

        Assert.Null(Refusal());
    }

    [Fact]
    public void RulesetsGitHubWillNotShow_AreAToolError()
    {
        _s.GitHub.On("GET", Rules, Reply(403, """{ "message": "Resource not accessible by integration" }"""));

        var stop = Assert.Throws<MergeStop>(Refusal);

        Assert.Equal(2, stop.ExitCode);
        Assert.Contains("Resource not accessible by integration (HTTP 403)", stop.Lines[0]);
    }

    [Fact]
    public void ZeroSuites_IsNoChecks()
    {
        SuitesAre();
        RunsAre();

        Assert.Equal("no checks reported on 0123456789ab; wait for CI, or this repo runs no CI for PRs into feature/x", Refusal());
    }

    // blacksmith-sh leaves a queued suite with no runs on every bodnarbalazs/dydo and LC commit.
    [Fact]
    public void AQueuedSuiteThatHoldsNoRun_IsNoCiYet()
    {
        SuitesAre(Suite("blacksmith.sh", "queued", null, runs: 0), Suite("GitHub Actions", "completed", "success"));

        Assert.Null(Refusal());
    }

    [Fact]
    public void OnlyAQueuedSuiteThatHoldsNoRun_IsNoChecks()
    {
        SuitesAre(Suite("blacksmith.sh", "queued", null, runs: 0));
        RunsAre();

        Assert.Equal("no checks reported on 0123456789ab; wait for CI, or this repo runs no CI for PRs into feature/x", Refusal());
    }

    [Fact]
    public void ACompletedSuiteThatHoldsNoRun_StillCounts()
    {
        SuitesAre(Suite("GitHub Actions", "completed", "startup_failure", runs: 0));
        RunsAre();

        Assert.Equal(NotGreen + "GitHub Actions startup_failure", Refusal());
    }

    [Fact]
    public void UnreadableChecks_AreAToolError()
    {
        _s.GitHub.On("GET", Runs, Reply(0));

        var stop = Assert.Throws<MergeStop>(Refusal);

        Assert.Equal(2, stop.ExitCode);
        Assert.StartsWith("error: could not read the checks on 0123456789ab", stop.Lines[0]);
    }
}
