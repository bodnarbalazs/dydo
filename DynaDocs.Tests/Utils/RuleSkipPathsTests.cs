namespace DynaDocs.Tests.Utils;

using DynaDocs.Utils;
using Xunit;

public class RuleSkipPathsTests
{
    [Theory]
    [InlineData("_system/template-additions/extra-y.md", true)]
    [InlineData("_system/template-additions/_README.md", true)]
    [InlineData("understand/about.md", false)]
    [InlineData("_system/audit/x.json", false)]
    [InlineData("_system/audit/2026/foo.md", false)]
    [InlineData("_system/.local/worktrees/foo/bar.md", false)]
    [InlineData("project/decisions/foo.md", false)]
    [InlineData("", false)]
    public void IsTemplateAddition_ClassifiesPaths(string path, bool expected)
    {
        Assert.Equal(expected, RuleSkipPaths.IsTemplateAddition(path));
    }

    [Fact]
    public void IsTemplateAddition_IsCaseInsensitive()
    {
        Assert.True(RuleSkipPaths.IsTemplateAddition("_System/Template-Additions/foo.md"));
    }
}
