namespace DynaDocs.Tests.Integration;

/// <summary>DYD-431 Break A-02 proofs: guard hook entry and reader paths that fail open.</summary>
[Collection("Integration")]
public class GuardA02ProofTests : IntegrationTestBase
{
    private static string Payload(object toolInput, string toolName = "Bash", string? turnId = null) =>
        System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["session_id"] = TestSessionId,
            ["hook_event_name"] = "PreToolUse",
            ["tool_name"] = toolName,
            ["turn_id"] = turnId,
            ["tool_input"] = toolInput,
        });

    // A here-string fed a backtick substitution throws inside ShellReader on 3.6.0; the guard's
    // catch-all then allows the call. 42882830 refused this line.
    [Fact]
    public async Task HereStringBacktick_ReaderException_DoesNotAllowProtectedDelete()
    {
        await InitProjectAsync("none");
        var result = await GuardWithStdinAsync(Payload(new Dictionary<string, string> { ["command"] = "<<<`\nrm dydo.json" }));
        Assert.DoesNotContain("internal error", result.Stderr);
        result.AssertPreToolUseDenied();
    }

    // DYD-881 MUST-ALLOW twin: a closed backtick here-word is valid bash, read without an error.
    [Fact]
    public async Task HereStringClosedBacktick_IsAllowed()
    {
        await InitProjectAsync("none");
        var result = await GuardWithStdinAsync(Payload(new Dictionary<string, string> { ["command"] = "cat <<< `echo hi`" }));
        Assert.DoesNotContain("internal error", result.Stderr);
        result.AssertPreToolUseAllowed();
    }
}
