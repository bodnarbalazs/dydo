namespace DynaDocs.Tests.Services;

using DynaDocs.Models;
using DynaDocs.Services;

public class WorktreeMergeSafetyTests
{
    private static DydoConfig DefaultConfig() => new()
    {
        Structure = new StructureConfig { Root = "dydo", Tasks = "project/tasks" },
        Paths = new PathsConfig
        {
            Source = ["Commands/**", "Services/**", "Models/**", "Program.cs"],
            Tests = ["DynaDocs.Tests/**"]
        }
    };

    [Fact]
    public void Classify_EmptyInput_ReturnsEmpty()
    {
        var result = WorktreeMergeSafety.Classify(string.Empty, DefaultConfig());

        Assert.Empty(result.Junk);
        Assert.Empty(result.Suspicious);
    }

    [Fact]
    public void Classify_OnlyJunk_ReturnsJunkEmptySuspicious()
    {
        var porcelain = string.Join("\n",
            "?? dydo/_system/audit/2026/abc.json",
            "?? DynaDocs.Tests/coverage/__pycache__/cache.pyc",
            "?? bin/Debug/app.dll",
            "?? obj/Debug/app.pdb",
            "?? debug.log"
        );

        var result = WorktreeMergeSafety.Classify(porcelain, DefaultConfig());

        Assert.Empty(result.Suspicious);
        Assert.Equal(5, result.Junk.Count);
    }

    [Fact]
    public void Classify_OnlySuspicious_ReturnsAllSuspicious()
    {
        var porcelain = string.Join("\n",
            " M Services/Foo.cs",
            "?? dydo/project/tasks/new-task.md"
        );

        var result = WorktreeMergeSafety.Classify(porcelain, DefaultConfig());

        Assert.Empty(result.Junk);
        Assert.Equal(2, result.Suspicious.Count);
    }

    [Fact]
    public void Classify_Mixed_SeparatesCorrectly()
    {
        var porcelain = string.Join("\n",
            " M Services/Foo.cs",
            "?? dydo/_system/audit/2026/xyz.json",
            "?? dydo/project/tasks/work.md"
        );

        var result = WorktreeMergeSafety.Classify(porcelain, DefaultConfig());

        Assert.Single(result.Junk);
        Assert.Equal("dydo/_system/audit/2026/xyz.json", result.Junk[0].Path);
        Assert.Equal(2, result.Suspicious.Count);
    }

    [Fact]
    public void Classify_TaskFile_CategorizedAsTaskFile()
    {
        var porcelain = "?? dydo/project/tasks/my-task.md";

        var result = WorktreeMergeSafety.Classify(porcelain, DefaultConfig());

        var file = Assert.Single(result.Suspicious);
        Assert.Equal(SuspiciousCategory.TaskFile, file.Category);
    }

    [Fact]
    public void Classify_SourceFile_CategorizedAsSource()
    {
        var porcelain = " M Commands/WorktreeCommand.cs";

        var result = WorktreeMergeSafety.Classify(porcelain, DefaultConfig());

        var file = Assert.Single(result.Suspicious);
        Assert.Equal(SuspiciousCategory.Source, file.Category);
    }

    [Fact]
    public void Classify_TestFile_CategorizedAsTest()
    {
        var porcelain = "?? DynaDocs.Tests/Services/FooTests.cs";

        var result = WorktreeMergeSafety.Classify(porcelain, DefaultConfig());

        var file = Assert.Single(result.Suspicious);
        Assert.Equal(SuspiciousCategory.Test, file.Category);
    }

    [Fact]
    public void Classify_UnknownPath_CategorizedAsOther()
    {
        var porcelain = "?? random/unknown.txt";

        var result = WorktreeMergeSafety.Classify(porcelain, DefaultConfig());

        var file = Assert.Single(result.Suspicious);
        Assert.Equal(SuspiciousCategory.Other, file.Category);
    }

    [Fact]
    public void Classify_IgnoreDefaultsFalse_BuiltInsNotApplied()
    {
        var config = DefaultConfig();
        config.Worktree.MergeSafety.IgnoreDefaults = false;

        var porcelain = "?? dydo/_system/audit/2026/xyz.json";

        var result = WorktreeMergeSafety.Classify(porcelain, config);

        Assert.Empty(result.Junk);
        Assert.Single(result.Suspicious);
    }

    [Fact]
    public void Classify_UserIgnorePatternsApplied()
    {
        var config = DefaultConfig();
        config.Worktree.MergeSafety.Ignore.Add(".claude/settings.local.json");

        var porcelain = " M .claude/settings.local.json";

        var result = WorktreeMergeSafety.Classify(porcelain, config);

        var junk = Assert.Single(result.Junk);
        Assert.Equal(".claude/settings.local.json", junk.MatchedPattern);
        Assert.Empty(result.Suspicious);
    }

    [Fact]
    public void Classify_Rename_UsesNewPath()
    {
        var porcelain = "R  old.cs -> Services/New.cs";

        var result = WorktreeMergeSafety.Classify(porcelain, DefaultConfig());

        var file = Assert.Single(result.Suspicious);
        Assert.Equal("Services/New.cs", file.Path);
        Assert.Equal(SuspiciousCategory.Source, file.Category);
    }

    [Fact]
    public void Classify_QuotedPathWithSpace_Unquoted()
    {
        var porcelain = "?? \"Services/My File.cs\"";

        var result = WorktreeMergeSafety.Classify(porcelain, DefaultConfig());

        var file = Assert.Single(result.Suspicious);
        Assert.Equal("Services/My File.cs", file.Path);
        Assert.Equal(SuspiciousCategory.Source, file.Category);
    }

    [Fact]
    public void Classify_BackslashPathsNormalizedToForwardSlash()
    {
        var porcelain = @"?? Services\Foo.cs";

        var result = WorktreeMergeSafety.Classify(porcelain, DefaultConfig());

        var file = Assert.Single(result.Suspicious);
        Assert.Equal("Services/Foo.cs", file.Path);
    }

    [Fact]
    public void Classify_IgnoreDefaultsOn_UserPatternsStillAppended()
    {
        var config = DefaultConfig();
        config.Worktree.MergeSafety.Ignore.Add("extra/**");

        var porcelain = string.Join("\n",
            "?? dydo/_system/audit/2026/x.json",
            "?? extra/custom.txt"
        );

        var result = WorktreeMergeSafety.Classify(porcelain, config);

        Assert.Equal(2, result.Junk.Count);
        Assert.Empty(result.Suspicious);
    }

    [Fact]
    public void Classify_CustomTasksStructure_TaskFilePrefixRespected()
    {
        var config = DefaultConfig();
        config.Structure.Tasks = "workboard/tickets";

        var porcelain = "?? dydo/workboard/tickets/tk-1.md";

        var result = WorktreeMergeSafety.Classify(porcelain, config);

        var file = Assert.Single(result.Suspicious);
        Assert.Equal(SuspiciousCategory.TaskFile, file.Category);
    }

    [Fact]
    public void EffectiveIgnorePatterns_DefaultsOn_CombinesBuiltinsAndUser()
    {
        var config = DefaultConfig();
        config.Worktree.MergeSafety.Ignore.Add("foo/**");

        var patterns = WorktreeMergeSafety.EffectiveIgnorePatterns(config);

        Assert.Contains("dydo/_system/audit/**", patterns);
        Assert.Contains("foo/**", patterns);
    }

    [Fact]
    public void EffectiveIgnorePatterns_DefaultsOff_OnlyUserPatterns()
    {
        var config = DefaultConfig();
        config.Worktree.MergeSafety.IgnoreDefaults = false;
        config.Worktree.MergeSafety.Ignore.Add("foo/**");

        var patterns = WorktreeMergeSafety.EffectiveIgnorePatterns(config);

        Assert.Single(patterns);
        Assert.Equal("foo/**", patterns[0]);
    }
}
