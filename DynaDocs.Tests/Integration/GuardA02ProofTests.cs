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

    // Codex runs the command in tool_input.workdir; the guard parses it (ToolCwd) but resolves the
    // command's file operations against its own process directory, so a relative delete there passes.
    [Fact]
    public async Task CodexWorkdir_RelativeDeleteOfProtectedFile_IsDenied()
    {
        await InitProjectAsync("none");
        var result = await GuardWithStdinAsync(Payload(
            new Dictionary<string, string> { ["command"] = "rm index.md", ["workdir"] = Path.Combine(TestDir, "dydo") },
            turnId: "turn-1"), "--host", "codex");
        Assert.False(result.IsPreToolUseAllowed, "guard allowed rm of dydo/index.md run in workdir dydo");
        result.AssertPreToolUseDenied();
    }
}
