namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;
using DynaDocs.Services;

[Collection("Integration")]
public class WaitCommandTests : IntegrationTestBase
{
    #region --cancel Tests

    [Fact]
    public async Task Wait_Cancel_SpecificTask_RemovesMarker()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "auth", "Brian");

        StoreSessionContext();
        var command = WaitCommand.Create();
        var result = await RunAsync(command, "--task", "auth", "--cancel");

        result.AssertSuccess();
        result.AssertStdoutContains("Wait cancelled for task 'auth'");

        // Verify marker was removed
        registry = new AgentRegistry(TestDir);
        var markers = registry.GetWaitMarkers("Adele");
        Assert.Empty(markers);
    }

    [Fact]
    public async Task Wait_Cancel_AllTasks_RemovesAllMarkers()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "auth", "Brian");
        registry.CreateWaitMarker("Adele", "login", "Charlie");

        StoreSessionContext();
        var command = WaitCommand.Create();
        var result = await RunAsync(command, "--cancel");

        result.AssertSuccess();
        result.AssertStdoutContains("All wait markers cleared");

        registry = new AgentRegistry(TestDir);
        var markers = registry.GetWaitMarkers("Adele");
        Assert.Empty(markers);
    }

    [Fact]
    public async Task Wait_Cancel_NonexistentMarker_Succeeds()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        StoreSessionContext();
        var command = WaitCommand.Create();
        var result = await RunAsync(command, "--task", "nonexistent", "--cancel");

        result.AssertSuccess();
    }

    #endregion

    #region Channel Isolation Tests

    [Fact]
    public async Task Wait_Cancel_PreservesOtherMarkers()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "auth", "Brian");
        registry.CreateWaitMarker("Adele", "login", "Charlie");

        // Cancel only "auth"
        StoreSessionContext();
        var command = WaitCommand.Create();
        await RunAsync(command, "--task", "auth", "--cancel");

        registry = new AgentRegistry(TestDir);
        var markers = registry.GetWaitMarkers("Adele");
        Assert.Single(markers);
        Assert.Equal("login", markers[0].Task);
    }

    #endregion

    #region Wait Marker Infrastructure

    [Fact]
    public async Task WaitMarker_CreateAndRead_RoundTrips()
    {
        await InitProjectAsync("none", "testuser", 3);

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "auth-login", "Brian");

        var markers = registry.GetWaitMarkers("Adele");
        Assert.Single(markers);
        Assert.Equal("Brian", markers[0].Target);
        Assert.Equal("auth-login", markers[0].Task);
    }

    [Fact]
    public async Task WaitMarker_Remove_DeletesSpecificMarker()
    {
        await InitProjectAsync("none", "testuser", 3);

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "auth", "Brian");
        registry.CreateWaitMarker("Adele", "login", "Charlie");

        var removed = registry.RemoveWaitMarker("Adele", "auth");
        Assert.True(removed);

        var markers = registry.GetWaitMarkers("Adele");
        Assert.Single(markers);
        Assert.Equal("login", markers[0].Task);
    }

    [Fact]
    public async Task WaitMarker_Remove_NonExistent_ReturnsFalse()
    {
        await InitProjectAsync("none", "testuser", 3);

        var registry = new AgentRegistry(TestDir);
        var removed = registry.RemoveWaitMarker("Adele", "nonexistent");
        Assert.False(removed);
    }

    [Fact]
    public async Task WaitMarker_ClearAll_RemovesDirectory()
    {
        await InitProjectAsync("none", "testuser", 3);

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "auth", "Brian");
        registry.CreateWaitMarker("Adele", "login", "Charlie");

        registry.ClearAllWaitMarkers("Adele");

        var markers = registry.GetWaitMarkers("Adele");
        Assert.Empty(markers);
    }

    [Fact]
    public async Task WaitMarker_Overwrite_IsIdempotent()
    {
        await InitProjectAsync("none", "testuser", 3);

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "auth", "Brian");
        registry.CreateWaitMarker("Adele", "auth", "Charlie");

        var markers = registry.GetWaitMarkers("Adele");
        Assert.Single(markers);
        Assert.Equal("Charlie", markers[0].Target);
    }

    #endregion

    #region Helper Methods

    private void CreateMessageFile(string agentName, string fromAgent, string subject, string body)
    {
        var inboxPath = Path.Combine(TestDir, "dydo", "agents", agentName, "inbox");
        Directory.CreateDirectory(inboxPath);

        var id = Guid.NewGuid().ToString("N")[..8];
        var content = $"""
            ---
            id: {id}
            type: message
            from: {fromAgent}
            subject: {subject}
            received: {DateTime.UtcNow:o}
            ---

            # Message from {fromAgent}

            ## Subject

            {subject}

            ## Body

            {body}
            """;

        File.WriteAllText(Path.Combine(inboxPath, $"{id}-msg-{subject}.md"), content);
    }

    #endregion
}
