namespace DynaDocs.Tests.Commands;

using System.Text;
using DynaDocs.Commands;
using DynaDocs.Services;

/// <summary>
/// What the guard may read as a PR body file (DYD-639): a regular file in the call directory's work
/// tree once its links resolve, not off-limits, bounded and UTF-8. Everything else reads as null.
/// </summary>
public sealed class GuardPrReadyBodyFileTests : IDisposable
{
    private const string Body = "## Independent review\n\nPASS - rubric/type: code\n";

    private readonly string _scratch = Path.Combine(Path.GetTempPath(), "dydo-pr-body-" + Guid.NewGuid().ToString("N")[..8]);

    private string Tree => Path.Combine(_scratch, "tree");

    private string Outside => Path.Combine(_scratch, "outside");

    public GuardPrReadyBodyFileTests()
    {
        Directory.CreateDirectory(Tree);
        Directory.CreateDirectory(Outside);
        Assert.True(GitRunner.Run(Tree, "init").Ok);
        Write(Path.Combine(Tree, "body.md"), Body);
        Write(Path.Combine(Outside, "body.md"), Body);
        Directory.CreateDirectory(Path.Combine(Tree, "dir"));
    }

    public void Dispose() => Directory.Delete(_scratch, recursive: true);

    private static void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private string? Read(string path, string? directory = null, Func<string, bool>? offLimits = null) =>
        GuardCommand.ReadPrBodyFile(path, directory ?? Tree, new OffLimits(offLimits ?? (_ => false)));

    [Fact]
    public void InTreeFile_IsRead()
    {
        Assert.Equal(Body, Read("body.md"));
        Assert.Equal(Body, Read(Path.Combine(Tree, "body.md")));
    }

    [Fact]
    public void FileNamedFromASubdirectory_IsReadFromThere()
    {
        Write(Path.Combine(Tree, "docs", "pr.md"), "docs body");

        Assert.Equal("docs body", Read("pr.md", directory: Path.Combine(Tree, "docs")));
        Assert.Equal(Body, Read("../body.md", directory: Path.Combine(Tree, "docs")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("-")]
    [InlineData("missing.md")]
    [InlineData("missing/body.md")]
    [InlineData(".")]
    [InlineData("dir")]
    [InlineData(".git/config")]
    [InlineData("../outside/body.md")]
    public void PathTheGuardMayNotRead_IsNull(string path)
    {
        Assert.Null(Read(path));
    }

    [Fact]
    public void OutsideFileNamedAbsolutely_IsNull()
    {
        Assert.Null(Read(Path.Combine(Outside, "body.md")));
    }

    // git reports the tree's root for a repository whose core.worktree names the tree, though its
    // own directory lies outside that tree.
    [Fact]
    public void OutsideRepositoryNamingTheTreeAsItsWorkTree_IsNull()
    {
        Assert.True(GitRunner.Run(Outside, "init").Ok);
        Assert.True(GitRunner.Run(Outside, "config", "core.worktree", Tree).Ok);

        Assert.Null(Read(Path.Combine(Outside, "body.md")));
    }

    [Fact]
    public void CallDirectoryOutsideGit_IsNull()
    {
        Assert.Null(Read("body.md", directory: Outside));
        Assert.Null(Read("body.md", directory: Path.Combine(_scratch, "gone")));
    }

    [Fact]
    public void OffLimitsFile_IsNull()
    {
        Assert.Null(Read("body.md", offLimits: path => path.EndsWith("body.md", StringComparison.Ordinal)));
    }

    [Fact]
    public void LinkedDirectoryLeavingTheTree_IsNull()
    {
        DirectoryLinks.Create(Path.Combine(Tree, "linked"), Outside);

        Assert.Null(Read("linked/body.md"));
    }

    [SkippableFact]
    public void FileLinks_AreFollowedOnlyWithinTheTree()
    {
        Skip.If(OperatingSystem.IsWindows(), "Creating file symbolic links needs Windows developer mode.");
        File.CreateSymbolicLink(Path.Combine(Tree, "out.md"), Path.Combine(Outside, "body.md"));
        File.CreateSymbolicLink(Path.Combine(Tree, "in.md"), "body.md");
        File.CreateSymbolicLink(Path.Combine(Tree, "loop-a.md"), "loop-b.md");
        File.CreateSymbolicLink(Path.Combine(Tree, "loop-b.md"), "loop-a.md");

        Assert.Null(Read("out.md"));
        Assert.Equal(Body, Read("in.md"));
        Assert.Null(Read("loop-a.md"));
        Assert.Null(Read("in.md", offLimits: path => path.EndsWith("in.md", StringComparison.Ordinal)));
        Assert.Null(Read("in.md", offLimits: path => path.EndsWith("body.md", StringComparison.Ordinal)));
    }

    // gh opens a/../body.md as the OS does: .. leaves the directory a links to, not a itself.
    [SkippableFact]
    public void DotDotAfterALinkedDirectory_LeavesTheLinkTarget()
    {
        Skip.If(OperatingSystem.IsWindows(), "Windows takes .. by name.");
        Write(Path.Combine(Tree, "x", "body.md"), "physical");
        Directory.CreateSymbolicLink(Path.Combine(Tree, "a"), Path.Combine(Tree, "x", "y"));
        Directory.CreateDirectory(Path.Combine(Tree, "x", "y"));
        Directory.CreateSymbolicLink(Path.Combine(Tree, "escape"), Path.Combine(Outside, "deeper"));
        Directory.CreateDirectory(Path.Combine(Outside, "deeper"));

        Assert.Equal("physical", Read("a/../body.md"));
        Assert.Null(Read("escape/../body.md"));
    }

    [Fact]
    public void EmptyFile_IsNull()
    {
        Write(Path.Combine(Tree, "empty.md"), "");

        Assert.Null(Read("empty.md"));
    }

    [Fact]
    public void FileAtTheCap_IsRead_AndOneByteOver_IsNull()
    {
        Write(Path.Combine(Tree, "cap.md"), new string('x', GuardCommand.PrBodyFileMaxBytes));
        Write(Path.Combine(Tree, "over.md"), new string('x', GuardCommand.PrBodyFileMaxBytes + 1));

        Assert.Equal(GuardCommand.PrBodyFileMaxBytes, Read("cap.md")?.Length);
        Assert.Null(Read("over.md"));
    }

    [Fact]
    public void InvalidUtf8_IsNull()
    {
        File.WriteAllBytes(Path.Combine(Tree, "binary.md"), [0x23, 0xC3, 0x28]);

        Assert.Null(Read("binary.md"));
    }

    [Fact]
    public void Utf8Body_IsReadAsText()
    {
        File.WriteAllBytes(Path.Combine(Tree, "utf8.md"), Encoding.UTF8.GetBytes("PASS – rubric/type: code"));

        Assert.Equal("PASS – rubric/type: code", Read("utf8.md"));
    }

    private sealed class OffLimits(Func<string, bool> isOffLimits) : IOffLimitsService
    {
        public void LoadPatterns(string? basePath = null) { }
        public string? IsPathOffLimits(string path) => isOffLimits(path) ? "pattern" : null;
        public IReadOnlyList<string> Patterns => [];
        public IReadOnlyList<string> WhitelistPatterns => [];
        public IEnumerable<string> ValidateLiteralPaths(string basePath) => [];
        public bool OffLimitsFileExists(string? basePath = null) => false;
        public IEnumerable<FormatValidationIssue> ValidateFormat(string? basePath = null) => [];
    }
}
