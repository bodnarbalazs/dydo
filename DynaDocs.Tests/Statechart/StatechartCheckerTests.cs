namespace DynaDocs.Tests.Statechart;

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using DynaDocs.Services.Statechart;

public class StatechartCheckerTests
{
    private static string CopyInput(string root)
    {
        var input = Environment.GetEnvironmentVariable("TLA2TOOLS");
        Assert.True(input is not null && Path.IsPathFullyQualified(input), "TLA2TOOLS must identify real pinned checker DATA; no Java is invoked.");
        var source = Path.Combine(root, "input with spaces");
        Directory.CreateDirectory(source);
        var jar = Path.Combine(source, "original.jar");
        File.Copy(StatechartChecker.Input(input!), jar, overwrite: false);
        return jar;
    }

    [Fact]
    public void OwnedCopyRetainsOfficialBytesAndManifestWithoutChangingOriginal()
    {
        using var scope = new StatechartTestDirectory();
        var source = CopyInput(scope.Path);
        var original = File.ReadAllBytes(source);
        var checker = StatechartChecker.Prepare(source, scope.Path);
        var copy = checker.Verify();
        Assert.Equal(original, File.ReadAllBytes(copy));
        Assert.Equal(original, File.ReadAllBytes(source));
        Assert.Equal([copy], Directory.GetFileSystemEntries(Path.GetDirectoryName(copy)!));
        using var archive = ZipFile.OpenRead(copy);
        using var manifest = archive.GetEntry("META-INF/MANIFEST.MF")!.Open();
        Assert.Equal("4b779480fedf8e1bf24411052bfbd00a914b1c80caee1c7b4719d8cb67c7b7ce",
            Convert.ToHexStringLower(SHA256.HashData(manifest)));
    }

    [Theory]
    [InlineData("file")]
    [InlineData("directory")]
    [InlineData("dangling")]
    [InlineData("file-link")]
    [InlineData("directory-link")]
    public void CompanionIsRejectedByExplicitDiscoveryAndPreparationWithoutChangingForeignBytes(string kind)
    {
        using var scope = new StatechartTestDirectory();
        var source = CopyInput(scope.Path);
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
        var error = Assert.Throws<IOException>(() => StatechartPrerequisites.Tlc(scope.Path, source));
        Assert.Contains("Unsupported unpinned dependency CommunityModules.jar", error.Message);
        Assert.Contains(source, error.Message);
        Assert.Throws<IOException>(() => StatechartChecker.Prepare(source, scope.Path));
        Assert.False(Directory.Exists(Path.Combine(scope.Path, "checker")));
        Assert.Equal(original, File.ReadAllBytes(source));
        Assert.Contains(companion, Directory.GetFileSystemEntries(Path.GetDirectoryName(source)!));
        if (kind == "file") Assert.Equal("foreign companion", File.ReadAllText(companion));
        if (kind == "file-link") Assert.Equal("foreign target", File.ReadAllText(foreign));
    }

    [Fact]
    public void AliasedFileAndDirectoryInputsCheckTheActualJarCompanion()
    {
        using var scope = new StatechartTestDirectory();
        var source = CopyInput(scope.Path);
        var file = Path.Combine(scope.Path, "alias.jar");
        var directory = Path.Combine(scope.Path, "alias-directory");
        File.CreateSymbolicLink(file, source);
        Directory.CreateSymbolicLink(directory, Path.GetDirectoryName(source)!);
        var aliases = new[] { file, Path.Combine(directory, "original.jar") };
        foreach (var alias in aliases) Assert.Equal(source, StatechartChecker.Input(alias));
        var companion = Path.Combine(Path.GetDirectoryName(source)!, "CommunityModules.jar");
        File.WriteAllText(companion, "foreign");
        foreach (var alias in aliases) Assert.Throws<IOException>(() => StatechartChecker.Input(alias));
        Assert.Equal("foreign", File.ReadAllText(companion));
    }

    [Theory]
    [InlineData("source")]
    [InlineData("destination")]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("file-link")]
    [InlineData("directory-link")]
    [InlineData("ancestor-link")]
    [InlineData("late-companion")]
    public void EveryLaunchRechecksSourceDestinationMembershipAndPlainAncestors(string change)
    {
        using var scope = new StatechartTestDirectory();
        var source = CopyInput(scope.Path);
        var owned = Path.Combine(scope.Path, "owned");
        Directory.CreateDirectory(owned);
        var checker = StatechartChecker.Prepare(source, owned);
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

    [Fact]
    public void ExistingCheckerDirectoryIsRetainedWithoutOverwrite()
    {
        using var scope = new StatechartTestDirectory();
        var source = CopyInput(scope.Path);
        var directory = Path.Combine(scope.Path, "checker");
        Directory.CreateDirectory(directory);
        var sentinel = Path.Combine(directory, "foreign");
        File.WriteAllText(sentinel, "retain");
        Assert.Throws<System.ComponentModel.Win32Exception>(() => StatechartChecker.Prepare(source, scope.Path));
        Assert.Equal("retain", File.ReadAllText(sentinel));
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("missing")]
    [InlineData("changed")]
    [InlineData("unbuilt")]
    public void BootstrapResolutionUsesSealedIdentityAndNeverFallsBackToRawTlc(string state)
    {
        using var scope = new StatechartTestDirectory();
        var checker = StatechartChecker.Prepare(CopyInput(scope.Path), scope.Path);
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
}
