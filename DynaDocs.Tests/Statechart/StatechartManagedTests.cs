namespace DynaDocs.Tests.Statechart;

using DynaDocs.Services.Statechart;
using System.Text.Json.Nodes;

public class StatechartManagedTests
{
    [Fact]
    public void MissingManagedRootReportsMissingWithoutCreatingAnInstallation()
    {
        using var scope = new StatechartTestDirectory();
        var managed = Create(scope.Path);
        Assert.Equal(["node", "java", "tlc", "pele"], StatechartManaged.Missing(managed.Read()));
        Assert.False(Directory.Exists(managed.Root));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ForeignMarkerOrUnexpectedChildIsRetainedAndRejected(bool validMarker)
    {
        using var scope = new StatechartTestDirectory();
        var assets = new StatechartAssets();
        var managed = Create(scope.Path);
        Directory.CreateDirectory(managed.Root);
        File.WriteAllText(Path.Combine(managed.Root, ".statechart-store"), validMarker ? assets.LockDigest : "foreign");
        var sentinel = Path.Combine(managed.Root, "foreign");
        File.WriteAllText(sentinel, "keep");
        Assert.Throws<IOException>(() => managed.Read());
        Assert.Equal("keep", File.ReadAllText(sentinel));
    }

    [Fact]
    public void ComponentWithoutAReadinessReceiptIsNotAdoptedOrRepairedImplicitly()
    {
        using var scope = new StatechartTestDirectory();
        var managed = Create(scope.Path);
        Directory.CreateDirectory(Path.Combine(managed.Root, "node"));
        File.WriteAllText(Path.Combine(managed.Root, ".statechart-store"), new StatechartAssets().LockDigest);
        var file = Path.Combine(managed.Root, "node", "foreign.exe");
        File.WriteAllText(file, "keep");
        Assert.ThrowsAny<IOException>(() => managed.Read());
        Assert.Equal("keep", File.ReadAllText(file));
    }

    [Fact]
    public void ExplicitSelectionWinsWhenManagedValuesFillMissingDependencies()
    {
        var explicitSelection = new StatechartDependencies("selected-node", "explicit-java", null, null);
        var managed = new StatechartDependencies("managed-node", "managed-java", "locked-tlc", "locked-pele");
        Assert.Equal(new StatechartDependencies("selected-node", "explicit-java", "locked-tlc", "locked-pele"),
            StatechartManaged.Combine(explicitSelection, managed));
        Assert.Empty(StatechartManaged.Missing(StatechartManaged.Combine(explicitSelection, managed)));
    }

    [Fact]
    public async Task ConcurrentInstallerLockRejectsBeforeCreatingStoreOrDownloading()
    {
        using var scope = new StatechartTestDirectory();
        var managed = Create(scope.Path);
        var parent = Path.GetDirectoryName(managed.Root)!;
        Directory.CreateDirectory(parent);
        using var held = new FileStream(Path.Combine(parent, "win-x64.install.lock"),
            FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        await Assert.ThrowsAsync<IOException>(() => managed.InstallAsync(new(null, null, null, null),
            Path.Combine(scope.Path, "unavailable-payload"), CancellationToken.None));
        Assert.False(Directory.Exists(managed.Root));
        Assert.Empty(Directory.GetDirectories(parent));
        Assert.Single(Directory.GetFiles(parent));
    }

    [Fact]
    public void ReceiptFromAnotherReleaseRejectsBeforeUsingComponentBytes()
    {
        using var scope = new StatechartTestDirectory();
        var assets = new StatechartAssets();
        var managed = Create(scope.Path);
        var component = Path.Combine(managed.Root, "tlc");
        Directory.CreateDirectory(component);
        File.WriteAllText(Path.Combine(managed.Root, ".statechart-store"), assets.LockDigest);
        File.WriteAllText(Path.Combine(component, "tla2tools.jar"), "not a provisioned fixture");
        var receipt = new JsonObject { ["schema"] = 1, ["protocol"] = 1, ["release"] = "old",
            ["rid"] = "win-x64", ["payload"] = assets.Digest, ["lock"] = assets.LockDigest,
            ["integrity"] = StatechartDownloads.Select(assets, "win-x64").Single(item => item.Name == "tlc").Integrity,
            ["files"] = StatechartInventory.Capture(component) };
        File.WriteAllText(Path.Combine(component, ".ready.json"), receipt.ToJsonString());
        var error = Assert.Throws<IOException>(() => managed.Read());
        Assert.Contains("content changed", error.Message);
    }

    private static StatechartManaged Create(string root) => new(new StatechartHome(
        Path.Combine(root, "assets"), Path.Combine(root, "dependencies"), Path.Combine(root, "state")),
        new StatechartAssets(), "win-x64");
}
