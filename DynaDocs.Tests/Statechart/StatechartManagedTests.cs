namespace DynaDocs.Tests.Statechart;

using DynaDocs.Services.Statechart;
using System.Security.Cryptography;
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
        Assert.Null(managed.Read().Node);
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

    // Another process holds the lock while it installs or re-stamps; a waiter creates and downloads nothing.
    [Fact]
    public async Task AnInstallWaitsForAnotherProcessesLockThenRescans()
    {
        using var scope = new StatechartTestDirectory();
        var managed = new StatechartManaged(new StatechartHome(Path.Combine(scope.Path, "assets"),
            Path.Combine(scope.Path, "dependencies"), Path.Combine(scope.Path, "state")), new StatechartAssets(), "win-x64", Unreachable());
        var parent = Path.GetDirectoryName(managed.Root)!;
        Directory.CreateDirectory(parent);
        var held = new FileStream(Path.Combine(parent, "win-x64.install.lock"),
            FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        var selected = new StatechartDependencies("node", "java", "tlc", "pele");
        var install = managed.InstallAsync(selected, Path.Combine(scope.Path, "unavailable-payload"), CancellationToken.None);
        Assert.False(install.IsCompleted);
        held.Dispose();
        Assert.Equal(selected, await install);
        Assert.False(Directory.Exists(managed.Root));
        Assert.Empty(Directory.GetDirectories(parent));
    }

    // The components are fakes, so a verification after the wait would fail the run.
    [Fact]
    public async Task ARestampThatWaitedRescansAndFindsTheHoldersRestamp()
    {
        using var scope = new StatechartTestDirectory();
        var assets = new StatechartAssets();
        var (managed, jar) = Installed(scope.Path, release: "0.0.1", payload: "earlier-payload");
        var held = new FileStream(Path.Combine(Path.GetDirectoryName(managed.Root)!, "win-x64.install.lock"),
            FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        var restamp = managed.RestampAsync(new(null, null, jar, null), Path.Combine(scope.Path, "unavailable-payload"), CancellationToken.None);
        Assert.False(restamp.IsCompleted);
        var receipt = Path.Combine(Path.GetDirectoryName(jar)!, ".ready.json");
        var identity = JsonNode.Parse(File.ReadAllText(receipt))!.AsObject();
        identity["release"] = assets.Release;
        identity["payload"] = assets.Digest;
        File.WriteAllText(receipt, identity.ToJsonString());
        held.Dispose();
        Assert.False(await restamp);
        Assert.Empty(managed.Inspect().Stale);
    }

    [Fact]
    public async Task CancellingAWaitingInstallLeavesNothingBehind()
    {
        using var scope = new StatechartTestDirectory();
        var managed = new StatechartManaged(new StatechartHome(Path.Combine(scope.Path, "assets"),
            Path.Combine(scope.Path, "dependencies"), Path.Combine(scope.Path, "state")), new StatechartAssets(), "win-x64", Unreachable());
        var parent = Path.GetDirectoryName(managed.Root)!;
        Directory.CreateDirectory(parent);
        using var held = new FileStream(Path.Combine(parent, "win-x64.install.lock"),
            FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        using var cancel = new CancellationTokenSource();
        var install = managed.InstallAsync(new(null, null, null, null), Path.Combine(scope.Path, "unavailable-payload"), cancel.Token);
        await cancel.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => install);
        Assert.False(Directory.Exists(managed.Root));
        Assert.Empty(Directory.GetDirectories(parent));
        Assert.Single(Directory.GetFiles(parent));
    }

    [Fact]
    public void ReceiptFromAnotherReleaseNeverAdoptsUnverifiedBytes()
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
        Assert.Null(managed.Read().Tlc);
        Assert.Equal("not a provisioned fixture", File.ReadAllText(Path.Combine(component, "tla2tools.jar")));
    }

    [Fact]
    public void ReceiptFromAnotherReleaseOrPayloadIsReadyWhenItsContentVerifies()
    {
        using var scope = new StatechartTestDirectory();
        var (managed, jar) = Installed(scope.Path, release: "0.0.1", payload: "earlier-payload");
        Assert.Equal(jar, managed.Read().Tlc);
    }

    [Theory]
    [InlineData("tampered")]
    [InlineData("receipt-missing")]
    [InlineData("receipt-unreadable")]
    public void ContentThatFailsVerificationIsReportedMissingAndLeftInPlace(string damage)
    {
        using var scope = new StatechartTestDirectory();
        var (managed, jar) = Installed(scope.Path, new StatechartAssets().Release, new StatechartAssets().Digest);
        var receipt = Path.Combine(Path.GetDirectoryName(jar)!, ".ready.json");
        if (damage == "tampered") File.AppendAllText(jar, "tampered");
        else if (damage == "receipt-missing") File.Delete(receipt);
        else File.WriteAllText(receipt, "{not json");
        var bytes = File.ReadAllBytes(jar);
        Assert.Null(managed.Read().Tlc);
        Assert.Equal(bytes, File.ReadAllBytes(jar));
    }

    [Fact]
    public void ADifferentLockIsAnotherStoreSoItsContentIsNeverConsulted()
    {
        using var scope = new StatechartTestDirectory();
        var (managed, _) = Installed(scope.Path, new StatechartAssets().Release, new StatechartAssets().Digest);
        var other = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(managed.Root)!)!, new string('0', 64), "win-x64");
        Directory.CreateDirectory(Path.GetDirectoryName(other)!);
        Directory.Move(managed.Root, other);
        Assert.Equal(["node", "java", "tlc", "pele"], StatechartManaged.Missing(managed.Read()));
    }

    [Fact]
    public async Task FailedInstallLeavesNoStagingAndSweepsStaleStagesItOwns()
    {
        using var scope = new StatechartTestDirectory();
        var assets = new StatechartAssets();
        var home = new StatechartHome(Path.Combine(scope.Path, "assets"), Path.Combine(scope.Path, "dependencies"), Path.Combine(scope.Path, "state"));
        var managed = new StatechartManaged(home, assets, "win-x64", Unreachable());
        var parent = Path.GetDirectoryName(managed.Root)!;
        Directory.CreateDirectory(Path.Combine(parent, ".install-win-x64-" + Guid.NewGuid().ToString("N"), "node"));
        await Assert.ThrowsAnyAsync<Exception>(() => managed.InstallAsync(new(null, null, null, null),
            Path.Combine(scope.Path, "unavailable-payload"), CancellationToken.None));
        Assert.Empty(Directory.GetDirectories(parent, ".install-*"));
    }

    // A verified tlc component whose receipt names the given release and payload; the lock row is synthetic.
    private static (StatechartManaged Managed, string Jar) Installed(string root, string release, string payload)
    {
        var assets = new StatechartAssets();
        byte[] bytes = [1, 2, 3, 4];
        var downloads = Unreachable().Select(item => item.Name == "tlc"
            ? item with { Integrity = Convert.ToHexStringLower(SHA256.HashData(bytes)) } : item).ToArray();
        var managed = new StatechartManaged(new StatechartHome(Path.Combine(root, "assets"),
            Path.Combine(root, "dependencies"), Path.Combine(root, "state")), assets, "win-x64", downloads);
        var component = Path.Combine(managed.Root, "tlc");
        Directory.CreateDirectory(component);
        File.WriteAllText(Path.Combine(managed.Root, ".statechart-store"), assets.LockDigest);
        var jar = Path.Combine(component, "tla2tools.jar");
        File.WriteAllBytes(jar, bytes);
        var receipt = new JsonObject { ["schema"] = 1, ["protocol"] = 1, ["release"] = release, ["rid"] = "win-x64",
            ["payload"] = payload, ["lock"] = assets.LockDigest, ["integrity"] = downloads.Single(item => item.Name == "tlc").Integrity,
            ["files"] = StatechartInventory.Capture(component) };
        File.WriteAllText(Path.Combine(component, ".ready.json"), receipt.ToJsonString());
        return (managed, jar);
    }

    private static StatechartDownload[] Unreachable() => new[] { "node", "java", "tlc", "pele" }
        .Select(name => new StatechartDownload(name,
            $"http://127.0.0.1:9/{name}/{(name == "tlc" ? "tla2tools.jar" : "archive.tar.gz")}", new string('a', 64), 1024))
        .ToArray();

    private static StatechartManaged Create(string root) => new(new StatechartHome(
        Path.Combine(root, "assets"), Path.Combine(root, "dependencies"), Path.Combine(root, "state")),
        new StatechartAssets(), "win-x64");
}
