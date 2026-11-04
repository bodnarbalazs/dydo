namespace DynaDocs.Tests.Statechart;

using DynaDocs.Services.Statechart;

public class StatechartHomeTests
{
    [Fact]
    public void ProductRootsAreAbsoluteAndSeparateWithoutCreatingOrInstallingAnything()
    {
        var home = StatechartHome.Current();
        var roots = new[] { home.Assets, home.Dependencies, home.State };
        Assert.All(roots, root => Assert.True(Path.IsPathFullyQualified(root)));
        Assert.Equal(3, roots.Distinct(StringComparer.Ordinal).Count());
    }
}
