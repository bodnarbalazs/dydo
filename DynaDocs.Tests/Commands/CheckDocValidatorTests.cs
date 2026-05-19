namespace DynaDocs.Tests.Commands;

using DynaDocs.Commands;

public class CheckDocValidatorTests
{
    [Fact]
    public void IsUnderScope_ReturnsTrue_WhenPathEqualsScope()
    {
        Assert.True(CheckDocValidator.IsUnderScope("/base/dydo/x", "/base/dydo/x"));
    }

    [Fact]
    public void IsUnderScope_ReturnsTrue_WhenPathBelowScope()
    {
        Assert.True(CheckDocValidator.IsUnderScope("/base/dydo/x/y/z/file.md", "/base/dydo/x"));
    }

    [Fact]
    public void IsUnderScope_ReturnsFalse_WhenPathOutsideScope()
    {
        Assert.False(CheckDocValidator.IsUnderScope("/base/dydo/other/file.md", "/base/dydo/x"));
    }

    [Fact]
    public void IsUnderScope_HandlesMixedSeparators()
    {
        Assert.True(CheckDocValidator.IsUnderScope("/base\\dydo\\x\\file.md", "/base/dydo/x"));
        Assert.True(CheckDocValidator.IsUnderScope("/base/dydo/x/file.md", "/base\\dydo\\x"));
    }

    [Fact]
    public void IsUnderScope_IsCaseInsensitive()
    {
        Assert.True(CheckDocValidator.IsUnderScope("/Base/Dydo/X/file.md", "/base/dydo/x"));
    }

    [Fact]
    public void IsUnderScope_HandlesTrailingSlashInScope()
    {
        Assert.True(CheckDocValidator.IsUnderScope("/base/dydo/x/file.md", "/base/dydo/x/"));
    }

    [Fact]
    public void IsUnderScope_RejectsPartialPrefixMatch()
    {
        Assert.False(CheckDocValidator.IsUnderScope("/base/dydo/xy/file.md", "/base/dydo/x"));
    }
}
