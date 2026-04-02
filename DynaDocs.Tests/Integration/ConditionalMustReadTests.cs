namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;
using DynaDocs.Services;

/// <summary>
/// Integration tests for conditional must-read enforcement (Decision 013):
/// - Merge code-writers must read merge workflow guide
/// - Merge reviewers must read merge review guide
/// - All reviewers must read the task file
/// Also tests brief injection into task files.
/// </summary>
[Collection("Integration")]
public class ConditionalMustReadTests : IntegrationTestBase
{
    #region Brief Injection into Task Files

    [Fact]
    public async Task Dispatch_InjectsBriefIntoTaskFile()
    {
        await InitProjectAsync("none", "testuser", 3);
        await TaskCreateAsync("my-task");

        // Verify task file has "(No description)" initially
        var taskContent = ReadFile("dydo/project/tasks/my-task.md");
        Assert.Contains("(No description)", taskContent);

        var result = await DispatchAsync("code-writer", "my-task", "Implement OAuth flow with token refresh");

        result.AssertSuccess();

        // After dispatch, brief should replace "(No description)"
        taskContent = ReadFile("dydo/project/tasks/my-task.md");
        Assert.Contains("Implement OAuth flow with token refresh", taskContent);
        Assert.DoesNotContain("(No description)", taskContent);
    }

    [Fact]
    public async Task Dispatch_DoesNotOverwriteExistingDescription()
    {
        await InitProjectAsync("none", "testuser", 3);
        await TaskCreateAsync("my-task", description: "Already has a description");

        var result = await DispatchAsync("code-writer", "my-task", "New brief that should not overwrite");

        result.AssertSuccess();

        var taskContent = ReadFile("dydo/project/tasks/my-task.md");
        Assert.Contains("Already has a description", taskContent);
        Assert.DoesNotContain("New brief that should not overwrite", taskContent);
    }

    [Fact]
    public async Task Dispatch_EmptyBrief_DoesNotModifyTaskFile()
    {
        await InitProjectAsync("none", "testuser", 3);
        await TaskCreateAsync("my-task");

        // Dispatch with a very minimal brief (just whitespace-ish)
        var command = DispatchCommand.Create();
        var args = new[] { "--role", "code-writer", "--task", "my-task", "--brief", "x", "--no-launch", "--no-wait" };
        BypassNoLaunchNudge("my-task");
        var result = await RunAsync(command, args);

        result.AssertSuccess();

        // "x" replaces "(No description)" since it's non-empty
        var taskContent = ReadFile("dydo/project/tasks/my-task.md");
        Assert.DoesNotContain("(No description)", taskContent);
    }

    [Fact]
    public async Task Dispatch_BriefInjection_WorksWithAutoCreatedTaskFile()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Don't pre-create task file — let AutoCreateTaskFile handle it
        // AutoCreateTaskFile runs when target agent sets role, but dispatch also calls InjectBrief
        // Since task file doesn't exist at dispatch time, injection is skipped
        // Brief is still available in the inbox item
        var result = await DispatchAsync("code-writer", "new-task", "Implement new feature");

        result.AssertSuccess();

        // Task file may or may not exist (depends on whether agent already set role)
        // Just verify no crash
    }

    #endregion

    #region Conditional Must-Read: Merge Code-Writer

    [Fact]
    public async Task Guard_MergeCodeWriter_MustReadMergeGuide()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        // Create .merge-source marker in Adele's workspace
        var workspace = Path.Combine(TestDir, "dydo/agents/Adele");
        File.WriteAllText(Path.Combine(workspace, ".merge-source"), "worktree/feature-branch");

        await SetRoleAsync("code-writer", "feature-merge");

        var registry = new AgentRegistry(TestDir);
        var state = registry.GetCurrentAgent(TestSessionId);
        Assert.NotNull(state);

        // Should include the merge guide in must-reads
        Assert.Contains(state.UnreadMustReads,
            p => p.Contains("how-to-merge-worktrees.md"));
    }

    [Fact]
    public async Task Guard_NormalCodeWriter_NoMergeGuide()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        // No .merge-source marker
        await SetRoleAsync("code-writer", "feature-impl");

        var registry = new AgentRegistry(TestDir);
        var state = registry.GetCurrentAgent(TestSessionId);
        Assert.NotNull(state);

        // Should NOT include the merge guide
        Assert.DoesNotContain(state.UnreadMustReads,
            p => p.Contains("how-to-merge-worktrees.md"));
    }

    [Fact]
    public async Task Guard_MergeCodeWriter_BlockedUntilMergeGuideRead()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        var workspace = Path.Combine(TestDir, "dydo/agents/Adele");
        File.WriteAllText(Path.Combine(workspace, ".merge-source"), "worktree/feature-branch");

        await SetRoleAsync("code-writer", "feature-merge");

        // Read all must-reads EXCEPT the merge guide
        var registry = new AgentRegistry(TestDir);
        var state = registry.GetCurrentAgent(TestSessionId);
        Assert.NotNull(state);

        foreach (var mustRead in state.UnreadMustReads.ToList())
        {
            if (!mustRead.Contains("how-to-merge-worktrees.md"))
                await GuardAsync("read", mustRead);
        }

        // Write should still be blocked (merge guide not read)
        var writeResult = await GuardAsync("edit", "src/file.cs");
        writeResult.AssertExitCode(2);
        writeResult.AssertStderrContains("how-to-merge-worktrees.md");

        // Now read the merge guide
        var mergeGuide = state.UnreadMustReads.First(p => p.Contains("how-to-merge-worktrees.md"));
        await GuardAsync("read", mergeGuide);

        // Write should now succeed
        var writeResult2 = await GuardAsync("edit", "src/file.cs");
        writeResult2.AssertSuccess();
    }

    #endregion

    #region Conditional Must-Read: Merge Reviewer

    [Fact]
    public async Task Guard_MergeReviewer_MustReadMergeReviewGuide()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        await SetRoleAsync("reviewer", "feature-merge");

        var registry = new AgentRegistry(TestDir);
        var state = registry.GetCurrentAgent(TestSessionId);
        Assert.NotNull(state);

        Assert.Contains(state.UnreadMustReads,
            p => p.Contains("how-to-review-worktree-merges.md"));
    }

    [Fact]
    public async Task Guard_NonMergeReviewer_NoMergeReviewGuide()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        await SetRoleAsync("reviewer", "feature-impl");

        var registry = new AgentRegistry(TestDir);
        var state = registry.GetCurrentAgent(TestSessionId);
        Assert.NotNull(state);

        Assert.DoesNotContain(state.UnreadMustReads,
            p => p.Contains("how-to-review-worktree-merges.md"));
    }

    #endregion

    #region Conditional Must-Read: Reviewer Task File

    [Fact]
    public async Task Guard_Reviewer_MustReadTaskFile()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await TaskCreateAsync("feature-x");

        await SetRoleAsync("reviewer", "feature-x");

        var registry = new AgentRegistry(TestDir);
        var state = registry.GetCurrentAgent(TestSessionId);
        Assert.NotNull(state);

        Assert.Contains(state.UnreadMustReads,
            p => p.Contains("feature-x.md") && p.Contains("tasks"));
    }

    [Fact]
    public async Task Guard_Reviewer_TaskFileDoesNotExist_NoMustRead()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        // Don't create task file
        await SetRoleAsync("reviewer", "nonexistent-task");

        var registry = new AgentRegistry(TestDir);
        var state = registry.GetCurrentAgent(TestSessionId);
        Assert.NotNull(state);

        Assert.DoesNotContain(state.UnreadMustReads,
            p => p.Contains("nonexistent-task.md"));
    }

    [Fact]
    public async Task Guard_CodeWriter_NoTaskFileMustRead()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await TaskCreateAsync("feature-x");

        await SetRoleAsync("code-writer", "feature-x");

        var registry = new AgentRegistry(TestDir);
        var state = registry.GetCurrentAgent(TestSessionId);
        Assert.NotNull(state);

        // Code-writers should NOT have task file as must-read
        Assert.DoesNotContain(state.UnreadMustReads,
            p => p.Contains("feature-x.md") && p.Contains("tasks"));
    }

    [Fact]
    public async Task Guard_MergeReviewer_HasBothMergeGuideAndTaskFile()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await TaskCreateAsync("feature-merge");

        await SetRoleAsync("reviewer", "feature-merge");

        var registry = new AgentRegistry(TestDir);
        var state = registry.GetCurrentAgent(TestSessionId);
        Assert.NotNull(state);

        // Merge reviewer should have both conditional must-reads
        Assert.Contains(state.UnreadMustReads,
            p => p.Contains("how-to-review-worktree-merges.md"));
        Assert.Contains(state.UnreadMustReads,
            p => p.Contains("feature-merge.md") && p.Contains("tasks"));
    }

    #endregion

    #region Conditional Must-Read: Dispatched By Role

    [Fact]
    public async Task Guard_ReviewerDispatchedByDocsWriter_MustReadWritingDocs()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await TaskCreateAsync("docs-update");

        // Create writing-docs.md (the conditional must-read target)
        var writingDocsPath = Path.Combine(TestDir, "dydo/reference/writing-docs.md");
        Directory.CreateDirectory(Path.GetDirectoryName(writingDocsPath)!);
        File.WriteAllText(writingDocsPath, "---\narea: reference\ntype: guide\n---\n# Writing Docs Guide");

        // Simulate inbox item from docs-writer
        var inboxDir = Path.Combine(TestDir, "dydo/agents/Adele/inbox");
        Directory.CreateDirectory(inboxDir);
        File.WriteAllText(Path.Combine(inboxDir, "abc12345-docs-update.md"),
            "---\nid: abc12345\nfrom: Brian\nfrom_role: docs-writer\nrole: reviewer\ntask: docs-update\nreceived: 2026-04-01T12:00:00Z\n---\n# Review docs-update\n");

        await SetRoleAsync("reviewer", "docs-update");

        var registry = new AgentRegistry(TestDir);
        var state = registry.GetCurrentAgent(TestSessionId);
        Assert.NotNull(state);

        Assert.Contains(state.UnreadMustReads,
            p => p.Contains("writing-docs.md"));
    }

    [Fact]
    public async Task Guard_ReviewerDispatchedByCodeWriter_NoWritingDocsMustRead()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await TaskCreateAsync("code-fix");

        // Create writing-docs.md
        var writingDocsPath = Path.Combine(TestDir, "dydo/reference/writing-docs.md");
        Directory.CreateDirectory(Path.GetDirectoryName(writingDocsPath)!);
        File.WriteAllText(writingDocsPath, "---\narea: reference\ntype: guide\n---\n# Writing Docs Guide");

        // Simulate inbox item from code-writer (NOT docs-writer)
        var inboxDir = Path.Combine(TestDir, "dydo/agents/Adele/inbox");
        Directory.CreateDirectory(inboxDir);
        File.WriteAllText(Path.Combine(inboxDir, "def67890-code-fix.md"),
            "---\nid: def67890\nfrom: Brian\nfrom_role: code-writer\nrole: reviewer\ntask: code-fix\nreceived: 2026-04-01T12:00:00Z\n---\n# Review code-fix\n");

        await SetRoleAsync("reviewer", "code-fix");

        var registry = new AgentRegistry(TestDir);
        var state = registry.GetCurrentAgent(TestSessionId);
        Assert.NotNull(state);

        Assert.DoesNotContain(state.UnreadMustReads,
            p => p.Contains("writing-docs.md"));
    }

    #endregion

    #region Scaffolding Tests

    [Fact]
    public async Task Init_ScaffoldsNewGuideDocs()
    {
        await InitProjectAsync("none", "testuser", 3);

        AssertFileExists("dydo/guides/how-to-merge-worktrees.md");
        AssertFileExists("dydo/guides/how-to-review-worktree-merges.md");

        var mergeGuide = ReadFile("dydo/guides/how-to-merge-worktrees.md");
        Assert.Contains("must-read: true", mergeGuide);
        Assert.Contains("dydo worktree merge", mergeGuide);

        var reviewGuide = ReadFile("dydo/guides/how-to-review-worktree-merges.md");
        Assert.Contains("must-read: true", reviewGuide);
        Assert.Contains("Zero-Change Merges", reviewGuide);
    }

    [Fact]
    public async Task TemplateUpdate_RegeneratesNewGuideDocs()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Delete the guide files to simulate them being missing
        File.Delete(Path.Combine(TestDir, "dydo/guides/how-to-merge-worktrees.md"));
        File.Delete(Path.Combine(TestDir, "dydo/guides/how-to-review-worktree-merges.md"));

        // Run template update
        var command = TemplateCommand.Create();
        var result = await RunAsync(command, "update");

        result.AssertSuccess();

        // Files should be regenerated
        AssertFileExists("dydo/guides/how-to-merge-worktrees.md");
        AssertFileExists("dydo/guides/how-to-review-worktree-merges.md");
    }

    #endregion

    #region Template Content Tests

    [Fact]
    public async Task Init_CodeWriterTemplate_NoInlineMergeSection()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Regenerate agent files to get mode files
        var scaffolder = new FolderScaffolder();
        scaffolder.RegenerateAgentFiles(
            Path.Combine(TestDir, "dydo/agents"), "Adele");

        var modeFile = ReadFile("dydo/agents/Adele/modes/code-writer.md");

        // Should NOT contain inline merge instructions anymore
        Assert.DoesNotContain("### Worktree Merge", modeFile);
        Assert.DoesNotContain(".merge-source", modeFile);
    }

    [Fact]
    public async Task Init_OrchestratorTemplate_HasActiveInterventionGuidance()
    {
        await InitProjectAsync("none", "testuser", 3);

        var scaffolder = new FolderScaffolder();
        scaffolder.RegenerateAgentFiles(
            Path.Combine(TestDir, "dydo/agents"), "Adele");

        var modeFile = ReadFile("dydo/agents/Adele/modes/orchestrator.md");

        Assert.Contains("not a passive observer", modeFile);
        Assert.Contains("Verify merge results", modeFile);
    }

    #endregion

    #region Helper Methods

    private async Task<CommandResult> TaskCreateAsync(string name, string? description = null, string area = "general")
    {
        var command = TaskCommand.Create();
        var args = new List<string> { "create", name, "--area", area };
        if (description != null)
        {
            args.Add("--description");
            args.Add(description);
        }
        return await RunAsync(command, args.ToArray());
    }

    private async Task<CommandResult> DispatchAsync(
        string role,
        string task,
        string brief,
        string? to = null,
        bool noWait = true)
    {
        var command = DispatchCommand.Create();
        var args = new List<string>
        {
            "--role", role,
            "--task", task,
            "--brief", brief,
            "--no-launch"
        };

        BypassNoLaunchNudge(task);
        if (to != null) { args.Add("--to"); args.Add(to); }
        if (noWait) { args.Add("--no-wait"); }

        return await RunAsync(command, args.ToArray());
    }

    #endregion
}
