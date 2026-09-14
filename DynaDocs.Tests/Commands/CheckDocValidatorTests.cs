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

    [Fact]
    public void Validate_Scoped_AcceptsCrossScopeLink_ButCatchesMissingTarget()
    {
        var basePath = Path.Combine(Path.GetTempPath(), $"check-doc-validator-{Guid.NewGuid():N}");
        var guidesPath = Path.Combine(basePath, "guides");
        var sourcePath = Path.Combine(guidesPath, "source.md");

        try
        {
            Directory.CreateDirectory(guidesPath);
            Directory.CreateDirectory(Path.Combine(basePath, "reference"));
            File.WriteAllText(sourcePath, """
                ---
                area: guides
                type: guide
                ---

                # Source

                [Reference](../reference/target.md)
                [Missing](./missing.md)
                """);
            File.WriteAllText(Path.Combine(basePath, "reference", "target.md"), """
                ---
                area: reference
                type: reference
                ---

                # Target
                """);

            var result = CheckDocValidator.Validate(basePath, guidesPath);

            var violation = Assert.Single(result.Violations, v => v.RuleName == "BrokenLinks");
            Assert.Equal("guides/source.md", violation.FilePath);
            Assert.Equal("Broken link: ./missing.md", violation.Message);
        }
        finally
        {
            if (Directory.Exists(basePath)) Directory.Delete(basePath, recursive: true);
        }
    }
}
