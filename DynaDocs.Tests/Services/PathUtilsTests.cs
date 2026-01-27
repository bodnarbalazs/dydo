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
}
