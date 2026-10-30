namespace DynaDocs.Tests.Statechart;

using System.Formats.Tar;
using System.IO.Compression;
using DynaDocs.Services.Statechart;

public class StatechartArchiveTests
{
    [Fact]
    public void LockedZipExtractsExactBytesToFreshOwnedStagingWithoutTouchingArchive()
    {
        using var scope = new StatechartTestDirectory();
        var archive = Path.Combine(scope.Path, "node.zip");
        Zip(archive, [("node/bin/node.exe", "native fixture"), ("node/LICENSE", "license")]);
        var original = File.ReadAllBytes(archive);
        var target = Path.Combine(scope.Path, "staging");
        StatechartArchive.Extract(archive, target, CancellationToken.None);
        Assert.Equal("native fixture", File.ReadAllText(Path.Combine(target, "node/bin/node.exe")));
        Assert.Equal("license", File.ReadAllText(Path.Combine(target, "node/LICENSE")));
        Assert.Equal(original, File.ReadAllBytes(archive));
    }

    [Fact]
    public void GzipTarRetainsMacJreLayoutAndMaterializesOnlyContainedHardlinkContent()
    {
        using var scope = new StatechartTestDirectory();
        var archive = Path.Combine(scope.Path, "jre.tar.gz");
        using (var file = File.Create(archive))
        using (var gzip = new GZipStream(file, CompressionMode.Compress))
        using (var writer = new TarWriter(gzip))
        {
            var entry = new PaxTarEntry(TarEntryType.RegularFile, "jdk/Contents/Home/bin/java")
            {
                Mode = UnixFileMode.UserRead | UnixFileMode.UserExecute,
                DataStream = new MemoryStream("java fixture"u8.ToArray())
            };
            writer.WriteEntry(entry);
            writer.WriteEntry(new PaxTarEntry(TarEntryType.HardLink, "jdk/Contents/Home/bin/java-copy")
                { LinkName = entry.Name });
        }
        var target = Path.Combine(scope.Path, "staging");
        StatechartArchive.Extract(archive, target, CancellationToken.None);
        var java = Path.Combine(target, "jdk/Contents/Home/bin/java");
        Assert.Equal("java fixture", File.ReadAllText(java));
        Assert.Equal("java fixture", File.ReadAllText(Path.Combine(target, "jdk/Contents/Home/bin/java-copy")));
        if (!OperatingSystem.IsWindows())
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserExecute, File.GetUnixFileMode(java));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("/absolute")]
    [InlineData("C:/drive")]
    [InlineData("//server/share")]
    [InlineData("nested\\escape")]
    [InlineData("a/./b")]
    [InlineData("a//b")]
    [InlineData("node/CON")]
    [InlineData("node/trailing.")]
    public void UnsafeNamesCannotWriteOutsideOwnedStaging(string name)
    {
        using var scope = new StatechartTestDirectory();
        var archive = Path.Combine(scope.Path, "unsafe.zip");
        Zip(archive, [(name, "bad")]);
        Assert.Throws<IOException>(() => StatechartArchive.Extract(archive, Path.Combine(scope.Path, "staging"), CancellationToken.None));
        Assert.False(File.Exists(Path.Combine(scope.Path, "escape")));
    }

    [Theory]
    [InlineData("same", "same")]
    [InlineData("Node/file", "node/FILE")]
    [InlineData("node", "node/child")]
    public void DuplicateCaseCollisionAndFileAncestorReject(string first, string second)
    {
        using var scope = new StatechartTestDirectory();
        var archive = Path.Combine(scope.Path, "duplicate.zip");
        Zip(archive, [(first, "one"), (second, "two")]);
        Assert.Throws<IOException>(() => StatechartArchive.Extract(archive, Path.Combine(scope.Path, "staging"), CancellationToken.None));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExpandedBytesAndEntryCountAreStreamedBounds(bool count)
    {
        using var scope = new StatechartTestDirectory();
        var archive = Path.Combine(scope.Path, "bounded.zip");
        Zip(archive, [("one", "1234"), ("two", "5678")]);
        var target = Path.Combine(scope.Path, "staging");
        var error = Assert.Throws<IOException>(() => StatechartArchive.Extract(archive, target, CancellationToken.None,
            expandedLimit: count ? 8 : 3, entryLimit: count ? 1 : 2));
        Assert.Contains("bound", error.Message);
        Assert.False(File.Exists(Path.Combine(target, "two")));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("/absolute")]
    [InlineData("C:/drive")]
    public void EscapingTarLinksRejectBeforeLinkCreation(string link)
    {
        using var scope = new StatechartTestDirectory();
        var archive = Path.Combine(scope.Path, "link.tgz");
        using (var file = File.Create(archive))
        using (var gzip = new GZipStream(file, CompressionMode.Compress))
        using (var writer = new TarWriter(gzip))
            writer.WriteEntry(new PaxTarEntry(TarEntryType.SymbolicLink, "link") { LinkName = link });
        var target = Path.Combine(scope.Path, "staging");
        Assert.Throws<IOException>(() => StatechartArchive.Extract(archive, target, CancellationToken.None));
        Assert.Null(new FileInfo(Path.Combine(target, "link")).LinkTarget);
    }

    [Fact]
    public void ForeignStagingAndCancellationAreRetainedWithoutOverwrite()
    {
        using var scope = new StatechartTestDirectory();
        var archive = Path.Combine(scope.Path, "node.zip");
        Zip(archive, [("file", "new")]);
        var target = Path.Combine(scope.Path, "foreign");
        Directory.CreateDirectory(target);
        var sentinel = Path.Combine(target, "keep");
        File.WriteAllText(sentinel, "foreign");
        Assert.Throws<IOException>(() => StatechartArchive.Extract(archive, target, CancellationToken.None));
        Assert.Equal("foreign", File.ReadAllText(sentinel));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var fresh = Path.Combine(scope.Path, "cancelled");
        Assert.Throws<OperationCanceledException>(() => StatechartArchive.Extract(archive, fresh, cancelled.Token));
        Assert.False(Directory.Exists(fresh));
    }

    private static void Zip(string path, (string Name, string Content)[] entries)
    {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, content) in entries)
        {
            using var writer = new StreamWriter(zip.CreateEntry(name).Open());
            writer.Write(content);
        }
    }
}
