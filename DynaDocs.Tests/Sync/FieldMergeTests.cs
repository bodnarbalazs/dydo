// @test-tier: 2
namespace DynaDocs.Tests.Sync;

using DynaDocs.Models;
using DynaDocs.Sync;

public class FieldMergeTests
{
    private static List<SyncField> F(params (string Key, string Value)[] pairs) =>
        pairs.Select(p => new SyncField { Key = p.Key, Value = p.Value }).ToList();

    [Fact]
    public void NoChanges_ReturnsBaseValues_InRepoOrder()
    {
        var r = FieldMerge.Merge(
            F(("a", "1"), ("b", "2")),
            F(("a", "1"), ("b", "2")),
            F(("a", "1"), ("b", "2")));

        Assert.False(r.Conflicted);
        Assert.Equal(["a", "b"], r.Fields.Select(f => f.Key));
    }

    [Fact]
    public void RepoChangedKey_TakesRepo()
    {
        var r = FieldMerge.Merge(F(("status", "open")), F(("status", "done")), F(("status", "open")));
        Assert.False(r.Conflicted);
        Assert.Equal("done", r.Fields[0].Value);
    }

    [Fact]
    public void ExternalChangedKey_TakesExternal()
    {
        var r = FieldMerge.Merge(F(("status", "open")), F(("status", "open")), F(("status", "done")));
        Assert.False(r.Conflicted);
        Assert.Equal("done", r.Fields[0].Value);
    }

    [Fact]
    public void BothChangedSameKeyDifferently_Conflicts_RepoWins()
    {
        var r = FieldMerge.Merge(F(("status", "open")), F(("status", "a")), F(("status", "b")));
        Assert.True(r.Conflicted);
        Assert.Equal("a", r.Fields[0].Value);
    }

    [Fact]
    public void NonOverlappingFieldEdits_MergeBoth()
    {
        var r = FieldMerge.Merge(
            F(("status", "open"), ("priority", "low")),
            F(("status", "done"), ("priority", "low")),
            F(("status", "open"), ("priority", "high")));

        Assert.False(r.Conflicted);
        Assert.Equal("done", r.Fields.First(f => f.Key == "status").Value);
        Assert.Equal("high", r.Fields.First(f => f.Key == "priority").Value);
    }

    [Fact]
    public void ExternalOnlyNewKey_AppendedAtEnd()
    {
        var r = FieldMerge.Merge(
            F(("a", "1")),
            F(("a", "1")),
            F(("a", "1"), ("notion-extra", "x")));

        Assert.Equal(["a", "notion-extra"], r.Fields.Select(f => f.Key));
        Assert.Equal("x", r.Fields.Last().Value);
    }

    [Fact]
    public void RepoOnlyKey_ExternalLacksIt_TakesRepoValue()
    {
        // External never had this key (hasExt == false): the short-circuit must keep the repo value.
        var r = FieldMerge.Merge(F(("a", "1")), F(("a", "1"), ("repo-only", "x")), F(("a", "1")));
        Assert.False(r.Conflicted);
        Assert.Equal(["a", "repo-only"], r.Fields.Select(f => f.Key));
        Assert.Equal("x", r.Fields.First(f => f.Key == "repo-only").Value);
    }

    [Fact]
    public void BothSetSameKeySameValue_NoConflict()
    {
        var r = FieldMerge.Merge(F(("s", "open")), F(("s", "done")), F(("s", "done")));
        Assert.False(r.Conflicted);
        Assert.Equal("done", r.Fields[0].Value);
    }
}
