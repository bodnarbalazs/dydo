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
    private string CodexApplyPatch(string patch, string? agentId = null, string? cwd = null, bool omitCwd = false)
    {
        var payload = new Dictionary<string, object?>
        {
            ["session_id"] = TestSessionId,
            ["turn_id"] = "turn-1",
            ["agent_id"] = agentId,
            ["cwd"] = cwd ?? TestDir,
            ["hook_event_name"] = "PreToolUse",
            ["tool_name"] = "apply_patch",
            ["tool_input"] = new { command = patch },
        };
        if (omitCwd)
            payload.Remove("cwd");
        return JsonSerializer.Serialize(payload);
    }

    private static string CodexHome()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Environment.GetEnvironmentVariable("CODEX_HOME") ?? Path.Combine(home, ".codex");
    }

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
        var target = Path.Combine(CodexHome(), "memories", "note.md");

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

    // A relative target reaches host memory once resolved against the hook cwd: from a sibling of
    // CODEX_HOME through '..', or from CODEX_HOME itself.
    [Theory]
    [InlineData("sibling", "lf", null)]
    [InlineData("sibling", "crlf", "wkr-1")]
    [InlineData("codex-home", "crlf", null)]
    [InlineData("codex-home", "lf", "wkr-1")]
    public async Task RelativeUpdateFileIntoCodexMemories_Denied(string from, string newline, string? agentId)
    {
        await InitProjectAsync("none");
        var codexHome = Path.TrimEndingDirectorySeparator(CodexHome());
        var (cwd, target) = from == "sibling"
            ? (Path.Combine(Path.GetDirectoryName(codexHome)!, "project"), $"../{Path.GetFileName(codexHome)}/memories/x.md")
            : (codexHome, "memories/x.md");

        var result = await GuardWithStdinAsync(CodexApplyPatch(
            Patch(Newline(newline), $"*** Update File: {target}", "@@", "-old", "+new"), agentId, cwd));

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("DR 055");
    }

    // No payload cwd: a relative target resolves against the process working directory, '..'
    // collapsed, as Codex would apply it there.
    [Theory]
    [InlineData("src", "*** Delete File: ../dydo/index.md", "lf", null)]
    [InlineData("src", "*** Delete File: ../dydo/index.md", "crlf", "wkr-1")]
    [InlineData("dydo", "*** Update File: index.md", "crlf", null)]
    [InlineData("dydo", "*** Update File: index.md", "lf", "wkr-1")]
    public async Task OmittedCwd_RelativeTargetResolvesAgainstProcessDirectory_Denied(
        string processDir, string header, string newline, string? agentId)
    {
        await InitProjectAsync("none");
        var dir = Path.Combine(TestDir, processDir);
        Directory.CreateDirectory(dir);
        Environment.CurrentDirectory = dir;

        var result = await GuardWithStdinAsync(CodexApplyPatch(
            Patch(Newline(newline), header, "@@", "-a", "+b"), agentId, omitCwd: true));

        result.AssertPreToolUseDenied();
        result.AssertStderrContains("protected");
    }

    // An ordinary doc that quotes patch syntax: the quoted header is hunk context, not a target.
    [Theory]
    [InlineData(" *** Update File: dydo/index.md", "lf", null)]
    [InlineData(" *** Delete File: dydo/index.md", "crlf", null)]
    [InlineData("+*** Update File: dydo/index.md", "crlf", "wkr-1")]
    [InlineData("-*** Add File: dydo.json", "lf", "wkr-1")]
    public async Task UpdateFileQuotingPatchHeader_Allowed(string quoted, string newline, string? agentId)
    {
        await InitProjectAsync("none");
        await File.WriteAllTextAsync(Path.Combine(TestDir, "notes.md"), "old");

        var result = await GuardWithStdinAsync(CodexApplyPatch(
            Patch(Newline(newline), "*** Update File: notes.md", "@@", quoted, "-old", "+new"), agentId));

        result.AssertPreToolUseAllowed();
    }
}
