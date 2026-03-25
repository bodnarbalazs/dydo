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

    #region No Agent Tests

    [Fact]
    public async Task Wait_WithoutClaimedAgent_ReturnsError()
    {
        await InitProjectAsync("none", "testuser", 3);
        // Do NOT claim an agent — session context exists but no agent claimed
        StoreSessionContext();

        var command = WaitCommand.Create();
        var result = await RunAsync(command);

        result.AssertExitCode(2);
        result.AssertStderrContains("No agent identity");
    }

    #endregion

    #region Wait With Message Tests

    [Fact]
    public async Task Wait_General_FindsExistingMessage()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        // Pre-populate inbox with a message
        CreateMessageFile("Adele", "Brian", "test-subject", "Hello from Brian");

        StoreSessionContext();
        var command = WaitCommand.Create();
        var result = await RunAsync(command);

        result.AssertSuccess();
        result.AssertStdoutContains("Message received from Brian");
        result.AssertStdoutContains("test-subject");
    }

    [Fact]
    public async Task Wait_ForTask_FindsExistingMessage()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        // Pre-populate inbox with a message matching the task filter
        CreateMessageFile("Adele", "Charlie", "my-task", "Task results ready");

        StoreSessionContext();
        var command = WaitCommand.Create();
        var result = await RunAsync(command, "--task", "my-task");

        result.AssertSuccess();
        result.AssertStdoutContains("Message received from Charlie");
    }

    [Fact]
    public void MessageFinder_FindMessage_NonexistentPath_ReturnsNull()
    {
        var result = MessageFinder.FindMessage("/nonexistent/inbox", null);
        Assert.Null(result);
    }

    #endregion

    #region Parent Liveness Tests

    [Fact]
    public async Task WaitForTask_ExitsWhenParentDies()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "my-task", "Brian");

        ProcessUtils.IsProcessRunningOverride = _ => false;
        try
        {
            StoreSessionContext();
            var command = WaitCommand.Create();
            var result = await RunAsync(command, "--task", "my-task");

            result.AssertExitCode(2);

            // Marker should be reset (listening=false, pid=null)
            registry = new AgentRegistry(TestDir);
            var markers = registry.GetWaitMarkers("Adele");
            Assert.Single(markers);
            Assert.False(markers[0].Listening);
            Assert.Null(markers[0].Pid);
        }
        finally
        {
            ProcessUtils.IsProcessRunningOverride = null;
        }
    }

    [Fact]
    public async Task WaitGeneral_ExitsWhenParentDies()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        ProcessUtils.IsProcessRunningOverride = _ => false;
        try
        {
            StoreSessionContext();
            var command = WaitCommand.Create();
            var result = await RunAsync(command);

            result.AssertExitCode(2);

            // General wait marker should be cleaned up
            var registry = new AgentRegistry(TestDir);
            var markers = registry.GetWaitMarkers("Adele");
            Assert.Empty(markers);
        }
        finally
        {
            ProcessUtils.IsProcessRunningOverride = null;
        }
    }

    [Fact]
    public async Task WaitForTask_MessageFound_StillWorks()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "my-task", "Brian");

        CreateMessageFile("Adele", "Charlie", "my-task", "Done");

        // Parent alive — should find message and succeed
        ProcessUtils.IsProcessRunningOverride = _ => true;
        try
        {
            StoreSessionContext();
            var command = WaitCommand.Create();
            var result = await RunAsync(command, "--task", "my-task");

            result.AssertSuccess();
            result.AssertStdoutContains("Message received from Charlie");

            // Marker should be fully removed (message found)
            registry = new AgentRegistry(TestDir);
            var markers = registry.GetWaitMarkers("Adele");
            Assert.Empty(markers);
        }
        finally
        {
            ProcessUtils.IsProcessRunningOverride = null;
        }
    }

    [Fact]
    public async Task WaitGeneral_RecordsPidInMarker()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        // Pre-populate message so the loop exits immediately
        CreateMessageFile("Adele", "Brian", "general-subject", "Hello");

        ProcessUtils.IsProcessRunningOverride = _ => true;
        try
        {
            StoreSessionContext();
            var command = WaitCommand.Create();
            var result = await RunAsync(command);

            result.AssertSuccess();

            // General marker should be cleaned up after exit
            var registry = new AgentRegistry(TestDir);
            var markers = registry.GetWaitMarkers("Adele");
            Assert.Empty(markers);
        }
        finally
        {
            ProcessUtils.IsProcessRunningOverride = null;
        }
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
