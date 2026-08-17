namespace DynaDocs.Tests.Sync.Projection;

using DynaDocs.Sync.Projection;

public sealed class ProjectedMarkdownMutationTests
{
    [Theory]
    [InlineData("- [ ] task", "- [x] task")]
    [InlineData("`code`", "`changed`")]
    [InlineData("| a | b |\n| - | - |\n| 1 | 2 |", "| a | b |\n| - | - |\n| 2 | 1 |")]
    [InlineData("> quote", "> changed")]
    [InlineData("```txt\ncode\n```", "```txt\nchanged\n```")]
    [InlineData("```txt\n- item\n```", "```txt\n* item\n```")]
    [InlineData("a * b", "a + b")]
    [InlineData("punctuation!", "punctuation?")]
    [InlineData("escape \\*", "escape \\+")]
    [InlineData("a ** b", "a __ b")]
    public void ObservableSemanticMutations_AreNotNormalizedAway(string body, string external)
    {
        var result = ProjectedMarkdownMerge.Merge(body, body, body, external);

        Assert.True(result.IsSuccess, result.Conflict?.Reason);
        Assert.Equal(external, result.Body);
    }

    [Fact]
    public void NestedListEdit_LeavesSiblingSourceUntouched()
    {
        var result = ProjectedMarkdownMerge.Merge("- parent\n  - child\n\nend", "- parent\n  - child\n\nend",
            "- parent\n  - child\n\nend", "- parent\n  - changed\n\nend");

        Assert.True(result.IsSuccess);
        Assert.Equal("- parent\n  - changed\n\nend", result.Body);
    }

    [Fact]
    public void OversizedDocument_FailsClosed()
    {
        var huge = new string('x', 2 * 1024 * 1024 + 1);
        var result = ProjectedMarkdownMerge.Merge(huge, huge, huge, huge);

        Assert.False(result.IsSuccess);
        Assert.Contains("2 MiB", result.Conflict!.Reason);
    }

    [Fact]
    public void SemanticTextMap_UsesParsedLiteralRangesWithoutDroppingPunctuation()
    {
        var map = SemanticTextMap.Create("a * b");

        Assert.Equal("a * b", map.Text);
        Assert.Equal(5, map.RawEnd(4));
    }

    [Fact]
    public void SemanticTextMap_ExcludesListMarkersButKeepsNestedLeafText()
    {
        Assert.Equal("parentchild", SemanticTextMap.Create("- parent\n  - child").Text.Replace("\n", ""));
        Assert.Equal("parentchild", SemanticTextMap.Create("* parent\n    * child").Text.Replace("\n", ""));
    }

    [Fact]
    public void SemanticTextMap_ExcludesEquivalentEmphasisMarkers()
    {
        Assert.Equal("hello", SemanticTextMap.Create("__hello__").Text);
        Assert.Equal("hello", SemanticTextMap.Create("**hello**").Text);
    }

    [Fact]
    public void LargeUnanchoredSemanticLeaf_ReturnsBoundedConflict()
    {
        var before = string.Join(' ', Enumerable.Range(0, 257).Select(i => "before" + i));
        var after = string.Join(' ', Enumerable.Range(0, 257).Select(i => "after" + i));
        var result = ProjectedMarkdownMerge.Merge(before, before, before, after);

        Assert.False(result.IsSuccess);
        Assert.Contains("256-token", result.Conflict!.Reason);
    }

    [Fact]
    public void LiteralAsteriskRuns_AreNotMistakenForEmphasis()
    {
        var result = ProjectedMarkdownMerge.Merge("a ** b ** c", "a ** b ** c", "a ** b ** c", "a __ b __ c");

        Assert.True(result.IsSuccess, result.Conflict?.Reason);
        Assert.Equal("a __ b __ c", result.Body);
    }

    [Fact]
    public void InlineCodeLiteralAsteriskRuns_AreNotMistakenForEmphasis()
    {
        var result = ProjectedMarkdownMerge.Merge("`a ** b ** c`", "`a ** b ** c`", "`a ** b ** c`", "`a __ b __ c`");

        Assert.True(result.IsSuccess, result.Conflict?.Reason);
        Assert.Equal("`a __ b __ c`", result.Body);
    }

    [Fact]
    public void FencedCodeLanguageAndLiteral_AlignAcrossFenceSpellings()
    {
        var result = ProjectedMarkdownMerge.Merge("~~~txt\ncode\n~~~", "```txt\ncode\n```", "~~~txt\ncode\n~~~", "```txt\nchanged\n```");

        Assert.True(result.IsSuccess, result.Conflict?.Reason);
        Assert.Equal("~~~txt\nchanged\n~~~", result.Body);
    }

}
