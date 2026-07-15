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

        // Modes are compiled by dydo sync, never created at init.
        var modesPath = Path.Combine(TestDir, "dydo", "agents", "Adele", "modes");
        Assert.False(Directory.Exists(modesPath), "Modes folder should NOT exist after init");
    }
}
