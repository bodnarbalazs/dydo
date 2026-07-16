namespace DynaDocs.Tests.Services;

using DynaDocs.Models;
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
    public void CreateDefaultModels_UsesDistinctOpenAiTiers()
    {
        var openAi = ConfigFactory.CreateDefaultModels().Tiers["openai"];

        Assert.Equal("gpt-5.6-sol", openAi["strong"]);
        Assert.Equal("gpt-5.6-terra", openAi["standard"]);
        Assert.Equal("gpt-5.6-luna", openAi["light"]);
    }

    [Fact]
    public void UpgradeLegacyOpenAiTierDefaults_PreservesCustomizedTiers()
    {
        var config = ConfigFactory.CreateDefault();
        config.Models!.Tiers["openai"]["strong"] = "custom-strong";

        var upgraded = ConfigFactory.UpgradeLegacyOpenAiTierDefaults(config);

        Assert.False(upgraded);
        Assert.Equal("custom-strong", config.Models.Tiers["openai"]["strong"]);
    }

    [Fact]
    public void UpgradeLegacyOpenAiTierDefaults_RebindsLegacyGpt55Tiers()
    {
        var config = ConfigFactory.CreateDefault();
        config.Models!.Tiers["openai"] = new Dictionary<string, string>
        {
            ["strong"] = "gpt-5.5",
            ["standard"] = "gpt-5.5",
            ["light"] = "gpt-5.5"
        };

        var upgraded = ConfigFactory.UpgradeLegacyOpenAiTierDefaults(config);

        Assert.True(upgraded);
        Assert.Equal("gpt-5.6-sol", config.Models.Tiers["openai"]["strong"]);
        Assert.Equal("gpt-5.6-terra", config.Models.Tiers["openai"]["standard"]);
        Assert.Equal("gpt-5.6-luna", config.Models.Tiers["openai"]["light"]);
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
    public void CreateDefault_ShipsNoDefaultQueues()
    {
        var config = ConfigFactory.CreateDefault();

        Assert.Empty(config.Queues);
    }

    [Fact]
    public void EnsureDefaultQueues_AddsToEmptyList()
    {
        var config = new DydoConfig();

        var added = ConfigFactory.EnsureDefaultQueues(config);

        Assert.Equal(ConfigFactory.DefaultQueues.Count, added);
        Assert.Equal(ConfigFactory.DefaultQueues.Count, config.Queues.Count);
    }

    [Fact]
    public void EnsureDefaultQueues_SkipsAlreadyPresent()
    {
        var config = ConfigFactory.CreateDefault();

        var added = ConfigFactory.EnsureDefaultQueues(config);

        Assert.Equal(0, added);
    }

    [Fact]
    public void EnsureDefaultQueues_PreservesCustomQueues()
    {
        var config = new DydoConfig { Queues = ["hotfix"] };

        var added = ConfigFactory.EnsureDefaultQueues(config);

        Assert.Equal(ConfigFactory.DefaultQueues.Count, added);
        Assert.Equal(ConfigFactory.DefaultQueues.Count + 1, config.Queues.Count);
        Assert.Contains("hotfix", config.Queues);
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
    [InlineData("dydo wait --task foo")]
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
