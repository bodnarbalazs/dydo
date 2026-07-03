namespace DynaDocs.Tests.Integration;

/// <summary>
/// Guard pipeline integration tests for the Tier-1 source-write nudge
/// (Decision 026 §4): a soft, exit-0 reminder that Tier-1 agents (no agent_type
/// in the hook payload, per Decision 024) delegate implementation to workflows.
/// Fires only for Edit/Write/NotebookEdit on {source}/{tests} paths; Tier-2
/// worker calls and non-source paths stay silent.
/// </summary>
[Collection("Integration")]
public class GuardTier1NudgeTests : IntegrationTestBase
{
    private const string NudgeMarker = "if it needs a reviewer, it needs a workflow";

    private string Tier1Json(string toolName, string inputJson) =>
        $"{{\"session_id\":\"{TestSessionId}\",\"tool_name\":\"{toolName}\",\"tool_input\":{inputJson}}}";

    private string WorkerJson(string toolName, string inputJson) =>
        $"{{\"session_id\":\"{TestSessionId}\",\"agent_id\":\"wkr-test-1\",\"agent_type\":\"code-writer\","
        + $"\"tool_name\":\"{toolName}\",\"tool_input\":{inputJson}}}";

    private async Task OnboardTier1Async()
    {
        await InitProjectAsync();
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");
        await ReadMustReadsAsync();
    }

    [Fact]
    public async Task Tier1_EditSourcePath_FiresSoftNudge()
    {
        await OnboardTier1Async();

        var result = await GuardWithStdinAsync(Tier1Json("Edit", "{\"file_path\":\"src/Foo.cs\"}"));

        // Exit-0 warning, never a block
        result.AssertSuccess();
        result.AssertStderrContains(NudgeMarker);
    }

    [Fact]
    public async Task Tier1_WriteTestsPath_FiresSoftNudge()
    {
        await OnboardTier1Async();

        var result = await GuardWithStdinAsync(Tier1Json("Write", "{\"file_path\":\"tests/FooTests.cs\"}"));

        result.AssertSuccess();
        result.AssertStderrContains(NudgeMarker);
    }

    [Fact]
    public async Task Tier1_NotebookEditSourcePath_FiresSoftNudge()
    {
        await OnboardTier1Async();

        var result = await GuardWithStdinAsync(
            Tier1Json("NotebookEdit", "{\"notebook_path\":\"src/analysis.ipynb\"}"));

        result.AssertSuccess();
        result.AssertStderrContains(NudgeMarker);
    }

    [Fact]
    public async Task Tier2_Worker_SourcePath_Silent()
    {
        await InitProjectAsync();

        // agent_type present = Tier-2 worker: same path, no nudge
        var result = await GuardWithStdinAsync(WorkerJson("Write", "{\"file_path\":\"src/Foo.cs\"}"));

        result.AssertSuccess();
        Assert.DoesNotContain(NudgeMarker, result.Stderr);
    }

    [Fact]
    public async Task Tier1_DocsPath_Silent()
    {
        await OnboardTier1Async();

        var result = await GuardWithStdinAsync(
            Tier1Json("Edit", "{\"file_path\":\"dydo/project/decisions/099-test.md\"}"));

        result.AssertSuccess();
        Assert.DoesNotContain(NudgeMarker, result.Stderr);
    }

    [Fact]
    public async Task Tier1_ReadSourcePath_Silent()
    {
        await OnboardTier1Async();

        // Reads are not writes — the nudge is scoped to Edit/Write/NotebookEdit
        var result = await GuardWithStdinAsync(Tier1Json("Read", "{\"file_path\":\"src/Foo.cs\"}"));

        result.AssertSuccess();
        Assert.DoesNotContain(NudgeMarker, result.Stderr);
    }
}
