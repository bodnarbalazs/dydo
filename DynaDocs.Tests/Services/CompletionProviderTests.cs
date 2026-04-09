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
    [InlineData("agent")]
    [InlineData("guard")]
    [InlineData("dispatch")]
    [InlineData("inbox")]
    [InlineData("task")]
    [InlineData("review")]
    [InlineData("workspace")]
    [InlineData("whoami")]
    [InlineData("audit")]
    [InlineData("version")]
    [InlineData("help")]
    [InlineData("completions")]
    [InlineData("message")]
    [InlineData("msg")]
    [InlineData("wait")]
    [InlineData("issue")]
    [InlineData("inquisition")]
    [InlineData("complete")]
    [InlineData("template")]
    [InlineData("roles")]
    [InlineData("validate")]
    [InlineData("watchdog")]
    [InlineData("worktree")]
    [InlineData("queue")]
    public void TopLevelCommands_ContainsCommand(string command)
    {
        var completions = CompletionProvider.GetCompletions(1, ["dydo"]).ToList();
        Assert.Contains(command, completions);
    }

    [Theory]
    [InlineData("code-writer")]
    [InlineData("reviewer")]
    [InlineData("co-thinker")]
    [InlineData("docs-writer")]
    [InlineData("planner")]
    [InlineData("test-writer")]
    [InlineData("orchestrator")]
    [InlineData("inquisitor")]
    [InlineData("judge")]
    public void Roles_ContainsRole(string role)
    {
        var completions = CompletionProvider.GetOptionValueCompletions("--role")!.ToList();
        Assert.Contains(role, completions);
    }

    [Theory]
    [InlineData("agent", new[] { "claim", "release", "status", "list", "role", "new", "rename", "remove", "reassign", "clean", "tree" })]
    [InlineData("task", new[] { "approve", "create", "list", "ready-for-review", "reject", "compact" })]
    [InlineData("issue", new[] { "create", "list", "resolve" })]
    [InlineData("inquisition", new[] { "coverage" })]
    [InlineData("roles", new[] { "list", "create", "reset" })]
    [InlineData("template", new[] { "update" })]
    [InlineData("worktree", new[] { "cleanup", "merge", "init-settings", "prune" })]
    [InlineData("queue", new[] { "create", "show", "cancel", "clear" })]
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
