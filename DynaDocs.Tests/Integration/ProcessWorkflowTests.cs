namespace DynaDocs.Tests.Integration;

/// <summary>
/// Integration tests for init/workspace shape. The self-review-prevention and task-role-history
/// tests were removed with the role-set/CanTakeRole machinery (DR-041).
/// </summary>
[Collection("Integration")]
public class ProcessWorkflowTests : IntegrationTestBase
{
    [Fact]
    public async Task Init_AgentWorkspaces_DoNotHaveModeFiles()
    {
        var result = await InitProjectAsync();
        result.AssertSuccess();

        // Modes are not compiled at all (DR 049 retired the compiler); init never creates them.
        var modesPath = Path.Combine(TestDir, "dydo", "agents", "sample", "modes");
        Assert.False(Directory.Exists(modesPath), "Modes folder should NOT exist after init");
    }
}
