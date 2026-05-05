namespace DynaDocs.Tests.Utils;

using DynaDocs.Utils;
using Xunit;

public class RuleSkipPathsTests
{
    [Theory]
    [InlineData("_system/templates/foo.md", true)]
    [InlineData("_system/templates/mode-code-writer.template.md", true)]
    [InlineData("_system/template-additions/extra-y.md", true)]
    [InlineData("_system/template-additions/_README.md", true)]
    [InlineData("understand/about.md", false)]
    [InlineData("_system/audit/x.json", false)]
    [InlineData("_system/audit/2026/foo.md", false)]
    [InlineData("_system/.local/worktrees/foo/bar.md", false)]
    [InlineData("project/tasks/foo.md", false)]
    [InlineData("", false)]
    public void IsTemplateOrAddition_ClassifiesPaths(string path, bool expected)
    {
        Assert.Equal(expected, RuleSkipPaths.IsTemplateOrAddition(path));
    }

    [Fact]
    public void IsTemplateOrAddition_IsCaseInsensitive()
    {
        Assert.True(RuleSkipPaths.IsTemplateOrAddition("_SYSTEM/TEMPLATES/foo.md"));
        Assert.True(RuleSkipPaths.IsTemplateOrAddition("_System/Template-Additions/foo.md"));
    }
}
