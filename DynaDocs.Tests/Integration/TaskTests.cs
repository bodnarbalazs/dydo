namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;

/// <summary>
/// Integration tests for task management commands:
/// task create, list, ready-for-review, approve, reject.
/// </summary>
[Collection("Integration")]
public class TaskTests : IntegrationTestBase
{
    #region Task Create

    [Fact]
    public async Task Task_Create_CreatesFile()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await TaskCreateAsync("my-feature", area: "backend");

        result.AssertSuccess();
        result.AssertStdoutContains("Created task");
        AssertFileExists("dydo/project/tasks/my-feature.md");
        AssertFileContains("dydo/project/tasks/my-feature.md", "area: backend");
    }

    [Fact]
    public async Task Task_Create_WithDescription()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await TaskCreateAsync("auth-fix", "Fix authentication bug in login", area: "backend");

        result.AssertSuccess();
        AssertFileContains("dydo/project/tasks/auth-fix.md", "Fix authentication bug");
    }

    [Fact]
    public async Task Task_Create_AssignsCurrentAgent()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        var result = await TaskCreateAsync("my-task", area: "general");

        result.AssertSuccess();
        AssertFileContains("dydo/project/tasks/my-task.md", "assigned: Adele");
    }

    [Fact]
    public async Task Task_Create_WithSpecialChars_SanitizesFilename()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await TaskCreateAsync("Review: Auth & Email", area: "general");

        result.AssertSuccess();
        result.AssertStdoutContains("sanitized");

        // File should use sanitized name
        AssertFileExists("dydo/project/tasks/Review- Auth & Email.md");

        // Original name preserved in content
        AssertFileContains("dydo/project/tasks/Review- Auth & Email.md", "Review: Auth & Email");
    }

    [Fact]
    public async Task Task_Create_DuplicateFails()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("duplicate-task", area: "general");

        var result = await TaskCreateAsync("duplicate-task", area: "general");

        result.AssertExitCode(2);
        result.AssertStderrContains("already exists");
    }

    [Fact]
    public async Task Task_Create_FailsWhenChangelogExistsForToday()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Create and approve a task so it becomes a changelog entry for today
        await TaskCreateAsync("collision-task", area: "backend");
        await TaskReadyForReviewAsync("collision-task", "Done");
        await TaskApproveAsync("collision-task");

        // Creating a new task with the same name should fail
        var result = await TaskCreateAsync("collision-task", area: "backend");

        result.AssertExitCode(2);
        result.AssertStderrContains("changelog entry named 'collision-task' already exists for today");
    }

    [Fact]
    public async Task Task_Create_WithoutArea_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);

        // System.CommandLine will reject the command since --area is required
        var command = TaskCommand.Create();
        var result = await RunAsync(command, "create", "no-area-task");

        // Required option missing produces a non-zero exit code
        Assert.True(result.HasError, "Expected error when --area is omitted");
    }

    [Fact]
    public async Task Task_Create_InvalidArea_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await TaskCreateAsync("bad-area-task", area: "invalid-area");

        result.AssertExitCode(2);
        result.AssertStderrContains("Invalid area");
    }

    #endregion

    #region Task List

    [Fact]
    public async Task Task_List_ShowsTasks()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("task-one", area: "backend");
        await TaskCreateAsync("task-two", area: "frontend");

        var result = await TaskListAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("task-one");
        result.AssertStdoutContains("task-two");
    }

    [Fact]
    public async Task Task_List_Empty_ShowsMessage()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await TaskListAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("No tasks found");
    }

    [Fact]
    public async Task Task_List_NeedsReview_FiltersCorrectly()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("pending-task", area: "general");
        await TaskCreateAsync("review-task", area: "general");
        await TaskReadyForReviewAsync("review-task", "Ready for review");

        var result = await TaskListAsync(needsReview: true);

        result.AssertSuccess();
        result.AssertStdoutContains("review-task");
        Assert.DoesNotContain("pending-task", result.Stdout);
    }

    #endregion

    #region Task Ready For Review

    [Fact]
    public async Task Task_ReadyForReview_UpdatesStatus()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("feature-x", area: "backend");

        var result = await TaskReadyForReviewAsync("feature-x", "Feature complete, tests pass");

        result.AssertSuccess();
        result.AssertStdoutContains("ready for review");
        AssertFileContains("dydo/project/tasks/feature-x.md", "status: review-pending");
        AssertFileContains("dydo/project/tasks/feature-x.md", "Feature complete, tests pass");
    }

    [Fact]
    public async Task Task_ReadyForReview_WithoutSummary_GivesHelpfulError()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("no-summary-task", area: "backend");

        // Call ready-for-review without --summary
        var command = TaskCommand.Create();
        var result = await RunAsync(command, "ready-for-review", "no-summary-task");

        result.AssertExitCode(2);
        result.AssertStderrContains("--summary is required");
        result.AssertStderrContains("Brief description of completed work");
    }

    [Fact]
    public async Task Task_ReadyForReview_WithEmptySummary_GivesHelpfulError()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("empty-summary-task", area: "backend");

        // Call ready-for-review with --summary "" (empty string)
        var command = TaskCommand.Create();
        var result = await RunAsync(command, "ready-for-review", "empty-summary-task", "--summary", "");

        result.AssertExitCode(2);
        result.AssertStderrContains("--summary is required");
    }

    [Fact]
    public async Task Task_ReadyForReview_WithWhitespaceSummary_GivesHelpfulError()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("ws-summary-task", area: "backend");

        // Call ready-for-review with --summary "   " (whitespace only)
        var command = TaskCommand.Create();
        var result = await RunAsync(command, "ready-for-review", "ws-summary-task", "--summary", "   ");

        result.AssertExitCode(2);
        result.AssertStderrContains("--summary is required");
    }

    [Fact]
    public async Task Task_ReadyForReview_NotFound_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await TaskReadyForReviewAsync("nonexistent", "Summary");

        result.AssertExitCode(2);
        result.AssertStderrContains("not found");
    }

    #endregion

    #region Task Approve/Reject

    [Fact]
    public async Task Task_Approve_MovesToChangelog()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("approved-task", area: "backend");
        await TaskReadyForReviewAsync("approved-task", "Done");

        var result = await TaskApproveAsync("approved-task");

        result.AssertSuccess();
        result.AssertStdoutContains("approved");
        result.AssertStdoutContains("Changelog entry created");

        // Original task file should be gone
        AssertFileNotExists("dydo/project/tasks/approved-task.md");

        // Changelog entry should exist
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var year = DateTime.UtcNow.ToString("yyyy");
        AssertFileExists($"dydo/project/changelog/{year}/{today}/approved-task.md");
    }

    [Fact]
    public async Task Task_Approve_WithNotes()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("good-task", area: "frontend");
        await TaskReadyForReviewAsync("good-task", "Implemented feature");

        var result = await TaskApproveAsync("good-task", "Great work!");

        result.AssertSuccess();

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var year = DateTime.UtcNow.ToString("yyyy");
        AssertFileContains($"dydo/project/changelog/{year}/{today}/good-task.md", "Great work!");
    }

    [Fact]
    public async Task Task_Approve_GeneratesChangelogFrontmatter()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("fm-task", area: "backend");
        await TaskReadyForReviewAsync("fm-task", "Done");

        await TaskApproveAsync("fm-task");

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var year = DateTime.UtcNow.ToString("yyyy");
        var changelogPath = $"dydo/project/changelog/{year}/{today}/fm-task.md";
        AssertFileContains(changelogPath, "type: changelog");
        AssertFileContains(changelogPath, $"date: {today}");
        AssertFileContains(changelogPath, "area: backend");

        // Task-specific fields should be removed
        var content = ReadFile(changelogPath);
        Assert.DoesNotContain("name: fm-task", content);
        Assert.DoesNotContain("status:", content);
        Assert.DoesNotContain("assigned:", content);
    }

    [Fact]
    public async Task Task_Approve_CreatesChangelogDirectoryStructure()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("dir-task", area: "general");
        await TaskReadyForReviewAsync("dir-task", "Done");

        await TaskApproveAsync("dir-task");

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var year = DateTime.UtcNow.ToString("yyyy");

        // Hub files should be created
        AssertFileExists("dydo/project/changelog/_index.md");
        AssertFileExists($"dydo/project/changelog/{year}/_index.md");
        AssertFileExists($"dydo/project/changelog/{year}/{today}/_index.md");

        // Hub files should have proper frontmatter
        AssertFileContains("dydo/project/changelog/_index.md", "type: hub");
        AssertFileContains($"dydo/project/changelog/{year}/_index.md", "type: hub");

        // Date index should link to the task
        AssertFileContains($"dydo/project/changelog/{year}/{today}/_index.md", "dir-task.md");
    }

    [Fact]
    public async Task Task_Approve_IncludesAuditFileChanges()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "audit-task");
        await ReadMustReadsAsync();

        // Mark task ready so it can be approved
        await TaskReadyForReviewAsync("audit-task", "Done with file changes");

        // Simulate real file operations through the guard — this creates actual audit entries
        var writeResult = await GuardAsync("write", "src/Services/AuthService.cs");
        writeResult.AssertSuccess();
        var editResult = await GuardAsync("edit", "src/Models/User.cs");
        editResult.AssertSuccess();
        var deleteResult = await GuardAsync("delete", "src/Legacy/OldAuth.cs");
        deleteResult.AssertSuccess();

        var result = await TaskApproveAsync("audit-task");

        result.AssertSuccess();

        // Console output should show file change prefixes
        result.AssertStdoutContains("+ src/Services/AuthService.cs");
        result.AssertStdoutContains("~ src/Models/User.cs");
        result.AssertStdoutContains("- src/Legacy/OldAuth.cs");

        // Changelog entry should contain the Files Changed section
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var thisYear = DateTime.UtcNow.ToString("yyyy");
        var changelogPath = $"dydo/project/changelog/{thisYear}/{today}/audit-task.md";
        AssertFileContains(changelogPath, "src/Services/AuthService.cs — Created");
        AssertFileContains(changelogPath, "src/Models/User.cs — Modified");
        AssertFileContains(changelogPath, "src/Legacy/OldAuth.cs — Deleted");
    }

    [Fact]
    public async Task Task_Approve_MultipleOnSameDate_LinksCorrectly()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("first-task", area: "backend");
        await TaskCreateAsync("second-task", area: "frontend");
        await TaskReadyForReviewAsync("first-task", "First done");
        await TaskReadyForReviewAsync("second-task", "Second done");

        await TaskApproveAsync("first-task");
        await TaskApproveAsync("second-task");

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var year = DateTime.UtcNow.ToString("yyyy");

        // Both changelog entries should exist
        AssertFileExists($"dydo/project/changelog/{year}/{today}/first-task.md");
        AssertFileExists($"dydo/project/changelog/{year}/{today}/second-task.md");

        // Date _index.md should link to both tasks
        var dateIndex = ReadFile($"dydo/project/changelog/{year}/{today}/_index.md");
        Assert.Contains("first-task.md", dateIndex);
        Assert.Contains("second-task.md", dateIndex);

        // Year _index.md should have only one link to the date folder (not duplicated)
        var yearIndex = ReadFile($"dydo/project/changelog/{year}/_index.md");
        var dateLinks = yearIndex.Split('\n').Count(l => l.Contains($"{today}/"));
        Assert.Equal(1, dateLinks);
    }

    [Fact]
    public async Task Task_Approve_HubsHaveAutoGeneratedComment()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("comment-task", area: "backend");
        await TaskReadyForReviewAsync("comment-task", "Done");

        await TaskApproveAsync("comment-task");

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var year = DateTime.UtcNow.ToString("yyyy");

        AssertFileContains($"dydo/project/changelog/{year}/{today}/_index.md", "<!-- Auto-generated");
        AssertFileContains($"dydo/project/changelog/{year}/_index.md", "<!-- Auto-generated");
        AssertFileContains("dydo/project/changelog/_index.md", "<!-- Auto-generated");
    }

    [Fact]
    public async Task Task_Approve_DateHubHasContentsSection()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("contents-task", area: "general");
        await TaskReadyForReviewAsync("contents-task", "Done");

        await TaskApproveAsync("contents-task");

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var year = DateTime.UtcNow.ToString("yyyy");

        var dateHub = ReadFile($"dydo/project/changelog/{year}/{today}/_index.md");
        Assert.Contains("## Contents", dateHub);
        Assert.Contains("contents-task.md", dateHub);
    }

    [Fact]
    public async Task Task_Approve_YearHubLinksToDateSubfolder()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("subfolder-task", area: "general");
        await TaskReadyForReviewAsync("subfolder-task", "Done");

        await TaskApproveAsync("subfolder-task");

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var year = DateTime.UtcNow.ToString("yyyy");

        var yearHub = ReadFile($"dydo/project/changelog/{year}/_index.md");
        Assert.Contains("## Subfolders", yearHub);
        Assert.Contains($"{today}/_index.md", yearHub);
    }

    [Fact]
    public async Task Task_Approve_TopHubLinksToYearSubfolder()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("top-link-task", area: "general");
        await TaskReadyForReviewAsync("top-link-task", "Done");

        await TaskApproveAsync("top-link-task");

        var year = DateTime.UtcNow.ToString("yyyy");

        var topHub = ReadFile("dydo/project/changelog/_index.md");
        Assert.Contains("## Subfolders", topHub);
        Assert.Contains($"{year}/_index.md", topHub);
    }

    [Fact]
    public async Task Task_Approve_DoesNotClobberProjectHub()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Capture the project hub content before approval
        var projectHubBefore = ReadFile("dydo/project/_index.md");

        await TaskCreateAsync("clobber-task", area: "general");
        await TaskReadyForReviewAsync("clobber-task", "Done");
        await TaskApproveAsync("clobber-task");

        // Project hub should be unchanged — approval only touches changelog hubs
        var projectHubAfter = ReadFile("dydo/project/_index.md");
        Assert.Equal(projectHubBefore, projectHubAfter);
    }

    [Fact]
    public async Task Task_Approve_OutputDoesNotSuggestRunningFix()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("no-fix-task", area: "general");
        await TaskReadyForReviewAsync("no-fix-task", "Done");

        var result = await TaskApproveAsync("no-fix-task");

        result.AssertSuccess();
        Assert.DoesNotContain("dydo fix", result.Stdout);
    }

    [Fact]
    public async Task Task_Approve_FailsWhenChangelogAlreadyExistsForToday()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Approve a first task to create a changelog entry
        await TaskCreateAsync("dup-changelog", area: "backend");
        await TaskReadyForReviewAsync("dup-changelog", "Done");
        await TaskApproveAsync("dup-changelog");

        // Manually create a second task file with the same name (bypassing the create guard)
        var tasksPath = Path.Combine(TestDir, "dydo", "project", "tasks");
        Directory.CreateDirectory(tasksPath);
        File.WriteAllText(Path.Combine(tasksPath, "dup-changelog.md"),
            "---\narea: backend\nname: dup-changelog\nstatus: review-pending\ncreated: 2026-01-01T00:00:00Z\nassigned: unassigned\n---\n\n# Task: dup-changelog\n");

        // Approving should fail because the changelog entry already exists
        var result = await TaskApproveAsync("dup-changelog");

        result.AssertExitCode(2);
        result.AssertStderrContains("changelog entry named 'dup-changelog' already exists for today");
    }

    [Fact]
    public async Task Task_ApproveAll_ApprovesMultipleTasks()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("task-a", area: "backend");
        await TaskCreateAsync("task-b", area: "frontend");
        await TaskCreateAsync("task-c", area: "general");
        await TaskReadyForReviewAsync("task-a", "Done A");
        await TaskReadyForReviewAsync("task-b", "Done B");
        await TaskReadyForReviewAsync("task-c", "Done C");

        var result = await TaskApproveAsync("*");

        result.AssertSuccess();
        result.AssertStdoutContains("Approved 3 task(s).");

        // Original task files should be gone
        AssertFileNotExists("dydo/project/tasks/task-a.md");
        AssertFileNotExists("dydo/project/tasks/task-b.md");
        AssertFileNotExists("dydo/project/tasks/task-c.md");

        // Changelog entries should exist
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var year = DateTime.UtcNow.ToString("yyyy");
        AssertFileExists($"dydo/project/changelog/{year}/{today}/task-a.md");
        AssertFileExists($"dydo/project/changelog/{year}/{today}/task-b.md");
        AssertFileExists($"dydo/project/changelog/{year}/{today}/task-c.md");
    }

    [Fact]
    public async Task Task_ApproveAll_WithNotes()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("note-a", area: "backend");
        await TaskCreateAsync("note-b", area: "frontend");
        await TaskReadyForReviewAsync("note-a", "Done A");
        await TaskReadyForReviewAsync("note-b", "Done B");

        var result = await TaskApproveAsync("*", "Batch approved");

        result.AssertSuccess();

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var year = DateTime.UtcNow.ToString("yyyy");
        AssertFileContains($"dydo/project/changelog/{year}/{today}/note-a.md", "Batch approved");
        AssertFileContains($"dydo/project/changelog/{year}/{today}/note-b.md", "Batch approved");
    }

    [Fact]
    public async Task Task_ApproveAll_NoTasks_Succeeds()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await TaskApproveAsync("*");

        result.AssertSuccess();
        result.AssertStdoutContains("No tasks to approve");
    }

    [Fact]
    public async Task Task_ApproveAll_SkipsUnderscorePrefixedFiles()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("real-task", area: "backend");
        await TaskReadyForReviewAsync("real-task", "Done");

        // Manually create an underscore-prefixed file in the tasks directory
        var tasksPath = Path.Combine(TestDir, "dydo", "project", "tasks");
        File.WriteAllText(Path.Combine(tasksPath, "_template.md"), "---\narea: general\n---\n\nTemplate content");

        var result = await TaskApproveAsync("*");

        result.AssertSuccess();
        result.AssertStdoutContains("Approved 1 task(s).");

        // The underscore-prefixed file should still exist
        Assert.True(File.Exists(Path.Combine(tasksPath, "_template.md")));
    }

    [Fact]
    public async Task Task_ApproveAll_WithAllFlag_ApprovesMultipleTasks()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("flag-a", area: "backend");
        await TaskCreateAsync("flag-b", area: "frontend");
        await TaskReadyForReviewAsync("flag-a", "Done A");
        await TaskReadyForReviewAsync("flag-b", "Done B");

        var result = await TaskApproveAsync(all: true);

        result.AssertSuccess();
        result.AssertStdoutContains("Approved 2 task(s).");
        AssertFileNotExists("dydo/project/tasks/flag-a.md");
        AssertFileNotExists("dydo/project/tasks/flag-b.md");
    }

    [Fact]
    public async Task Task_ApproveAll_ShortAlias_Works()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("alias-task", area: "general");
        await TaskReadyForReviewAsync("alias-task", "Done");

        var command = TaskCommand.Create();
        var result = await RunAsync(command, "approve", "-a");

        result.AssertSuccess();
        result.AssertStdoutContains("Approved 1 task(s).");
    }

    [Fact]
    public async Task Task_ApproveAll_WithAllFlagAndNotes()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("noted-a", area: "backend");
        await TaskCreateAsync("noted-b", area: "frontend");
        await TaskReadyForReviewAsync("noted-a", "Done A");
        await TaskReadyForReviewAsync("noted-b", "Done B");

        var result = await TaskApproveAsync(all: true, notes: "Batch approved");

        result.AssertSuccess();

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var year = DateTime.UtcNow.ToString("yyyy");
        AssertFileContains($"dydo/project/changelog/{year}/{today}/noted-a.md", "Batch approved");
        AssertFileContains($"dydo/project/changelog/{year}/{today}/noted-b.md", "Batch approved");
    }

    [Fact]
    public async Task Task_Approve_NoArgsNoAll_GivesError()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await TaskApproveAsync();

        result.AssertExitCode(2);
        result.AssertStderrContains("Specify a task name or use --all");
    }

    [Fact]
    public async Task Task_Approve_DoesNotCompactByDefault()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("no-compact-task", area: "general");
        await TaskReadyForReviewAsync("no-compact-task", "Done");

        // Create audit files with inline snapshots
        var year = DateTime.UtcNow.ToString("yyyy");
        var auditDir = Path.Combine(TestDir, "dydo", "_system", "audit", year);
        Directory.CreateDirectory(auditDir);

        var snapshot = new DynaDocs.Models.ProjectSnapshot
        {
            GitCommit = "abc123",
            Files = ["src/file1.cs", "src/file2.cs", "src/file3.cs"],
            Folders = ["src/"],
            DocLinks = new() { ["a.md"] = ["b.md"] }
        };

        for (var i = 0; i < 3; i++)
        {
            var session = new DynaDocs.Models.AuditSession
            {
                SessionId = $"session-{i}",
                AgentName = "Adele",
                Started = DateTime.UtcNow.AddHours(-i),
                Events = [],
                Snapshot = snapshot
            };
            var json = System.Text.Json.JsonSerializer.Serialize(session, new System.Text.Json.JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
            File.WriteAllText(
                Path.Combine(auditDir, $"{year}-01-01-session-{i}.json"), json);
        }

        // Approve — should NOT trigger compaction (counter starts at 0, interval is 20)
        var result = await TaskApproveAsync("no-compact-task");

        result.AssertSuccess();
        Assert.DoesNotContain("compacted", result.Stdout);

        // Verify audit files are untouched (still have inline snapshots)
        var sessionFiles = Directory.GetFiles(auditDir, "*.json")
            .Where(f => !Path.GetFileName(f).StartsWith("_baseline-"))
            .ToList();
        foreach (var file in sessionFiles)
        {
            var content = File.ReadAllText(file);
            Assert.DoesNotContain("snapshot_ref", content);
        }
    }

    [Fact]
    public async Task Task_Compact_CompactsAuditSnapshots()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Create audit files with inline snapshots
        var year = DateTime.UtcNow.ToString("yyyy");
        var auditDir = Path.Combine(TestDir, "dydo", "_system", "audit", year);
        Directory.CreateDirectory(auditDir);

        var snapshot = new DynaDocs.Models.ProjectSnapshot
        {
            GitCommit = "abc123",
            Files = ["src/file1.cs", "src/file2.cs", "src/file3.cs"],
            Folders = ["src/"],
            DocLinks = new() { ["a.md"] = ["b.md"] }
        };

        for (var i = 0; i < 3; i++)
        {
            var session = new DynaDocs.Models.AuditSession
            {
                SessionId = $"session-{i}",
                AgentName = "Adele",
                Started = DateTime.UtcNow.AddHours(-i),
                Events = [],
                Snapshot = snapshot
            };
            var json = System.Text.Json.JsonSerializer.Serialize(session, new System.Text.Json.JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
            File.WriteAllText(
                Path.Combine(auditDir, $"{year}-01-01-session-{i}.json"), json);
        }

        var result = await TaskCompactAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("compacted");

        // Verify baseline was created
        var baselines = Directory.GetFiles(auditDir, "_baseline-*.json");
        Assert.Single(baselines);

        // Verify sessions now use snapshot_ref instead of inline snapshot
        var sessionFiles = Directory.GetFiles(auditDir, "*.json")
            .Where(f => !Path.GetFileName(f).StartsWith("_baseline-"))
            .ToList();
        foreach (var file in sessionFiles)
        {
            var content = File.ReadAllText(file);
            Assert.Contains("snapshot_ref", content);
            Assert.DoesNotContain("\"snapshot\"", content);
        }
    }

    [Fact]
    public async Task Task_Compact_NothingToCompact()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await TaskCompactAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Nothing to compact");
    }

    [Fact]
    public async Task Task_Compact_DirectoryExistsButNoSessions()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Create the audit year directory but leave it empty — no session files
        var year = DateTime.UtcNow.ToString("yyyy");
        var auditDir = Path.Combine(TestDir, "dydo", "_system", "audit", year);
        Directory.CreateDirectory(auditDir);

        var result = await TaskCompactAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Nothing to compact");
    }

    [Fact]
    public async Task Task_Approve_AutoCompact_TriggersAtInterval()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Set autoCompactInterval to 2
        var configPath = Path.Combine(TestDir, "dydo.json");
        var configJson = File.ReadAllText(configPath);
        var config = System.Text.Json.JsonSerializer.Deserialize(configJson,
            DynaDocs.Serialization.DydoConfigJsonContext.Default.DydoConfig)!;
        config.Tasks.AutoCompactInterval = 2;
        var updatedJson = System.Text.Json.JsonSerializer.Serialize(config,
            DynaDocs.Serialization.DydoConfigJsonContext.Default.DydoConfig);
        File.WriteAllText(configPath, updatedJson);

        // Create audit files with inline snapshots
        var year = DateTime.UtcNow.ToString("yyyy");
        var auditDir = Path.Combine(TestDir, "dydo", "_system", "audit", year);
        Directory.CreateDirectory(auditDir);

        var snapshot = new DynaDocs.Models.ProjectSnapshot
        {
            GitCommit = "abc123",
            Files = ["src/file1.cs"],
            Folders = ["src/"],
            DocLinks = new() { ["a.md"] = ["b.md"] }
        };

        for (var i = 0; i < 2; i++)
        {
            var session = new DynaDocs.Models.AuditSession
            {
                SessionId = $"session-{i}",
                AgentName = "Adele",
                Started = DateTime.UtcNow.AddHours(-i),
                Events = [],
                Snapshot = snapshot
            };
            var json = System.Text.Json.JsonSerializer.Serialize(session, new System.Text.Json.JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
            File.WriteAllText(
                Path.Combine(auditDir, $"{year}-01-01-session-{i}.json"), json);
        }

        // First approve — counter goes to 1, no compaction
        await TaskCreateAsync("auto-a", area: "general");
        await TaskReadyForReviewAsync("auto-a", "Done");
        var result1 = await TaskApproveAsync("auto-a");
        result1.AssertSuccess();
        Assert.DoesNotContain("Auto-compacting", result1.Stdout);

        // Second approve — counter hits 2, compaction triggers
        await TaskCreateAsync("auto-b", area: "general");
        await TaskReadyForReviewAsync("auto-b", "Done");
        var result2 = await TaskApproveAsync("auto-b");
        result2.AssertSuccess();
        result2.AssertStdoutContains("Auto-compacting");
        result2.AssertStdoutContains("compacted");
    }

    [Fact]
    public async Task Task_Approve_AutoCompact_DisabledWhenZero()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Set autoCompactInterval to 0 (disabled)
        var configPath = Path.Combine(TestDir, "dydo.json");
        var configJson = File.ReadAllText(configPath);
        var config = System.Text.Json.JsonSerializer.Deserialize(configJson,
            DynaDocs.Serialization.DydoConfigJsonContext.Default.DydoConfig)!;
        config.Tasks.AutoCompactInterval = 0;
        var updatedJson = System.Text.Json.JsonSerializer.Serialize(config,
            DynaDocs.Serialization.DydoConfigJsonContext.Default.DydoConfig);
        File.WriteAllText(configPath, updatedJson);

        // Create audit files
        var year = DateTime.UtcNow.ToString("yyyy");
        var auditDir = Path.Combine(TestDir, "dydo", "_system", "audit", year);
        Directory.CreateDirectory(auditDir);

        var snapshot = new DynaDocs.Models.ProjectSnapshot
        {
            GitCommit = "abc123",
            Files = ["src/file1.cs"],
            Folders = ["src/"],
            DocLinks = new() { ["a.md"] = ["b.md"] }
        };

        var session = new DynaDocs.Models.AuditSession
        {
            SessionId = "session-0",
            AgentName = "Adele",
            Started = DateTime.UtcNow,
            Events = [],
            Snapshot = snapshot
        };
        var json = System.Text.Json.JsonSerializer.Serialize(session, new System.Text.Json.JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
        File.WriteAllText(Path.Combine(auditDir, $"{year}-01-01-session-0.json"), json);

        // Approve many tasks — none should trigger compaction
        for (var i = 0; i < 5; i++)
        {
            await TaskCreateAsync($"disabled-{i}", area: "general");
            await TaskReadyForReviewAsync($"disabled-{i}", "Done");
            var result = await TaskApproveAsync($"disabled-{i}");
            result.AssertSuccess();
            Assert.DoesNotContain("compacted", result.Stdout);
        }

        // No counter file should exist
        var counterPath = Path.Combine(TestDir, "dydo", "_system", "compact-counter");
        Assert.False(File.Exists(counterPath));
    }

    [Fact]
    public async Task Task_Approve_CompactionFailure_DoesNotBlockApproval()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("no-audit-task", area: "general");
        await TaskReadyForReviewAsync("no-audit-task", "Done");

        // No audit directory exists — compaction should silently skip
        var result = await TaskApproveAsync("no-audit-task");

        result.AssertSuccess();
        result.AssertStdoutContains("approved");
        Assert.DoesNotContain("compacted", result.Stdout);
    }

    [Fact]
    public async Task Task_Reject_MarksForRework()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("rejected-task", area: "general");
        await TaskReadyForReviewAsync("rejected-task", "Needs review");

        var result = await TaskRejectAsync("rejected-task", "Missing error handling");

        result.AssertSuccess();
        result.AssertStdoutContains("rejected");
        AssertFileContains("dydo/project/tasks/rejected-task.md", "status: review-failed");
        AssertFileContains("dydo/project/tasks/rejected-task.md", "Missing error handling");
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

    private async Task<CommandResult> TaskListAsync(bool needsReview = false, bool all = false)
    {
        var command = TaskCommand.Create();
        var args = new List<string> { "list" };
        if (needsReview) args.Add("--needs-review");
        if (all) args.Add("--all");
        return await RunAsync(command, args.ToArray());
    }

    private async Task<CommandResult> TaskReadyForReviewAsync(string name, string summary)
    {
        var command = TaskCommand.Create();
        return await RunAsync(command, "ready-for-review", name, "--summary", summary);
    }

    private async Task<CommandResult> TaskApproveAsync(string? name = null, string? notes = null, bool all = false)
    {
        var command = TaskCommand.Create();
        var args = new List<string> { "approve" };
        if (name != null) args.Add(name);
        if (all) args.Add("--all");
        if (notes != null) { args.Add("--notes"); args.Add(notes); }
        return await RunAsync(command, args.ToArray());
    }

    private async Task<CommandResult> TaskCompactAsync()
    {
        var command = TaskCommand.Create();
        return await RunAsync(command, "compact");
    }

    private async Task<CommandResult> TaskRejectAsync(string name, string notes)
    {
        var command = TaskCommand.Create();
        return await RunAsync(command, "reject", name, "--notes", notes);
    }

    #endregion
}
