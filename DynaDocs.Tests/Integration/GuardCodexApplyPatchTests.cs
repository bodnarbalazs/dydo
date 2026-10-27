namespace DynaDocs.Tests.Integration;

using System.Text.Json;

/// <summary>
/// Codex's real apply_patch payload (DYD-396): the whole patch in tool_input.command, a turn_id
/// and no file_path. Every path-based write tier judges every patch target, Update File and
/// Delete File included, in the main and the worker lane.
/// </summary>
[Collection("Integration")]
public class GuardCodexApplyPatchTests : IntegrationTestBase
{
    private string CodexApplyPatch(string patch, string? agentId = null, string? cwd = null) =>
        JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["session_id"] = TestSessionId,
            ["turn_id"] = "turn-1",
            ["agent_id"] = agentId,
            ["cwd"] = cwd ?? TestDir,
            ["hook_event_name"] = "PreToolUse",
            ["tool_name"] = "apply_patch",
            ["tool_input"] = new { command = patch },
        });

    private static string Patch(string newline, params string[] body) =>
        string.Join(newline, ["*** Begin Patch", .. body, "*** End Patch"]) + newline;

    private static string Newline(string name) => name == "crlf" ? "\r\n" : "\n";

    private string IndexPath(string form) => form switch
    {
        "relative" => "dydo/index.md",
        "backslash" => "dydo\\index.md",
        // Path.Combine yields a backslashed drive path on Windows.
        _ => Path.Combine(TestDir, "dydo", "index.md"),
    };

    [Theory]
    [InlineData("lf", null)]
    [InlineData("crlf", null)]
    [InlineData("lf", "wkr-1")]
    [InlineData("crlf", "wkr-1")]
    public async Task UpdateFileIntoCodexMemories_Denied(string newline, string? agentId)
    {
        await InitProjectAsync("none");
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var codexHome = Environment.GetEnvironmentVariable("CODEX_HOME") ?? Path.Combine(home, ".codex");
        var target = Path.Combine(codexHome, "memories", "note.md");

        var result = await GuardWithStdinAsync(CodexApplyPatch(
            Patch(Newline(newline), $"*** Update File: {target}", "@@", "-old", "+new"), agentId));

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("DR 055");
    }

    [Theory]
    [InlineData("lf", "relative", null)]
    [InlineData("crlf", "relative", null)]
    [InlineData("lf", "backslash", null)]
    [InlineData("crlf", "absolute", null)]
    [InlineData("lf", "relative", "wkr-1")]
    [InlineData("crlf", "absolute", "wkr-1")]
    public async Task DeleteFileOfProtectedIndex_Denied(string newline, string form, string? agentId)
    {
        await InitProjectAsync("none");

        var result = await GuardWithStdinAsync(CodexApplyPatch(
            Patch(Newline(newline), $"*** Delete File: {IndexPath(form)}"), agentId));

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("protected");
    }

    [Theory]
    [InlineData("*** Add File: dydo.json", null)]
    [InlineData("*** Add File: dydo/files-off-limits.md", "wkr-1")]
    // Moving an ordinary file onto a protected path overwrites it.
    [InlineData("*** Update File: notes.md\n*** Move to: dydo/index.md", null)]
    public async Task AddOrMoveOntoProtectedPath_Denied(string headers, string? agentId)
    {
        await InitProjectAsync("none");

        var result = await GuardWithStdinAsync(CodexApplyPatch(Patch("\n", headers, "+x"), agentId));

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("protected");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("wkr-1")]
    public async Task UpdateFileOfOffLimitsPath_Denied(string? agentId)
    {
        await InitProjectAsync("none");

        var result = await GuardWithStdinAsync(CodexApplyPatch(
            Patch("\n", "*** Update File: .env", "@@", "-A=1", "+A=2"), agentId));

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("off-limits");
    }

    [Fact]
    public async Task RelativeTarget_ResolvesAgainstHookCwd()
    {
        await InitProjectAsync("none");

        // Codex applies a relative path from its cwd: from dydo/, index.md is dydo/index.md.
        var result = await GuardWithStdinAsync(CodexApplyPatch(
            Patch("\n", "*** Update File: index.md", "@@", "-a", "+b"), cwd: DydoDir));

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("protected");
    }

    [Theory]
    [InlineData("lf", null)]
    [InlineData("crlf", null)]
    [InlineData("crlf", "wkr-1")]
    public async Task UpdateFileOfOrdinaryFile_Allowed(string newline, string? agentId)
    {
        await InitProjectAsync("none");
        Directory.CreateDirectory(Path.Combine(TestDir, "src"));
        await File.WriteAllTextAsync(Path.Combine(TestDir, "src", "Foo.cs"), "old\n");

        var result = await GuardWithStdinAsync(CodexApplyPatch(
            Patch(Newline(newline), $"*** Update File: {Path.Combine(TestDir, "src", "Foo.cs")}", "@@", "-old", "+new",
                "*** Delete File: src/Gone.cs"), agentId));

        result.AssertPreToolUseAllowed();
    }
}
