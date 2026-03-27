namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;
using DynaDocs.Services;

[Collection("Integration")]
public class GuardLiftTests : IntegrationTestBase
{
    private async Task SetupClaimedAgent(string agentName = "Adele", string role = "code-writer", string task = "test-task")
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync(agentName);
        await SetRoleAsync(role, task);
        await ReadMustReadsAsync();
    }

    private void LiftGuard(string agentName, int? minutes = null)
    {
        var service = new GuardLiftService(TestDir);
        service.Lift(agentName, "testuser", minutes);
    }

    private void RestoreGuard(string agentName)
    {
        var service = new GuardLiftService(TestDir);
        service.Restore(agentName);
    }

    private bool IsLifted(string agentName)
    {
        var service = new GuardLiftService(TestDir);
        return service.IsLifted(agentName);
    }

    // ================================================================
    // Guard pipeline: lifted agent bypasses RBAC
    // ================================================================

    [Fact]
    public async Task Guard_LiftedAgent_CanWriteOutsideRolePermissions()
    {
        await SetupClaimedAgent();
        LiftGuard("Adele");

        // dydo/** is read-only for code-writer, but lift should bypass RBAC
        var result = await GuardAsync("write", "dydo/some-file.md");
        result.AssertSuccess();
    }

    [Fact]
    public async Task Guard_LiftedAgent_StillBlockedByOffLimits()
    {
        await SetupClaimedAgent();
        LiftGuard("Adele");

        // dydo/files-off-limits.md is off-limits to ALL agents, even with lifted guard
        var result = await GuardAsync("write", "dydo/files-off-limits.md");
        result.AssertExitCode(2);
        result.AssertStderrContains("off-limits");
    }

    [Fact]
    public async Task Guard_UnliftedAgent_BlockedByRBAC()
    {
        await SetupClaimedAgent();
        // No lift — should be blocked by RBAC for dydo/** path
        var result = await GuardAsync("write", "dydo/some-file.md");
        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");
    }

    [Fact]
    public async Task Guard_ExpiredLift_BlockedByRBAC()
    {
        await SetupClaimedAgent();

        // Create an already-expired lift marker in the agent workspace
        var markerPath = Path.Combine(TestDir, "dydo", "agents", "Adele", ".guard-lift.json");
        var marker = new DynaDocs.Models.GuardLiftMarker
        {
            Agent = "Adele",
            LiftedBy = "testuser",
            LiftedAt = DateTime.UtcNow.AddMinutes(-10),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5) // expired 5 min ago
        };
        var json = System.Text.Json.JsonSerializer.Serialize(marker,
            DynaDocs.Serialization.DydoDefaultJsonContext.Default.GuardLiftMarker);
        File.WriteAllText(markerPath, json);

        var result = await GuardAsync("write", "dydo/some-file.md");
        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED");

        // Marker file should have been cleaned up
        Assert.False(File.Exists(markerPath));
    }

    // ================================================================
    // Guard pipeline: bash file operations with lifted guard
    // ================================================================

    [Fact]
    public async Task Guard_LiftedAgent_BashWriteOutsideRole_Allowed()
    {
        await SetupClaimedAgent();
        LiftGuard("Adele");

        // Bash write to a path outside code-writer permissions
        StoreSessionContext();
        var command = GuardCommand.Create();
        var result = await RunAsync(command, "--command", "echo test > dydo/some-file.md");
        result.AssertSuccess();
    }

    // ================================================================
    // Lift survives role change
    // ================================================================

    [Fact]
    public async Task Guard_LiftSurvivesRoleChange()
    {
        await SetupClaimedAgent();
        LiftGuard("Adele");

        // Change role
        await SetRoleAsync("planner", "test-task");
        await ReadMustReadsAsync();

        // Lift should still be active
        Assert.True(IsLifted("Adele"));
    }

    // ================================================================
    // Lift cleared on release
    // ================================================================

    [Fact]
    public async Task Guard_LiftClearedOnRelease()
    {
        await SetupClaimedAgent();
        LiftGuard("Adele");

        Assert.True(IsLifted("Adele"));

        await ReleaseAgentAsync();

        Assert.False(IsLifted("Adele"));
    }

    // ================================================================
    // GuardLiftService unit-level tests
    // ================================================================

    [Fact]
    public void GuardLiftService_Lift_CreatesMarkerFile()
    {
        var service = new GuardLiftService(TestDir);
        Directory.CreateDirectory(Path.Combine(TestDir, "dydo", "agents", "TestAgent"));

        service.Lift("TestAgent", "testuser", null);

        var markerPath = Path.Combine(TestDir, "dydo", "agents", "TestAgent", ".guard-lift.json");
        Assert.True(File.Exists(markerPath));
    }

    [Fact]
    public void GuardLiftService_Lift_WithMinutes_SetsExpiry()
    {
        var service = new GuardLiftService(TestDir);
        Directory.CreateDirectory(Path.Combine(TestDir, "dydo", "agents", "TestAgent"));

        service.Lift("TestAgent", "testuser", 30);

        Assert.True(service.IsLifted("TestAgent"));

        var markerPath = Path.Combine(TestDir, "dydo", "agents", "TestAgent", ".guard-lift.json");
        var json = File.ReadAllText(markerPath);
        var marker = System.Text.Json.JsonSerializer.Deserialize(json,
            DynaDocs.Serialization.DydoDefaultJsonContext.Default.GuardLiftMarker);
        Assert.NotNull(marker!.ExpiresAt);
    }

    [Fact]
    public void GuardLiftService_Lift_ExpiresAtIsUtc()
    {
        var service = new GuardLiftService(TestDir);
        Directory.CreateDirectory(Path.Combine(TestDir, "dydo", "agents", "TestAgent"));

        service.Lift("TestAgent", "testuser", 10);

        var markerPath = Path.Combine(TestDir, "dydo", "agents", "TestAgent", ".guard-lift.json");
        var json = File.ReadAllText(markerPath);
        var marker = System.Text.Json.JsonSerializer.Deserialize(json,
            DynaDocs.Serialization.DydoDefaultJsonContext.Default.GuardLiftMarker);

        // ExpiresAt must round-trip as UTC — a mismatch causes timezone bugs
        Assert.Equal(DateTimeKind.Utc, marker!.ExpiresAt!.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, marker.LiftedAt.Kind);

        // Sanity: expiry should be ~10 minutes from now (within 1 minute tolerance)
        var delta = marker.ExpiresAt.Value - DateTime.UtcNow;
        Assert.InRange(delta.TotalMinutes, 9, 11);
    }

    [Fact]
    public void GuardLiftService_Restore_RemovesMarker()
    {
        var service = new GuardLiftService(TestDir);
        Directory.CreateDirectory(Path.Combine(TestDir, "dydo", "agents", "TestAgent"));

        service.Lift("TestAgent", "testuser", null);
        Assert.True(service.IsLifted("TestAgent"));

        service.Restore("TestAgent");
        Assert.False(service.IsLifted("TestAgent"));
    }

    [Fact]
    public void GuardLiftService_IsLifted_FalseWhenNoMarker()
    {
        var service = new GuardLiftService(TestDir);
        Assert.False(service.IsLifted("NoSuchAgent"));
    }

    [Fact]
    public void GuardLiftService_ClearLift_IdempotentWhenNoMarker()
    {
        var service = new GuardLiftService(TestDir);
        // Should not throw
        service.ClearLift("NoSuchAgent");
    }

    // ================================================================
    // GuardLiftCommand tests
    // ================================================================

    [Fact]
    public async Task GuardLiftCommand_Lift_CreatesMarkerAndPrintsConfirmation()
    {
        await SetupClaimedAgent();
        StoreSessionContext();

        var command = GuardCommand.Create();
        var result = await RunAsync(command, "lift", "Adele");

        result.AssertSuccess();
        result.AssertStdoutContains("Guard lifted for Adele");
        Assert.True(IsLifted("Adele"));
    }

    [Fact]
    public async Task GuardLiftCommand_Lift_WithMinutes_PrintsDuration()
    {
        await SetupClaimedAgent();
        StoreSessionContext();

        var command = GuardCommand.Create();
        var result = await RunAsync(command, "lift", "Adele", "30");

        result.AssertSuccess();
        result.AssertStdoutContains("30 minutes");
        Assert.True(IsLifted("Adele"));
    }

    [Fact]
    public async Task GuardLiftCommand_Lift_InvalidAgent_Errors()
    {
        await InitProjectAsync("none", "testuser", 3);
        StoreSessionContext();

        var command = GuardCommand.Create();
        var result = await RunAsync(command, "lift", "NonExistent");

        result.AssertExitCode(2);
        result.AssertStderrContains("does not exist");
    }

    [Fact]
    public async Task GuardLiftCommand_Lift_FreeAgent_Errors()
    {
        await InitProjectAsync("none", "testuser", 3);
        StoreSessionContext();

        // Brian is free (not claimed)
        var command = GuardCommand.Create();
        var result = await RunAsync(command, "lift", "Brian");

        result.AssertExitCode(2);
        result.AssertStderrContains("not currently claimed");
    }

    [Theory]
    [InlineData("-5")]
    [InlineData("0")]
    [InlineData("-1")]
    public async Task GuardLiftCommand_Lift_NegativeOrZeroMinutes_Errors(string minutes)
    {
        await SetupClaimedAgent();
        StoreSessionContext();

        var command = GuardCommand.Create();
        var result = await RunAsync(command, "lift", "Adele", minutes);

        result.AssertExitCode(2);
        result.AssertStderrContains("Minutes must be a positive number");
        Assert.False(IsLifted("Adele"));
    }

    [Fact]
    public async Task GuardLiftCommand_Restore_RemovesLift()
    {
        await SetupClaimedAgent();
        LiftGuard("Adele");
        StoreSessionContext();

        var command = GuardCommand.Create();
        var result = await RunAsync(command, "restore", "Adele");

        result.AssertSuccess();
        result.AssertStdoutContains("Guard restored for Adele");
        Assert.False(IsLifted("Adele"));
    }

    [Fact]
    public async Task GuardLiftCommand_Restore_NoLift_GracefulMessage()
    {
        await SetupClaimedAgent();
        StoreSessionContext();

        var command = GuardCommand.Create();
        var result = await RunAsync(command, "restore", "Adele");

        result.AssertSuccess();
        result.AssertStdoutContains("No active lift");
    }
}
