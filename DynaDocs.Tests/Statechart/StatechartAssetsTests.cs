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
        Assert.True(File.Exists(Path.Combine(first, "skills/engineering/statechart/scripts/java/statechart-tlc-bootstrap.jar")),
            "The sealed Java11 helper must be in the verified payload; no raw-TLC fallback.");
        Assert.Equal("3ad231072bddc04588d0f2cf1b61a3e62fc38b4ff400ccea5c3b5d41409575ab", assets.LockDigest);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrChangedBootstrapCannotReuseInstalledPayload(bool changed)
    {
        using var scope = new StatechartTestDirectory();
        var assets = new StatechartAssets();
        var folder = assets.Extract(scope.Path);
        var helper = Path.Combine(folder, "skills/engineering/statechart/scripts/java/statechart-tlc-bootstrap.jar");
        Assert.True(File.Exists(helper), "A sealed helper is required; an unbuilt Stage A draft is not executable proof.");
        if (changed) File.AppendAllText(helper, "changed");
        else File.Delete(helper);
        Assert.Throws<IOException>(() => assets.Extract(scope.Path));
        if (changed) Assert.EndsWith("changed", File.ReadAllText(helper));
        else Assert.False(File.Exists(helper));
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
