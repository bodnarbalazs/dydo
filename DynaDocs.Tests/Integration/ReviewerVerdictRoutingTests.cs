namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;
using DynaDocs.Services;

/// <summary>
/// Integration tests for reviewer-verdict routing.
/// - Item 1: `dydo review complete --status pass` auto-CCs nearest canOrchestrate ancestor
/// - Item 2: better error when messaging a released target (lists subject waiters)
/// - Item 3: soft warning on `dydo msg` when subject doesn't match recipient's active waits
/// </summary>
[Collection("Integration")]
public class ReviewerVerdictRoutingTests : IntegrationTestBase
{
    #region Item 1 — Auto-CC canOrchestrate ancestor on review pass

    [Fact]
    public async Task ReviewCompletePass_CCsNearestCanOrchestrateAncestor()
    {
        await InitProjectAsync("none", "testuser", 4);

        // Charlie is the reviewer — claim as current agent
        await ClaimAgentAsync("Charlie");
        await SetRoleAsync("reviewer", "verdict-task");

        // Wire up the dispatch chain:
        //   Adele (orchestrator) -> Brian (code-writer) -> Charlie (reviewer)
        WriteAgentStateFile("Brian", role: "code-writer", dispatchedBy: "Adele", dispatchedByRole: "orchestrator", status: "working");
        WriteAgentStateFile("Adele", role: "orchestrator", dispatchedBy: null, dispatchedByRole: null, status: "working");
        PatchDispatchedBy("Charlie", "Brian", "code-writer");

        // Task must exist in review-pending state
        var tasksPath = Path.Combine(TestDir, "dydo", "project", "tasks");
        Directory.CreateDirectory(tasksPath);
        File.WriteAllText(Path.Combine(tasksPath, "verdict-task.md"),
            "---\nname: verdict-task\nstatus: review-pending\n---\n");

        var result = await ReviewCompleteAsync("verdict-task", "pass", "LGTM");
        result.AssertSuccess();

        // Dispatcher (Brian) receives the verdict message
        var brianInbox = Path.Combine(TestDir, "dydo/agents/Brian/inbox");
        Assert.True(Directory.Exists(brianInbox), "Brian inbox directory should exist");
        var brianMessages = Directory.GetFiles(brianInbox, "*-msg-*.md");
        Assert.NotEmpty(brianMessages);
        var brianContent = File.ReadAllText(brianMessages[0]);
        Assert.Contains("subject: verdict-task", brianContent);
        Assert.Contains("from: Charlie", brianContent);

        // canOrchestrate ancestor (Adele) is auto-CC'd
        var adeleInbox = Path.Combine(TestDir, "dydo/agents/Adele/inbox");
        Assert.True(Directory.Exists(adeleInbox), "Adele inbox directory should exist");
        var adeleMessages = Directory.GetFiles(adeleInbox, "*-msg-*.md");
        Assert.NotEmpty(adeleMessages);
        var adeleContent = File.ReadAllText(adeleMessages[0]);
        Assert.Contains("subject: verdict-task", adeleContent);
        Assert.Contains("from: Charlie", adeleContent);
    }

    [Fact]
    public async Task ReviewCompletePass_NoOrchestratorInChain_DoesNotCC()
    {
        await InitProjectAsync("none", "testuser", 3);

        await ClaimAgentAsync("Charlie");
        await SetRoleAsync("reviewer", "root-task");

        // Brian is a root code-writer — no dispatcher above him
        WriteAgentStateFile("Brian", role: "code-writer", dispatchedBy: null, dispatchedByRole: null, status: "working");
        PatchDispatchedBy("Charlie", "Brian", "code-writer");

        var tasksPath = Path.Combine(TestDir, "dydo", "project", "tasks");
        Directory.CreateDirectory(tasksPath);
        File.WriteAllText(Path.Combine(tasksPath, "root-task.md"),
            "---\nname: root-task\nstatus: review-pending\n---\n");

        var result = await ReviewCompleteAsync("root-task", "pass");
        result.AssertSuccess();

        // Dispatcher (Brian) still receives the verdict
        var brianMessages = Directory.GetFiles(
            Path.Combine(TestDir, "dydo/agents/Brian/inbox"), "*-msg-*.md");
        Assert.NotEmpty(brianMessages);

        // Adele is NOT CC'd — no canOrchestrate ancestor exists
        var adeleInbox = Path.Combine(TestDir, "dydo/agents/Adele/inbox");
        var adeleMessages = Directory.Exists(adeleInbox)
            ? Directory.GetFiles(adeleInbox, "*-msg-*.md")
            : Array.Empty<string>();
        Assert.Empty(adeleMessages);
    }

    [Fact]
    public async Task ReviewCompletePass_DispatcherReleased_VerdictRedirectsToWorkingAncestor()
    {
        await InitProjectAsync("none", "testuser", 4);

        await ClaimAgentAsync("Charlie");
        await SetRoleAsync("reviewer", "released-dispatcher-task");

        // Adele (orchestrator, working) -> Brian (code-writer, free) -> Charlie (reviewer)
        WriteAgentStateFile("Brian", role: "code-writer", dispatchedBy: "Adele", dispatchedByRole: "orchestrator", status: "free");
        WriteAgentStateFile("Adele", role: "orchestrator", dispatchedBy: null, dispatchedByRole: null, status: "working");
        PatchDispatchedBy("Charlie", "Brian", "code-writer");

        var tasksPath = Path.Combine(TestDir, "dydo", "project", "tasks");
        Directory.CreateDirectory(tasksPath);
        File.WriteAllText(Path.Combine(tasksPath, "released-dispatcher-task.md"),
            "---\nname: released-dispatcher-task\nstatus: review-pending\n---\n");

        var result = await ReviewCompleteAsync("released-dispatcher-task", "pass", "LGTM");
        result.AssertSuccess();

        // Brian (released) gets no inbox write
        var brianInbox = Path.Combine(TestDir, "dydo/agents/Brian/inbox");
        var brianMessages = Directory.Exists(brianInbox)
            ? Directory.GetFiles(brianInbox, "*-msg-*.md")
            : Array.Empty<string>();
        Assert.Empty(brianMessages);

        // Adele (working orchestrator) receives the CC
        var adeleMessages = Directory.GetFiles(
            Path.Combine(TestDir, "dydo/agents/Adele/inbox"), "*-msg-*.md");
        Assert.NotEmpty(adeleMessages);
        var adeleContent = File.ReadAllText(adeleMessages[0]);
        Assert.Contains("subject: released-dispatcher-task", adeleContent);
    }

    [Fact]
    public async Task ReviewCompletePass_DispatcherAndAncestorReleased_NoWrites()
    {
        await InitProjectAsync("none", "testuser", 4);

        await ClaimAgentAsync("Charlie");
        await SetRoleAsync("reviewer", "all-released-task");

        // Both Adele and Brian released — no Working CanOrchestrate ancestor
        WriteAgentStateFile("Brian", role: "code-writer", dispatchedBy: "Adele", dispatchedByRole: "orchestrator", status: "free");
        WriteAgentStateFile("Adele", role: "orchestrator", dispatchedBy: null, dispatchedByRole: null, status: "free");
        PatchDispatchedBy("Charlie", "Brian", "code-writer");

        var tasksPath = Path.Combine(TestDir, "dydo", "project", "tasks");
        Directory.CreateDirectory(tasksPath);
        File.WriteAllText(Path.Combine(tasksPath, "all-released-task.md"),
            "---\nname: all-released-task\nstatus: review-pending\n---\n");

        var result = await ReviewCompleteAsync("all-released-task", "pass");
        result.AssertSuccess();

        // No inbox writes anywhere
        foreach (var name in new[] { "Brian", "Adele" })
        {
            var inbox = Path.Combine(TestDir, "dydo/agents", name, "inbox");
            var messages = Directory.Exists(inbox)
                ? Directory.GetFiles(inbox, "*-msg-*.md")
                : Array.Empty<string>();
            Assert.Empty(messages);
        }

        // Task file still records the review
        var taskContent = File.ReadAllText(Path.Combine(tasksPath, "all-released-task.md"));
        Assert.Contains("PASSED", taskContent);
        Assert.Contains("status: human-reviewed", taskContent);
    }

    [Fact]
    public async Task ReviewCompletePass_AncestorWalkSkipsReleasedIntermediate()
    {
        await InitProjectAsync("none", "testuser", 4);

        await ClaimAgentAsync("Charlie");
        await SetRoleAsync("reviewer", "skip-walk-task");

        // Chain: Adele (orchestrator, working) <- Dexter (orchestrator, working)
        //        <- Brian (code-writer, released) <- Charlie (reviewer)
        // Expected CC target: Dexter — the nearest *Working* CanOrchestrate ancestor.
        WriteAgentStateFile("Brian", role: "code-writer", dispatchedBy: "Dexter", dispatchedByRole: "orchestrator", status: "free");
        WriteAgentStateFile("Dexter", role: "orchestrator", dispatchedBy: "Adele", dispatchedByRole: "orchestrator", status: "working");
        WriteAgentStateFile("Adele", role: "orchestrator", dispatchedBy: null, dispatchedByRole: null, status: "working");
        PatchDispatchedBy("Charlie", "Brian", "code-writer");

        var tasksPath = Path.Combine(TestDir, "dydo", "project", "tasks");
        Directory.CreateDirectory(tasksPath);
        File.WriteAllText(Path.Combine(tasksPath, "skip-walk-task.md"),
            "---\nname: skip-walk-task\nstatus: review-pending\n---\n");

        var result = await ReviewCompleteAsync("skip-walk-task", "pass");
        result.AssertSuccess();

        // Brian (released) gets nothing
        var brianInbox = Path.Combine(TestDir, "dydo/agents/Brian/inbox");
        var brianMessages = Directory.Exists(brianInbox)
            ? Directory.GetFiles(brianInbox, "*-msg-*.md")
            : Array.Empty<string>();
        Assert.Empty(brianMessages);

        // Dexter (nearest Working CanOrchestrate) is the CC target
        var dexterMessages = Directory.GetFiles(
            Path.Combine(TestDir, "dydo/agents/Dexter/inbox"), "*-msg-*.md");
        Assert.NotEmpty(dexterMessages);

        // Adele (further up) is NOT CC'd — the walk stops at the first Working ancestor
        var adeleInbox = Path.Combine(TestDir, "dydo/agents/Adele/inbox");
        var adeleMessages = Directory.Exists(adeleInbox)
            ? Directory.GetFiles(adeleInbox, "*-msg-*.md")
            : Array.Empty<string>();
        Assert.Empty(adeleMessages);
    }

    [Fact]
    public async Task ReviewCompleteFail_DoesNotCCOrchestrator()
    {
        await InitProjectAsync("none", "testuser", 4);

        await ClaimAgentAsync("Charlie");
        await SetRoleAsync("reviewer", "fail-task");

        WriteAgentStateFile("Brian", role: "code-writer", dispatchedBy: "Adele", dispatchedByRole: "orchestrator");
        WriteAgentStateFile("Adele", role: "orchestrator", dispatchedBy: null, dispatchedByRole: null);
        PatchDispatchedBy("Charlie", "Brian", "code-writer");

        var tasksPath = Path.Combine(TestDir, "dydo", "project", "tasks");
        Directory.CreateDirectory(tasksPath);
        File.WriteAllText(Path.Combine(tasksPath, "fail-task.md"),
            "---\nname: fail-task\nstatus: review-pending\n---\n");

        var result = await ReviewCompleteAsync("fail-task", "fail", "Needs rework");
        result.AssertSuccess();

        // Fail path never CCs the orchestrator
        var adeleInbox = Path.Combine(TestDir, "dydo/agents/Adele/inbox");
        var adeleMessages = Directory.Exists(adeleInbox)
            ? Directory.GetFiles(adeleInbox, "*-msg-*.md")
            : Array.Empty<string>();
        Assert.Empty(adeleMessages);
    }

    #endregion

    #region Item 2 — Better error when messaging a released target

    [Fact]
    public async Task SendMessage_ToReleasedTarget_ErrorListsWaitersOnSubject()
    {
        await InitProjectAsync("none", "testuser", 4);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "adele-task");

        // Brian and Charlie are waiting on subject "foo-subject"
        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Brian", "foo-subject", "Adele");
        registry.CreateWaitMarker("Charlie", "foo-subject", "Adele");

        // Dexter is not claimed (released), send to Dexter with subject foo-subject
        var result = await SendMessageAsync("Dexter", "test", subject: "foo-subject");

        result.AssertExitCode(2);
        result.AssertStderrContains("has been released");
        result.AssertStderrContains("Brian");
        result.AssertStderrContains("Charlie");
        result.AssertStderrContains("foo-subject");
    }

    #endregion

    #region Item 3 — Soft warning when subject doesn't match recipient's active waits

    [Fact]
    public async Task SendMessage_SubjectDoesNotMatchActiveWaits_EmitsWarning()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "sender-task");

        // Brian is active and waiting on "bar-subject"
        ClaimAgentInSeparateSession("Brian");
        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Brian", "bar-subject", "Adele");

        // Send with a typo'd subject "fooo-subject"
        var result = await SendMessageAsync("Brian", "test", subject: "fooo-subject");

        // Delivery still succeeds
        result.AssertSuccess();
        var inboxFiles = Directory.GetFiles(
            Path.Combine(TestDir, "dydo/agents/Brian/inbox"), "*-msg-*.md");
        Assert.NotEmpty(inboxFiles);

        // Warning on stderr mentions the sent subject and the recipient's wait subjects
        result.AssertStderrContains("fooo-subject");
        result.AssertStderrContains("bar-subject");
    }

    #endregion

    #region Helpers

    private async Task<CommandResult> ReviewCompleteAsync(string task, string status, string? notes = null)
    {
        StoreSessionContext();
        var command = ReviewCommand.Create();
        var args = new List<string> { "complete", task, "--status", status };
        if (notes != null) { args.Add("--notes"); args.Add(notes); }
        return await RunAsync(command, args.ToArray());
    }

    private async Task<CommandResult> SendMessageAsync(string to, string body, string? subject = null)
    {
        StoreSessionContext();
        var command = MessageCommand.Create();
        var args = new List<string> { "--to", to, "--body", body };
        if (subject != null) { args.Add("--subject"); args.Add(subject); }
        return await RunAsync(command, args.ToArray());
    }

    private void WriteAgentStateFile(string agentName, string? role, string? dispatchedBy, string? dispatchedByRole, string status = "free")
    {
        var workspace = Path.Combine(TestDir, "dydo", "agents", agentName);
        Directory.CreateDirectory(workspace);
        var content = $$"""
            ---
            agent: {{agentName}}
            role: {{role ?? "null"}}
            task: null
            status: {{status}}
            assigned: testuser
            dispatched-by: {{dispatchedBy ?? "null"}}
            dispatched-by-role: {{dispatchedByRole ?? "null"}}
            window-id: null
            auto-close: false
            started: null
            writable-paths: []
            readonly-paths: []
            unread-must-reads: []
            unread-messages: []
            task-role-history: {}
            ---

            # {{agentName}} — Session State
            """;
        File.WriteAllText(Path.Combine(workspace, "state.md"), content);
    }

    private void PatchDispatchedBy(string agentName, string dispatchedBy, string dispatchedByRole)
    {
        var statePath = Path.Combine(TestDir, "dydo", "agents", agentName, "state.md");
        var content = File.ReadAllText(statePath);
        content = System.Text.RegularExpressions.Regex.Replace(content,
            @"dispatched-by: [^\r\n]+", $"dispatched-by: {dispatchedBy}");
        content = System.Text.RegularExpressions.Regex.Replace(content,
            @"dispatched-by-role: [^\r\n]+", $"dispatched-by-role: {dispatchedByRole}");
        File.WriteAllText(statePath, content);
    }

    private void ClaimAgentInSeparateSession(string agentName)
    {
        var registry = new AgentRegistry(TestDir);
        var otherSession = $"other-session-{agentName}";
        registry.StorePendingSessionId(agentName, otherSession);

        var contextPath = Path.Combine(DydoDir, "agents", ".session-context");
        File.WriteAllText(contextPath, otherSession);

        var agentCmd = AgentCommand.Create();
        _ = RunAsync(agentCmd, "claim", agentName).Result;

        StoreSessionContext();
    }

    #endregion
}
