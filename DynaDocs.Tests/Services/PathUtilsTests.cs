namespace DynaDocs.Tests.Services;

using DynaDocs.Utils;
using Xunit;

public class PathUtilsTests
{
    [Theory]
    [InlineData("coding-standards.md", true)]
    [InlineData("_index.md", true)]
    [InlineData("CLAUDE.md", true)]
    [InlineData("my-doc.md", true)]
    [InlineData("api-v2.md", true)]
    [InlineData("CodingStandards.md", false)]
    [InlineData("Coding Standards.md", false)]
    [InlineData("coding_standards.md", false)]
    [InlineData("CODING-STANDARDS.md", false)]
    public void IsKebabCase_ValidatesCorrectly(string filename, bool expected)
    {
        var result = PathUtils.IsKebabCase(filename);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("a/b/c", "a/b/c")]
    [InlineData("a/./b", "a/b")]
    [InlineData("a/b/../c", "a/c")]
    [InlineData("a/b/../../c", "c")]
    [InlineData("C:/x/y/../z", "C:/x/z")]
    [InlineData("C:\\x\\y\\..\\z", "C:/x/z")]
    [InlineData("/home/u/.claude/projects/p/memory/../../../../etc/secret", "/home/u/etc/secret")]
    [InlineData("a/../../escape", "../escape")]      // relative escape kept (can't be mistaken for a trusted root)
    [InlineData("/a/../../escape", "/escape")]        // rooted '..' at top is dropped
    public void CollapseRelativeSegments_ResolvesDotDot(string input, string expected)
    {
        Assert.Equal(expected, PathUtils.CollapseRelativeSegments(input));
    }

    [Theory]
    [InlineData("Coding Standards", "coding-standards")]
    [InlineData("CodingStandards", "coding-standards")]
    [InlineData("CODING_STANDARDS", "coding-standards")]
    [InlineData("My Document", "my-document")]
    [InlineData("already-kebab", "already-kebab")]
    [InlineData("Some_File_Name", "some-file-name")]
    public void ToKebabCase_ConvertsCorrectly(string input, string expected)
    {
        var result = PathUtils.ToKebabCase(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("path/to/file.md", "path/to/file.md")]
    [InlineData("path\\to\\file.md", "path/to/file.md")]
    [InlineData("path\\to/mixed\\file.md", "path/to/mixed/file.md")]
    public void NormalizePath_ConvertsBackslashes(string input, string expected)
    {
        var result = PathUtils.NormalizePath(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("path/to/file.md", "path/to/file.md")]
    [InlineData("./path/to/file.md", "path/to/file.md")]
    [InlineData("/path/to/file.md", "path/to/file.md")]
    [InlineData("path\\to\\file.md", "path/to/file.md")]
    [InlineData("./path\\to/file.md", "path/to/file.md")]
    [InlineData("Path/To/File.md", "Path/To/File.md")]  // preserves case
    [InlineData("///leading/slashes.md", "leading/slashes.md")]
    [InlineData("./", "")]
    [InlineData("/", "")]
    public void NormalizeForPattern_NormalizesCorrectly(string input, string expected)
    {
        var result = PathUtils.NormalizeForPattern(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Review Coordinator: Auth & Email", "Review Coordinator- Auth & Email")]
    [InlineData("task<name>with|pipes*and?questions", "task-name-with-pipes-and-questions")]
    [InlineData("some/path\\task", "some-path-task")]
    [InlineData("simple-task-name", "simple-task-name")]
    [InlineData("already clean", "already clean")]
    [InlineData(":::", "")]
    public void SanitizeForFilename_ReplacesIllegalChars(string input, string expected)
    {
        var result = PathUtils.SanitizeForFilename(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SanitizeForFilename_TruncatesLongNames()
    {
        var longName = new string('a', 200);
        var result = PathUtils.SanitizeForFilename(longName);
        Assert.True(result.Length <= 100);
    }

    [Theory]
    [InlineData("path/to/file.md", "path/to/file.md")]
    [InlineData("Path/To/File.md", "path/to/file.md")]  // lowercases
    [InlineData("./Path\\To/File.md", "path/to/file.md")]
    [InlineData("/PATH/TO/FILE.MD", "path/to/file.md")]
    [InlineData("INDEX.MD", "index.md")]
    public void NormalizeForKey_NormalizesAndLowercases(string input, string expected)
    {
        var result = PathUtils.NormalizeForKey(input);
        Assert.Equal(expected, result);
    }

    #region GetMainProjectRoot

    [Theory]
    [InlineData("C:/Projects/DynaDocs/dydo/_system/.local/worktrees/fix-auth", "C:/Projects/DynaDocs")]
    [InlineData("C:/Projects/DynaDocs/dydo/_system/.local/worktrees/fix-auth/", "C:/Projects/DynaDocs")]
    [InlineData("C:\\Projects\\DynaDocs\\dydo\\_system\\.local\\worktrees\\fix-auth", "C:/Projects/DynaDocs")]
    public void GetMainProjectRoot_WorktreeCwd_ReturnsMainRoot(string cwd, string expected)
    {
        Assert.Equal(expected, PathUtils.GetMainProjectRoot(cwd));
    }

    [Theory]
    [InlineData("C:/Projects/DynaDocs")]
    [InlineData("C:/Projects/DynaDocs/Commands")]
    [InlineData("/home/user/project")]
    public void GetMainProjectRoot_NonWorktreeCwd_ReturnsNull(string cwd)
    {
        Assert.Null(PathUtils.GetMainProjectRoot(cwd));
    }

    #endregion

    #region EnsureLocalDirExists

    [Fact]
    public void EnsureLocalDirExists_CreatesDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dydo-test-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            PathUtils.EnsureLocalDirExists(tempDir);
            Assert.True(Directory.Exists(Path.Combine(tempDir, "_system", ".local")));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void EnsureLocalDirExists_IdempotentWhenAlreadyExists()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dydo-test-{Guid.NewGuid():N}");
        try
        {
            var localDir = Path.Combine(tempDir, "_system", ".local");
            Directory.CreateDirectory(localDir);
            PathUtils.EnsureLocalDirExists(tempDir);
            Assert.True(Directory.Exists(localDir));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    #endregion
}
