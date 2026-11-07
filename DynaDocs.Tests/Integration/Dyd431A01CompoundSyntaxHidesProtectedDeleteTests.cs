namespace DynaDocs.Tests.Integration;

/// <summary>
/// DYD-431 A-01 proof (red on d8ca1786): a delete inside if/then, for/do, a subshell, a brace group, after a lone & or a PowerShell call operator or block is never read as a command.
/// The protected tier (dydo/files-off-limits.md) documents every shell write, delete, move, copy
/// and permission change of a protected file, and every force push or remote branch deletion, as blocked.
/// </summary>
[Collection("Integration")]
public class Dyd431A01CompoundSyntaxHidesProtectedDeleteTests : IntegrationTestBase
{
    private static string ShellJson(string toolName, string command) =>
        System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["session_id"] = TestSessionId,
            ["tool_name"] = toolName,
            ["tool_input"] = new Dictionary<string, string> { ["command"] = command },
        });

    [Theory]
    [InlineData("Bash", "if true; then rm dydo/index.md; fi")]
    [InlineData("Bash", "for f in a; do rm dydo/index.md; done")]
    [InlineData("Bash", "(rm dydo/index.md)")]
    [InlineData("Bash", "{ rm dydo/index.md; }")]
    [InlineData("Bash", "true & rm dydo/index.md")]
    [InlineData("PowerShell", "if ($true) { Remove-Item dydo/index.md }")]
    [InlineData("PowerShell", "& rm dydo/index.md")]
    public async Task CompoundSyntaxHidesProtectedDelete_IsRefused(string toolName, string command)
    {
        await InitProjectAsync("none");

        var result = await GuardWithStdinAsync(ShellJson(toolName, command));

        Assert.False(result.IsPreToolUseAllowed, $"Guard allowed: {command}\nStderr: {result.Stderr}");
        result.AssertPreToolUseDenied();
    }
}
