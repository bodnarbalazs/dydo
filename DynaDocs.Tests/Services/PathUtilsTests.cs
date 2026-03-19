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

    #region NormalizeWorktreePath

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NormalizeWorktreePath_NullOrEmpty_ReturnsUnchanged(string? input)
    {
        Assert.Equal(input, PathUtils.NormalizeWorktreePath(input));
    }

    [Theory]
    [InlineData("C:/Users/User/DynaDocs/Commands/GuardCommand.cs")]
    [InlineData("Commands/GuardCommand.cs")]
    [InlineData("dydo/understand/about.md")]
    [InlineData("dydo/_system/roles/code-writer.role.json")]
    public void NormalizeWorktreePath_NonWorktreePath_ReturnsUnchanged(string input)
    {
        Assert.Equal(input, PathUtils.NormalizeWorktreePath(input));
    }

    [Fact]
    public void NormalizeWorktreePath_SingleLevelWorktree_DydoContent()
    {
        // Set up temp dir with worktree structure containing dydo.json
        var tempDir = Path.Combine(Path.GetTempPath(), $"dydo-test-{Guid.NewGuid():N}");
        try
        {
            var worktreeRoot = Path.Combine(tempDir, "dydo", "_system", ".local", "worktrees", "fix-auth");
            Directory.CreateDirectory(worktreeRoot);
            File.WriteAllText(Path.Combine(worktreeRoot, "dydo.json"), "{}");

            var input = Path.Combine(tempDir, "dydo/_system/.local/worktrees/fix-auth/dydo/understand/about.md").Replace('\\', '/');
            var expected = Path.Combine(tempDir, "dydo/understand/about.md").Replace('\\', '/');

            Assert.Equal(expected, PathUtils.NormalizeWorktreePath(input));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void NormalizeWorktreePath_SingleLevelWorktree_SourceContent()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dydo-test-{Guid.NewGuid():N}");
        try
        {
            var worktreeRoot = Path.Combine(tempDir, "dydo", "_system", ".local", "worktrees", "fix-auth");
            Directory.CreateDirectory(worktreeRoot);
            File.WriteAllText(Path.Combine(worktreeRoot, "dydo.json"), "{}");

            var input = Path.Combine(tempDir, "dydo/_system/.local/worktrees/fix-auth/Commands/GuardCommand.cs").Replace('\\', '/');
            var expected = Path.Combine(tempDir, "Commands/GuardCommand.cs").Replace('\\', '/');

            Assert.Equal(expected, PathUtils.NormalizeWorktreePath(input));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void NormalizeWorktreePath_HierarchicalWorktree()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dydo-test-{Guid.NewGuid():N}");
        try
        {
            // Create parent worktree (also has dydo.json)
            var parentRoot = Path.Combine(tempDir, "dydo", "_system", ".local", "worktrees", "parent");
            Directory.CreateDirectory(parentRoot);
            File.WriteAllText(Path.Combine(parentRoot, "dydo.json"), "{}");

            // Create child worktree (deepest match should win)
            var childRoot = Path.Combine(tempDir, "dydo", "_system", ".local", "worktrees", "parent", "child");
            Directory.CreateDirectory(childRoot);
            File.WriteAllText(Path.Combine(childRoot, "dydo.json"), "{}");

            var input = Path.Combine(tempDir, "dydo/_system/.local/worktrees/parent/child/Services/Foo.cs").Replace('\\', '/');
            var expected = Path.Combine(tempDir, "Services/Foo.cs").Replace('\\', '/');

            Assert.Equal(expected, PathUtils.NormalizeWorktreePath(input));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void NormalizeWorktreePath_NoWorktreeRootIdentifiable_ReturnsUnchanged()
    {
        // Path contains the marker but no directory has dydo.json
        var input = "C:/nowhere/dydo/_system/.local/worktrees/mystery/Commands/Foo.cs";
        Assert.Equal(input, PathUtils.NormalizeWorktreePath(input));
    }

    [Fact]
    public void NormalizeWorktreePath_BackslashesInInput()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dydo-test-{Guid.NewGuid():N}");
        try
        {
            var worktreeRoot = Path.Combine(tempDir, "dydo", "_system", ".local", "worktrees", "fix-auth");
            Directory.CreateDirectory(worktreeRoot);
            File.WriteAllText(Path.Combine(worktreeRoot, "dydo.json"), "{}");

            // Input uses backslashes (Windows-style)
            var input = $"{tempDir}\\dydo\\_system\\.local\\worktrees\\fix-auth\\Services\\Foo.cs";
            var expected = Path.Combine(tempDir, "Services/Foo.cs").Replace('\\', '/');

            Assert.Equal(expected, PathUtils.NormalizeWorktreePath(input));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void NormalizeWorktreePath_PathPointsToWorktreeRoot_ReturnsUnchanged()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dydo-test-{Guid.NewGuid():N}");
        try
        {
            var worktreeRoot = Path.Combine(tempDir, "dydo", "_system", ".local", "worktrees", "fix-auth");
            Directory.CreateDirectory(worktreeRoot);
            File.WriteAllText(Path.Combine(worktreeRoot, "dydo.json"), "{}");

            // Path ends at worktree root with trailing slash — no project content after it
            var input = Path.Combine(tempDir, "dydo/_system/.local/worktrees/fix-auth/").Replace('\\', '/');

            // Should return unchanged since there's no project content
            Assert.Equal(input, PathUtils.NormalizeWorktreePath(input));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    #endregion
}
