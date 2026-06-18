// @test-tier: 2
namespace DynaDocs.Tests.Sync;

using DynaDocs.Sync;

public class ThreeWayTextMergeTests
{
    [Fact]
    public void NoChanges_ReturnsBase()
    {
        var r = ThreeWayTextMerge.Merge("a\nb\nc", "a\nb\nc", "a\nb\nc");
        Assert.False(r.Conflicted);
        Assert.Equal("a\nb\nc", r.Text);
    }

    [Fact]
    public void OnlyOursChanged_TakesOurs()
    {
        var r = ThreeWayTextMerge.Merge("a\nb\nc", "a\nB\nc", "a\nb\nc");
        Assert.False(r.Conflicted);
        Assert.Equal("a\nB\nc", r.Text);
    }

    [Fact]
    public void OnlyTheirsChanged_TakesTheirs()
    {
        var r = ThreeWayTextMerge.Merge("a\nb\nc", "a\nb\nc", "a\nb\nC");
        Assert.False(r.Conflicted);
        Assert.Equal("a\nb\nC", r.Text);
    }

    [Fact]
    public void NonOverlappingChanges_MergeBoth()
    {
        var r = ThreeWayTextMerge.Merge("a\nb\nc\nd", "A\nb\nc\nd", "a\nb\nc\nD");
        Assert.False(r.Conflicted);
        Assert.Equal("A\nb\nc\nD", r.Text);
    }

    [Fact]
    public void SameLineBothChangedDifferently_Conflicts()
    {
        var r = ThreeWayTextMerge.Merge("a\nb\nc", "a\nX\nc", "a\nY\nc");
        Assert.True(r.Conflicted);
        Assert.Contains("<<<<<<< repo", r.Text);
        Assert.Contains("X", r.Text);
        Assert.Contains("Y", r.Text);
        Assert.Contains(">>>>>>> external", r.Text);
    }

    [Fact]
    public void SameLineBothChangedIdentically_NoConflict()
    {
        var r = ThreeWayTextMerge.Merge("a\nb\nc", "a\nZ\nc", "a\nZ\nc");
        Assert.False(r.Conflicted);
        Assert.Equal("a\nZ\nc", r.Text);
    }

    [Fact]
    public void BothAppendDifferentTail_Conflicts()
    {
        var r = ThreeWayTextMerge.Merge("a\nb", "a\nb\nours", "a\nb\ntheirs");
        Assert.True(r.Conflicted);
        Assert.Contains("ours", r.Text);
        Assert.Contains("theirs", r.Text);
    }

    [Fact]
    public void OneSideAppends_TakesAppend()
    {
        var r = ThreeWayTextMerge.Merge("a\nb", "a\nb\nextra", "a\nb");
        Assert.False(r.Conflicted);
        Assert.Equal("a\nb\nextra", r.Text);
    }

    [Fact]
    public void OnlyTheirsAppends_TakesAppend()
    {
        var r = ThreeWayTextMerge.Merge("a\nb", "a\nb", "a\nb\ntheirs");
        Assert.False(r.Conflicted);
        Assert.Equal("a\nb\ntheirs", r.Text);
    }

    [Fact]
    public void LineDeletion_OnOneSide_Applies()
    {
        var r = ThreeWayTextMerge.Merge("a\nb\nc", "a\nc", "a\nb\nc");
        Assert.False(r.Conflicted);
        Assert.Equal("a\nc", r.Text);
    }

    [Fact]
    public void BothAppendSameTail_NoConflict_AppendedOnce()
    {
        var r = ThreeWayTextMerge.Merge("a\nb", "a\nb\ntail", "a\nb\ntail");
        Assert.False(r.Conflicted);
        Assert.Equal("a\nb\ntail", r.Text);
    }

    [Fact]
    public void BothSidesInsertDifferentMiddleBlocks_NonAdjacent_Merge()
    {
        // Repo inserts after 'a', external inserts after 'c' — distinct base regions, clean merge.
        var r = ThreeWayTextMerge.Merge("a\nb\nc\nd", "a\nR\nb\nc\nd", "a\nb\nc\nE\nd");
        Assert.False(r.Conflicted);
        Assert.Equal("a\nR\nb\nc\nE\nd", r.Text);
    }

    [Fact]
    public void OneSideInsertsBeforeFirstLine_TakesInsert()
    {
        var r = ThreeWayTextMerge.Merge("a\nb", "x\na\nb", "a\nb");
        Assert.False(r.Conflicted);
        Assert.Equal("x\na\nb", r.Text);
    }

    [Fact]
    public void BothInsertBeforeSameAnchorDifferently_Conflicts_AnchorPreserved()
    {
        var r = ThreeWayTextMerge.Merge("a\nb", "a\nR\nb", "a\nE\nb");
        Assert.True(r.Conflicted);
        Assert.Contains("R", r.Text);
        Assert.Contains("E", r.Text);
        Assert.EndsWith("b", r.Text);
    }

    [Fact]
    public void RepoBlockRewrite_ExternalEditsLineInsideIt_Conflicts_NoSilentLoss()
    {
        // Repo collapses the two-line L1/L2 block into a single line R (a multi-line region keyed
        // at base index 1, consuming 2 lines). External independently edits L2 -> E, keyed at base
        // index 2 — *inside* repo's region. The pre-fix loop advanced past index 2 and silently
        // dropped E with no conflict. Required: deterministic winner PLUS visible markers, never
        // silent loss.
        var r = ThreeWayTextMerge.Merge("L0\nL1\nL2\nL3", "L0\nR\nL3", "L0\nL1\nE\nL3");

        Assert.True(r.Conflicted);
        Assert.Contains("<<<<<<< repo", r.Text);
        Assert.Contains("R", r.Text);
        Assert.Contains("E", r.Text);          // external's edit must survive under the marker
        Assert.Contains(">>>>>>> external", r.Text);
        Assert.StartsWith("L0", r.Text);
        Assert.EndsWith("L3", r.Text);
    }

    [Fact]
    public void RepoBlockDelete_ExternalEditsLineInsideDeletedBlock_Conflicts_NoSilentLoss()
    {
        // Repo deletes the whole L1/L2 block (region at index 1, consumes 2, empty replacement).
        // External edits L2 -> E inside that deleted block. The delete must not silently win:
        // external's surviving content has to surface under a conflict marker.
        var r = ThreeWayTextMerge.Merge("L0\nL1\nL2\nL3", "L0\nL3", "L0\nL1\nE\nL3");

        Assert.True(r.Conflicted);
        Assert.Contains("<<<<<<< repo", r.Text);
        Assert.Contains("E", r.Text);          // external's edit must survive under the marker
        Assert.Contains(">>>>>>> external", r.Text);
        Assert.StartsWith("L0", r.Text);
        Assert.EndsWith("L3", r.Text);
    }

    [Fact]
    public void ExternalBlockRewrite_RepoEditsLineInsideIt_Conflicts_NoSilentLoss()
    {
        // Symmetric to the repo-block-rewrite case: external owns the spanning region, repo edits
        // a single line inside it. Repo's content must not be dropped.
        var r = ThreeWayTextMerge.Merge("L0\nL1\nL2\nL3", "L0\nR\nL2\nL3", "L0\nE\nL3");

        Assert.True(r.Conflicted);
        Assert.Contains("R", r.Text);          // repo's edit must survive
        Assert.Contains("E", r.Text);
        Assert.Contains("<<<<<<< repo", r.Text);
        Assert.Contains(">>>>>>> external", r.Text);
    }

    [Fact]
    public void OverlappingBlockRewrites_ExternalSpanExtendsRepoSpan_Conflicts_NoSilentLoss()
    {
        // Repo rewrites the L1/L2 block (region keyed at base index 1, consumes 2). External
        // rewrites the L2/L3 block (keyed at index 2, consumes 2) — its span reaches *past* repo's
        // region end, so the region must grow to swallow it. Both sides touched the combined span,
        // so neither side's content may be silently dropped.
        var r = ThreeWayTextMerge.Merge("L0\nL1\nL2\nL3\nL4", "L0\nR\nL3\nL4", "L0\nL1\nE\nL4");

        Assert.True(r.Conflicted);
        Assert.Contains("<<<<<<< repo", r.Text);
        Assert.Contains("R", r.Text);
        Assert.Contains("E", r.Text);
        Assert.Contains(">>>>>>> external", r.Text);
        Assert.StartsWith("L0", r.Text);
        Assert.EndsWith("L4", r.Text);
    }

    [Fact]
    public void BothBlockRewriteSameSpanIdentically_NoConflict()
    {
        // Both sides rewrite the L1/L2 block to the exact same single line — agreement, no marker.
        var r = ThreeWayTextMerge.Merge("L0\nL1\nL2\nL3", "L0\nM\nL3", "L0\nM\nL3");
        Assert.False(r.Conflicted);
        Assert.Equal("L0\nM\nL3", r.Text);
    }
}
