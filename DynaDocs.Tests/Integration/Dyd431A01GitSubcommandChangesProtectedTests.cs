namespace DynaDocs.Tests.Integration;

/// <summary>
/// DYD-431 A-01 proof (red on d8ca1786): git rm and git checkout -- delete or overwrite a protected file unchecked.
/// The protected tier (dydo/files-off-limits.md) documents every shell write, delete, move, copy
/// and permission change of a protected file, and every force push or remote branch deletion, as blocked.
/// </summary>
[Collection("Integration")]
public class Dyd431A01GitSubcommandChangesProtectedTests : IntegrationTestBase
{
    private static string ShellJson(string toolName, string command) =>
        System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["session_id"] = TestSessionId,
            ["tool_name"] = toolName,
            ["tool_input"] = new Dictionary<string, string> { ["command"] = command },
        });

    [Theory]
    [InlineData("Bash", "git rm dydo/index.md")]
    [InlineData("Bash", "git checkout -- dydo/files-off-limits.md")]
    public async Task GitSubcommandChangesProtected_IsRefused(string toolName, string command)
    {
        await InitProjectAsync("none");

        var result = await GuardWithStdinAsync(ShellJson(toolName, command));

        Assert.False(result.IsPreToolUseAllowed, $"Guard allowed: {command}\nStderr: {result.Stderr}");
        result.AssertPreToolUseDenied();
    }
}
