namespace DynaDocs.Tests.Services;

using DynaDocs.Models;
using DynaDocs.Services;

public class ConfigFactoryTests
{
    [Fact]
    public void CreateDefault_SetsVersion1()
    {
        var config = ConfigFactory.CreateDefault("alice");

        Assert.Equal(1, config.Version);
    }

    [Fact]
    public void CreateDefault_AssignsAllAgentsToHuman()
    {
        var config = ConfigFactory.CreateDefault("alice", 5);

        Assert.Equal(5, config.Agents.Pool.Count);
        Assert.Equal(5, config.Agents.Assignments["alice"].Count);
    }

    [Fact]
    public void CreateDefault_UsesDefaultRoot()
    {
        var config = ConfigFactory.CreateDefault("alice");

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
        var config = ConfigFactory.CreateDefault("alice");
        config.Models!.Tiers["openai"]["strong"] = "custom-strong";

        var upgraded = ConfigFactory.UpgradeLegacyOpenAiTierDefaults(config);

        Assert.False(upgraded);
        Assert.Equal("custom-strong", config.Models.Tiers["openai"]["strong"]);
    }

    [Fact]
    public void UpgradeLegacyOpenAiTierDefaults_EmptyDisplayNames_RemainsEmptyAndUsesShippedDefaults()
    {
        var config = ConfigFactory.CreateDefault("alice");
        config.Models!.Tiers["openai"] = new Dictionary<string, string>
        {
            ["strong"] = "gpt-5.5",
            ["standard"] = "gpt-5.5",
            ["light"] = "gpt-5.5"
        };
        config.Models.DisplayNames.Clear();

        var upgraded = ConfigFactory.UpgradeLegacyOpenAiTierDefaults(config);

        Assert.True(upgraded);
        Assert.Empty(config.Models.DisplayNames);
        Assert.Equal("Sonnet 5", ModelDisplay.Resolve("claude-sonnet-5", config.Models));
    }

    [Fact]
    public void UpgradeLegacyOpenAiTierDefaults_NonEmptyDisplayNames_AddsOpenAiNamesAndPreservesCustomEntries()
    {
        var config = ConfigFactory.CreateDefault("alice");
        config.Models!.Tiers["openai"] = new Dictionary<string, string>
        {
            ["strong"] = "gpt-5.5",
            ["standard"] = "gpt-5.5",
            ["light"] = "gpt-5.5"
        };
        config.Models.DisplayNames = new Dictionary<string, string> { ["custom-model"] = "Custom model" };

        var upgraded = ConfigFactory.UpgradeLegacyOpenAiTierDefaults(config);

        Assert.True(upgraded);
        Assert.Equal("Custom model", config.Models.DisplayNames["custom-model"]);
        Assert.Equal("Gpt 5.6 Sol", config.Models.DisplayNames["gpt-5.6-sol"]);
        Assert.Equal("Gpt 5.6 Terra", config.Models.DisplayNames["gpt-5.6-terra"]);
        Assert.Equal("Gpt 5.6 Luna", config.Models.DisplayNames["gpt-5.6-luna"]);
    }

    [Fact]
    public void AddHuman_AssignsAvailableAgents()
    {
        var config = ConfigFactory.CreateDefault("alice", 5);
        ConfigFactory.AddHuman(config, "bob", 3);

        Assert.True(config.Agents.Assignments.ContainsKey("bob"));
    }

    [Fact]
    public void AddHuman_ExpandsPool_WhenNotEnoughAvailable()
    {
        var config = ConfigFactory.CreateDefault("alice", 3);
        var originalPoolSize = config.Agents.Pool.Count;

        ConfigFactory.AddHuman(config, "bob", 5);

        Assert.True(config.Agents.Pool.Count > originalPoolSize);
        Assert.Equal(5, config.Agents.Assignments["bob"].Count);
    }

    [Fact]
    public void AddHuman_UsesUnassignedAgentsFirst()
    {
        var config = new DydoConfig
        {
            Agents = new AgentsConfig
            {
                Pool = ["Adele", "Brian", "Charlie", "Dexter"],
                Assignments = new Dictionary<string, List<string>>
                {
                    ["alice"] = ["Adele", "Brian"]
                }
            }
        };

        ConfigFactory.AddHuman(config, "bob", 2);

        var bobAgents = config.Agents.Assignments["bob"];
        Assert.Contains("Charlie", bobAgents);
        Assert.Contains("Dexter", bobAgents);
    }

    [Fact]
    public void CreateDefault_IncludesDefaultNudges()
    {
        var config = ConfigFactory.CreateDefault("alice");

        Assert.NotEmpty(config.Nudges);
        Assert.Equal(ConfigFactory.DefaultNudges.Count, config.Nudges.Count);
        Assert.All(config.Nudges, n => Assert.False(string.IsNullOrEmpty(n.Pattern)));
    }

    [Fact]
    public void CreateDefault_NudgesAreDeepCopied()
    {
        var config = ConfigFactory.CreateDefault("alice");
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
        var config = ConfigFactory.CreateDefault("alice");
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
    public void CreateDefault_IncludesDefaultQueues()
    {
        var config = ConfigFactory.CreateDefault("alice");

        Assert.Contains("merge", config.Queues);
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
        var config = ConfigFactory.CreateDefault("alice");

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
    [InlineData("git worktree add my-wt")]
    [InlineData("git worktree remove my-wt")]
    [InlineData("git -C repo worktree add feature")]
    [InlineData("git -C repo worktree remove feature")]
    public void DefaultNudges_ContainsGitWorktreeBlockNudge(string command)
    {
        var matchingNudge = ConfigFactory.DefaultNudges.FirstOrDefault(n =>
        {
            var regex = new System.Text.RegularExpressions.Regex(n.Pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return regex.IsMatch(command);
        });

        Assert.NotNull(matchingNudge);
        Assert.Equal("block", matchingNudge.Severity);
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
    [InlineData("rm -rf dydo/_system/.local/worktrees/abc123")]
    [InlineData("rm -r dydo/_system/.local/worktrees/")]
    public void DefaultNudges_ContainsRmWorktreeBlockNudge(string command)
    {
        var matchingNudge = ConfigFactory.DefaultNudges.FirstOrDefault(n =>
        {
            var regex = new System.Text.RegularExpressions.Regex(n.Pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return regex.IsMatch(command);
        });

        Assert.NotNull(matchingNudge);
        Assert.Equal("block", matchingNudge.Severity);
    }

    [Theory]
    [InlineData("dydo worktree merge --force")]
    [InlineData("dydo worktree merge --finalize --force")]
    [InlineData("dydo   worktree   merge --foo --force")]
    public void DefaultNudges_MatchesWorktreeMergeForce_AsWarn(string command)
    {
        var matchingNudge = ConfigFactory.DefaultNudges.FirstOrDefault(n =>
        {
            var regex = new System.Text.RegularExpressions.Regex(n.Pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return regex.IsMatch(command);
        });

        Assert.NotNull(matchingNudge);
        Assert.Equal("warn", matchingNudge.Severity);
        Assert.Contains("destroy", matchingNudge.Message);
    }

    [Theory]
    [InlineData("dydo worktree merge")]
    [InlineData("dydo worktree merge --finalize")]
    [InlineData("dydo worktree cleanup --force my-wt")]
    public void DefaultNudges_DoesNotMatchWorktreeMerge_WithoutForce_OrOtherForce(string command)
    {
        var forceNudge = ConfigFactory.DefaultNudges.FirstOrDefault(n =>
            n.Pattern.Contains("worktree") && n.Pattern.Contains("merge") && n.Severity == "warn");
        Assert.NotNull(forceNudge);

        Assert.DoesNotMatch(forceNudge.Pattern, command);
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
        var config = ConfigFactory.CreateDefault("alice");
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
