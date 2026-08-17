namespace DynaDocs.Tests.Sync.Projection;

using DynaDocs.Sync.Projection;

public sealed class ProjectedMarkdownAlignmentTests
{
    [Fact]
    public void EquivalentListMarkerAndEscape_AlignWithoutChangingLocalBytes()
    {
        var result = ProjectedMarkdownMerge.Merge("- a\\*b\n  - nested", "* a*b\n    * nested", "- a\\*b\n  - nested", "* a*b\n    * nested");

        Assert.True(result.IsSuccess);
        Assert.Equal("- a\\*b\n  - nested", result.Body);
    }

    [Fact]
    public void MissingExternalH1_IsIgnoredOnlyWhenItMatchesThePageTitle()
    {
        var result = ProjectedMarkdownMerge.Merge("# Title\n\nbody", "body", "# Title\n\nbody", "changed", "Title");

        Assert.True(result.IsSuccess);
        Assert.Equal("# Title\n\nchanged", result.Body);
    }

    [Fact]
    public void MatchingLeadingH1_IsOmittedByTheParsedIdentity()
    {
        Assert.True(MarkdownSyntaxNode.TryParse("# Title", "Title", out var nodes, out var reason), reason);
        Assert.Empty(nodes);
    }

    [Fact]
    public void EmptyBaseInsertion_IsUnambiguous()
    {
        var result = ProjectedMarkdownMerge.Merge("", "", "", "body");

        Assert.True(result.IsSuccess, result.Conflict?.Reason);
        Assert.Equal("body", result.Body);
    }

    [Fact]
    public void MatchingH1OnlyLocalBody_AllowsExternalInsertion()
    {
        var result = ProjectedMarkdownMerge.Merge("# Title", "", "# Title", "body", "Title");

        Assert.True(result.IsSuccess, result.Conflict?.Reason);
        Assert.Equal("# Title\n\nbody", result.Body);
    }

    [Fact]
    public void DifferentLeadingHeading_IsNotDiscardedAsPageTitle()
    {
        var result = ProjectedMarkdownMerge.Merge("# Other\n\nbody", "body", "# Other\n\nbody", "changed", "Title");

        Assert.True(result.IsSuccess, result.Conflict?.Reason);
        Assert.Equal("# Other\n\nchanged", result.Body);
    }

    [Fact]
    public void RepeatedBaseRegions_AreMarkedAmbiguousWhenEdited()
    {
        var result = ProjectedMarkdownMerge.Merge("same\n\nsame", "same\n\nsame", "same\n\nsame", "changed\n\nsame");

        Assert.False(result.IsSuccess);
        Assert.Contains("ambiguous", result.Conflict!.Reason);
    }

    [Fact]
    public void RepeatedHeadings_AreMarkedAmbiguousWhenEdited()
    {
        var result = ProjectedMarkdownMerge.Merge("## same\n\nbody\n\n## same\n\nbody", "## same\n\nbody\n\n## same\n\nbody",
            "## same\n\nbody\n\n## same\n\nbody", "## changed\n\nbody\n\n## same\n\nbody");

        Assert.False(result.IsSuccess);
        Assert.Contains("ambiguous", result.Conflict!.Reason);
    }

    [Fact]
    public void LinkTargetChange_RemainsSemanticallyVisible()
    {
        var result = ProjectedMarkdownMerge.Merge("[site](https://a)", "[site](https://a)", "[site](https://a)", "[site](https://b)");

        Assert.True(result.IsSuccess);
        Assert.Equal("[site](https://b)", result.Body);
    }

    [Fact]
    public void NestedListChildren_AlignAcrossEquivalentMarkerSpellings()
    {
        Assert.True(MarkdownSyntaxNode.TryParse("- parent\n  - child", null, out var local, out var localReason), localReason);
        Assert.True(MarkdownSyntaxNode.TryParse("* parent\n    * child", null, out var external, out var externalReason), externalReason);

        Assert.Equal(local[0].Identity, external[0].Identity);
    }

    [Fact]
    public void NestedRepeatedItems_ConflictRatherThanChoosingAnArbitraryChild()
    {
        var result = ProjectedMarkdownMerge.Merge("- same\n- same", "- same\n- same", "- same\n- same", "- changed\n- same");

        Assert.False(result.IsSuccess);
        Assert.Contains("ambiguous", result.Conflict!.Reason);
    }

    [Fact]
    public void NestedStructuralInsertion_Succeeds()
    {
        var result = ProjectedMarkdownMerge.Merge("- parent\n  - child", "- parent\n  - child",
            "- parent\n  - child", "- parent\n  - child\n  - added");

        Assert.True(result.IsSuccess, result.Conflict?.Reason);
        Assert.Equal("- parent\n  - child\n  - added", result.Body);
    }

    [Fact]
    public void SingleCharacterEmphasisDelimiters_AlignAndPreserveTheLocalMarker()
    {
        Assert.True(MarkdownSyntaxNode.TryParse("_hello_", null, out var underscore, out var underscoreReason), underscoreReason);
        Assert.True(MarkdownSyntaxNode.TryParse("*hello*", null, out var asterisk, out var asteriskReason), asteriskReason);

        Assert.Equal(underscore[0].Identity, asterisk[0].Identity);
        var result = ProjectedMarkdownMerge.Merge("_hello_", "*hello*", "_hello_", "*world*");
        Assert.True(result.IsSuccess, result.Conflict?.Reason);
        Assert.Equal("_world_", result.Body);
    }

    [Fact]
    public void EmphasisStrength_RemainsPartOfTheIdentity()
    {
        Assert.True(MarkdownSyntaxNode.TryParse("_hello_", null, out var single, out var singleReason), singleReason);
        Assert.True(MarkdownSyntaxNode.TryParse("**hello**", null, out var strong, out var strongReason), strongReason);

        Assert.NotEqual(single[0].Identity, strong[0].Identity);
    }

    [Fact]
    public void LargeUniqueAnchorSequence_UsesBoundedPartitions()
    {
        var body = string.Join("\n\n", Enumerable.Range(0, 1_000).Select(index => "item " + index));
        var external = body.Replace("item 500", "changed 500", StringComparison.Ordinal);

        var result = ProjectedMarkdownMerge.Merge(body, body, body, external);

        Assert.True(result.IsSuccess, result.Conflict?.Reason);
        Assert.Equal(external, result.Body);
    }
}
