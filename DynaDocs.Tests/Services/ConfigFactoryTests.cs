namespace DynaDocs.Tests.Services;

using System.Text.Json;
using System.Text.RegularExpressions;
using DynaDocs.Models;
using DynaDocs.Serialization;
using DynaDocs.Services;

public class ConfigFactoryTests
{
    [Fact]
    public void CreateDefault_SetsVersion1()
    {
        var config = ConfigFactory.CreateDefault();

        Assert.Equal(1, config.Version);
    }

    [Fact]
    public void CreateDefault_UsesDefaultRoot()
    {
        var config = ConfigFactory.CreateDefault();

        Assert.Equal("dydo", config.Structure.Root);
    }

    [Fact]
    public void CreateDefault_HasNoLocalWorkPathConfiguration()
    {
        var properties = typeof(StructureConfig).GetProperties().Select(p => p.Name).ToList();

        Assert.Equal(["Root"], properties);
    }

    [Fact]
    public void DefaultNudges_DotnetRunPatternExcludesRetiredWorkCommands()
    {
        var nudge = ConfigFactory.DefaultNudges.Single(n => n.Pattern.Contains("dotnet\\s+run"));

        Assert.DoesNotContain("task", nudge.Pattern);
        Assert.DoesNotContain("issue", nudge.Pattern);
        Assert.DoesNotContain("review", nudge.Pattern);
        Assert.Contains("roles", nudge.Pattern);
    }

    [Fact]
    public void CreateDefaultModels_UsesDistinctOpenAiTiers()
    {
        var openAi = ConfigFactory.CreateDefaultModels().Tiers["openai"];

        Assert.Equal("gpt-5.6-sol", openAi["strong"]);
        Assert.Equal("gpt-5.6-terra", openAi["standard"]);
        Assert.Equal("gpt-5.6-luna", openAi["light"]);
    }

    [Fact]
    public void CreateDefault_IncludesDefaultNudges()
    {
        var config = ConfigFactory.CreateDefault();

        Assert.NotEmpty(config.Nudges);
        Assert.Equal(ConfigFactory.DefaultNudges.Count, config.Nudges.Count);
        Assert.All(config.Nudges, n => Assert.False(string.IsNullOrEmpty(n.Pattern)));
    }

    [Fact]
    public void CreateDefault_NudgesAreDeepCopied()
    {
        var config = ConfigFactory.CreateDefault();
        var originalMessage = ConfigFactory.DefaultNudges[0].Message;

        config.Nudges[0].Message = "mutated";

        Assert.Equal(originalMessage, ConfigFactory.DefaultNudges[0].Message);
    }

    [Fact]
    public void CreateDefault_SerializesNoAudienceScopedOrRetiredNudge()
    {
        // The Decision 026 managers-doctrine nudge was the only audience-scoped shipped nudge
        // and the last mention of the retired workflow; both leave with it (DR 045).
        var json = JsonSerializer.Serialize(ConfigFactory.CreateDefault(), DydoConfigJsonContext.Default.DydoConfig);

        Assert.DoesNotContain("\"audience\"", json);
        Assert.DoesNotContain("run-sprint", json);
    }

    [Fact]
    public void NudgeAudience_OmissionDefaultsToAllAndNormalizesOutput()
    {
        var omitted = JsonSerializer.Deserialize("{}", DydoConfigJsonContext.Default.NudgeConfig)!;
        var configured = new NudgeConfig { Audience = "WORKER" };
        var json = JsonSerializer.Serialize(configured, DydoConfigJsonContext.Default.NudgeConfig);

        Assert.Equal("all", omitted.Audience);
        Assert.Contains("\"audience\": \"worker\"", json);
    }

    [Fact]
    public void EnsureDefaultNudges_AddsToEmptyList()
    {
        var config = new DydoConfig();

        var added = ConfigFactory.EnsureDefaultNudges(config);

        Assert.Equal(ConfigFactory.DefaultNudges.Count, added);
        Assert.Equal(ConfigFactory.DefaultNudges.Count, config.Nudges.Count);
    }

    [Fact]
    public void EnsureDefaultNudges_SkipsAlreadyPresent()
    {
        var config = ConfigFactory.CreateDefault();
        var originalCount = config.Nudges.Count;

        var added = ConfigFactory.EnsureDefaultNudges(config);

        Assert.Equal(0, added);
        Assert.Equal(originalCount, config.Nudges.Count);
    }

    [Fact]
    public void EnsureDefaultNudges_PreservesCustomNudges()
    {
        var config = new DydoConfig
        {
            Nudges = [new NudgeConfig { Pattern = "custom-pattern", Message = "Custom", Severity = "block" }]
        };

        var added = ConfigFactory.EnsureDefaultNudges(config);

        Assert.Equal(ConfigFactory.DefaultNudges.Count, added);
        Assert.Equal(ConfigFactory.DefaultNudges.Count + 1, config.Nudges.Count);
        Assert.Contains(config.Nudges, n => n.Pattern == "custom-pattern");
    }

    [Fact]
    public void EnsureDefaultNudges_PreservesCustomNudgeAudience()
    {
        var config = new DydoConfig
        {
            Nudges = [new NudgeConfig { Pattern = "custom-pattern", Message = "Custom", Audience = "worker" }]
        };

        ConfigFactory.EnsureDefaultNudges(config);

        Assert.Equal("worker", config.Nudges.Single(n => n.Pattern == "custom-pattern").Audience);
    }

    [Fact]
    public void CreateDefaultModels_BindsTheDr045Agents()
    {
        var agents = ConfigFactory.CreateDefaultModels().Agents;

        Assert.Equal("strong", agents["project-planner"]);
        Assert.Equal("strong", agents["issue-planner"]);
        Assert.Equal("strong", agents["issue-captain"]);
        Assert.Equal("standard", agents["research"]);
    }

    [Fact]
    public void DefaultNudges_ReviewBlockNudge_WarnsOnlyWhenThePrCarriesNoBlock()
    {
        var nudge = Assert.Single(ConfigFactory.DefaultNudges, n => n.Pattern.Contains(@"pr\s+create"));
        var regex = new Regex(nudge.Pattern, RegexOptions.IgnoreCase);

        Assert.Equal("warn", nudge.Severity);
        Assert.Null(nudge.Tools);
        Assert.Matches(regex, "gh pr create --title x --body 'ships the fix'");
        Assert.DoesNotMatch(regex, "gh pr create --body '## Independent review\nverdict: PASS'");
    }



    [Theory]
    [InlineData("digit worktree add foo")]
    [InlineData("digit worktree remove bar")]
    public void DefaultNudges_DoesNotMatchWordsContainingGit(string command)
    {
        var matchingNudge = ConfigFactory.DefaultNudges.FirstOrDefault(n =>
        {
            var regex = new System.Text.RegularExpressions.Regex(n.Pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return regex.IsMatch(command);
        });

        Assert.Null(matchingNudge);
    }

    [Fact]
    public void DefaultNudges_HasNoInquisitorNudge()
    {
        // The inquisitor role is retired (Decision 024); its dispatch nudge is gone.
        Assert.DoesNotContain(ConfigFactory.DefaultNudges, n => n.Pattern.Contains("inquisitor"));
    }

    [Theory]
    [InlineData("until [ -s /tmp/claude/foo ]; do sleep 1; done")]
    [InlineData("until [ ! -f /tmp/lock ]; do sleep 2; done")]
    [InlineData("until  [ -e foo ]; do :; done")]
    public void DefaultNudges_MatchesOpenEndedUntilLoop_AsWarn(string command)
    {
        var matchingNudge = FindUntilLoopNudge();

        Assert.Matches(matchingNudge.Pattern, command);
        Assert.Equal("warn", matchingNudge.Severity);
        Assert.Contains("0177", matchingNudge.Message);
    }

    [Theory]
    [InlineData("for i in {1..30}; do test -f x; sleep 1; done")]
    [InlineData("gh run watch 12345")]
    [InlineData("dydo wait")]
    [InlineData("dydo wait --work foo")]
    [InlineData("while [ ! -f x ]; do sleep 1; done")]
    public void DefaultNudges_DoesNotMatchValidPollingPatterns(string command)
    {
        var untilNudge = FindUntilLoopNudge();

        Assert.DoesNotMatch(untilNudge.Pattern, command);
    }

    [Fact]
    public void DefaultNudges_UntilLoopNudge_IsIdempotent_InEnsureDefaultNudges()
    {
        var config = ConfigFactory.CreateDefault();
        var firstCount = config.Nudges.Count(n => n.Pattern == @"\buntil\s+\[");

        var added = ConfigFactory.EnsureDefaultNudges(config);
        var secondCount = config.Nudges.Count(n => n.Pattern == @"\buntil\s+\[");

        Assert.Equal(1, firstCount);
        Assert.Equal(1, secondCount);
        Assert.Equal(0, added);
    }

    private static NudgeConfig FindUntilLoopNudge()
    {
        var nudge = ConfigFactory.DefaultNudges.FirstOrDefault(n => n.Pattern == @"\buntil\s+\[");
        Assert.NotNull(nudge);
        return nudge;
    }
}
