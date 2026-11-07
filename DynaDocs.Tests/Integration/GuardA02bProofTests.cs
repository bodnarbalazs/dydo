namespace DynaDocs.Tests.Integration;

/// <summary>DYD-431 Break A-02 follow-up proofs: guard reader paths that fail open on valid syntax.</summary>
[Collection("Integration")]
public class GuardA02bProofTests : IntegrationTestBase
{
    private static string BashPayload(string command) =>
        System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["session_id"] = TestSessionId,
            ["hook_event_name"] = "PreToolUse",
            ["tool_name"] = "Bash",
            ["tool_input"] = new Dictionary<string, string> { ["command"] = command },
        });

    // F-19 valid-syntax form. A here-string whose word is a CLOSED backtick substitution ending the
    // command is valid bash: bash runs the substitution, so `<<<\`rm dydo.json\`` deletes dydo.json.
    // On 3.6.0 ShellReader.EndWord indexes _text[_wordStart] with _wordStart == _text.Length
    // (Services/ShellReader.cs:857), throws IndexOutOfRange, and the guard's catch-all allows the call.
    // 42882830 denied every one of these.
    [Theory]
    [InlineData("<<<`rm dydo.json`")]
    [InlineData("cat <<<`rm dydo.json`")]
    [InlineData("<<<`rm dydo/index.md`")]
    [InlineData("<<<`echo x > dydo.json`")]
    public async Task HereStringClosedBacktick_AtEndOfCommand_IsDenied(string command)
    {
        await InitProjectAsync("none");
        var result = await GuardWithStdinAsync(BashPayload(command));
        Assert.DoesNotContain("internal error", result.Stderr);
        result.AssertPreToolUseDenied();
    }

    // F-19, push form. 3.6.0 refuses `git push origin main` (DYD-425); wrapped as a here-string's
    // closing backtick substitution, bash still runs the push and the reader throw lets it through.
    [Fact]
    public async Task HereStringClosedBacktick_PushToMain_IsDenied()
    {
        await InitProjectAsync("none");
        File.AppendAllText(Path.Combine(TestDir, ".gitignore"), "\n/origin.git/\n");
        PushedRepository.Create(TestDir, Path.Combine(TestDir, "origin.git"));
        var result = await GuardWithStdinAsync(BashPayload("cat <<<`git push origin main`"));
        Assert.DoesNotContain("internal error", result.Stderr);
        result.AssertPreToolUseDenied();
    }

    // Pre-existing. In bash an escaped backtick inside a backtick substitution opens a nested one,
    // and the nested command runs: `echo \`echo \\\`rm dydo.json\\\`\`` deletes dydo.json. The
    // reader keeps the inner text as one capture and the guard allows it, on 42882830 and 3.6.0.
    [Fact]
    public async Task NestedEscapedBacktick_ProtectedDelete_IsDenied()
    {
        await InitProjectAsync("none");
        var result = await GuardWithStdinAsync(BashPayload("echo `echo \\`rm dydo.json\\``"));
        Assert.False(result.IsPreToolUseAllowed, "guard allowed a nested backtick that deletes dydo.json");
        result.AssertPreToolUseDenied();
    }
}
