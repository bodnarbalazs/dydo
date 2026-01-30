namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;

/// <summary>
/// Integration tests for dispatch command --to and --escalate flags.
/// </summary>
[Collection("Integration")]
public class DispatchCommandTests : IntegrationTestBase
{
    #region --to Success Cases

    [Fact]
    public async Task Dispatch_ToValidAgent_DispatchesToSpecifiedAgent()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Implement feature", to: "Brian");

        result.AssertSuccess();
        result.AssertStdoutContains("Brian");

        // Verify inbox file created for Brian specifically
        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Brian/inbox"), "*.md");
        Assert.True(inboxFiles.Length > 0, "Inbox item should be created for Brian");

        // Verify NOT created for Adele (first alphabetically)
        var adeleInbox = Path.Combine(TestDir, "dydo/agents/Adele/inbox");
        if (Directory.Exists(adeleInbox))
        {
            var adeleFiles = Directory.GetFiles(adeleInbox, "*.md");
            Assert.Empty(adeleFiles);
        }
    }

    [Fact]
    public async Task Dispatch_WithoutTo_AutoSelectsFirstFreeAlphabetically()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Implement feature");

        result.AssertSuccess();
        result.AssertStdoutContains("Adele"); // First alphabetically

        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Adele/inbox"), "*.md");
        Assert.True(inboxFiles.Length > 0);
    }

    #endregion

    #region --to Error Cases

    [Fact]
    public async Task Dispatch_ToNonExistentAgent_Fails()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Brief", to: "Zorro");

        result.AssertExitCode(2);
        result.AssertStderrContains("does not exist");
    }

    [Fact]
    public async Task Dispatch_ToAgentAssignedToDifferentHuman_Fails()
    {
        // Init project with alice's agents
        await InitProjectAsync("none", "alice", 3);

        // Switch to bob's context
        SetHuman("bob");

        var result = await DispatchAsync("code-writer", "my-task", "Brief", to: "Adele");

        result.AssertExitCode(2);
        result.AssertStderrContains("not assigned to you");
    }

    [Fact]
    public async Task Dispatch_ToBusyAgent_Fails()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Claim Adele (makes it Working status)
        await ClaimAgentAsync("Adele");

        var result = await DispatchAsync("code-writer", "my-task", "Brief", to: "Adele");

        result.AssertExitCode(2);
        result.AssertStderrContains("not free");
    }

    #endregion

    #region --escalate Tests

    [Fact]
    public async Task Dispatch_WithEscalate_SetsEscalatedFlag()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Urgent fix needed", escalate: true);

        result.AssertSuccess();
        result.AssertStdoutContains("[ESCALATED]");

        // Verify inbox file contains escalation fields
        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Adele/inbox"), "*.md");
        Assert.True(inboxFiles.Length > 0);

        var content = File.ReadAllText(inboxFiles[0]);
        Assert.Contains("escalated: true", content);
        Assert.Contains("escalated_at:", content);
    }

    [Fact]
    public async Task Dispatch_WithEscalate_InboxHeaderShowsEscalated()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "urgent-fix", "Fix this now", escalate: true);

        result.AssertSuccess();

        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Adele/inbox"), "*.md");
        var content = File.ReadAllText(inboxFiles[0]);

        // Header should have ESCALATED prefix
        Assert.Contains("# ESCALATED CODE-WRITER Request: urgent-fix", content);
    }

    [Fact]
    public async Task Dispatch_WithoutEscalate_NoEscalationInFile()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "normal-task", "Normal work");

        result.AssertSuccess();

        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Adele/inbox"), "*.md");
        var content = File.ReadAllText(inboxFiles[0]);

        Assert.DoesNotContain("escalated:", content);
        Assert.DoesNotContain("ESCALATED", content);
    }

    #endregion

    #region Inbox Display Tests

    [Fact]
    public async Task InboxShow_DisplaysEscalatedIndicator()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Create an escalated inbox item
        CreateEscalatedInboxItem("Adele", "Brian", "code-writer", "urgent-task", "Urgent work");

        // Claim Adele to view inbox
        await ClaimAgentAsync("Adele");

        var result = await InboxShowAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("[ESCALATED]");
    }

    [Fact]
    public async Task InboxShow_NonEscalatedItem_NoIndicator()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Create a non-escalated inbox item
        CreateInboxItem("Adele", "Brian", "code-writer", "normal-task", "Normal work");

        await ClaimAgentAsync("Adele");

        var result = await InboxShowAsync();

        result.AssertSuccess();
        Assert.DoesNotContain("[ESCALATED]", result.Stdout);
    }

    #endregion

    #region Combined Flag Tests

    [Fact]
    public async Task Dispatch_ToValidAgent_WithEscalate_BothWork()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "critical-fix", "Critical bug", to: "Brian", escalate: true);

        result.AssertSuccess();
        result.AssertStdoutContains("Brian");
        result.AssertStdoutContains("[ESCALATED]");

        // Verify dispatched to Brian
        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Brian/inbox"), "*.md");
        Assert.True(inboxFiles.Length > 0);

        var content = File.ReadAllText(inboxFiles[0]);
        Assert.Contains("escalated: true", content);
    }

    [Fact]
    public async Task Dispatch_ToInvalidAgent_WithEscalate_FailsBeforeEscalation()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Brief", to: "InvalidAgent", escalate: true);

        result.AssertExitCode(2);
        result.AssertStderrContains("does not exist");

        // Verify no inbox files created anywhere
        foreach (var agentName in new[] { "Adele", "Brian", "Charlie" })
        {
            var inboxPath = Path.Combine(TestDir, "dydo/agents", agentName, "inbox");
            if (Directory.Exists(inboxPath))
            {
                var files = Directory.GetFiles(inboxPath, "*.md");
                Assert.Empty(files);
            }
        }
    }

    #endregion

    #region Helper Methods

    private async Task<CommandResult> DispatchAsync(
        string role,
        string task,
        string brief,
        string? files = null,
        string? contextFile = null,
        string? to = null,
        bool escalate = false)
    {
        var command = DispatchCommand.Create();
        var args = new List<string>
        {
            "--role", role,
            "--task", task,
            "--brief", brief,
            "--no-launch"
        };

        if (files != null) { args.Add("--files"); args.Add(files); }
        if (contextFile != null) { args.Add("--context-file"); args.Add(contextFile); }
        if (to != null) { args.Add("--to"); args.Add(to); }
        if (escalate) { args.Add("--escalate"); }

        return await RunAsync(command, args.ToArray());
    }

    private async Task<CommandResult> InboxShowAsync()
    {
        var command = InboxCommand.Create();
        return await RunAsync(command, "show");
    }

    private void CreateInboxItem(string agentName, string fromAgent, string role, string task, string brief)
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

    private void CreateEscalatedInboxItem(string agentName, string fromAgent, string role, string task, string brief)
    {
        var inboxPath = Path.Combine(TestDir, "dydo", "agents", agentName, "inbox");
        Directory.CreateDirectory(inboxPath);

        var id = Guid.NewGuid().ToString("N")[..8];
        var escalatedAt = DateTime.UtcNow;
        var content = $"""
            ---
            id: {id}
            from: {fromAgent}
            role: {role}
            task: {task}
            received: {DateTime.UtcNow:o}
            escalated: true
            escalated_at: {escalatedAt:o}
            ---

            # ESCALATED {role.ToUpperInvariant()} Request: {task}

            ## From

            {fromAgent}

            ## Brief

            {brief}
            """;

        File.WriteAllText(Path.Combine(inboxPath, $"{id}-{task}.md"), content);
    }

    #endregion
}
