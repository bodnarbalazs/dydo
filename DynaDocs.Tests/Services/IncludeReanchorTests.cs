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

    #region Reanchor — ambiguous anchors (hypothesis hyp-3)

    [Fact]
    public void Reanchor_AnchorIsDashes_MatchesFrontmatterInsteadOfHorizontalRule()
    {
        // Hypothesis hyp-3: When '---' appears as both YAML frontmatter delimiters
        // and a horizontal rule, FindLineIndex returns the FIRST occurrence (frontmatter),
        // causing user includes to be misplaced at the top of the file.

        // Stock template has no user includes
        var stockContent = string.Join('\n',
            "---",
            "type: guide",
            "---",
            "# Title",
            "Some content",
            "---",
            "## Section");

        // User added an include between the horizontal rule '---' and '## Section'
        var userContent = string.Join('\n',
            "---",
            "type: guide",
            "---",
            "# Title",
            "Some content",
            "---",
            "{{include:user-note}}",
            "## Section");

        var extracted = IncludeReanchor.ExtractUserIncludes(stockContent, userContent);

        Assert.Single(extracted);
        Assert.Equal("{{include:user-note}}", extracted[0].Tag);
        // The upper anchor should be '---' (the horizontal rule)
        Assert.Equal("---", extracted[0].UpperAnchor);
        Assert.Equal("## Section", extracted[0].LowerAnchor);

        // Now re-anchor against the same template content
        var result = IncludeReanchor.Reanchor(stockContent, extracted);

        Assert.Single(result.Placed);
        Assert.Empty(result.Unplaced);

        // The include SHOULD be placed near the horizontal rule (line index 5),
        // i.e. between '---' (hr) and '## Section'.
        // But FindLineIndex returns the FIRST '---' (line 0, frontmatter opener),
        // so the include lands at line 1 — inside the frontmatter block.
        var lines = result.Content.Split('\n');
        var includeIdx = Array.IndexOf(lines, "{{include:user-note}}");
        var sectionIdx = Array.FindIndex(lines, l => l.Trim() == "## Section");
        var frontmatterCloseIdx = Array.FindIndex(lines, 1, l => l.Trim() == "---");

        // If the bug exists: include lands at index 1 (after first '---'), inside frontmatter
        // If correct: include lands at index 6 (after the horizontal rule '---'), before '## Section'
        Assert.True(includeIdx > frontmatterCloseIdx,
            $"Include was placed at line {includeIdx}, which is inside or before the frontmatter " +
            $"(frontmatter closes at line {frontmatterCloseIdx}). " +
            "FindLineIndex matched the frontmatter '---' instead of the horizontal rule '---'.");
    }

    [Fact]
    public void Reanchor_UpperOnlyAnchorIsDashes_UsesLastOccurrence()
    {
        // When only an upper anchor exists and it's ambiguous (e.g. '---'),
        // the search should prefer the last occurrence (likely the HR rule)
        // rather than the first (frontmatter opener).
        var newContent = string.Join('\n',
            "---",
            "type: guide",
            "---",
            "# Title",
            "Some content",
            "---",
            "## Section");

        var includes = new List<IncludeReanchor.IncludeTag>
        {
            new("{{include:user-note}}", "---", null)
        };

        var result = IncludeReanchor.Reanchor(newContent, includes);

        Assert.Single(result.Placed);
        var lines = result.Content.Split('\n');
        var includeIdx = Array.IndexOf(lines, "{{include:user-note}}");
        var frontmatterCloseIdx = Array.FindIndex(lines, 1, l => l.Trim() == "---");

        // Include should be placed after the LAST '---' (the HR rule at index 5),
        // not after the first '---' (frontmatter opener at index 0).
        Assert.True(includeIdx > frontmatterCloseIdx,
            $"Include at line {includeIdx} should be after frontmatter close at line {frontmatterCloseIdx}");
    }

    #endregion

    #region Reanchor — tricky cases

    [Fact]
    public void Reanchor_BothAnchorsFound_PreservesBlankLineSeparation()
    {
        var newContent = "## Work\n\n1. Step one\n\n2. Step two\n";
        var includes = new List<IncludeReanchor.IncludeTag>
        {
            new("{{include:my-hook}}", "1. Step one", "2. Step two")
        };

        var result = IncludeReanchor.Reanchor(newContent, includes);

        Assert.Single(result.Placed);
        var lines = result.Content.Split('\n');
        var hookIdx = Array.IndexOf(lines, "{{include:my-hook}}");
        Assert.True(hookIdx > 0);
        // Verify the tag is between the two anchors
        var step1Idx = Array.FindIndex(lines, l => l.Trim() == "1. Step one");
        var step2Idx = Array.FindIndex(lines, l => l.Trim() == "2. Step two");
        Assert.True(hookIdx > step1Idx);
        Assert.True(hookIdx < step2Idx);
    }

    [Fact]
    public void Reanchor_UserTagAdjacentToShippedTag_BothSurvive()
    {
        var newContent = "## Work\n\n1. Step one\n{{include:extra-verify}}\n2. Step two\n";
        var includes = new List<IncludeReanchor.IncludeTag>
        {
            new("{{include:my-custom}}", "{{include:extra-verify}}", "2. Step two")
        };

        var result = IncludeReanchor.Reanchor(newContent, includes);

        Assert.Single(result.Placed);
        Assert.Contains("{{include:extra-verify}}", result.Content);
        Assert.Contains("{{include:my-custom}}", result.Content);
    }

    [Fact]
    public void Reanchor_NewTemplateHasNewSections_AnchorsStillMatch()
    {
        // New template has extra sections, but the anchor lines still exist
        var newContent = "## Work\n\n1. Step one\n2. Step two\n\n## New Section\n\nNew content here.\n";
        var includes = new List<IncludeReanchor.IncludeTag>
        {
            new("{{include:my-hook}}", "1. Step one", "2. Step two")
        };

        var result = IncludeReanchor.Reanchor(newContent, includes);

        Assert.Single(result.Placed);
        Assert.Empty(result.Unplaced);
        Assert.Contains("{{include:my-hook}}", result.Content);
    }

    [Fact]
    public void Reanchor_AnchorContentReworded_AnchorNotFound()
    {
        // Framework changed the anchor line text — tag can't be placed
        var newContent = "## Work\n\n1. Step alpha\n2. Step beta\n";
        var includes = new List<IncludeReanchor.IncludeTag>
        {
            new("{{include:my-hook}}", "1. Step one", "2. Step two")
        };

        var result = IncludeReanchor.Reanchor(newContent, includes);

        Assert.Empty(result.Placed);
        Assert.Single(result.Unplaced);
        Assert.Equal("{{include:my-hook}}", result.Unplaced[0]);
    }

    [Fact]
    public void Reanchor_MultipleTagsBetweenSameAnchors_AllInserted()
    {
        var newContent = "Line A\nLine B\n";
        var includes = new List<IncludeReanchor.IncludeTag>
        {
            new("{{include:hook-1}}", "Line A", "Line B"),
            new("{{include:hook-2}}", "Line A", "Line B")
        };

        var result = IncludeReanchor.Reanchor(newContent, includes);

        Assert.Equal(2, result.Placed.Count);
        Assert.Empty(result.Unplaced);
        Assert.Contains("{{include:hook-1}}", result.Content);
        Assert.Contains("{{include:hook-2}}", result.Content);
    }

    #endregion

    #region Hypothesis: hyp-2 — insertion order with shared upper anchor

    // Hypothesis: Reanchor reverses insertion order when multiple user includes
    // share the same upper anchor. This test uses the end-to-end pipeline
    // (ExtractUserIncludes → Reanchor) as specified in the hypothesis brief.
    [Fact]
    public void Reanchor_AdjacentUserIncludes_PreservesInsertionOrder()
    {
        var oldStock = "Line A\nLine B";
        var userContent = "Line A\n{{include:first}}\n{{include:second}}\nLine B";
        var newStock = "Line A\nLine B";

        var userIncludes = IncludeReanchor.ExtractUserIncludes(oldStock, userContent);
        var result = IncludeReanchor.Reanchor(newStock, userIncludes);

        Assert.Equal(2, result.Placed.Count);
        var lines = result.Content.Split('\n');
        var firstIdx = Array.IndexOf(lines, "{{include:first}}");
        var secondIdx = Array.IndexOf(lines, "{{include:second}}");
        Assert.True(firstIdx >= 0, "{{include:first}} should be placed");
        Assert.True(secondIdx >= 0, "{{include:second}} should be placed");
        // Hypothesis check: is {{include:first}} before {{include:second}}?
        Assert.True(firstIdx < secondIdx,
            $"Expected {{{{include:first}}}} (at {firstIdx}) before {{{{include:second}}}} (at {secondIdx}), but order was reversed");
    }

    // Direct test of Reanchor with shared upper anchor — insertion order must be preserved.
    [Fact]
    public void Reanchor_SharedUpperAnchor_PreservesInsertionOrder()
    {
        var newContent = "Line A\nLine B";
        var includes = new List<IncludeReanchor.IncludeTag>
        {
            new("{{include:first}}", "Line A", null),
            new("{{include:second}}", "Line A", null)
        };

        var result = IncludeReanchor.Reanchor(newContent, includes);

        Assert.Equal(2, result.Placed.Count);
        var lines = result.Content.Split('\n');
        var firstIdx = Array.IndexOf(lines, "{{include:first}}");
        var secondIdx = Array.IndexOf(lines, "{{include:second}}");

        Assert.True(firstIdx < secondIdx,
            $"Expected {{{{include:first}}}} (at {firstIdx}) before {{{{include:second}}}} (at {secondIdx})");
    }

    #endregion
}
