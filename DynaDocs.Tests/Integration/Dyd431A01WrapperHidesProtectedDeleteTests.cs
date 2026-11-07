namespace DynaDocs.Tests.Integration;

/// <summary>
/// DYD-431 A-01 proof (red on d8ca1786): a delete run through a wrapper (env, command, timeout, nohup, xargs) or a path-qualified program is never read as a delete.
/// The protected tier (dydo/files-off-limits.md) documents every shell write, delete, move, copy
/// and permission change of a protected file, and every force push or remote branch deletion, as blocked.
/// </summary>
[Collection("Integration")]
public class Dyd431A01WrapperHidesProtectedDeleteTests : IntegrationTestBase
{
    private static string ShellJson(string toolName, string command) =>
        System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["session_id"] = TestSessionId,
            ["tool_name"] = toolName,
            ["tool_input"] = new Dictionary<string, string> { ["command"] = command },
        });

    [Theory]
    [InlineData("Bash", "env rm dydo/index.md")]
    [InlineData("Bash", "command rm dydo/index.md")]
    [InlineData("Bash", "timeout 10 rm dydo/index.md")]
    [InlineData("Bash", "nohup rm dydo/index.md")]
    [InlineData("Bash", "echo x | xargs rm dydo/index.md")]
    [InlineData("Bash", "/bin/rm dydo/index.md")]
    public async Task WrapperHidesProtectedDelete_IsRefused(string toolName, string command)
    {
        await InitProjectAsync("none");

        var result = await GuardWithStdinAsync(ShellJson(toolName, command));

        Assert.False(result.IsPreToolUseAllowed, $"Guard allowed: {command}\nStderr: {result.Stderr}");
        result.AssertPreToolUseDenied();
    }
}
