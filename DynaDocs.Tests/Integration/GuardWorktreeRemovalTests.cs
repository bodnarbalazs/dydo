namespace DynaDocs.Tests.Integration;

/// <summary>
/// The guard lets <c>dydo clean</c> through for the main session and for sub-agents (a Ticket Captain
/// cleans its own worktree), and refuses raw worktree removal with a pointer to <c>dydo clean</c>.
/// </summary>
[Collection("Integration")]
public class GuardWorktreeRemovalTests : IntegrationTestBase
{
    private async Task<CommandResult> ShellAsync(string tool, string command, bool subAgent)
    {
        await InitProjectAsync();
        var agent = subAgent ? "\"agent_id\":\"wkr-1\",\"agent_type\":\"general-purpose\"," : "";
        var json = $"{{\"session_id\":\"{TestSessionId}\",{agent}\"hook_event_name\":\"PreToolUse\","
            + $"\"tool_name\":\"{tool}\",\"tool_input\":{{\"command\":{System.Text.Json.JsonSerializer.Serialize(command)}}}}}";
        return await GuardWithStdinAsync(json);
    }

    [Theory]
    [InlineData("Bash", "dydo clean --dry-run", false)]
    [InlineData("Bash", "dydo clean --all", true)]
    [InlineData("PowerShell", "dydo clean .claude/worktrees/dyd-1", true)]
    [InlineData("Bash", "rm -rf .claude/worktrees/dyd-1/bin", true)]
    public async Task DydoCleanAndDeletesInsideATree_AreAllowed(string tool, string command, bool subAgent)
    {
        var result = await ShellAsync(tool, command, subAgent);

        result.AssertPreToolUseAllowed();
    }

    [Theory]
    [InlineData("Bash", "git worktree remove --force .claude/worktrees/dyd-1")]
    [InlineData("Bash", "rm -rf .claude/worktrees/dyd-1")]
    [InlineData("Bash", "git worktree remove -ff .claude/worktrees/dyd-1")]
    [InlineData("PowerShell", "Remove-Item -Recurse -Force .claude\\worktrees\\dyd-1")]
    public async Task RawWorktreeRemoval_IsDeniedWithTheDydoCleanHint(string tool, string command)
    {
        var result = await ShellAsync(tool, command, subAgent: false);

        var reason = result.AssertPreToolUseDenied();
        Assert.Contains("dydo clean --dry-run <worktree>, then dydo clean <worktree>", reason);
    }
}
