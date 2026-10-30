namespace DynaDocs.Tests.Integration;

/// <summary>
/// <c>dydo merge</c> in the guard's worker lane (DYD-424): doctrine gives the merge to the Merge
/// Sub-ticket's code-writer, a sub-agent, so the lane allows it in every shell form.
/// </summary>
[Collection("Integration")]
public class GuardWorkerLaneMergeTests : IntegrationTestBase
{
    [Theory]
    [InlineData("Bash", "dydo merge 12 --match-head-commit 0123456789abcdef0123456789abcdef01234567")]
    [InlineData("Bash", "dydo merge 12 --match-head-commit 0123456789abcdef0123456789abcdef01234567 --dry-run")]
    [InlineData("PowerShell", "dydo merge 12 --match-head-commit 0123456789abcdef0123456789abcdef01234567")]
    [InlineData("PowerShell", "dydo check; ./dydo.exe merge 12 --match-head-commit 0123456789abcdef0123456789abcdef01234567 --dry-run")]
    public async Task Worker_DydoMerge_Allowed(string tool, string command)
    {
        await InitProjectAsync();
        var input = $"{{\"command\":{System.Text.Json.JsonSerializer.Serialize(command)}}}";

        var result = await GuardWithStdinAsync(
            $"{{\"session_id\":\"{TestSessionId}\",\"agent_id\":\"wkr-merge-1\",\"agent_type\":\"code-writer\","
            + $"\"tool_name\":\"{tool}\",\"tool_input\":{input}}}");

        result.AssertPreToolUseAllowed();
    }
}
