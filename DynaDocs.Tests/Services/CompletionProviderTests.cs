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
    [InlineData("task")]
    [InlineData("review")]
    [InlineData("version")]
    [InlineData("help")]
    [InlineData("completions")]
    [InlineData("issue")]
    [InlineData("complete")]
    [InlineData("template")]
    [InlineData("roles")]
    [InlineData("validate")]
    [InlineData("watchdog")]
    public void TopLevelCommands_ContainsCommand(string command)
    {
        var completions = CompletionProvider.GetCompletions(1, ["dydo"]).ToList();
        Assert.Contains(command, completions);
    }

    [Theory]
    [InlineData("code-writer")]
    [InlineData("reviewer")]
    [InlineData("co-thinker")]
    [InlineData("chief-of-staff")]
    [InlineData("docs-writer")]
    [InlineData("test-writer")]
    [InlineData("orchestrator")]
    public void Roles_ContainsRole(string role)
    {
        var completions = CompletionProvider.GetOptionValueCompletions("--role")!.ToList();
        Assert.Contains(role, completions);
    }


    [Theory]
    [InlineData("task", new[] { "create", "done", "list", "ready-for-review" })]
    [InlineData("issue", new[] { "create", "list", "resolve" })]
    [InlineData("roles", new[] { "list", "create", "reset" })]
    [InlineData("template", new[] { "update" })]
    [InlineData("watchdog", new[] { "start", "stop", "run" })]
    public void Subcommands_ContainsExpectedEntries(string command, string[] expectedSubcommands)
    {
        var completions = CompletionProvider.GetSubcommandCompletions(command, 2, ["dydo", command]).ToList();
        foreach (var sub in expectedSubcommands)
            Assert.Contains(sub, completions);
    }

    [Fact]
    public void OptionValueHandlers_SubjectReturnsTaskNames()
    {
        var completions = CompletionProvider.GetOptionValueCompletions("--subject");
        Assert.NotNull(completions);
    }
}
