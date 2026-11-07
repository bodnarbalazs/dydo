namespace DynaDocs.Tests.Statechart;

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using DynaDocs.Services.Statechart;

public class StatechartCheckerTests
{
    public static TheoryData<bool> Inputs => new() { true, false };

    // Each row runs on the pinned jar from TLA2TOOLS and on a stand-in pinned by its own digest. Neither is ever run.
    public static TheoryData<string, bool> Both(params string[] values)
    {
        var data = new TheoryData<string, bool>();
        foreach (var value in values)
            foreach (var pinned in new[] { true, false }) data.Add(value, pinned);
        return data;
    }

    public static TheoryData<string, bool> Companions => Both("file", "directory", "dangling", "file-link", "directory-link");
    public static TheoryData<string, bool> Changes =>
        Both("source", "destination", "missing", "extra", "file-link", "directory-link", "ancestor-link", "late-companion");
    public static TheoryData<string, bool> Bootstraps => Both("valid", "missing", "changed", "unbuilt");

    private static (string Jar, string Digest) CopyInput(string root, bool pinned)
    {
        var input = Environment.GetEnvironmentVariable("TLA2TOOLS");
        if (pinned)
        {
            Skip.If(input is null, "TLA2TOOLS must name the pinned tla2tools.jar 1.7.4; these tests read its bytes and invoke no Java.");
            Assert.True(Path.IsPathFullyQualified(input), "TLA2TOOLS must identify real pinned checker DATA; no Java is invoked.");
        }
        var source = Path.Combine(root, "input with spaces");
        Directory.CreateDirectory(source);
        var jar = Path.Combine(source, "original.jar");
        if (!pinned)
        {
            var standIn = StatechartStandIns.Jar();
            File.WriteAllBytes(jar, standIn);
            return (jar, StatechartStandIns.Sha256(standIn));
        }
        var digest = new StatechartAssets().CheckerDigest;
        File.Copy(StatechartChecker.Input(input!, digest), jar, overwrite: false);
        return (jar, digest);
    }

    [SkippableTheory]
    [MemberData(nameof(Inputs))]
    public void OwnedCopyRetainsOfficialBytesAndManifestWithoutChangingOriginal(bool pinned)
    {
        using var scope = new StatechartTestDirectory();
        var (source, digest) = CopyInput(scope.Path, pinned);
        var original = File.ReadAllBytes(source);
        var checker = StatechartChecker.Prepare(source, scope.Path, digest);
        var copy = checker.Verify();
        Assert.Equal(original, File.ReadAllBytes(copy));
        Assert.Equal(original, File.ReadAllBytes(source));
        Assert.Equal([copy], Directory.GetFileSystemEntries(Path.GetDirectoryName(copy)!));
        using var archive = ZipFile.OpenRead(copy);
        using var manifest = archive.GetEntry("META-INF/MANIFEST.MF")!.Open();
        if (pinned)
            Assert.Equal("4b779480fedf8e1bf24411052bfbd00a914b1c80caee1c7b4719d8cb67c7b7ce",
                Convert.ToHexStringLower(SHA256.HashData(manifest)));
    }

    // The digest is the lock's pin: the payload's own lock names the real jar, and nothing else passes for it.
    [SkippableTheory]
    [MemberData(nameof(Inputs))]
    public void AnInputOtherThanThePinnedJarIsRefusedBeforeAnyCopy(bool pinned)
    {
        using var scope = new StatechartTestDirectory();
        var (source, digest) = CopyInput(scope.Path, pinned);
        Assert.Equal("936a262061c914694dfd669a543be24573c45d5aa0ff20a8b96b23d01e050e88", new StatechartAssets().CheckerDigest);
        var other = pinned ? StatechartStandIns.Sha256(StatechartStandIns.Jar()) : new StatechartAssets().CheckerDigest;
        var error = Assert.Throws<IOException>(() => StatechartChecker.Prepare(source, scope.Path, other));
        Assert.Equal("Statechart requires the pinned TLC1.7.4 jar: " + source, error.Message);
        Assert.False(Directory.Exists(Path.Combine(scope.Path, "checker")));
        Assert.Equal(source, StatechartPrerequisites.Tlc(scope.Path, source, digest));
    }

    [SkippableTheory]
    [MemberData(nameof(Companions))]
    public void CompanionIsRejectedByExplicitDiscoveryAndPreparationWithoutChangingForeignBytes(string kind, bool pinned)
    {
        using var scope = new StatechartTestDirectory();
        var (source, digest) = CopyInput(scope.Path, pinned);
        var companion = Path.Combine(Path.GetDirectoryName(source)!, "CommunityModules.jar");
        var foreign = Path.Combine(scope.Path, "foreign");
        if (kind == "file") File.WriteAllText(companion, "foreign companion");
        else if (kind == "directory") Directory.CreateDirectory(companion);
        else if (kind == "directory-link")
        {
            Directory.CreateDirectory(foreign);
            Directory.CreateSymbolicLink(companion, foreign);
        }
        else
        {
            if (kind == "file-link") File.WriteAllText(foreign, "foreign target");
            File.CreateSymbolicLink(companion, foreign);
        }
        var original = File.ReadAllBytes(source);
        var error = Assert.Throws<IOException>(() => StatechartPrerequisites.Tlc(scope.Path, source, digest));
        Assert.Contains("Unsupported unpinned dependency CommunityModules.jar", error.Message);
        Assert.Contains(source, error.Message);
        Assert.Throws<IOException>(() => StatechartChecker.Prepare(source, scope.Path, digest));
        Assert.False(Directory.Exists(Path.Combine(scope.Path, "checker")));
        Assert.Equal(original, File.ReadAllBytes(source));
        Assert.Contains(companion, Directory.GetFileSystemEntries(Path.GetDirectoryName(source)!));
        if (kind == "file") Assert.Equal("foreign companion", File.ReadAllText(companion));
        if (kind == "file-link") Assert.Equal("foreign target", File.ReadAllText(foreign));
    }

    [SkippableTheory]
    [MemberData(nameof(Inputs))]
    public void AliasedFileAndDirectoryInputsCheckTheActualJarCompanion(bool pinned)
    {
        using var scope = new StatechartTestDirectory();
        var (source, digest) = CopyInput(scope.Path, pinned);
        var file = Path.Combine(scope.Path, "alias.jar");
        var directory = Path.Combine(scope.Path, "alias-directory");
        File.CreateSymbolicLink(file, source);
        Directory.CreateSymbolicLink(directory, Path.GetDirectoryName(source)!);
        var aliases = new[] { file, Path.Combine(directory, "original.jar") };
        foreach (var alias in aliases) Assert.Equal(source, StatechartChecker.Input(alias, digest));
        var companion = Path.Combine(Path.GetDirectoryName(source)!, "CommunityModules.jar");
        File.WriteAllText(companion, "foreign");
        foreach (var alias in aliases) Assert.Throws<IOException>(() => StatechartChecker.Input(alias, digest));
        Assert.Equal("foreign", File.ReadAllText(companion));
    }

    [SkippableTheory]
    [MemberData(nameof(Changes))]
    public void EveryLaunchRechecksSourceDestinationMembershipAndPlainAncestors(string change, bool pinned)
    {
        using var scope = new StatechartTestDirectory();
        var (source, digest) = CopyInput(scope.Path, pinned);
        var owned = Path.Combine(scope.Path, "owned");
        Directory.CreateDirectory(owned);
        var checker = StatechartChecker.Prepare(source, owned, digest);
        var copy = checker.Verify();
        var directory = Path.GetDirectoryName(copy)!;
        if (change == "source") File.AppendAllText(source, "changed");
        if (change == "destination") File.AppendAllText(copy, "changed");
        if (change == "missing") File.Delete(copy);
        if (change == "extra") File.WriteAllText(Path.Combine(directory, "foreign"), "sentinel");
        if (change == "late-companion") File.WriteAllText(Path.Combine(Path.GetDirectoryName(source)!, "CommunityModules.jar"), "sentinel");
        if (change == "file-link") { File.Delete(copy); File.CreateSymbolicLink(copy, source); }
        if (change is "directory-link" or "ancestor-link")
        {
            var original = change == "directory-link" ? directory : owned;
            Directory.Move(original, original + "-retained");
            Directory.CreateSymbolicLink(original, original + "-retained");
        }
        Assert.ThrowsAny<IOException>(() => checker.Verify());
        if (change == "extra") Assert.Equal("sentinel", File.ReadAllText(Path.Combine(directory, "foreign")));
    }

    [SkippableTheory]
    [MemberData(nameof(Inputs))]
    public void ExistingCheckerDirectoryIsRetainedWithoutOverwrite(bool pinned)
    {
        using var scope = new StatechartTestDirectory();
        var (source, digest) = CopyInput(scope.Path, pinned);
        var directory = Path.Combine(scope.Path, "checker");
        Directory.CreateDirectory(directory);
        var sentinel = Path.Combine(directory, "foreign");
        File.WriteAllText(sentinel, "retain");
        Assert.Throws<System.ComponentModel.Win32Exception>(() => StatechartChecker.Prepare(source, scope.Path, digest));
        Assert.Equal("retain", File.ReadAllText(sentinel));
    }

    [SkippableTheory]
    [MemberData(nameof(Bootstraps))]
    public void BootstrapResolutionUsesSealedIdentityAndNeverFallsBackToRawTlc(string state, bool pinned)
    {
        using var scope = new StatechartTestDirectory();
        var (source, digest) = CopyInput(scope.Path, pinned);
        var checker = StatechartChecker.Prepare(source, scope.Path, digest);
        var root = Path.Combine(scope.Path, "payload");
        var java = Path.Combine(root, "skills/engineering/statechart/scripts/java");
        Directory.CreateDirectory(java);
        var helper = Path.Combine(java, "statechart-tlc-bootstrap.jar");
        // Hash-resolution fixture only; these bytes are never passed to a JVM.
        var raw = "synthetic helper fixture"u8.ToArray();
        File.WriteAllBytes(helper, raw);
        File.WriteAllText(Path.Combine(java, "build-lock.json"), JsonSerializer.Serialize(new
        {
            schema = 1, artifact = new { state = state == "unbuilt" ? "unbuilt" : "sealed-build",
                sha256 = Convert.ToHexStringLower(SHA256.HashData(raw)) }
        }));
        if (state == "missing") File.Delete(helper);
        if (state == "changed") File.AppendAllText(helper, "changed");
        if (state != "valid") Assert.Throws<IOException>(() => checker.TlcArguments(root));
        else Assert.Equal(["-cp", helper + Path.PathSeparator + checker.Verify(), "dydo.statechart.LoopbackTlc"], checker.TlcArguments(root));
    }

    // A linked build lock could point resolution at another helper, so it is refused before it is read.
    [SkippableFact]
    public void ALinkedBootstrapBuildLockIsRefusedWithoutAFallback()
    {
        Skip.If(OperatingSystem.IsWindows(), "Creating symbolic links needs Windows developer mode.");
        using var scope = new StatechartTestDirectory();
        var (source, digest) = CopyInput(scope.Path, pinned: false);
        var checker = StatechartChecker.Prepare(source, scope.Path, digest);
        var java = Directory.CreateDirectory(Path.Combine(scope.Path, "payload/skills/engineering/statechart/scripts/java")).FullName;
        File.WriteAllText(Path.Combine(scope.Path, "elsewhere.json"), "{}");
        File.CreateSymbolicLink(Path.Combine(java, "build-lock.json"), Path.Combine(scope.Path, "elsewhere.json"));
        Assert.Equal("Linked statechart bootstrap build lock; no raw TLC fallback.",
            Assert.Throws<IOException>(() => checker.TlcArguments(Path.Combine(scope.Path, "payload"))).Message);
    }
}
