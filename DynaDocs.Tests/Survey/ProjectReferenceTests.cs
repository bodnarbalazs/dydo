namespace DynaDocs.Tests.Survey;

using DynaDocs.Services.Survey;

public class ProjectReferenceTests
{
    [Theory]
    [InlineData("4e6aa9da-f325-4b53-a318-deba4b1b4d26", "4e6aa9da-f325-4b53-a318-deba4b1b4d26")]
    [InlineData("25acbbb9a90b", "25acbbb9a90b")]
    [InlineData("https://linear.app/bodnar-balazs/project/dydo-360-current-release-25acbbb9a90b", "25acbbb9a90b")]
    [InlineData("https://linear.app/bodnar-balazs/project/dydo-360-current-release-25acbbb9a90b/overview", "25acbbb9a90b")]
    [InlineData("https://linear.app/bodnar-balazs/project/25acbbb9a90b/issues", "25acbbb9a90b")]
    public void AUuidSlugIdOrProjectUrl_NamesTheProject(string argument, string id)
    {
        Assert.Equal(id, ProjectReference.Parse(argument));
    }

    [Theory]
    [InlineData("")]
    [InlineData("P-DYD-46")]
    [InlineData("dydo 3.6.0")]
    [InlineData("25ACBBB9A90B")]
    [InlineData("25acbbb9a90")]
    [InlineData("{4e6aa9da-f325-4b53-a318-deba4b1b4d26}")]
    [InlineData("https://example.com/x/project/dydo-25acbbb9a90b")]
    [InlineData("https://linear.app/x/issue/DYD-530/dydo-survey")]
    [InlineData("https://linear.app/x/project")]
    [InlineData("https://linear.app/x/project/dydo-current-release")]
    public void AnythingElse_IsNoProject(string argument)
    {
        Assert.Null(ProjectReference.Parse(argument));
    }
}
