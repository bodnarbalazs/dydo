namespace DynaDocs.Tests.Statechart;

using DynaDocs.Services.Statechart;

public class StatechartPathsTests
{
    [Fact]
    public void RelativeOrMissingRootsAreRejectedBeforeWriting()
    {
        Assert.Throws<IOException>(() => StatechartPaths.RequirePlainDirectory("relative"));
        using var scope = new StatechartTestDirectory();
        var missing = Path.Combine(scope.Path, "missing");
        Assert.Throws<IOException>(() => StatechartPaths.RequirePlainDirectory(missing));
        Assert.False(Directory.Exists(missing));
        StatechartPaths.EnsureDirectory(missing);
        StatechartPaths.RequirePlainDirectory(missing);
    }
}
