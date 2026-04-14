namespace DynaDocs.Tests.Services;

using DynaDocs.Utils;

public class PathUtilsDiscoveryTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _originalDir;

    public PathUtilsDiscoveryTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-pathutils-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _originalDir = Environment.CurrentDirectory;
    }

    public void Dispose()
    {
        Environment.CurrentDirectory = _originalDir;
        if (Directory.Exists(_testDir))
        {
            for (var i = 0; i < 3; i++)
            {
                try
                {
                    Directory.Delete(_testDir, true);
                    return;
                }
                catch (IOException) when (i < 2)
                {
                    Thread.Sleep(50 * (i + 1));
                }
            }
        }
    }

    [Theory]
    [InlineData("a/b/file.md", "../c/other.md")]
    [InlineData("file.md", "other.md")]
    public void ResolvePath_ResolvesRelative(string source, string relative)
    {
        var result = PathUtils.ResolvePath(source, relative);

        Assert.DoesNotContain("\\", result);
        Assert.DoesNotContain("..", result);
    }

    [Fact]
    public void ResolvePath_NormalizesBackslashes()
    {
        var result = PathUtils.ResolvePath("a\\b\\file.md", "other.md");

        Assert.DoesNotContain("\\", result);
    }

    [Theory]
    [InlineData("a/b/file.md", "a/b/other.md", "./other.md")]
    [InlineData("a/file.md", "a/b/other.md", "./b/other.md")]
    [InlineData("a/b/file.md", "c/other.md", "../../c/other.md")]
    public void GetRelativePath_ComputesCorrectly(string from, string to, string expected)
    {
        var result = PathUtils.GetRelativePath(from, to);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetRelativePath_HandlesRootFile()
    {
        var result = PathUtils.GetRelativePath("file.md", "other.md");
        Assert.Equal("./other.md", result);
    }

    [Fact]
    public void GetRelativePath_HandlesDeeplyNested()
    {
        var result = PathUtils.GetRelativePath("a/b/c/d/file.md", "a/b/x/file.md");
        Assert.Equal("../../x/file.md", result);
    }

    [Fact]
    public void FindDocsFolder_ReturnsDydoRoot_WhenConfigExists()
    {
        // Set up a minimal dydo project
        var dydoJson = Path.Combine(_testDir, "dydo.json");
        File.WriteAllText(dydoJson, """{"name": "test", "dydoDir": "dydo"}""");

        var dydoDir = Path.Combine(_testDir, "dydo");
        Directory.CreateDirectory(dydoDir);
        File.WriteAllText(Path.Combine(dydoDir, "index.md"), "# Test");

        Environment.CurrentDirectory = _testDir;

        var result = PathUtils.FindDocsFolder(_testDir);

        Assert.NotNull(result);
        Assert.EndsWith("dydo", result.Replace('\\', '/'));
    }

    [Fact]
    public void FindDocsFolder_FallsBackToLegacyDocs()
    {
        var docsDir = Path.Combine(_testDir, "docs");
        Directory.CreateDirectory(docsDir);
        File.WriteAllText(Path.Combine(docsDir, "index.md"), "# Docs");

        var result = PathUtils.FindDocsFolder(_testDir);

        Assert.NotNull(result);
        Assert.EndsWith("docs", result.Replace('\\', '/'));
    }

    [Fact]
    public void FindDocsFolder_FindsLegacySubdirectory()
    {
        var docsDir = Path.Combine(_testDir, "docs");
        Directory.CreateDirectory(docsDir);
        var subDir = Path.Combine(docsDir, "project");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "index.md"), "# Project Docs");

        var result = PathUtils.FindDocsFolder(_testDir);

        Assert.NotNull(result);
        Assert.EndsWith("project", result.Replace('\\', '/'));
    }

    [Fact]
    public void FindDocsFolder_ReturnsNull_WhenNoDocsExist()
    {
        var result = PathUtils.FindDocsFolder(_testDir);

        Assert.Null(result);
    }

    [Fact]
    public void FindProjectRoot_ReturnsNull_ForNonProject()
    {
        var result = PathUtils.FindProjectRoot(_testDir);

        Assert.Null(result);
    }

    [Fact]
    public void FindProjectRoot_FromSubdirectoryCwd_WalksToRoot()
    {
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), """{"name": "test"}""");
        var subDir = Path.Combine(_testDir, "dydo", "project", "tasks");
        Directory.CreateDirectory(subDir);
        Environment.CurrentDirectory = subDir;

        var result = PathUtils.FindProjectRoot();

        Assert.NotNull(result);
        Assert.Equal(
            Path.GetFullPath(_testDir).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(result).TrimEnd(Path.DirectorySeparatorChar));
    }

    [Fact]
    public void FindDydoRoot_ReturnsNull_ForNonProject()
    {
        var result = PathUtils.FindDydoRoot(_testDir);

        Assert.Null(result);
    }
}
