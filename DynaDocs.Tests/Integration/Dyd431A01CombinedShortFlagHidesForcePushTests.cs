namespace DynaDocs.Tests.Integration;

/// <summary>
/// DYD-431 A-01 proof (red on d8ca1786): a force flag combined with another short flag (-fu, -uf) is no force push to OndrejGitPushRegex.
/// The protected tier (dydo/files-off-limits.md) documents every shell write, delete, move, copy
/// and permission change of a protected file, and every force push or remote branch deletion, as blocked.
/// </summary>
[Collection("Integration")]
public class Dyd431A01CombinedShortFlagHidesForcePushTests : IntegrationTestBase
{
    private static string ShellJson(string toolName, string command) =>
        System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["session_id"] = TestSessionId,
            ["tool_name"] = toolName,
            ["tool_input"] = new Dictionary<string, string> { ["command"] = command },
        });

    [Theory]
    [InlineData("Bash", "git push -fu origin feature")]
    [InlineData("Bash", "git push -uf origin feature")]
    public async Task CombinedShortFlagHidesForcePush_IsRefused(string toolName, string command)
    {
        await InitProjectAsync("none");

        var result = await GuardWithStdinAsync(ShellJson(toolName, command));

        Assert.False(result.IsPreToolUseAllowed, $"Guard allowed: {command}\nStderr: {result.Stderr}");
        result.AssertPreToolUseDenied();
    }
}
