namespace DynaDocs.Tests.Integration;

/// <summary>
/// DYD-431 A-01 proof (red on d8ca1786): a git global option before push (git -C, --no-pager) hides a force push or remote branch deletion from OndrejGitPushRegex.
/// The protected tier (dydo/files-off-limits.md) documents every shell write, delete, move, copy
/// and permission change of a protected file, and every force push or remote branch deletion, as blocked.
/// </summary>
[Collection("Integration")]
public class Dyd431A01GitGlobalOptionHidesForcePushTests : IntegrationTestBase
{
    private static string ShellJson(string toolName, string command) =>
        System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["session_id"] = TestSessionId,
            ["tool_name"] = toolName,
            ["tool_input"] = new Dictionary<string, string> { ["command"] = command },
        });

    [Theory]
    [InlineData("Bash", "git -C . push -f origin feature")]
    [InlineData("Bash", "git -C /tmp/repo push --force origin feature")]
    [InlineData("Bash", "git -C . push origin --delete feature")]
    [InlineData("Bash", "git --no-pager push --force origin feature")]
    public async Task GitGlobalOptionHidesForcePush_IsRefused(string toolName, string command)
    {
        await InitProjectAsync("none");

        var result = await GuardWithStdinAsync(ShellJson(toolName, command));

        Assert.False(result.IsPreToolUseAllowed, $"Guard allowed: {command}\nStderr: {result.Stderr}");
        result.AssertPreToolUseDenied();
    }
}
