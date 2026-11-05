namespace DynaDocs.Tests.Statechart;

using DynaDocs.Services.Statechart;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

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
    [InlineData("valid")]
    [InlineData("foreign")]
    [InlineData("missing")]
    public void ForeignMarkerOrUnexpectedChildIsRetainedAndRejectedByPathWithTheRemedy(string marker)
    {
        using var scope = new StatechartTestDirectory();
        var assets = new StatechartAssets();
        var managed = Create(scope.Path);
        Directory.CreateDirectory(managed.Root);
        if (marker != "missing") File.WriteAllText(Path.Combine(managed.Root, ".statechart-store"), marker == "valid" ? assets.LockDigest : "foreign");
        var sentinel = Path.Combine(managed.Root, "foreign");
        File.WriteAllText(sentinel, "keep");
        var error = Assert.Throws<IOException>(() => managed.Read());
        Assert.Contains(managed.Root, error.Message);
        Assert.Contains("Move it aside", error.Message);
        Assert.Equal("keep", File.ReadAllText(sentinel));
    }

    // Crashes left these behind; the install lock shows no other run still uses them.
    [Fact]
    public async Task StagingAndReceiptTempFilesACrashLeftAreSweptUnderTheLock()
    {
        using var scope = new StatechartTestDirectory();
        var managed = new StatechartManaged(new StatechartHome(Path.Combine(scope.Path, "assets"),
            Path.Combine(scope.Path, "dependencies"), Path.Combine(scope.Path, "state")), new StatechartAssets(), "win-x64", Unreachable());
        var parent = Path.GetDirectoryName(managed.Root)!;
        Directory.CreateDirectory(Path.Combine(parent, ".install-win-x64-" + Guid.NewGuid().ToString("N"), "store"));
        File.WriteAllText(Path.Combine(parent, ".receipt-win-x64-" + Guid.NewGuid().ToString("N")), "{}");
        var other = Path.Combine(parent, ".receipt-linux-x64-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(other, "{}");
        await managed.InstallAsync(new("node", "java", "tlc", "pele"), Path.Combine(scope.Path, "unavailable-payload"), CancellationToken.None);
        Assert.Equal([other, Path.Combine(parent, "win-x64.install.lock")],
            Directory.GetFileSystemEntries(parent).Order(StringComparer.Ordinal));
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

    // The receipt records content hashes, not modes, so a runtime that cannot start still matches it.
    [SkippableTheory]
    [InlineData(false)]
    [InlineData(true)]
    [UnsupportedOSPlatform("windows")]
    public void AManagedRuntimeThatCannotExecuteIsDamagedAndLeftInPlace(bool executable)
    {
        Skip.If(OperatingSystem.IsWindows(), "POSIX modes decide whether a runtime can start.");
        using var scope = new StatechartTestDirectory();
        var (managed, java) = InstalledJava(scope.Path);
        if (!executable) File.SetUnixFileMode(java, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        var mode = File.GetUnixFileMode(java);
        Assert.Equal(executable ? java : null, managed.Read().Java);
        Assert.Equal(executable ? [] : ["java"], managed.Inspect().Damaged);
        Assert.Equal(mode, File.GetUnixFileMode(java));
    }

    // Stand-in archives arrive from loopback under a test-built lock and face the real fetch, integrity check, extraction,
    // readiness and placement; a later release finds them stale and re-stamps them by a full proof without reinstalling.
    [SkippableFact]
    [UnsupportedOSPlatform("windows")]
    public async Task AnApprovedInstallFetchesVerifiesProvesAndPlacesEveryLockedComponent()
    {
        Skip.IfNot(OperatingSystem.IsLinux(), "POSIX ownership runs on Linux.");
        StatechartExecutable.RelaunchThrough(StatechartTestBinary.Start().FileName);
        using var scope = new StatechartTestDirectory();
        var log = Path.Combine(scope.Path, "java.log");
        var served = StatechartStandIns.Components(log, StatechartStandIns.CleanRun);
        await StatechartStandIns.Serve(served, async root =>
        {
            var assets = StatechartStandIns.Assets(StatechartStandIns.Lock(root, served, StatechartStandIns.Sha256(served["/tlc/tla2tools.jar"])));
            var home = new StatechartHome(Path.Combine(scope.Path, "assets"), Path.Combine(scope.Path, "dependencies"), Path.Combine(scope.Path, "state"));
            var managed = new StatechartManaged(home, assets, RuntimeInformation.RuntimeIdentifier);
            var payload = await assets.ExtractAsync(home.Assets, CancellationToken.None);
            var installed = await managed.InstallAsync(new(null, null, null, null), payload, CancellationToken.None);
            Assert.Empty(StatechartManaged.Missing(installed));
            Assert.Equal(installed, managed.Read());
            Assert.All(new[] { installed.Node!, installed.Java!, installed.Tlc!, installed.Renderer! },
                path => Assert.StartsWith(managed.Root + Path.DirectorySeparatorChar, path));
            Assert.Equal(served["/tlc/tla2tools.jar"], File.ReadAllBytes(installed.Tlc!));
            Assert.All(Receipts(managed.Root), receipt =>
            {
                Assert.Equal(assets.Release, receipt["release"]!.GetValue<string>());
                Assert.True(receipt["readiness"]!["sanyAndTlc"]!.GetValue<bool>());
            });
            Assert.Empty(Directory.GetDirectories(Path.GetDirectoryName(managed.Root)!, ".install-*"));
            foreach (var receipt in Directory.GetFiles(managed.Root, ".ready.json", SearchOption.AllDirectories))
            {
                var identity = JsonNode.Parse(File.ReadAllText(receipt))!.AsObject();
                identity["release"] = "0.0.1";
                File.WriteAllText(receipt, identity.ToJsonString());
            }
            var later = new StatechartManaged(home, assets, RuntimeInformation.RuntimeIdentifier);
            Assert.Equal(["node", "java", "tlc", "pele"], later.Inspect().Stale);
            Assert.True(await later.RestampAsync(later.Read(), payload, CancellationToken.None));
            Assert.All(Receipts(managed.Root), receipt => Assert.Equal(assets.Release, receipt["release"]!.GetValue<string>()));
            Assert.False(await later.RestampAsync(later.Read(), payload, CancellationToken.None));
            Assert.Equal(installed, later.Read());
        });
        Assert.Equal(2, File.ReadAllLines(log).Count(call => call.Contains("dydo.statechart.LoopbackTlc", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task AServedArchiveThatDiffersFromItsLockIsRejectedAndNothingIsPlaced()
    {
        using var scope = new StatechartTestDirectory();
        var served = StatechartStandIns.Components(Path.Combine(scope.Path, "java.log"), StatechartStandIns.CleanRun);
        await StatechartStandIns.Serve(served, async root =>
        {
            var assets = StatechartStandIns.Assets(StatechartStandIns.Lock(root, served, StatechartStandIns.Sha256(served["/tlc/tla2tools.jar"])));
            served["/node/node.tar.gz"] = StatechartStandIns.TarGz(("node-v26.13.0-linux/bin/node", "#!/bin/sh\necho substituted\n"));
            var home = new StatechartHome(Path.Combine(scope.Path, "assets"), Path.Combine(scope.Path, "dependencies"), Path.Combine(scope.Path, "state"));
            var managed = new StatechartManaged(home, assets, RuntimeInformation.RuntimeIdentifier);
            var error = await Assert.ThrowsAsync<IOException>(() =>
                managed.InstallAsync(new(null, null, null, null), Path.Combine(scope.Path, "unused-payload"), CancellationToken.None));
            Assert.Equal("Locked statechart dependency integrity mismatch: node", error.Message);
            Assert.Equal([".statechart-store"], Directory.GetFileSystemEntries(managed.Root).Select(Path.GetFileName));
            Assert.Empty(Directory.GetDirectories(Path.GetDirectoryName(managed.Root)!, ".install-*"));
        });
    }

    private static JsonObject[] Receipts(string root) => Directory.GetFiles(root, ".ready.json", SearchOption.AllDirectories)
        .Select(receipt => JsonNode.Parse(File.ReadAllText(receipt))!.AsObject()).ToArray();

    // A verified linux-x64 java component with a synthetic lock row.
    [UnsupportedOSPlatform("windows")]
    private static (StatechartManaged Managed, string Java) InstalledJava(string root)
    {
        var assets = new StatechartAssets();
        byte[] archive = [5, 6, 7];
        var downloads = Unreachable().Select(item => item.Name == "java"
            ? item with { Integrity = Convert.ToHexStringLower(SHA256.HashData(archive)) } : item).ToArray();
        var managed = new StatechartManaged(new StatechartHome(Path.Combine(root, "assets"),
            Path.Combine(root, "dependencies"), Path.Combine(root, "state")), assets, "linux-x64", downloads);
        var component = Path.Combine(managed.Root, "java");
        var java = Path.Combine(component, "content", "jre", "bin", "java");
        Directory.CreateDirectory(Path.GetDirectoryName(java)!);
        File.WriteAllText(java, "#!/bin/sh\n");
        File.SetUnixFileMode(java, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        File.WriteAllBytes(Path.Combine(component, "archive.tar.gz"), archive);
        File.WriteAllText(Path.Combine(managed.Root, ".statechart-store"), assets.LockDigest);
        var receipt = new JsonObject { ["schema"] = 1, ["protocol"] = 1, ["release"] = assets.Release, ["rid"] = "linux-x64",
            ["payload"] = assets.Digest, ["lock"] = assets.LockDigest, ["integrity"] = downloads.Single(item => item.Name == "java").Integrity,
            ["files"] = StatechartInventory.Capture(component) };
        File.WriteAllText(Path.Combine(component, ".ready.json"), receipt.ToJsonString());
        return (managed, java);
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
