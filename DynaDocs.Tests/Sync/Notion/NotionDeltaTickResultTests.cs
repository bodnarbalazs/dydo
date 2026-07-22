namespace DynaDocs.Tests.Sync.Notion;

using DynaDocs.Sync.Notion;

public class NotionDeltaTickResultTests
{
    [Fact]
    public void Empty_IsQuiet_AndCarriesCensusFlag()
    {
        Assert.True(NotionDeltaTickResult.Empty(census: false).Quiet);
        Assert.True(NotionDeltaTickResult.Empty(census: true).Census);
        Assert.False(NotionDeltaTickResult.Empty(census: false).Census);
    }

    // Each counter alone makes a tick non-quiet — one case per term so every branch of the Quiet conjunction is
    // exercised both ways.
    [Theory]
    [InlineData(1, 0, 0, 0, 0, 0)]
    [InlineData(0, 1, 0, 0, 0, 0)]
    [InlineData(0, 0, 1, 0, 0, 0)]
    [InlineData(0, 0, 0, 1, 0, 0)]
    [InlineData(0, 0, 0, 0, 1, 0)]
    [InlineData(0, 0, 0, 0, 0, 1)]
    public void AnyNonZeroCounter_IsNotQuiet(int reconciled, int created, int updated, int archived, int conflicts, int fuse)
    {
        var result = new NotionDeltaTickResult(created, updated, archived, conflicts, fuse, reconciled, Census: false);
        Assert.False(result.Quiet);
    }

    [Fact]
    public void Add_SumsCounters_AndOrsCensus()
    {
        var a = new NotionDeltaTickResult(1, 2, 3, 4, 5, 6, Census: false);
        var b = new NotionDeltaTickResult(10, 20, 30, 40, 50, 60, Census: true);

        var sum = a.Add(b);

        Assert.Equal(11, sum.Created);
        Assert.Equal(22, sum.Updated);
        Assert.Equal(33, sum.Archived);
        Assert.Equal(44, sum.Conflicts);
        Assert.Equal(55, sum.FuseTrips);
        Assert.Equal(66, sum.Reconciled);
        Assert.True(sum.Census);
    }
}
