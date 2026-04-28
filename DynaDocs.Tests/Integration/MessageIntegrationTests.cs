namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;
using DynaDocs.Services;

[Collection("Integration")]
public class MessageIntegrationTests : IntegrationTestBase
{
    #region MessageCommand Tests

    [Fact]
    public async Task Message_ToActiveAgent_CreatesInboxFile()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "test-task");

        // Claim Brian as active target
        ClaimAgentInSeparateSession("Brian");

        await SendMessageAsync("Brian", "Hello from Adele");

        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Brian/inbox"), "*-msg-*.md");
        Assert.Single(inboxFiles);

        var content = File.ReadAllText(inboxFiles[0]);
        Assert.Contains("type: message", content);
        Assert.Contains("from: Adele", content);
        Assert.Contains("Hello from Adele", content);
    }

    [Fact]
    public async Task Message_ToActiveAgent_UpdatesUnreadMessages()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "test-task");

        ClaimAgentInSeparateSession("Brian");

        var result = await SendMessageAsync("Brian", "test body");
        result.AssertSuccess();

        var registry = new AgentRegistry(TestDir);
        var brianState = registry.GetAgentState("Brian");
        Assert.NotNull(brianState);
        Assert.Single(brianState.UnreadMessages);
    }

    [Fact]
    public async Task Message_WithSubject_IncludesSubjectInFilename()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "test-task");

        ClaimAgentInSeparateSession("Brian");

        await SendMessageAsync("Brian", "Done with auth", subject: "auth-login");

        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Brian/inbox"), "*-msg-auth-login.md");
        Assert.Single(inboxFiles);
    }

    [Fact]
    public async Task Message_WithSubject_IncludesSubjectInFrontmatter()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "test-task");

        ClaimAgentInSeparateSession("Brian");

        await SendMessageAsync("Brian", "Done", subject: "auth-login");

        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Brian/inbox"), "*-msg-*.md");
        var content = File.ReadAllText(inboxFiles[0]);
        Assert.Contains("subject: auth-login", content);
    }

    [Fact]
    public async Task Message_WithoutSubject_UsesGeneralInFilename()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "test-task");

        ClaimAgentInSeparateSession("Brian");

        await SendMessageAsync("Brian", "Hello");

        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Brian/inbox"), "*-msg-general.md");
        Assert.Single(inboxFiles);
    }

    [Fact]
    public async Task Message_InboxFileHasTypeMessage()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "test-task");

        ClaimAgentInSeparateSession("Brian");

        await SendMessageAsync("Brian", "test");

        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Brian/inbox"), "*-msg-*.md");
        var content = File.ReadAllText(inboxFiles[0]);
        Assert.Contains("type: message", content);
    }

    #endregion

    #region MessageCommand Error Cases

    [Fact]
    public async Task Message_WithoutIdentity_Fails()
    {
        await InitProjectAsync("none", "testuser", 3);
        StoreSessionContext();

        var result = await SendMessageAsync("Brian", "test");

        result.AssertExitCode(2);
        result.AssertStderrContains("No agent identity");
    }

    [Fact]
    public async Task Message_WithoutBody_Fails()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        var command = MessageCommand.Create();
        var result = await RunAsync(command, "--to", "Brian");

        result.AssertExitCode(2);
        result.AssertStderrContains("--body or --body-file");
    }

    [Fact]
    public async Task Message_ToSelf_Fails()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        var result = await SendMessageAsync("Adele", "test");

        result.AssertExitCode(2);
        result.AssertStderrContains("yourself");
    }

    [Fact]
    public async Task Message_ToNonExistentAgent_Fails()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        var result = await SendMessageAsync("Zorro", "test");

        result.AssertExitCode(2);
        result.AssertStderrContains("does not exist");
    }

    [Fact]
    public async Task Message_ToCrossHumanAgent_Fails()
    {
        await InitProjectAsync("none", "alice", 3);
        await JoinProjectAsync("none", "bob", 2);

        SetHuman("alice");
        await ClaimAgentAsync("Adele");

        // Dexter is bob's agent
        var result = await SendMessageAsync("Dexter", "test");

        result.AssertExitCode(2);
        result.AssertStderrContains("not assigned to you");
    }

    [Fact]
    public async Task Message_ToInactiveAgent_WithoutForce_Fails()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        // Brian is not claimed = inactive
        var result = await SendMessageAsync("Brian", "test");

        result.AssertExitCode(2);
        result.AssertStderrContains("has been released");
        // General case should NOT offer --force
        Assert.DoesNotContain("--force", result.Stderr);
    }

    [Fact]
    public async Task Message_ToInactiveAgent_WithForce_Succeeds()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        var result = await SendMessageAsync("Brian", "test", force: true);

        result.AssertSuccess();

        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Brian/inbox"), "*-msg-*.md");
        Assert.Single(inboxFiles);
    }

    [Fact]
    public async Task Message_ToInactiveAgent_WithForce_DoesNotUpdateUnreadMessages()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        await SendMessageAsync("Brian", "test", force: true);

        var registry = new AgentRegistry(TestDir);
        var brianState = registry.GetAgentState("Brian");
        Assert.NotNull(brianState);
        Assert.Empty(brianState.UnreadMessages);
    }

    [Fact]
    public async Task Message_BodyWithShellMetacharacters_Fails()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        ClaimAgentInSeparateSession("Brian");

        var result = await SendMessageAsync("Brian", "Run $(whoami) now");

        result.AssertExitCode(2);
        result.AssertStderrContains("$(");
    }

    [Fact]
    public async Task Message_BodyFile_BypassesMetacharacterCheck()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        ClaimAgentInSeparateSession("Brian");

        var bodyPath = Path.Combine(TestDir, "body.txt");
        File.WriteAllText(bodyPath, "Run $(whoami) && echo done");

        var command = MessageCommand.Create();
        var result = await RunAsync(command, "--to", "Brian", "--body-file", bodyPath);

        result.AssertSuccess();

        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Brian/inbox"), "*-msg-*.md");
        var content = File.ReadAllText(inboxFiles[0]);
        Assert.Contains("$(whoami)", content);
    }

    #endregion

    #region InboxShow Message Display

    [Fact]
    public async Task InboxShow_DisplaysMessagesDistinctly()
    {
        await InitProjectAsync("none", "testuser", 3);

        CreateMessageFile("Adele", "Brian", "auth-done", "Auth is complete.");

        await ClaimAgentAsync("Adele");

        var command = InboxCommand.Create();
        var result = await RunAsync(command, "show");

        result.AssertSuccess();
        result.AssertStdoutContains("MESSAGE:");
        result.AssertStdoutContains("auth-done");
    }

    [Fact]
    public async Task InboxShow_MessageShowsBodyNotBrief()
    {
        await InitProjectAsync("none", "testuser", 3);

        CreateMessageFile("Adele", "Brian", "status", "Implementation complete.");

        await ClaimAgentAsync("Adele");

        var command = InboxCommand.Create();
        var result = await RunAsync(command, "show");

        result.AssertSuccess();
        result.AssertStdoutContains("Body:");
        result.AssertStdoutContains("Implementation complete.");
    }

    [Fact]
    public async Task InboxShow_MixedDispatchAndMessage_ShowsBoth()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Create a dispatch item
        CreateDispatchFile("Adele", "Charlie", "code-writer", "feature-x", "Implement feature X");

        // Create a message item
        CreateMessageFile("Adele", "Brian", "status", "Feature Y done.");

        await ClaimAgentAsync("Adele");

        var command = InboxCommand.Create();
        var result = await RunAsync(command, "show");

        result.AssertSuccess();
        result.AssertStdoutContains("CODE-WRITER:");
        result.AssertStdoutContains("MESSAGE:");
        result.AssertStdoutContains("2 item(s)");
    }

    [Fact]
    public async Task InboxParse_MissingType_DefaultsToDispatch()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Create an inbox item without type field (legacy dispatch)
        CreateDispatchFile("Adele", "Brian", "code-writer", "old-task", "Old dispatch");

        await ClaimAgentAsync("Adele");

        var command = InboxCommand.Create();
        var result = await RunAsync(command, "show");

        result.AssertSuccess();
        result.AssertStdoutContains("CODE-WRITER:");
        Assert.DoesNotContain("MESSAGE:", result.Stdout);
    }

    #endregion

    #region Guard Message Notification

    [Fact]
    public async Task Guard_AgentWithUnreadMessages_BlocksWithNotification()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "test-task");
        await ReadMustReadsAsync();

        // Set unread message in Adele's state
        var registry = new AgentRegistry(TestDir);
        registry.AddUnreadMessage("Adele", "abc12345");

        // Place corresponding message file
        CreateMessageFile("Adele", "Brian", "test-subject", "Hello", id: "abc12345");

        var result = await GuardAsync("edit", "Commands/Foo.cs");

        result.AssertExitCode(2);
        result.AssertStderrContains("NOTICE:");
        result.AssertStderrContains("unread message");
        result.AssertStderrContains("paused");
    }

    [Fact]
    public async Task Guard_AgentWithUnreadMessages_AllowsDydoCommands()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "test-task");
        await ReadMustReadsAsync();

        var registry = new AgentRegistry(TestDir);
        registry.AddUnreadMessage("Adele", "abc12345");

        // dydo commands should pass through even with unread messages
        var result = await GuardAsync("read", "dydo/agents/Adele/inbox/abc12345-msg-test.md");

        result.AssertSuccess();
    }

    [Fact]
    public async Task Guard_AgentWithNoUnreadMessages_Allows()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "test-task");
        await ReadMustReadsAsync();

        var result = await GuardAsync("edit", "src/Foo.cs");

        result.AssertSuccess();
    }

    [Fact]
    public async Task Guard_ReadingMessageFile_ClearsFromUnreadList()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "test-task");
        await ReadMustReadsAsync();

        var registry = new AgentRegistry(TestDir);
        registry.AddUnreadMessage("Adele", "abc12345");
        CreateMessageFile("Adele", "Brian", "test", "Hello", id: "abc12345");

        // Read the message file
        await GuardAsync("read", "dydo/agents/Adele/inbox/abc12345-msg-test.md");

        // Verify unread was cleared
        registry = new AgentRegistry(TestDir);
        var state = registry.GetAgentState("Adele");
        Assert.NotNull(state);
        Assert.Empty(state.UnreadMessages);
    }

    [Fact]
    public async Task Guard_ReadingNonMessageFile_DoesNotClearList()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "test-task");
        await ReadMustReadsAsync();

        var registry = new AgentRegistry(TestDir);
        registry.AddUnreadMessage("Adele", "abc12345");

        // Read some other file
        await GuardAsync("read", "Commands/Foo.cs");

        // Unread message should still be there
        // (the guard blocked this read due to unread messages, but the message wasn't cleared)
        registry = new AgentRegistry(TestDir);
        var state = registry.GetAgentState("Adele");
        Assert.NotNull(state);
        Assert.Single(state.UnreadMessages);
    }

    [Fact]
    public async Task Guard_NotificationMessage_ContainsSenderAndSubject()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "test-task");
        await ReadMustReadsAsync();

        var registry = new AgentRegistry(TestDir);
        registry.AddUnreadMessage("Adele", "abc12345");
        CreateMessageFile("Adele", "Brian", "auth-task", "Hello", id: "abc12345");

        var result = await GuardAsync("edit", "Commands/Foo.cs");

        result.AssertExitCode(2);
        result.AssertStderrContains("Brian");
        result.AssertStderrContains("auth-task");
    }

    #endregion

    #region End-to-End Workflows

    [Fact]
    public async Task Message_EndToEnd_SendAndReceive()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Adele sends a message to Brian
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "test-task");

        ClaimAgentInSeparateSession("Brian");

        var sendResult = await SendMessageAsync("Brian", "Auth implementation complete.", subject: "auth");
        sendResult.AssertSuccess();
        sendResult.AssertStdoutContains("Message sent to Brian");

        // Switch to Brian's session and show inbox
        StoreSessionContextForAgent("Brian");

        var inboxCommand = InboxCommand.Create();
        var showResult = await RunAsync(inboxCommand, "show");
        showResult.AssertSuccess();
        showResult.AssertStdoutContains("MESSAGE:");
        showResult.AssertStdoutContains("auth");
        showResult.AssertStdoutContains("Auth implementation complete.");
    }

    [Fact]
    public async Task Release_WithUnreadMessage_Blocked()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        // Place a message in inbox (release checks for unprocessed inbox items)
        CreateMessageFile("Adele", "Brian", "test", "Hello");

        var result = await ReleaseAgentAsync();

        result.AssertExitCode(2);
        result.AssertStderrContains("unprocessed inbox");
    }

    [Fact]
    public async Task Release_AfterClearingMessage_Succeeds()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        // Place and then clear message
        CreateMessageFile("Adele", "Brian", "test", "Hello");

        var clearCommand = InboxCommand.Create();
        await RunAsync(clearCommand, "clear", "--all");

        var result = await ReleaseAgentAsync();

        result.AssertSuccess();
    }

    #endregion

    #region AgentRegistry Unread Messages

    [Fact]
    public async Task UnreadMessages_RoundTrips_ThroughState()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        // Write unread-messages directly into Adele's state
        var statePath = Path.Combine(TestDir, "dydo", "agents", "Adele", "state.md");
        var content = File.ReadAllText(statePath);
        content = content.Replace("unread-messages: []", """unread-messages: ["abc123", "def456"]""");
        File.WriteAllText(statePath, content);

        var registry = new AgentRegistry(TestDir);
        var state = registry.GetAgentState("Adele");

        Assert.NotNull(state);
        Assert.Equal(2, state.UnreadMessages.Count);
        Assert.Contains("abc123", state.UnreadMessages);
        Assert.Contains("def456", state.UnreadMessages);
    }

    [Fact]
    public async Task AddUnreadMessage_AddsToState()
    {
        await InitProjectAsync("none", "testuser", 3);

        var registry = new AgentRegistry(TestDir);
        registry.AddUnreadMessage("Adele", "msg123");

        var state = registry.GetAgentState("Adele");
        Assert.NotNull(state);
        Assert.Single(state.UnreadMessages);
        Assert.Contains("msg123", state.UnreadMessages);
    }

    [Fact]
    public async Task AddUnreadMessage_NoDuplicates()
    {
        await InitProjectAsync("none", "testuser", 3);

        var registry = new AgentRegistry(TestDir);
        registry.AddUnreadMessage("Adele", "msg123");
        registry.AddUnreadMessage("Adele", "msg123");

        var state = registry.GetAgentState("Adele");
        Assert.NotNull(state);
        Assert.Single(state.UnreadMessages);
    }

    #endregion

    #region Contextual Inactive Agent Messaging

    [Fact]
    public async Task Message_ToInactiveAgent_ListsActiveAgents()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "test-task");

        // Brian is not claimed = inactive, Adele is the only active agent
        var result = await SendMessageAsync("Brian", "test");

        result.AssertExitCode(2);
        result.AssertStderrContains("has been released");
        result.AssertStderrContains("Adele");
        result.AssertStderrContains("code-writer");
    }

    [Fact]
    public async Task Message_ToInactiveAgent_OnlySenderActive_ShowsActiveList()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        var result = await SendMessageAsync("Brian", "test");

        result.AssertExitCode(2);
        result.AssertStderrContains("has been released");
        // Adele (sender) is the only active agent, so the list should include her
        result.AssertStderrContains("All active agents");
    }

    [Fact]
    public async Task Message_ToInactiveAgent_WithDispatcher_SuggestsDispatcher()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "test-task");

        // Claim Brian in separate session and set DispatchedBy to Adele
        ClaimAgentInSeparateSession("Brian");
        var statePath = Path.Combine(TestDir, "dydo/agents/Brian/state.md");
        var stateContent = File.ReadAllText(statePath);
        stateContent = stateContent.Replace("dispatched-by: null", "dispatched-by: Adele");
        File.WriteAllText(statePath, stateContent);

        // Release Brian (clear inbox first)
        StoreSessionContextForAgent("Brian");
        var brianRegistry = new AgentRegistry(TestDir);
        brianRegistry.ClearAllUnreadMessages("Brian");
        var releaseCommand = AgentCommand.Create();
        await RunAsync(releaseCommand, "release");

        // Restore Adele's session and try to message Brian
        StoreSessionContext();
        var result = await SendMessageAsync("Brian", "test");

        result.AssertExitCode(2);
        result.AssertStderrContains("dispatched by Adele");
    }

    [Fact]
    public async Task Message_ToInactiveAgent_WithReplyPending_NowFails()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "test-task");

        // Create a reply-pending marker for Adele -> Brian
        var registry = new AgentRegistry(TestDir);
        registry.CreateReplyPendingMarker("Adele", "test-task", "Brian");

        // Brian is not claimed = inactive. Reply-pending no longer bypasses — hard reject.
        var result = await SendMessageAsync("Brian", "Reply to Brian", subject: "test-task");

        result.AssertExitCode(2);
        result.AssertStderrContains("has been released");

        // Marker is preserved so the reply obligation remains visible
        var markers = registry.GetReplyPendingMarkers("Adele");
        Assert.Single(markers);
    }

    [Fact]
    public async Task Message_ToInactiveAgent_WithReplyPending_NoSubject_NowFails()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "test-task");

        var registry = new AgentRegistry(TestDir);
        registry.CreateReplyPendingMarker("Adele", "test-task", "Brian");

        // No subject — still hard reject; reply-pending bypass dropped.
        var result = await SendMessageAsync("Brian", "Reply to Brian");

        result.AssertExitCode(2);
        result.AssertStderrContains("has been released");

        // Marker preserved
        var markers = registry.GetReplyPendingMarkers("Adele");
        Assert.Single(markers);
    }

    [Fact]
    public async Task Message_ToInactiveAgent_WithReplyPending_HintsForceOrRedirect()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "test-task");

        var registry = new AgentRegistry(TestDir);
        registry.CreateReplyPendingMarker("Adele", "test-task", "Brian");

        var result = await SendMessageAsync("Brian", "Reply to Brian", subject: "test-task");

        result.AssertExitCode(2);
        result.AssertStderrContains("--force");
        result.AssertStderrContains("test-task");
    }

    [Fact]
    public async Task Message_ToInactiveAgent_WithoutReplyPending_NoForce()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "test-task");

        // No reply-pending marker, Brian not claimed
        var result = await SendMessageAsync("Brian", "test");

        result.AssertExitCode(2);
        result.AssertStderrContains("has been released");
        Assert.DoesNotContain("--force", result.Stderr);
    }

    [Fact]
    public async Task Message_ToActiveAgent_Succeeds()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "test-task");

        ClaimAgentInSeparateSession("Brian");

        var result = await SendMessageAsync("Brian", "test");

        result.AssertSuccess();
    }

    [Fact]
    public async Task Message_ToInactiveAgent_ForceTrue_Succeeds()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        var result = await SendMessageAsync("Brian", "test", force: true);

        result.AssertSuccess();
    }

    [Fact]
    public async Task GetActiveOversightAgents_ReturnsOnlyWorkingOrchestrators()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        // Use inquisitor — has CanOrchestrate=true and no role-assignment constraints
        await SetRoleAsync("inquisitor", "test-task");

        var registry = new AgentRegistry(TestDir);
        var oversight = registry.GetActiveOversightAgents();

        Assert.Single(oversight);
        Assert.Equal("Adele", oversight[0].Name);

        // Non-oversight roles should not appear
        var active = registry.GetActiveAgents();
        Assert.Single(active);
    }

    [Fact]
    public async Task GetActiveAgents_ReturnsOnlyWorkingAgents()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "test-task");

        // Charlie is not claimed = free
        var registry = new AgentRegistry(TestDir);
        var active = registry.GetActiveAgents();

        // Only Adele should be active
        Assert.Single(active);
        Assert.Equal("Adele", active[0].Name);
    }

    #endregion

    #region Helper Methods

    private async Task<CommandResult> SendMessageAsync(string to, string body, string? subject = null, bool force = false)
    {
        StoreSessionContext();
        var command = MessageCommand.Create();
        var args = new List<string> { "--to", to, "--body", body };
        if (subject != null) { args.Add("--subject"); args.Add(subject); }
        if (force) args.Add("--force");
        return await RunAsync(command, args.ToArray());
    }

    private void CreateMessageFile(string agentName, string fromAgent, string subject, string body, string? id = null)
    {
        var inboxPath = Path.Combine(TestDir, "dydo", "agents", agentName, "inbox");
        Directory.CreateDirectory(inboxPath);

        id ??= Guid.NewGuid().ToString("N")[..8];
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

    private void CreateDispatchFile(string agentName, string fromAgent, string role, string task, string brief)
    {
        var inboxPath = Path.Combine(TestDir, "dydo", "agents", agentName, "inbox");
        Directory.CreateDirectory(inboxPath);

        var id = Guid.NewGuid().ToString("N")[..8];
        var content = $"""
            ---
            id: {id}
            from: {fromAgent}
            role: {role}
            task: {task}
            received: {DateTime.UtcNow:o}
            ---

            # {role.ToUpperInvariant()} Request: {task}

            ## From

            {fromAgent}

            ## Brief

            {brief}
            """;

        File.WriteAllText(Path.Combine(inboxPath, $"{id}-{task}.md"), content);
    }

    private void ClaimAgentInSeparateSession(string agentName)
    {
        var registry = new AgentRegistry(TestDir);
        var otherSession = $"other-session-{agentName}";
        registry.StorePendingSessionId(agentName, otherSession);

        // Temporarily switch session context
        var contextPath = Path.Combine(DydoDir, "agents", ".session-context");
        File.WriteAllText(contextPath, otherSession);

        var agentCmd = AgentCommand.Create();
        var result = RunAsync(agentCmd, "claim", agentName).Result;

        // Restore original session context
        StoreSessionContext();
    }

    private void StoreSessionContextForAgent(string agentName)
    {
        var otherSession = $"other-session-{agentName}";
        var contextPath = Path.Combine(DydoDir, "agents", ".session-context");
        File.WriteAllText(contextPath, otherSession);
    }

    #endregion
}
