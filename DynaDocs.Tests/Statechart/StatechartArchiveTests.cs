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

    // The filesystem follows a link before it applies `..`; each lexical target names an existing decoy entry.
    [Theory]
    [InlineData("a,a/b,d", "a/outside", "a/b/l", "../../d", "escape", "a/b/l/../../outside")]
    [InlineData("p,p/q,r", "z", "p/q/x", "../../r", "p/q/y", "x/../../../z")]
    public void ComposedLinksThatLeaveTheRootRejectBeforeAnyLinkExists(string directories, string decoy,
        string first, string firstTarget, string second, string secondTarget)
    {
        using var scope = new StatechartTestDirectory();
        File.WriteAllText(Path.Combine(scope.Path, "outside"), "sentinel");
        var archive = Path.Combine(scope.Path, "links.tgz");
        Tar(archive, [.. directories.Split(',').Select(name => (name, TarEntryType.Directory, "")),
            (decoy, TarEntryType.RegularFile, "lexically contained decoy"),
            (first, TarEntryType.SymbolicLink, firstTarget), (second, TarEntryType.SymbolicLink, secondTarget)]);
        var target = Path.Combine(scope.Path, "staging");
        Assert.Throws<IOException>(() => StatechartArchive.Extract(archive, target, CancellationToken.None));
        Assert.Null(new FileInfo(Path.Combine(target, first)).LinkTarget);
        Assert.Null(new FileInfo(Path.Combine(target, second)).LinkTarget);
        Assert.Equal("sentinel", File.ReadAllText(Path.Combine(scope.Path, "outside")));
    }

    [Fact]
    public void LinksThatLoopThroughEachOtherReject()
    {
        using var scope = new StatechartTestDirectory();
        var archive = Path.Combine(scope.Path, "loop.tgz");
        Tar(archive, [("c", TarEntryType.Directory, ""), ("l", TarEntryType.SymbolicLink, "m/../c"),
            ("m", TarEntryType.SymbolicLink, "l/../c")]);
        var target = Path.Combine(scope.Path, "staging");
        Assert.Throws<IOException>(() => StatechartArchive.Extract(archive, target, CancellationToken.None));
        Assert.Null(new FileInfo(Path.Combine(target, "l")).LinkTarget);
    }

    [Fact]
    public void ALinkThroughAnotherLinkThatStaysInsideExtracts()
    {
        using var scope = new StatechartTestDirectory();
        var archive = Path.Combine(scope.Path, "contained.tgz");
        Tar(archive, [("a/b", TarEntryType.Directory, ""), ("d/x", TarEntryType.RegularFile, "inside"),
            ("a/b/l", TarEntryType.SymbolicLink, "../../d"), ("through", TarEntryType.SymbolicLink, "a/b/l/x")]);
        var target = Path.Combine(scope.Path, "staging");
        StatechartArchive.Extract(archive, target, CancellationToken.None);
        Assert.Equal("inside", File.ReadAllText(Path.Combine(target, "through")));
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

    // Content is a regular file's text or a link's target.
    private static void Tar(string path, (string Name, TarEntryType Type, string Content)[] entries)
    {
        using var file = File.Create(path);
        using var gzip = new GZipStream(file, CompressionMode.Compress);
        using var writer = new TarWriter(gzip);
        foreach (var (name, type, content) in entries)
        {
            var entry = new PaxTarEntry(type, name) { Mode = (UnixFileMode)0x1ed };
            if (type == TarEntryType.RegularFile) entry.DataStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
            if (type == TarEntryType.SymbolicLink) entry.LinkName = content;
            writer.WriteEntry(entry);
        }
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
