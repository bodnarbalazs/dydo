namespace DynaDocs.Tests.Utils;

using DynaDocs.Utils;

public class GlobMatcherTests
{
    #region ** (double star) matches any path

    [Theory]
    [InlineData("src/file.cs", "src/**", true)]
    [InlineData("src/nested/deep/file.cs", "src/**", true)]
    [InlineData("other/file.cs", "src/**", false)]
    public void IsMatch_DoubleStar_MatchesAnyPath(string path, string pattern, bool expected)
    {
        Assert.Equal(expected, GlobMatcher.IsMatch(path, pattern));
    }

    #endregion

    #region * (single star) matches within segment

    [Theory]
    [InlineData("Commands/Foo.cs", "Commands/*", true)]
    [InlineData("Commands/Sub/Foo.cs", "Commands/*", false)]
    [InlineData("file.cs", "*.cs", true)]
    [InlineData("src/file.cs", "*.cs", false)]
    public void IsMatch_SingleStar_MatchesWithinSegment(string path, string pattern, bool expected)
    {
        Assert.Equal(expected, GlobMatcher.IsMatch(path, pattern));
    }

    #endregion

    #region **/ prefix matches root or any depth

    [Theory]
    [InlineData("secrets.json", "**/secrets.json", true)]
    [InlineData("config/secrets.json", "**/secrets.json", true)]
    [InlineData("deep/nested/secrets.json", "**/secrets.json", true)]
    [InlineData("other.json", "**/secrets.json", false)]
    public void IsMatch_DoubleStarSlashPrefix_MatchesRootOrAnyDepth(string path, string pattern, bool expected)
    {
        Assert.Equal(expected, GlobMatcher.IsMatch(path, pattern));
    }

    #endregion

    #region ? matches single character

    [Theory]
    [InlineData("file1.cs", "file?.cs", true)]
    [InlineData("fileA.cs", "file?.cs", true)]
    [InlineData("file.cs", "file?.cs", false)]
    [InlineData("fileAB.cs", "file?.cs", false)]
    public void IsMatch_QuestionMark_MatchesSingleChar(string path, string pattern, bool expected)
    {
        Assert.Equal(expected, GlobMatcher.IsMatch(path, pattern));
    }

    #endregion

    #region Backslash normalization

    [Theory]
    [InlineData("config\\secrets.json", "**/secrets.json", true)]
    [InlineData("src\\nested\\file.cs", "src/**", true)]
    public void IsMatch_BackslashPaths_NormalizedToForwardSlash(string path, string pattern, bool expected)
    {
        Assert.Equal(expected, GlobMatcher.IsMatch(path, pattern));
    }

    #endregion

    #region Case insensitivity

    [Theory]
    [InlineData("SRC/File.cs", "src/**", true)]
    [InlineData("Commands/FOO.cs", "commands/*", true)]
    public void IsMatch_CaseInsensitive(string path, string pattern, bool expected)
    {
        Assert.Equal(expected, GlobMatcher.IsMatch(path, pattern));
    }

    #endregion

    #region Exact matches

    [Theory]
    [InlineData(".env", ".env", true)]
    [InlineData(".env.local", ".env", false)]
    [InlineData("src/.env", ".env", false)]
    public void IsMatch_ExactPattern_MatchesExactly(string path, string pattern, bool expected)
    {
        Assert.Equal(expected, GlobMatcher.IsMatch(path, pattern));
    }

    #endregion

    #region Combined patterns

    [Theory]
    [InlineData("src/config/secrets.json", "**/config/*.json", true)]
    [InlineData("config/secrets.json", "**/config/*.json", true)]
    [InlineData("src/secrets.json", "**/config/*.json", false)]
    public void IsMatch_CombinedPatterns(string path, string pattern, bool expected)
    {
        Assert.Equal(expected, GlobMatcher.IsMatch(path, pattern));
    }

    #endregion
}
