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

    [Theory]
    [InlineData("dydo dispatch --role inquisitor --task audit-auth --no-wait --brief test")]
    [InlineData("dydo dispatch --task audit-auth --role inquisitor --no-wait --brief test")]
    [InlineData("dydo dispatch --no-wait --role inquisitor --task foo --brief bar")]
    public void DefaultNudges_MatchesInquisitorDispatchWithoutNewWindow(string command)
    {
        var matchingNudge = ConfigFactory.DefaultNudges.FirstOrDefault(n =>
        {
            var regex = new System.Text.RegularExpressions.Regex(n.Pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return regex.IsMatch(command);
        });

        Assert.NotNull(matchingNudge);
        Assert.Equal("warn", matchingNudge.Severity);
    }

    [Theory]
    [InlineData("dydo dispatch --role inquisitor --new-window --task audit-auth --no-wait --brief test")]
    [InlineData("dydo dispatch --new-window --role inquisitor --task audit-auth --no-wait --brief test")]
    [InlineData("dydo dispatch --role inquisitor --task foo --new-window --no-wait --brief bar")]
    public void DefaultNudges_DoesNotMatchInquisitorDispatchWithNewWindow(string command)
    {
        var matchingNudge = ConfigFactory.DefaultNudges.FirstOrDefault(n =>
        {
            var regex = new System.Text.RegularExpressions.Regex(n.Pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return regex.IsMatch(command);
        });

        // Should not match the inquisitor nudge (may match other nudges, but not the inquisitor one)
        var inquisitorNudge = ConfigFactory.DefaultNudges.FirstOrDefault(n =>
            n.Pattern.Contains("inquisitor"));
        Assert.NotNull(inquisitorNudge);

        var inquisitorRegex = new System.Text.RegularExpressions.Regex(inquisitorNudge.Pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        Assert.False(inquisitorRegex.IsMatch(command));
    }

    [Theory]
    [InlineData("dydo dispatch --role code-writer --task fix-bug --no-wait --brief test")]
    [InlineData("dydo dispatch --role reviewer --task fix-bug --no-wait --brief test")]
    [InlineData("dydo dispatch --role orchestrator --task plan --no-wait --brief test")]
    public void DefaultNudges_DoesNotMatchNonInquisitorDispatch(string command)
    {
        var inquisitorNudge = ConfigFactory.DefaultNudges.FirstOrDefault(n =>
            n.Pattern.Contains("inquisitor"));
        Assert.NotNull(inquisitorNudge);

        var regex = new System.Text.RegularExpressions.Regex(inquisitorNudge.Pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        Assert.False(regex.IsMatch(command));
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
}
