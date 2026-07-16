namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;
using DynaDocs.Services;

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
        await InitProjectAsync("none", "balazs");

        // .env is off-limits by default
        var result = await GuardAsync("edit", ".env");

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
        result.AssertStderrContains("off-limits");
    }

    [Fact]
    public async Task Guard_DydoSystemFile_Blocks()
    {
        await InitProjectAsync("none", "balazs");

        // dydo/index.md is a system file
        var result = await GuardAsync("edit", "dydo/index.md");

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
    }


    [Fact]
    public async Task Guard_AllowedPath_Passes()
    {
        await InitProjectAsync("none", "balazs");

        // src/file.cs is not off-limits — allowed
        var result = await GuardAsync("edit", "src/file.cs");

        result.AssertSuccess();
    }

    #endregion

    #region Reads allowed unless off-limits

    [Fact]
    public async Task Guard_ReadRootFile_Allows()
    {
        await InitProjectAsync("none", "balazs");

        var result = await GuardAsync("read", "CLAUDE.md");

        result.AssertSuccess();
    }


    [Fact]
    public async Task Guard_ReadSourceFile_Allows()
    {
        await InitProjectAsync("none", "balazs");

        var result = await GuardAsync("read", "src/code.cs");

        result.AssertSuccess();
    }


    [Fact]
    public async Task Guard_ReadNonAgentWorkflow_Allows()
    {
        await InitProjectAsync("none", "balazs");

        // A file named workflow.md outside the agents folder should NOT be blocked
        var result = await GuardAsync("read", "docs/workflow.md");

        result.AssertSuccess();
    }

    #endregion

    #region Stdin Hook Mode

    [Fact]
    public async Task Guard_StdinHook_ReadBootstrapFile_Allows()
    {
        await InitProjectAsync("none", "balazs");

        var json = "{\"session_id\":\"" + TestSessionId + "\",\"tool_name\":\"Read\",\"tool_input\":{\"file_path\":\"CLAUDE.md\"}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertSuccess();
    }

    [Fact]
    public async Task Guard_StdinHook_Write_Allows()
    {
        await InitProjectAsync("none", "balazs");

        var json = "{\"session_id\":\"" + TestSessionId + "\",\"tool_name\":\"Write\",\"tool_input\":{\"file_path\":\"src/test.cs\"}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertSuccess();
    }

    #endregion

    #region Bash Commands (Hook Mode)

    [Fact]
    public async Task Guard_CommandOption_Parses()
    {
        await InitProjectAsync("none", "balazs");

        // CLI --command option is accepted (though bash analysis requires hook mode)
        var cmd = GuardCommand.Create();
        var result = await RunAsync(cmd, "--command", "dotnet build");

        result.AssertSuccess();
    }

    #endregion

    #region Indirect Dydo Invocation

    [Theory]
    [InlineData("npx dydo agent claim auto")]
    [InlineData("npx -q dydo agent claim auto")]
    [InlineData("npx --yes dydo agent claim auto")]
    [InlineData("dotnet dydo agent claim auto")]
    [InlineData("dotnet tool run dydo agent claim auto")]
    [InlineData("dotnet run -- task list")]
    [InlineData("dotnet run -- guard --action read --path foo.cs")]
    [InlineData("dotnet run -- roles list")]
    [InlineData("dotnet run -- validate")]
    [InlineData("dotnet run -- issue list")]
    [InlineData("dotnet run -- watchdog status")]
    [InlineData("dotnet run --project . -- task list")]
    [InlineData("bash dydo agent claim auto")]
    [InlineData("sh dydo agent claim auto")]
    [InlineData("bash -c \\\"dydo agent claim auto\\\"")]
    [InlineData("sh -c 'dydo agent claim auto'")]
    [InlineData("python dydo agent claim auto")]
    [InlineData("python3 dydo agent claim auto")]
    [InlineData("py dydo agent claim auto")]
    public async Task Guard_IndirectDydo_IsBlocked(string command)
    {
        await InitProjectAsync("none", "balazs");

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
    [InlineData("dotnet run -- task list", "dotnet run")]
    [InlineData("bash dydo agent claim auto", "bash")]
    [InlineData("sh -c \\\"dydo agent claim auto\\\"", "sh")]
    [InlineData("python dydo agent claim auto", "python")]
    [InlineData("python3 dydo agent claim auto", "python3")]
    [InlineData("py dydo agent claim auto", "py")]
    public async Task Guard_IndirectDydo_ShowsInvokerName(string command, string expectedInvoker)
    {
        await InitProjectAsync("none", "balazs");

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
        await InitProjectAsync("none", "balazs");

        var json = "{\"session_id\":\"" + TestSessionId + "\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"" + command + "\"}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertSuccess();
    }

    [Fact]
    public async Task Guard_DirectDydo_StillWorks()
    {
        await InitProjectAsync("none", "balazs");

        var json = "{\"session_id\":\"" + TestSessionId + "\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"dydo agent claim auto\"}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertSuccess();
    }

    [Fact]
    public async Task Guard_ChainedIndirectDydo_StillCaught()
    {
        await InitProjectAsync("none", "balazs");

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
        await InitProjectAsync("none", "balazs");

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
        await InitProjectAsync("none", "balazs");

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
        await InitProjectAsync("none", "balazs");

        var json = $"{{\"session_id\":\"{TestSessionId}\",\"tool_name\":\"{toolName}\",\"tool_input\":{{\"path\":\"src\",\"pattern\":\"*.cs\"}}}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertSuccess();
    }

    [Theory]
    [InlineData("Glob")]
    [InlineData("Grep")]
    public async Task Guard_SearchTool_NoPath_Allows(string toolName)
    {
        await InitProjectAsync("none", "balazs");

        var json = $"{{\"session_id\":\"{TestSessionId}\",\"tool_name\":\"{toolName}\",\"tool_input\":{{\"pattern\":\"*.cs\"}}}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertSuccess();
    }

    [Theory]
    [InlineData("Glob")]
    [InlineData("Grep")]
    public async Task Guard_SearchTool_OffLimitsPath_Blocks(string toolName)
    {
        await InitProjectAsync("none", "balazs");

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
        await InitProjectAsync("none", "balazs");

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
        await InitProjectAsync("none", "balazs");

        var json = $"{{\"session_id\":\"{TestSessionId}\",\"tool_name\":\"Bash\",\"tool_input\":{{\"command\":\"{command}\"}}}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertSuccess();
    }

    #endregion

    #region Blocked Tools

    [Fact]
    public async Task Guard_EnterPlanMode_Blocks()
    {
        await InitProjectAsync("none", "balazs");

        var json = "{\"session_id\":\"" + TestSessionId + "\",\"tool_name\":\"EnterPlanMode\",\"tool_input\":{}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
        result.AssertStderrContains("plan mode");
    }

    [Fact]
    public async Task Guard_ExitPlanMode_Blocks()
    {
        await InitProjectAsync("none", "balazs");

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
        await InitProjectAsync("none", "balazs");

        var json = $"{{\"session_id\":\"{TestSessionId}\",\"tool_name\":\"Agent\",\"tool_input\":{{\"prompt\":\"do something\"}}}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertSuccess();
        result.AssertStderrContains("NOTICE");
        result.AssertStderrContains("Tier-2 worker lane");
    }

    [Fact]
    public async Task Guard_GlobTool_DoesNotFireAgentNudge()
    {
        await InitProjectAsync("none", "balazs");

        var json = $"{{\"session_id\":\"{TestSessionId}\",\"tool_name\":\"Glob\",\"tool_input\":{{\"pattern\":\"**/*.cs\"}}}}";
        var result = await GuardWithStdinAsync(json);

        result.AssertSuccess();
        Assert.DoesNotContain("NOTICE", result.Stderr);
    }

    #endregion

    #region Model-cap restore on guard trigger (DR-041 Part E)

    [Fact]
    public async Task Guard_RestoresExpiredModelCap_OnTrigger()
    {
        await InitProjectAsync("none", "balazs");

        var previousResync = ModelCapService.ResyncOverride;
        ModelCapService.ResyncOverride = _ => 0; // don't emit native agents during the test
        try
        {
            // Simulate a strong tier capped with a reset time already in the past.
            var capDir = Path.Combine(TestDir, "dydo", "_system", ".local", "model-caps");
            Directory.CreateDirectory(capDir);
            var marker = Path.Combine(capDir, "claude-fable-5.json");
            File.WriteAllText(marker,
                "{\"model\":\"claude-fable-5\",\"fallback\":\"claude-sonnet-5\"," +
                "\"until\":\"2000-01-01T00:00:00+00:00\"," +
                "\"reboundTiers\":[{\"vendor\":\"anthropic\",\"tier\":\"strong\"}]}");
            Assert.True(File.Exists(marker));

            // Any guarded shell call trips the throttled model-cap restore on the guard trigger.
            var json = $"{{\"session_id\":\"{TestSessionId}\",\"tool_name\":\"Bash\",\"tool_input\":{{\"command\":\"git status\"}}}}";
            await GuardWithStdinAsync(json);

            Assert.False(File.Exists(marker),
                "expired model-cap marker should be restored (deleted) by the guard trigger");
        }
        finally
        {
            ModelCapService.ResyncOverride = previousResync;
        }
    }

    #endregion
}
