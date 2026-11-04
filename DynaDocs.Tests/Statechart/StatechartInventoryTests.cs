namespace DynaDocs.Tests.Statechart;

using DynaDocs.Services.Statechart;

public class StatechartInventoryTests
{
    // The filesystem follows a link before it applies `..`; each lexical target names an existing decoy entry.
    [SkippableTheory]
    [InlineData("a/b/l", "../../d", "escape", "a/b/l/../../outside")]
    [InlineData("p/q/x", "../../r", "p/q/y", "x/../../../z")]
    public void AnInstalledLinkWhoseFollowedPathLeavesTheRootIsRejected(string first, string firstTarget,
        string second, string secondTarget)
    {
        Skip.If(OperatingSystem.IsWindows(), "Creating symbolic links needs Windows developer mode.");
        using var scope = new StatechartTestDirectory();
        File.WriteAllText(Path.Combine(scope.Path, "outside"), "sentinel");
        var root = Path.Combine(scope.Path, "component");
        foreach (var directory in new[] { "a/b", "d", "p/q", "r" }) Directory.CreateDirectory(Path.Combine(root, directory));
        File.WriteAllText(Path.Combine(root, "a", "outside"), "decoy");
        File.WriteAllText(Path.Combine(root, "z"), "decoy");
        Directory.CreateSymbolicLink(Path.Combine(root, first), firstTarget);
        File.CreateSymbolicLink(Path.Combine(root, second), secondTarget);
        Assert.Throws<IOException>(() => StatechartInventory.Capture(root));
    }

    [SkippableFact]
    public void InstalledLinksThatLoopOrStayInsideAreJudgedByTheFollowedPath()
    {
        Skip.If(OperatingSystem.IsWindows(), "Creating symbolic links needs Windows developer mode.");
        using var scope = new StatechartTestDirectory();
        var root = Path.Combine(scope.Path, "component");
        Directory.CreateDirectory(Path.Combine(root, "a", "b"));
        Directory.CreateDirectory(Path.Combine(root, "d"));
        File.WriteAllText(Path.Combine(root, "d", "x"), "inside");
        Directory.CreateSymbolicLink(Path.Combine(root, "a", "b", "l"), "../../d");
        File.CreateSymbolicLink(Path.Combine(root, "through"), "a/b/l/x");
        var inventory = StatechartInventory.Capture(root);
        Assert.Contains(inventory, row => row!["path"]!.GetValue<string>() == "through" && row["link"]!.GetValue<string>() == "a/b/l/x");
        Directory.CreateDirectory(Path.Combine(root, "c"));
        Directory.CreateSymbolicLink(Path.Combine(root, "l"), "m/../c");
        Directory.CreateSymbolicLink(Path.Combine(root, "m"), "l/../c");
        Assert.Throws<IOException>(() => StatechartInventory.Capture(root));
    }
}
