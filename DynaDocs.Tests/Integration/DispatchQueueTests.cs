namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;
using DynaDocs.Services;

/// <summary>
/// Integration tests for dispatch queue feature: --queue flag, dequeue on release,
/// and watchdog stale detection.
/// </summary>
[Collection("Integration")]
public class DispatchQueueTests : IntegrationTestBase
{
    [Fact]
    public async Task Dispatch_WithQueue_NoActive_LaunchesImmediately()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchWithQueueAsync("code-writer", "task-1", "Do work", "merge");

        result.AssertSuccess();
        result.AssertStdoutContains("dispatched");
        // Should NOT contain "Queued" — first item launches immediately
        Assert.DoesNotContain("Queued", result.Stdout);

        // Active entry should exist
        var service = new QueueService(Path.Combine(TestDir, "dydo"));
        var active = service.GetActive("merge");
        Assert.NotNull(active);
        Assert.Equal("task-1", active.Task);
    }

    [Fact]
    public async Task Dispatch_WithQueue_ActiveExists_DefersLaunch()
    {
        await InitProjectAsync("none", "testuser", 3);

        // First dispatch — becomes active
        var result1 = await DispatchWithQueueAsync("code-writer", "task-1", "First", "merge");
        result1.AssertSuccess();

        // Second dispatch — should be queued
        var result2 = await DispatchWithQueueAsync("code-writer", "task-2", "Second", "merge");
        result2.AssertSuccess();
        result2.AssertStdoutContains("Queued");
        result2.AssertStdoutContains("merge");

        var service = new QueueService(Path.Combine(TestDir, "dydo"));
        var pending = service.GetPending("merge");
        Assert.Single(pending);
        Assert.Equal("task-2", pending[0].Entry.Task);
    }

    [Fact]
    public async Task Dispatch_WithNonExistentQueue_Fails()
    {
        await InitProjectAsync("none", "testuser", 3);

        var command = DispatchCommand.Create();
        var args = new List<string>
        {
            "--role", "code-writer",
            "--task", "my-task",
            "--brief", "Brief",
            "--no-wait",
            "--queue", "nonexistent"
        };

        var result = await RunAsync(command, args.ToArray());
        result.AssertExitCode(2);
        result.AssertStderrContains("No queue 'nonexistent'");
    }

    [Fact]
    public async Task Dispatch_QueueAndNoLaunch_Fails()
    {
        await InitProjectAsync("none", "testuser", 3);

        var command = DispatchCommand.Create();
        var args = new List<string>
        {
            "--role", "code-writer",
            "--task", "my-task",
            "--brief", "Brief",
            "--no-wait",
            "--queue", "merge",
            "--no-launch"
        };

        var result = await RunAsync(command, args.ToArray());
        result.AssertExitCode(2);
        result.AssertStderrContains("Cannot specify both --queue and --no-launch");
    }

    [Fact]
    public async Task Release_DequeuesNextFromQueue()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Set up: first dispatch becomes active
        var result1 = await DispatchWithQueueAsync("code-writer", "task-1", "First", "merge", to: "Adele");
        result1.AssertSuccess();

        // Second dispatch gets queued
        var result2 = await DispatchWithQueueAsync("code-writer", "task-2", "Second", "merge", to: "Brian");
        result2.AssertSuccess();

        var service = new QueueService(Path.Combine(TestDir, "dydo"));

        // Simulate release of Adele (active agent) by calling DequeueIfActive directly
        var config = new ConfigService().LoadConfig(TestDir);
        AgentLifecycleHandlers.DequeueIfActive("Adele", config);

        // Active should now be cleared or set to Brian
        var active = service.GetActive("merge");
        Assert.NotNull(active);
        Assert.Equal("Brian", active.Agent);

        // No more pending
        Assert.Empty(service.GetPending("merge"));
    }

    [Fact]
    public void WatchdogPollQueues_DetectsStaleActive()
    {
        // Set up a queue with a stale active entry (dead PID)
        var dydoRoot = Path.Combine(TestDir, "dydo");
        Directory.CreateDirectory(dydoRoot);

        // Write minimal dydo.json so ConfigService works
        File.WriteAllText(Path.Combine(TestDir, "dydo.json"),
            """{"version":1,"structure":{"root":"dydo"},"paths":{"source":[],"tests":[]},"agents":{"pool":[],"assignments":{}},"queues":["merge"]}""");

        var service = new QueueService(dydoRoot);
        service.SetActive("merge", "Adele", "task-1", 99999999); // Dead PID

        // Enqueue a pending item
        service.TryEnqueue("merge", "Brian", "task-2", true, false, null, null, null, null, null);

        var originalProcOverride = ProcessUtils.IsProcessRunningOverride;
        var originalTerminalOverride = TerminalLauncher.ProcessStarterOverride;
        try
        {
            ProcessUtils.IsProcessRunningOverride = _ => false;
            TerminalLauncher.ProcessStarterOverride = new NoOpProcessStarter();

            WatchdogService.PollQueues(dydoRoot);

            // Brian should now be active
            var active = service.GetActive("merge");
            Assert.NotNull(active);
            Assert.Equal("Brian", active.Agent);

            // No more pending
            Assert.Empty(service.GetPending("merge"));
        }
        finally
        {
            ProcessUtils.IsProcessRunningOverride = originalProcOverride;
            TerminalLauncher.ProcessStarterOverride = originalTerminalOverride;
        }
    }

    [Fact]
    public void WatchdogPollQueues_CleansUpEmptyTransient()
    {
        var dydoRoot = Path.Combine(TestDir, "dydo");
        Directory.CreateDirectory(dydoRoot);

        File.WriteAllText(Path.Combine(TestDir, "dydo.json"),
            """{"version":1,"structure":{"root":"dydo"},"paths":{"source":[],"tests":[]},"agents":{"pool":[],"assignments":{}},"queues":["merge"]}""");

        var service = new QueueService(dydoRoot);
        service.CreateQueue("temp-queue", out _);
        Assert.True(Directory.Exists(service.GetQueueDir("temp-queue")));

        var originalProcOverride = ProcessUtils.IsProcessRunningOverride;
        var originalTerminalOverride = TerminalLauncher.ProcessStarterOverride;
        try
        {
            TerminalLauncher.ProcessStarterOverride = new NoOpProcessStarter();
            WatchdogService.PollQueues(dydoRoot);

            // Empty transient queue should be cleaned up
            Assert.False(Directory.Exists(service.GetQueueDir("temp-queue")));
        }
        finally
        {
            ProcessUtils.IsProcessRunningOverride = originalProcOverride;
            TerminalLauncher.ProcessStarterOverride = originalTerminalOverride;
        }
    }

    [Fact]
    public async Task ReviewCommand_MergeHint_IncludesQueueFlag()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("reviewer", "my-task");
        await ReadMustReadsAsync();

        // Create task file in review-pending state
        var tasksDir = Path.Combine(TestDir, "dydo", "project", "tasks");
        Directory.CreateDirectory(tasksDir);
        File.WriteAllText(Path.Combine(tasksDir, "my-task.md"), """
            ---
            area: general
            name: my-task
            status: review-pending
            ---
            # Task: my-task
            """);

        // Create worktree marker so the merge hint appears
        var workspace = Path.Combine(TestDir, "dydo", "agents", "Adele");
        File.WriteAllText(Path.Combine(workspace, ".worktree"), "wt-test-123");

        var command = ReviewCommand.Create();
        var result = await RunAsync(command, "complete", "my-task", "--status", "pass");

        result.AssertSuccess();
        result.AssertStdoutContains("--queue merge");
    }

    #region Helper Methods

    private async Task<CommandResult> DispatchWithQueueAsync(
        string role, string task, string brief, string queue,
        string? to = null)
    {
        var command = DispatchCommand.Create();
        var args = new List<string>
        {
            "--role", role,
            "--task", task,
            "--brief", brief,
            "--no-wait",
            "--queue", queue
        };

        if (to != null) { args.Add("--to"); args.Add(to); }
        return await RunAsync(command, args.ToArray());
    }

    #endregion
}
