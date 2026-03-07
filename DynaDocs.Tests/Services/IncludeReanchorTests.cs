namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

public class IncludeReanchorTests
{
    #region ExtractUserIncludes

    [Fact]
    public void ExtractUserIncludes_NoUserTags_ReturnsEmpty()
    {
        var stock = "## Work\n\n1. Step one\n{{include:extra-verify}}\n2. Step two\n";
        var user = "## Work\n\n1. Step one\n{{include:extra-verify}}\n2. Step two\n";

        var result = IncludeReanchor.ExtractUserIncludes(stock, user);

        Assert.Empty(result);
    }

    [Fact]
    public void ExtractUserIncludes_OneUserTag_ExtractsWithAnchors()
    {
        var stock = "## Work\n\n1. Step one\n2. Step two\n";
        var user = "## Work\n\n1. Step one\n{{include:my-custom}}\n2. Step two\n";

        var result = IncludeReanchor.ExtractUserIncludes(stock, user);

        Assert.Single(result);
        Assert.Equal("{{include:my-custom}}", result[0].Tag);
        Assert.Equal("1. Step one", result[0].UpperAnchor);
        Assert.Equal("2. Step two", result[0].LowerAnchor);
    }

    [Fact]
    public void ExtractUserIncludes_MultipleUserTags_ExtractsAll()
    {
        var stock = "## Work\n\n1. Step one\n2. Step two\n3. Step three\n";
        var user = "## Work\n\n1. Step one\n{{include:hook-a}}\n2. Step two\n{{include:hook-b}}\n3. Step three\n";

        var result = IncludeReanchor.ExtractUserIncludes(stock, user);

        Assert.Equal(2, result.Count);
        Assert.Equal("{{include:hook-a}}", result[0].Tag);
        Assert.Equal("{{include:hook-b}}", result[1].Tag);
    }

    [Fact]
    public void ExtractUserIncludes_ShippedTagsIgnored()
    {
        var stock = "## Work\n\n{{include:extra-verify}}\n## Done\n";
        var user = "## Work\n\n{{include:extra-verify}}\n{{include:my-custom}}\n## Done\n";

        var result = IncludeReanchor.ExtractUserIncludes(stock, user);

        Assert.Single(result);
        Assert.Equal("{{include:my-custom}}", result[0].Tag);
    }

    [Fact]
    public void ExtractUserIncludes_TagBetweenBlankLines_SkipsBlanksForAnchors()
    {
        var stock = "Line A\n\nLine B\n";
        var user = "Line A\n\n{{include:custom}}\n\nLine B\n";

        var result = IncludeReanchor.ExtractUserIncludes(stock, user);

        Assert.Single(result);
        Assert.Equal("Line A", result[0].UpperAnchor);
        Assert.Equal("Line B", result[0].LowerAnchor);
    }

    [Fact]
    public void ExtractUserIncludes_TagAtTopOfFile_UpperAnchorNull()
    {
        var stock = "Line B\n";
        var user = "{{include:top-hook}}\nLine B\n";

        var result = IncludeReanchor.ExtractUserIncludes(stock, user);

        Assert.Single(result);
        Assert.Null(result[0].UpperAnchor);
        Assert.Equal("Line B", result[0].LowerAnchor);
    }

    [Fact]
    public void ExtractUserIncludes_TagAtBottomOfFile_LowerAnchorNull()
    {
        var stock = "Line A\n";
        var user = "Line A\n{{include:bottom-hook}}\n";

        var result = IncludeReanchor.ExtractUserIncludes(stock, user);

        Assert.Single(result);
        Assert.Equal("Line A", result[0].UpperAnchor);
        Assert.Null(result[0].LowerAnchor);
    }

    #endregion

    #region Reanchor — both anchors found

    [Fact]
    public void Reanchor_BothAnchorsFound_InsertsCorrectly()
    {
        var newContent = "## Work\n\n1. Step one\n2. Step two\n";
        var includes = new List<IncludeReanchor.IncludeTag>
        {
            new("{{include:my-hook}}", "1. Step one", "2. Step two")
        };

        var result = IncludeReanchor.Reanchor(newContent, includes);

        Assert.Contains("{{include:my-hook}}", result.Content);
        Assert.Single(result.Placed);
        Assert.Empty(result.Unplaced);

        var lines = result.Content.Split('\n');
        var hookIdx = Array.IndexOf(lines, "{{include:my-hook}}");
        var step1Idx = Array.FindIndex(lines, l => l.Trim() == "1. Step one");
        var step2Idx = Array.FindIndex(lines, l => l.Trim() == "2. Step two");
        Assert.True(hookIdx > step1Idx);
        Assert.True(hookIdx < step2Idx + 1); // +1 because we inserted before step2's original position
    }

    [Fact]
    public void Reanchor_MultipleUserTags_AllPlaced()
    {
        var newContent = "Line A\nLine B\nLine C\n";
        var includes = new List<IncludeReanchor.IncludeTag>
        {
            new("{{include:hook-1}}", "Line A", "Line B"),
            new("{{include:hook-2}}", "Line B", "Line C")
        };

        var result = IncludeReanchor.Reanchor(newContent, includes);

        Assert.Equal(2, result.Placed.Count);
        Assert.Empty(result.Unplaced);
        Assert.Contains("{{include:hook-1}}", result.Content);
        Assert.Contains("{{include:hook-2}}", result.Content);
    }

    [Fact]
    public void Reanchor_AnchorMatchIsTrimmed()
    {
        var newContent = "  1. Step one  \n  2. Step two  \n";
        var includes = new List<IncludeReanchor.IncludeTag>
        {
            new("{{include:my-hook}}", "1. Step one", "2. Step two")
        };

        var result = IncludeReanchor.Reanchor(newContent, includes);

        Assert.Single(result.Placed);
    }

    #endregion

    #region Reanchor — partial anchors

    [Fact]
    public void Reanchor_OnlyUpperAnchor_InsertsAfterIt()
    {
        var newContent = "Line A\nLine B\n";
        var includes = new List<IncludeReanchor.IncludeTag>
        {
            new("{{include:hook}}", "Line A", "Line GONE")
        };

        var result = IncludeReanchor.Reanchor(newContent, includes);

        Assert.Single(result.Placed);
        var lines = result.Content.Split('\n');
        var hookIdx = Array.IndexOf(lines, "{{include:hook}}");
        var upperIdx = Array.FindIndex(lines, l => l.Trim() == "Line A");
        Assert.Equal(upperIdx + 1, hookIdx);
    }

    [Fact]
    public void Reanchor_OnlyLowerAnchor_InsertsBeforeIt()
    {
        var newContent = "Line A\nLine B\n";
        var includes = new List<IncludeReanchor.IncludeTag>
        {
            new("{{include:hook}}", "Line GONE", "Line B")
        };

        var result = IncludeReanchor.Reanchor(newContent, includes);

        Assert.Single(result.Placed);
        var lines = result.Content.Split('\n');
        var hookIdx = Array.IndexOf(lines, "{{include:hook}}");
        var lowerIdx = Array.FindIndex(lines, l => l.Trim() == "Line B");
        Assert.True(hookIdx < lowerIdx);
    }

    #endregion

    #region Reanchor — no anchors

    [Fact]
    public void Reanchor_NeitherAnchorFound_ReportsUnplaced()
    {
        var newContent = "Line X\nLine Y\n";
        var includes = new List<IncludeReanchor.IncludeTag>
        {
            new("{{include:lost-hook}}", "Line GONE-A", "Line GONE-B")
        };

        var result = IncludeReanchor.Reanchor(newContent, includes);

        Assert.Empty(result.Placed);
        Assert.Single(result.Unplaced);
        Assert.Equal("{{include:lost-hook}}", result.Unplaced[0]);
        Assert.DoesNotContain("{{include:lost-hook}}", result.Content);
    }

    [Fact]
    public void Reanchor_MixOfPlacedAndUnplaced_CorrectLists()
    {
        var newContent = "Line A\nLine B\n";
        var includes = new List<IncludeReanchor.IncludeTag>
        {
            new("{{include:found}}", "Line A", "Line B"),
            new("{{include:lost}}", "Line GONE", "Line ALSO-GONE")
        };

        var result = IncludeReanchor.Reanchor(newContent, includes);

        Assert.Single(result.Placed);
        Assert.Single(result.Unplaced);
        Assert.Equal("{{include:found}}", result.Placed[0]);
        Assert.Equal("{{include:lost}}", result.Unplaced[0]);
    }

    [Fact]
    public void Reanchor_AnchorAppearsMultipleTimes_UsesFirstOccurrence()
    {
        var newContent = "Repeated line\nMiddle\nRepeated line\nEnd\n";
        var includes = new List<IncludeReanchor.IncludeTag>
        {
            new("{{include:hook}}", "Repeated line", "Middle")
        };

        var result = IncludeReanchor.Reanchor(newContent, includes);

        Assert.Single(result.Placed);
        var lines = result.Content.Split('\n');
        var hookIdx = Array.IndexOf(lines, "{{include:hook}}");
        Assert.Equal(1, hookIdx); // After first "Repeated line"
    }

    [Fact]
    public void Reanchor_EmptyNewTemplate_AllUnplaced()
    {
        var includes = new List<IncludeReanchor.IncludeTag>
        {
            new("{{include:hook}}", "Line A", "Line B")
        };

        var result = IncludeReanchor.Reanchor("", includes);

        Assert.Empty(result.Placed);
        Assert.Single(result.Unplaced);
    }

    #endregion
}
