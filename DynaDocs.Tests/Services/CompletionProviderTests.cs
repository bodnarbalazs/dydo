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
    [InlineData("template")]
    [InlineData("validate")]
    [InlineData("model")]
    public void TopLevelCommands_ContainsCommand(string command)
    {
        var completions = CompletionProvider.GetCompletions(1, ["dydo"]).ToList();
        Assert.Contains(command, completions);
    }



    [Theory]
    [InlineData("template", new[] { "update" })]
    [InlineData("model", new[] { "cap", "uncap", "status" })]
    public void Subcommands_ContainsExpectedEntries(string command, string[] expectedSubcommands)
    {
        var completions = CompletionProvider.GetSubcommandCompletions(command, 2, ["dydo", command]).ToList();
        foreach (var sub in expectedSubcommands)
            Assert.Contains(sub, completions);
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
