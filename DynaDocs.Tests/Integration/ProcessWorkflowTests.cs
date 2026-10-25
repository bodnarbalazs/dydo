namespace DynaDocs.Tests.Integration;

[Collection("Integration")]
public class ProcessWorkflowTests : IntegrationTestBase
{
    [Fact]
    public async Task Init_AgentWorkspaces_DoNotHaveModeFiles()
    {
        var result = await InitProjectAsync();
        result.AssertSuccess();

        // DR 049 retired the mode compiler; init never creates modes.
        var modesPath = Path.Combine(TestDir, "dydo", "agents", "sample", "modes");
        Assert.False(Directory.Exists(modesPath), "Modes folder should NOT exist after init");
    }
}
