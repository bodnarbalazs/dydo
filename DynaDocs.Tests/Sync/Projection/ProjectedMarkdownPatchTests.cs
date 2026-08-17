namespace DynaDocs.Tests.Sync.Projection;

using DynaDocs.Sync.Projection;

public sealed class ProjectedMarkdownPatchTests
{
    [Fact]
    public void DualBodyBase_UsesTheSameProjectionPipeline()
    {
        var result = ProjectedMarkdownMerge.Merge(new DualBodyBase("one", "one"), "one", "two");

        Assert.True(result.IsSuccess, result.Conflict?.Reason);
        Assert.Equal("two", result.Body);
    }

    [Fact]
    public void ExternalLeafEdit_PreservesLocalEmphasisMarkers()
    {
        var result = ProjectedMarkdownMerge.Merge("**hello**", "**hello**", "**hello**", "**world**");

        Assert.True(result.IsSuccess, result.Conflict?.Reason);
        Assert.Equal("**world**", result.Body);
    }

    [Fact]
    public void ExternalLeafEdit_PreservesLocalLinkSpelling()
    {
        var result = ProjectedMarkdownMerge.Merge("[hello](https://a)", "[hello](https://a)", "[hello](https://a)", "[world](https://a)");

        Assert.True(result.IsSuccess);
        Assert.Equal("[world](https://a)", result.Body);
    }

    [Fact]
    public void ExternalLeafInsertion_StaysInsideLocalInlineMarkers()
    {
        var result = ProjectedMarkdownMerge.Merge("**hello**", "**hello**", "**hello**", "**hello world**");

        Assert.True(result.IsSuccess, result.Conflict?.Reason);
        Assert.Equal("**hello world**", result.Body);
    }

    [Fact]
    public void SameParagraphDisjointLeafEdits_Compose()
    {
        const string body = "**one** middle *three*";
        var result = ProjectedMarkdownMerge.Merge(body, body, "**local** middle *three*", "**one** middle *remote*");

        Assert.True(result.IsSuccess, result.Conflict?.Reason);
        Assert.Equal("**local** middle *remote*", result.Body);
    }

    [Fact]
    public void MultipleDisjointSemanticHunks_Compose()
    {
        var result = ProjectedMarkdownMerge.Merge("one two three", "one two three", "ONE two THREE", "one TWO three");

        Assert.True(result.IsSuccess, result.Conflict?.Reason);
        Assert.Equal("ONE TWO THREE", result.Body);
    }

    [Fact]
    public void EquivalentEmphasisDelimiters_PreserveTheLocalMarkers()
    {
        Assert.True(MarkdownSyntaxNode.TryParse("__hello__", null, out var local, out var localReason), localReason);
        Assert.True(MarkdownSyntaxNode.TryParse("**hello**", null, out var external, out var externalReason), externalReason);
        Assert.Equal(local[0].Identity, external[0].Identity);

        var result = ProjectedMarkdownMerge.Merge("__hello__", "**hello**", "__hello__", "**world**");

        Assert.True(result.IsSuccess, result.Conflict?.Reason);
        Assert.Equal("__world__", result.Body);
    }

    [Fact]
    public void StructuralInsertion_PreservesCrLfGapAndNormalizesInsertedSpanToLf()
    {
        var result = ProjectedMarkdownMerge.Merge("one\r\n\r\ntwo", "one\r\n\r\ntwo", "one\r\n\r\ntwo", "one\r\n\r\ninserted\r\n\r\ntwo");

        Assert.True(result.IsSuccess, result.Conflict?.Reason);
        Assert.Equal("one\r\n\r\ninserted\n\ntwo", result.Body);
    }

    [Fact]
    public void NestedDisjointEdits_PreserveEachRepresentationMarkerSpelling()
    {
        var result = ProjectedMarkdownMerge.Merge("- parent\n  - child", "* parent\n    * child",
            "- local\n  - child", "* parent\n    * remote");

        Assert.True(result.IsSuccess, result.Conflict?.Reason);
        Assert.Equal("- local\n  - remote", result.Body);
    }

    [Fact]
    public void DisjointEdits_ComposeAndKeepBlankGapByteExact()
    {
        const string body = "local\n\n\nkeep\n\nlast";
        var result = ProjectedMarkdownMerge.Merge("first\n\n\nkeep\n\nlast", "first\n\n\nkeep\n\nlast", body, "first\n\n\nkeep\n\nremote");

        Assert.True(result.IsSuccess, result.Conflict?.Reason);
        Assert.Equal("local\n\n\nkeep\n\nremote", result.Body);
    }

    [Fact]
    public void SameNodeEdits_Conflict()
    {
        var result = ProjectedMarkdownMerge.Merge("one", "one", "local", "external");

        Assert.False(result.IsSuccess);
        Assert.Contains("overlap", result.Conflict!.Reason);
    }

    [Fact]
    public void ExternalStructuralInsertion_UsesLfAndOneBoundaryBlankLine()
    {
        var result = ProjectedMarkdownMerge.Merge("one\n\ntwo", "one\n\ntwo", "one\n\ntwo", "one\n\ninserted\n\ntwo");

        Assert.True(result.IsSuccess);
        Assert.Equal("one\n\ninserted\n\ntwo", result.Body);
    }

    [Fact]
    public void MiddleNestedInsertion_PreservesTheUntouchedLocalTailMarkerAndIndent()
    {
        const string local = "- parent\n  - one\n  - tail";
        const string external = "* parent\n    * one\n    * tail";
        var result = ProjectedMarkdownMerge.Merge(local, external, local, "* parent\n    * one\n    * middle\n    * tail");

        Assert.True(result.IsSuccess, result.Conflict?.Reason);
        Assert.Equal("- parent\n  - one\n    * middle\n  - tail", result.Body);
    }

    [Fact]
    public void DisjointChangesInsideOneWord_ComposeAtCharacterPrecision()
    {
        var result = ProjectedMarkdownMerge.Merge("abcde", "abcde", "Abcde", "abcdE");

        Assert.True(result.IsSuccess, result.Conflict?.Reason);
        Assert.Equal("AbcdE", result.Body);
    }

    [Fact]
    public void TableCellEdit_PreservesUntouchedLocalTableSpacing()
    {
        const string local = "| a | b |\n| --- | --- |\n| 1 |  2 |";
        const string external = "| a | b |\n| --- | --- |\n| 1 | 2 |";
        var result = ProjectedMarkdownMerge.Merge(local, external, local, "| a | b |\n| --- | --- |\n| 1 | 3 |");

        Assert.True(result.IsSuccess, result.Conflict?.Reason);
        Assert.Equal("| a | b |\n| --- | --- |\n| 1 |  3 |", result.Body);
    }

    [Fact]
    public void InsertionBeforeTheFirstUniqueSibling_UsesItsRightAnchor()
    {
        var result = ProjectedMarkdownMerge.Merge("one\n\ntwo", "one\n\ntwo", "one\n\ntwo", "inserted\n\none\n\ntwo");

        Assert.True(result.IsSuccess, result.Conflict?.Reason);
        Assert.Equal("inserted\n\none\n\ntwo", result.Body);
    }

    [Fact]
    public void ConcurrentLinkTargetChanges_ConflictAsSyntaxOverlap()
    {
        var result = ProjectedMarkdownMerge.Merge("[site](https://a)", "[site](https://a)", "[site](https://local)", "[site](https://external)");

        Assert.False(result.IsSuccess);
        Assert.Contains("syntax", result.Conflict!.Reason);
    }

    [Fact]
    public void ParagraphEdit_PreservesTheUntouchedCrLfSpan()
    {
        var result = ProjectedMarkdownMerge.Merge("one\r\ntwo", "one\r\ntwo", "one\r\ntwo", "ONE\r\ntwo");

        Assert.True(result.IsSuccess, result.Conflict?.Reason);
        Assert.Equal("ONE\r\ntwo", result.Body);
    }

    [Fact]
    public void StructuralDeletion_RemovesOnlyTheMappedSiblingSpan()
    {
        const string body = "one\n\ntwo\n\nthree";
        var result = ProjectedMarkdownMerge.Merge(body, body, body, "one\n\nthree");

        Assert.True(result.IsSuccess, result.Conflict?.Reason);
        Assert.Equal("one\n\n\n\nthree", result.Body);
    }
}
