namespace DynaDocs.Tests.Statechart;

using DynaDocs.Services.Statechart;

public class StatechartAssetsTests
{
    [Fact]
    public void InstalledPayloadExtractsOfflineAndReusesExactBytes()
    {
        using var scope = new StatechartTestDirectory();
        var assets = new StatechartAssets();
        var first = assets.Extract(scope.Path);
        Assert.Equal(first, assets.Extract(scope.Path));
        Assert.True(File.Exists(Path.Combine(first, "skills/engineering/statechart/scripts/worker.ts")));
        Assert.True(File.Exists(Path.Combine(first, "setup-skills.mjs")));
        Assert.Equal("7bdbd8727178911b1b9501285d21c16edbc86ef87b7884433088fb4509c280ad", assets.LockDigest);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ForeignOrChangedAssetsAreRetainedAndRejected(bool change)
    {
        using var scope = new StatechartTestDirectory();
        var assets = new StatechartAssets();
        var folder = assets.Extract(scope.Path);
        var target = Path.Combine(folder, change ? "setup-skills.mjs" : "foreign.txt");
        File.WriteAllText(target, "foreign sentinel");
        Assert.Throws<IOException>(() => assets.Extract(scope.Path));
        Assert.Equal("foreign sentinel", File.ReadAllText(target));
    }
}
