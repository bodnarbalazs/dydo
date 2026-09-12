namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

public class CompletionProviderTests
{
    [Theory]
    [InlineData("check")]
    [InlineData("fix")]
    [InlineData("index")]
    [InlineData("init")]
    [InlineData("graph")]
    [InlineData("guard")]
    [InlineData("version")]
    [InlineData("help")]
    [InlineData("completions")]
    [InlineData("complete")]
    [InlineData("validate")]
    public void TopLevelCommands_ContainsCommand(string command)
    {
        var completions = CompletionProvider.GetCompletions(1, ["dydo"]).ToList();
        Assert.Contains(command, completions);
    }

    [Fact]
    public void TopLevelCommands_ExcludesRetiredWorkCommands()
    {
        var completions = CompletionProvider.GetCompletions(1, ["dydo"]).ToList();

        Assert.DoesNotContain("task", completions);
        Assert.DoesNotContain("issue", completions);
        Assert.DoesNotContain("review", completions);
    }
}
