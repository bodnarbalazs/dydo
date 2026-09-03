namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;

/// <summary>
/// Integration tests for the guard command. Post-DR-041 the guard is identity-free: only the
/// universal layers remain (off-limits, dangerous-bash, nudges, git-safety, worktree-allow,
/// search-tool gating, plan-mode block). There is no claim/role/must-read setup.
/// </summary>
[Collection("Integration")]
public class GuardIntegrationTests : IntegrationTestBase
{
    #region Off-Limits

    [Fact]
    public async Task Guard_OffLimitsPath_Blocks()
    {
        await InitProjectAsync("none");

        // .env is off-limits by default
        var result = await GuardAsync("edit", ".env");

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
        result.AssertStderrContains("off-limits");
    }

    [Fact]
    public async Task Guard_DydoSystemFile_Blocks()
    {
        await InitProjectAsync("none");

        // dydo/index.md is a system file — protected, so the edit is blocked
        var result = await GuardAsync("edit", "dydo/index.md");

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
    }

    [Fact]
    public async Task Guard_DydoSystemFile_ReadAllows()
    {
        await InitProjectAsync("none");

        // …and read, because every entry prompt orders agents to read it (DR 045 §10).
        var result = await GuardAsync("read", "dydo/index.md");

        result.AssertSuccess();
    }

    [Fact]
    public async Task Guard_StdinHook_DirectReadOffLimits_Blocks()
    {
        await InitProjectAsync("none");

        // A Read tool-call against an off-limits secret (**/secrets.json) must be blocked —
        // off-limits binds on every direct file op, reads included, not just writes.
        var json = "{\"session_id\":\"" + TestSessionId
            + "\",\"tool_name\":\"Read\",\"tool_input\":{\"file_path\":\"config/secrets.json\"}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertExitCode(2);
        result.AssertStderrContains("off-limits");
    }


    [Fact]
    public async Task Guard_AllowedPath_Passes()
    {
        await InitProjectAsync("none");

        // src/file.cs is not off-limits — allowed
        var result = await GuardAsync("edit", "src/file.cs");

        result.AssertSuccess();
    }

    #endregion

    #region Protected Tier — readable by every tool, writable by none (DR 045 §10)

    [Theory]
    [InlineData("dydo/index.md")]
    [InlineData("dydo/files-off-limits.md")]
    [InlineData("dydo.json")]
    public async Task Guard_ProtectedPath_ReadAllowed(string path)
    {
        await InitProjectAsync("none");

        var argMode = await GuardAsync("read", path);
        var hookMode = await GuardWithStdinAsync(
            $"{{\"session_id\":\"{TestSessionId}\",\"tool_name\":\"Read\",\"tool_input\":{{\"file_path\":\"{path}\"}}}}");

        argMode.AssertSuccess();
        hookMode.AssertSuccess();
    }

    [Theory]
    [InlineData("Edit", "dydo/index.md")]
    [InlineData("Write", "dydo/files-off-limits.md")]
    [InlineData("NotebookEdit", "dydo.json")]
    // Codex's apply_patch maps to no action, so the tier must recognize it by tool name or it
    // would bind on Claude's lane only — and these files were writable on the Codex lane.
    [InlineData("apply_patch", "dydo/index.md")]
    public async Task Guard_ProtectedPath_DirectWriteToolBlocked(string toolName, string path)
    {
        await InitProjectAsync("none");

        var result = await GuardWithStdinAsync(
            $"{{\"session_id\":\"{TestSessionId}\",\"tool_name\":\"{toolName}\",\"tool_input\":{{\"file_path\":\"{path}\"}}}}");

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED:");
        result.AssertStderrContains("protected");
        result.AssertStderrContains(path);
    }

    [Theory]
    [InlineData("sed -i 's/a/b/' dydo/index.md")]
    [InlineData("echo broken > dydo/files-off-limits.md")]
    [InlineData("rm dydo.json")]
    // A move takes the protected file away or lands on top of it; both directions mutate.
    [InlineData("mv dydo/index.md gone.md")]
    [InlineData("mv other.md dydo/index.md")]
    // A copy can overwrite a protected file. The analyzer cannot tell a copy's source from
    // its destination, so copying *out of* a protected path is blocked too, by design —
    // the content stays readable through Read, cat and head.
    [InlineData("cp evil.json dydo.json")]
    [InlineData("cp permissive.md dydo/files-off-limits.md")]
    [InlineData("cp dydo/index.md backup.md")]
    // A permission change is not a content write, but it was blocked before the tier existed
    // and it is the classic self-escalation lever against the guard's own config.
    [InlineData("chmod 777 dydo.json")]
    public async Task Guard_ProtectedPath_BashMutatingOperationBlocked(string command)
    {
        await InitProjectAsync("none");

        var result = await GuardWithStdinAsync(
            $"{{\"session_id\":\"{TestSessionId}\",\"tool_name\":\"Bash\",\"tool_input\":{{\"command\":\"{command}\"}}}}");

        // "protected", not "off-limits": the tier, not the old block list, is what stops this.
        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED:");
        result.AssertStderrContains("protected");
    }

    [Theory]
    [InlineData("cat dydo/index.md")]
    [InlineData("head -n 5 dydo.json")]
    public async Task Guard_ProtectedPath_BashReadAllowed(string command)
    {
        await InitProjectAsync("none");

        var result = await GuardWithStdinAsync(
            $"{{\"session_id\":\"{TestSessionId}\",\"tool_name\":\"Bash\",\"tool_input\":{{\"command\":\"{command}\"}}}}");

        result.AssertSuccess();
    }

    [Fact]
    public async Task Guard_UnprotectedPath_WriteToolsStillPass()
    {
        await InitProjectAsync("none");

        // The tier is three files wide: ordinary source stays writable on both lanes.
        var edit = await GuardWithStdinAsync(
            $"{{\"session_id\":\"{TestSessionId}\",\"tool_name\":\"Edit\",\"tool_input\":{{\"file_path\":\"src/Foo.cs\"}}}}");
        var patch = await GuardWithStdinAsync(
            $"{{\"session_id\":\"{TestSessionId}\",\"tool_name\":\"apply_patch\",\"tool_input\":{{\"file_path\":\"src/Foo.cs\"}}}}");

        edit.AssertSuccess();
        patch.AssertSuccess();
    }

    #endregion

    #region Reads allowed unless off-limits

    [Fact]
    public async Task Guard_ReadRootFile_Allows()
    {
        await InitProjectAsync("none");

        var result = await GuardAsync("read", "CLAUDE.md");

        result.AssertSuccess();
    }


    [Fact]
    public async Task Guard_ReadSourceFile_Allows()
    {
        await InitProjectAsync("none");

        var result = await GuardAsync("read", "src/code.cs");

        result.AssertSuccess();
    }


    [Fact]
    public async Task Guard_ReadNonAgentWorkflow_Allows()
    {
        await InitProjectAsync("none");

        // A file named workflow.md outside the agents folder should NOT be blocked
        var result = await GuardAsync("read", "docs/workflow.md");

        result.AssertSuccess();
    }

    #endregion

    #region Stdin Hook Mode

    [Fact]
    public async Task Guard_StdinHook_ReadBootstrapFile_Allows()
    {
        await InitProjectAsync("none");

        var json = "{\"session_id\":\"" + TestSessionId + "\",\"tool_name\":\"Read\",\"tool_input\":{\"file_path\":\"CLAUDE.md\"}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertSuccess();
    }

    [Fact]
    public async Task Guard_StdinHook_Write_Allows()
    {
        await InitProjectAsync("none");

        var json = "{\"session_id\":\"" + TestSessionId + "\",\"tool_name\":\"Write\",\"tool_input\":{\"file_path\":\"src/test.cs\"}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertSuccess();
    }

    #endregion

    #region Bash Commands (Hook Mode)

    [Fact]
    public async Task Guard_CommandOption_Parses()
    {
        await InitProjectAsync("none");

        var cmd = GuardCommand.Create();
        var result = await RunAsync(cmd, "--command", "dotnet build");

        result.AssertSuccess();
    }

    [Fact]
    public async Task Guard_CommandOption_AppliesShellAnalysis()
    {
        // Issue 0302: the documented manual-testing lane used to exit 0 for EVERYTHING —
        // arg mode carried no tool name, so ShouldRouteToShellHandler never routed the
        // command to the shell analyzer. A CLI --command is a shell command by definition.
        await InitProjectAsync("none");

        var cmd = GuardCommand.Create();
        var result = await RunAsync(cmd, "--command", "npx dydo check");

        result.AssertExitCode(2);
        result.AssertStderrContains("npx");
    }

    [Fact]
    public async Task Guard_CommandOption_BlocksOffLimitsReference()
    {
        await InitProjectAsync("none");

        var cmd = GuardCommand.Create();
        var result = await RunAsync(cmd, "--command", "cat .env");

        result.AssertExitCode(2);
        result.AssertStderrContains("off-limits");
    }

    #endregion

    #region Indirect Dydo Invocation

    [Theory]
    [InlineData("npx dydo agent claim auto")]
    [InlineData("npx -q dydo agent claim auto")]
    [InlineData("npx --yes dydo agent claim auto")]
    [InlineData("dotnet dydo agent claim auto")]
    [InlineData("dotnet tool run dydo agent claim auto")]
    [InlineData("dotnet run -- guard --action read --path foo.cs")]
    [InlineData("dotnet run -- sync")]
    [InlineData("dotnet run -- validate")]
    [InlineData("bash dydo agent claim auto")]
    [InlineData("sh dydo agent claim auto")]
    [InlineData("bash -c \\\"dydo agent claim auto\\\"")]
    [InlineData("sh -c 'dydo agent claim auto'")]
    [InlineData("python dydo agent claim auto")]
    [InlineData("python3 dydo agent claim auto")]
    [InlineData("py dydo agent claim auto")]
    public async Task Guard_IndirectDydo_IsBlocked(string command)
    {
        await InitProjectAsync("none");

        var json = "{\"session_id\":\"" + TestSessionId + "\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"" + command + "\"}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
    }

    [Theory]
    [InlineData("npx dydo agent claim auto", "npx")]
    [InlineData("npx -q dydo agent claim auto", "npx")]
    [InlineData("dotnet dydo agent claim auto", "dotnet")]
    [InlineData("dotnet tool run dydo agent claim auto", "dotnet")]
    [InlineData("dotnet run -- validate", "dotnet run")]
    [InlineData("bash dydo agent claim auto", "bash")]
    [InlineData("sh -c \\\"dydo agent claim auto\\\"", "sh")]
    [InlineData("python dydo agent claim auto", "python")]
    [InlineData("python3 dydo agent claim auto", "python3")]
    [InlineData("py dydo agent claim auto", "py")]
    public async Task Guard_IndirectDydo_ShowsInvokerName(string command, string expectedInvoker)
    {
        await InitProjectAsync("none");

        var json = "{\"session_id\":\"" + TestSessionId + "\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"" + command + "\"}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertExitCode(2);
        result.AssertStderrContains(expectedInvoker);
    }

    [Theory]
    [InlineData("npx prettier --write .")]
    [InlineData("dotnet build")]
    [InlineData("dotnet run")]
    [InlineData("dotnet run --project SomeApp")]
    [InlineData("dotnet run -- --help")]
    [InlineData("dotnet run -- serve --port 8080")]
    [InlineData("dotnet run -- myarg1 myarg2")]
    [InlineData("dotnet tool run other-tool --flag")]
    [InlineData("bash script.sh")]
    [InlineData("python script.py")]
    [InlineData("python3 -m pytest")]
    [InlineData("py -3 script.py")]
    public async Task Guard_IndirectDydo_FalsePositiveSafety(string command)
    {
        await InitProjectAsync("none");

        var json = "{\"session_id\":\"" + TestSessionId + "\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"" + command + "\"}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertSuccess();
    }

    [Fact]
    public async Task Guard_DirectDydo_StillWorks()
    {
        await InitProjectAsync("none");

        var json = "{\"session_id\":\"" + TestSessionId + "\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"dydo agent claim auto\"}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertSuccess();
    }

    [Fact]
    public async Task Guard_ChainedIndirectDydo_StillCaught()
    {
        await InitProjectAsync("none");

        var json = "{\"session_id\":\"" + TestSessionId + "\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"cd /tmp && npx dydo agent claim auto\"}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
    }

    #endregion

    #region Coaching: cd Compound

    [Fact]
    public async Task Guard_CdGitCompound_BlocksWithCoachingMessage()
    {
        await InitProjectAsync("none");

        var json = "{\"session_id\":\"" + TestSessionId + "\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"cd /c/Users/User/Desktop/Projects && git diff --name-only\"}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
        result.AssertStderrContains("Don't chain cd / Set-Location with other commands");
        result.AssertStderrContains("just run: git diff --name-only");
    }

    [Fact]
    public async Task Guard_CdNonGitCompound_Blocked()
    {
        await InitProjectAsync("none");

        var json = "{\"session_id\":\"" + TestSessionId + "\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"cd /tmp && ls\"}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
        result.AssertStderrContains("Don't chain cd / Set-Location with other commands");
    }

    #endregion

    #region Search Tools (Glob/Grep)

    [Theory]
    [InlineData("Glob")]
    [InlineData("Grep")]
    public async Task Guard_SearchTool_WithPath_Allows(string toolName)
    {
        await InitProjectAsync("none");

        var json = $"{{\"session_id\":\"{TestSessionId}\",\"tool_name\":\"{toolName}\",\"tool_input\":{{\"path\":\"src\",\"pattern\":\"*.cs\"}}}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertSuccess();
    }

    [Theory]
    [InlineData("Glob")]
    [InlineData("Grep")]
    public async Task Guard_SearchTool_NoPath_Allows(string toolName)
    {
        await InitProjectAsync("none");

        var json = $"{{\"session_id\":\"{TestSessionId}\",\"tool_name\":\"{toolName}\",\"tool_input\":{{\"pattern\":\"*.cs\"}}}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertSuccess();
    }

    [Theory]
    [InlineData("Glob")]
    [InlineData("Grep")]
    public async Task Guard_SearchTool_OffLimitsPath_Blocks(string toolName)
    {
        await InitProjectAsync("none");

        // .env is off-limits by default — searching with it as the path should block
        var json = $"{{\"session_id\":\"{TestSessionId}\",\"tool_name\":\"{toolName}\",\"tool_input\":{{\"path\":\".env\",\"pattern\":\"*\"}}}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
        result.AssertStderrContains("off-limits");
    }

    #endregion

    #region Git commands (git stash / merge are ordinary now — DR-041 Part B)

    [Theory]
    [InlineData("git stash")]
    [InlineData("git stash pop")]
    [InlineData("git stash apply")]
    [InlineData("git merge feature-branch")]
    [InlineData("git merge --no-ff main")]
    public async Task Guard_GitStashAndMerge_NotBlocked(string command)
    {
        await InitProjectAsync("none");

        var json = $"{{\"session_id\":\"{TestSessionId}\",\"tool_name\":\"Bash\",\"tool_input\":{{\"command\":\"{command}\"}}}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertSuccess();
    }

    [Theory]
    [InlineData("git status")]
    [InlineData("git commit -m 'test'")]
    [InlineData("git diff")]
    [InlineData("git log")]
    public async Task Guard_OtherGitCommands_NotBlocked(string command)
    {
        await InitProjectAsync("none");

        var json = $"{{\"session_id\":\"{TestSessionId}\",\"tool_name\":\"Bash\",\"tool_input\":{{\"command\":\"{command}\"}}}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertSuccess();
    }

    #endregion

    #region Blocked Tools

    [Fact]
    public async Task Guard_EnterPlanMode_Blocks()
    {
        await InitProjectAsync("none");

        var json = "{\"session_id\":\"" + TestSessionId + "\",\"tool_name\":\"EnterPlanMode\",\"tool_input\":{}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
        result.AssertStderrContains("plan mode");
    }

    [Fact]
    public async Task Guard_ExitPlanMode_Blocks()
    {
        await InitProjectAsync("none");

        var json = "{\"session_id\":\"" + TestSessionId + "\",\"tool_name\":\"ExitPlanMode\",\"tool_input\":{}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
        result.AssertStderrContains("plan mode");
    }

    #endregion

    #region Agent Tool — Nudge

    [Fact]
    public async Task Guard_AgentTool_EmitsNudgeAndPasses()
    {
        await InitProjectAsync("none");

        var json = $"{{\"session_id\":\"{TestSessionId}\",\"tool_name\":\"Agent\",\"tool_input\":{{\"prompt\":\"do something\"}}}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertSuccess();
        result.AssertStderrContains("NOTICE");
        result.AssertStderrContains("Tier-2 worker lane");
    }

    [Fact]
    public async Task Guard_GlobTool_DoesNotFireAgentNudge()
    {
        await InitProjectAsync("none");

        var json = $"{{\"session_id\":\"{TestSessionId}\",\"tool_name\":\"Glob\",\"tool_input\":{{\"pattern\":\"**/*.cs\"}}}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertSuccess();
        Assert.DoesNotContain("NOTICE", result.Stderr);
    }

    #endregion
}
