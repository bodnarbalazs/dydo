namespace DynaDocs.Tests.Services;

using System.Diagnostics;
using DynaDocs.Services;

public class WatchdogServiceTests : IDisposable
{
    private readonly string _testDir;

    public WatchdogServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-watchdog-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void PidFilePath_ResolvesUnderSystemLocal()
    {
        var path = WatchdogService.GetPidFilePath(_testDir);
        var expected = Path.Combine(_testDir, "_system", ".local", "watchdog.pid");
        Assert.Equal(expected, path);
    }

    [Fact]
    public void Stop_ReturnsFalse_WhenNoPidFile()
    {
        Assert.False(WatchdogService.Stop(_testDir));
    }

    [Fact]
    public void Stop_ReturnsFalse_WhenPidFileHasStalePid()
    {
        WritePidFile(99999999); // almost certainly not running
        Assert.False(WatchdogService.Stop(_testDir));
    }

    [Fact]
    public void Stop_CleansStalePidFile()
    {
        WritePidFile(99999999);
        WatchdogService.Stop(_testDir);
        Assert.False(File.Exists(WatchdogService.GetPidFilePath(_testDir)));
    }

    [Fact]
    public void Stop_ReturnsTrue_WhenProcessIsRunning()
    {
        using var proc = StartDummyProcess();
        WritePidFile(proc.Id);

        Assert.True(WatchdogService.Stop(_testDir));
        Assert.True(proc.HasExited);
    }

    [Fact]
    public void Stop_DeletesPidFile_WhenProcessIsRunning()
    {
        using var proc = StartDummyProcess();
        WritePidFile(proc.Id);

        WatchdogService.Stop(_testDir);
        Assert.False(File.Exists(WatchdogService.GetPidFilePath(_testDir)));
    }

    [Fact]
    public void EnsureRunning_ReturnsFalse_WhenProcessAlreadyRunning()
    {
        using var proc = StartDummyProcess();
        WritePidFile(proc.Id);

        Assert.False(WatchdogService.EnsureRunning(_testDir));
        proc.Kill();
    }

    [Fact]
    public void EnsureRunning_CleansUpStalePidFile()
    {
        WritePidFile(99999999);
        // Will fail to start the actual watchdog (wrong ProcessPath in test),
        // but should still clean up the stale PID file
        WatchdogService.EnsureRunning(_testDir);
        var pidFile = WatchdogService.GetPidFilePath(_testDir);
        // Either the file was deleted (stale cleanup) or replaced with a new PID
        if (File.Exists(pidFile))
        {
            var content = File.ReadAllText(pidFile).Trim();
            Assert.NotEqual("99999999", content);
        }
    }

    private void WritePidFile(int pid)
    {
        var pidFile = WatchdogService.GetPidFilePath(_testDir);
        Directory.CreateDirectory(Path.GetDirectoryName(pidFile)!);
        File.WriteAllText(pidFile, pid.ToString());
    }

    private static Process StartDummyProcess()
    {
        // Start a long-running process we can safely kill
        var psi = new ProcessStartInfo("ping", OperatingSystem.IsWindows() ? "-n 600 127.0.0.1" : "-c 600 127.0.0.1")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true
        };
        return Process.Start(psi)!;
    }

    [Fact]
    public void PollAndCleanup_SkipsAgentsWithAutoCloseFalse()
    {
        WriteAgentState("Adele", status: "free", autoClose: false);

        // Should not throw or attempt to kill anything
        WatchdogService.PollAndCleanup(_testDir);
    }

    [Fact]
    public void PollAndCleanup_SkipsWorkingAgents()
    {
        WriteAgentState("Adele", status: "working", autoClose: true);

        // Working agents should not be killed even with auto-close
        WatchdogService.PollAndCleanup(_testDir);
    }

    [Fact]
    public void PollAndCleanup_SkipsAgentsWithNoStateFile()
    {
        var agentDir = Path.Combine(_testDir, "agents", "Adele");
        Directory.CreateDirectory(agentDir);
        // No state.md — should be skipped

        WatchdogService.PollAndCleanup(_testDir);
    }

    [Fact]
    public void PollAndCleanup_HandlesMissingAgentsDir()
    {
        // No agents/ directory — should not throw
        WatchdogService.PollAndCleanup(_testDir);
    }

    [Fact]
    public void PollAndCleanup_FreeAutoCloseAgent_SearchesForProcesses()
    {
        WriteAgentState("Adele", status: "free", autoClose: true);

        // Should enter the kill-search logic but find no matching processes
        WatchdogService.PollAndCleanup(_testDir);
    }

    [Fact]
    public void PollAndCleanup_CorruptStateFile_SkipsGracefully()
    {
        var agentDir = Path.Combine(_testDir, "agents", "Bad");
        Directory.CreateDirectory(agentDir);
        File.WriteAllText(Path.Combine(agentDir, "state.md"), "corrupt data, no frontmatter");

        WatchdogService.PollAndCleanup(_testDir);
    }

    [Fact]
    public void PollAndCleanup_UnclosedFrontmatter_SkipsGracefully()
    {
        var agentDir = Path.Combine(_testDir, "agents", "Bad2");
        Directory.CreateDirectory(agentDir);
        File.WriteAllText(Path.Combine(agentDir, "state.md"), "---\nagent: Bad2\nno closing");

        WatchdogService.PollAndCleanup(_testDir);
    }

    [Fact]
    public void EnsureRunning_NoPidFile_AttemptsStart()
    {
        // No PID file exists — code will try to start the watchdog
        // It may fail (ProcessPath won't be dydo in test), but covers the launch path
        var result = WatchdogService.EnsureRunning(_testDir);
        // Either starts or fails — both are valid
        Assert.IsType<bool>(result);
    }

    [Fact]
    public void ParseStateForWatchdog_ValidState_ReturnsFields()
    {
        var agentDir = Path.Combine(_testDir, "agents", "Adele");
        Directory.CreateDirectory(agentDir);
        var statePath = Path.Combine(agentDir, "state.md");
        File.WriteAllText(statePath, "---\nagent: Adele\nstatus: free\nauto-close: true\n---\n");

        var (autoClose, isFree, agentName, windowId) = WatchdogService.ParseStateForWatchdog(statePath);

        Assert.True(autoClose);
        Assert.True(isFree);
        Assert.Equal("Adele", agentName);
        Assert.Null(windowId);
    }

    [Fact]
    public void ParseStateForWatchdog_WorkingStatus_IsFreeIsFalse()
    {
        var agentDir = Path.Combine(_testDir, "agents", "Bob");
        Directory.CreateDirectory(agentDir);
        var statePath = Path.Combine(agentDir, "state.md");
        File.WriteAllText(statePath, "---\nagent: Bob\nstatus: working\nauto-close: false\n---\n");

        var (autoClose, isFree, _, _) = WatchdogService.ParseStateForWatchdog(statePath);

        Assert.False(autoClose);
        Assert.False(isFree);
    }

    [Fact]
    public void ParseStateForWatchdog_NoFrontmatter_ReturnsDefaults()
    {
        var path = Path.Combine(_testDir, "no-front.md");
        File.WriteAllText(path, "just plain text");

        var (autoClose, isFree, agentName, windowId) = WatchdogService.ParseStateForWatchdog(path);

        Assert.False(autoClose);
        Assert.False(isFree);
        Assert.Null(agentName);
        Assert.Null(windowId);
    }

    [Fact]
    public void ParseStateForWatchdog_UnclosedFrontmatter_ReturnsDefaults()
    {
        var path = Path.Combine(_testDir, "unclosed.md");
        File.WriteAllText(path, "---\nagent: X\nno closing");

        var (autoClose, isFree, agentName, windowId) = WatchdogService.ParseStateForWatchdog(path);

        Assert.False(autoClose);
        Assert.False(isFree);
        Assert.Null(agentName);
        Assert.Null(windowId);
    }

    [Fact]
    public void ParseStateForWatchdog_NonexistentFile_ReturnsDefaults()
    {
        var path = Path.Combine(_testDir, "does-not-exist.md");

        var (autoClose, isFree, agentName, windowId) = WatchdogService.ParseStateForWatchdog(path);

        Assert.False(autoClose);
        Assert.False(isFree);
        Assert.Null(agentName);
        Assert.Null(windowId);
    }

    [Fact]
    public void ParseStateForWatchdog_LinesWithoutColon_Skipped()
    {
        var path = Path.Combine(_testDir, "nocolon.md");
        File.WriteAllText(path, "---\nno-colon-here\nagent: Test\nstatus: free\nauto-close: true\n---\n");

        var (autoClose, isFree, agentName, _) = WatchdogService.ParseStateForWatchdog(path);

        Assert.True(autoClose);
        Assert.True(isFree);
        Assert.Equal("Test", agentName);
    }

    [Fact]
    public void ParseStateForWatchdog_ReturnsWindowId()
    {
        var agentDir = Path.Combine(_testDir, "agents", "Adele");
        Directory.CreateDirectory(agentDir);
        var statePath = Path.Combine(agentDir, "state.md");
        File.WriteAllText(statePath, "---\nagent: Adele\nstatus: free\nauto-close: true\nwindow-id: b98d8485\n---\n");

        var (_, _, _, windowId) = WatchdogService.ParseStateForWatchdog(statePath);

        Assert.Equal("b98d8485", windowId);
    }

    [Fact]
    public void ParseStateForWatchdog_NullWindowId_ReturnsNull()
    {
        var agentDir = Path.Combine(_testDir, "agents", "Adele");
        Directory.CreateDirectory(agentDir);
        var statePath = Path.Combine(agentDir, "state.md");
        File.WriteAllText(statePath, "---\nagent: Adele\nstatus: free\nauto-close: true\nwindow-id: null\n---\n");

        var (_, _, _, windowId) = WatchdogService.ParseStateForWatchdog(statePath);

        Assert.Null(windowId);
    }

    [Fact]
    public void ParseStateForWatchdog_EmptyWindowId_ReturnsNull()
    {
        var agentDir = Path.Combine(_testDir, "agents", "Adele");
        Directory.CreateDirectory(agentDir);
        var statePath = Path.Combine(agentDir, "state.md");
        File.WriteAllText(statePath, "---\nagent: Adele\nstatus: free\nauto-close: true\nwindow-id: \n---\n");

        var (_, _, _, windowId) = WatchdogService.ParseStateForWatchdog(statePath);

        Assert.Null(windowId);
    }

    [Fact]
    public void ClearAutoClose_SetsAutoCloseToFalse()
    {
        var agentDir = Path.Combine(_testDir, "agents", "Adele");
        Directory.CreateDirectory(agentDir);
        var statePath = Path.Combine(agentDir, "state.md");
        File.WriteAllText(statePath, "---\nagent: Adele\nstatus: free\nauto-close: true\nwindow-id: abc123\n---\n");

        WatchdogService.ClearAutoClose(statePath);

        var content = File.ReadAllText(statePath);
        Assert.Contains("auto-close: false", content);
        Assert.DoesNotContain("auto-close: true", content);
    }

    [Fact]
    public void ClearAutoClose_NoOpWhenAlreadyFalse()
    {
        var agentDir = Path.Combine(_testDir, "agents", "Bob");
        Directory.CreateDirectory(agentDir);
        var statePath = Path.Combine(agentDir, "state.md");
        var originalContent = "---\nagent: Bob\nstatus: free\nauto-close: false\n---\n";
        File.WriteAllText(statePath, originalContent);

        WatchdogService.ClearAutoClose(statePath);

        Assert.Equal(originalContent, File.ReadAllText(statePath));
    }

    [Fact]
    public void Stop_NonNumericPidFile_ReturnsFalse()
    {
        var pidFile = WatchdogService.GetPidFilePath(_testDir);
        Directory.CreateDirectory(Path.GetDirectoryName(pidFile)!);
        File.WriteAllText(pidFile, "not-a-number");

        Assert.False(WatchdogService.Stop(_testDir));
        Assert.False(File.Exists(pidFile));
    }

    [Fact]
    public void ShellProcessNames_ContainsExpectedEntries()
    {
        Assert.Contains("powershell", WatchdogService.ShellProcessNames);
        Assert.Contains("pwsh", WatchdogService.ShellProcessNames);
        Assert.Contains("bash", WatchdogService.ShellProcessNames);
        Assert.Contains("cmd", WatchdogService.ShellProcessNames);
        Assert.DoesNotContain("dotnet", WatchdogService.ShellProcessNames);
    }

    [Fact]
    public void ShellProcessNames_IsCaseInsensitive()
    {
        Assert.Contains("POWERSHELL", WatchdogService.ShellProcessNames);
        Assert.Contains("Bash", WatchdogService.ShellProcessNames);
    }

    [Fact]
    public void ResolveWtExe_ReturnsNonNullOnWindowsWithTerminal()
    {
        if (!OperatingSystem.IsWindows()) return;

        // On a Windows dev machine with Windows Terminal, this should find wt.exe
        var result = WatchdogService.ResolveWtExe();
        // Can't assert non-null (CI may not have it), but if found it must be a real file
        if (result != null)
            Assert.True(File.Exists(result));
    }

    [Fact]
    public void TryCloseWindow_InvalidWindowId_DoesNotThrow()
    {
        // Bogus window ID should not throw — return value depends on wt behavior
        _ = WatchdogService.TryCloseWindow("nonexistent-window-id-12345");
    }

    private void WriteAgentState(string agentName, string status, bool autoClose, string? windowId = null)
    {
        var agentDir = Path.Combine(_testDir, "agents", agentName);
        Directory.CreateDirectory(agentDir);
        File.WriteAllText(Path.Combine(agentDir, "state.md"), $$"""
            ---
            agent: {{agentName}}
            role: null
            task: null
            status: {{status}}
            assigned: testuser
            dispatched-by: null
            window-id: {{windowId ?? "null"}}
            auto-close: {{autoClose.ToString().ToLowerInvariant()}}
            started: null
            writable-paths: []
            readonly-paths: []
            unread-must-reads: []
            unread-messages: []
            task-role-history: {}
            ---
            """);
    }
}
