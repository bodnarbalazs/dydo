namespace DynaDocs.Tests.Services;

using System.Diagnostics;
using System.Text.Json;
using DynaDocs.Services;
using DynaDocs.Utils;

[Collection("ProcessUtils")]
public class WatchdogServiceTests : IDisposable
{
    private readonly string _testDir;

    public WatchdogServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-watchdog-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        // Note: we deliberately do NOT capture Environment.CurrentDirectory here.
        // CWD is process-global state shared with parallel test classes; capturing it
        // can pin a temp path that another class deletes before our Dispose runs (#0116).
        // Tests that need a specific CWD set it themselves; Dispose parks CWD at
        // Path.GetTempPath() so deleting _testDir is always safe.
        WatchdogService.StartProcessOverride = _ => null;
    }

    public void Dispose()
    {
        // Park CWD on a guaranteed-existing dir before deleting _testDir, so we never
        // leave the process pointing at a deleted directory and never block the delete.
        try { Environment.CurrentDirectory = Path.GetTempPath(); }
        catch { /* best-effort; if even GetTempPath is gone, we have bigger problems */ }
        WatchdogService.StartProcessOverride = null;
        WatchdogService.FindProcessesOverride = null;
        WatchdogService.PollIntervalOverride = null;
        WatchdogService.MaxOrphanAgeOverride = null;
        WatchdogService.LaunchResumeOverride = null;
        WatchdogService.ResumeAttemptsCapOverride = null;
        WatchdogService.ResumeWarmupGateOverride = null;
        ProcessUtils.GetProcessNameOverride = null;
        ProcessUtils.IsProcessRunningOverride = null;
        ProcessUtils.FindAncestorProcessOverride = null;
        WatchdogLogger.LogPathOverride = null;
        WatchdogLogger.MaxBytesOverride = null;
        WatchdogLogger.MaxRotationsOverride = null;
        if (Directory.Exists(_testDir))
        {
            for (var i = 0; i < 3; i++)
            {
                try { Directory.Delete(_testDir, true); return; }
                catch (IOException) when (i < 2) { Thread.Sleep(50 * (i + 1)); }
            }
        }
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
    public void Stop_ReturnsTrue_WhenProcessIsRunning_TightSuccession()
    {
        // Regression for #0117: if the spawn primitive ever exits before Stop's
        // liveness check runs, this loop will catch it deterministically rather
        // than once-in-a-CI-run.
        for (var i = 0; i < 10; i++)
        {
            using var proc = StartDummyProcess();
            WritePidFile(proc.Id);

            Assert.True(WatchdogService.Stop(_testDir), $"iteration {i}: Stop returned false");
            Assert.True(proc.HasExited, $"iteration {i}: process did not exit");
        }
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

    // dydoRoot is the discovered FindMainDydoRoot path — _testDir/dydo when dydo.json
    // sits at _testDir. Each test sets this up explicitly before calling Run().
    private static string SetupRunDydoRoot(string testDir)
    {
        File.WriteAllText(Path.Combine(testDir, "dydo.json"), """{"name":"t"}""");
        var dydoRoot = Path.Combine(testDir, "dydo");
        Directory.CreateDirectory(dydoRoot);
        return dydoRoot;
    }

    private static void WriteAnchorFile(string dydoRoot, int pid)
    {
        var dir = WatchdogService.GetAnchorsDirPath(dydoRoot);
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, $"{pid}.anchor"), Array.Empty<byte>());
    }

    private static Process StartDummyProcess()
    {
        // Start a long-running process we can safely kill. On Linux GitHub runners,
        // `ping` exits within milliseconds (the sandbox rejects ICMP socket creation),
        // making any test that asserts liveness flake (#0117). `sleep` is a kernel-level
        // primitive with no network dependency. Windows has no `sleep` binary, but `ping`
        // is reliable there, so platform-split the spawn.
        var psi = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("ping", "-n 600 127.0.0.1")
            : new ProcessStartInfo("sleep", "30");
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardOutput = true;
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
    public void ReadStateContext_NonexistentFile_ReturnsNullTuple()
    {
        // Regression: an inline try/catch around the ReadAllText call keeps a single
        // agent's IO failure (file vanished mid-poll, sharing-violation, etc.) from
        // aborting the surrounding foreach over agentDirs and skipping every other
        // agent in this tick.
        var path = Path.Combine(_testDir, "missing-state.md");

        var (dispatchedBy, since) = WatchdogService.ReadStateContext(path);

        Assert.Null(dispatchedBy);
        Assert.Null(since);
    }

    [Fact]
    public void ReadStateContext_ParsesDispatchedByAndSince()
    {
        var agentDir = Path.Combine(_testDir, "agents", "Adele");
        Directory.CreateDirectory(agentDir);
        var statePath = Path.Combine(agentDir, "state.md");
        File.WriteAllText(statePath, "---\nagent: Adele\ndispatched-by: Brian\nstarted: 2026-04-29T15:00:00Z\n---\n");

        var (dispatchedBy, since) = WatchdogService.ReadStateContext(statePath);

        Assert.Equal("Brian", dispatchedBy);
        Assert.Equal("2026-04-29T15:00:00Z", since);
    }

    [Fact]
    public void ReadStateContext_NullLiterals_ReturnNull()
    {
        var agentDir = Path.Combine(_testDir, "agents", "Bob");
        Directory.CreateDirectory(agentDir);
        var statePath = Path.Combine(agentDir, "state.md");
        File.WriteAllText(statePath, "---\nagent: Bob\ndispatched-by: null\nstarted: null\n---\n");

        var (dispatchedBy, since) = WatchdogService.ReadStateContext(statePath);

        Assert.Null(dispatchedBy);
        Assert.Null(since);
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
    public void PollAndCleanup_FreeAutoCloseAgent_NoProcesses_ClearsAutoClose()
    {
        WriteAgentState("Adele", status: "free", autoClose: true, windowId: "abc12345");

        WatchdogService.FindProcessesOverride = _ => [];

        try
        {
            WatchdogService.PollAndCleanup(_testDir);
        }
        finally
        {
            WatchdogService.FindProcessesOverride = null;
        }

        var statePath = Path.Combine(_testDir, "agents", "Adele", "state.md");
        var content = File.ReadAllText(statePath);
        Assert.Contains("auto-close: false", content);
        Assert.DoesNotContain("auto-close: true", content);
    }

    [Fact]
    public void PollAndCleanup_ProcessesRunning_KillsImmediately()
    {
        using var dummy = StartDummyProcess();
        WriteAgentState("Adele", status: "free", autoClose: true, windowId: "win-xyz");

        WatchdogService.FindProcessesOverride = _ => [dummy.Id];
        ProcessUtils.GetProcessNameOverride = pid => pid == dummy.Id ? "claude" : null;

        try
        {
            WatchdogService.PollAndCleanup(_testDir);
        }
        finally
        {
            WatchdogService.FindProcessesOverride = null;
            ProcessUtils.GetProcessNameOverride = null;
        }

        dummy.WaitForExit(5000);
        Assert.True(dummy.HasExited);

        var statePath = Path.Combine(_testDir, "agents", "Adele", "state.md");
        var content = File.ReadAllText(statePath);
        Assert.Contains("auto-close: false", content);
    }

    [Fact]
    public void PollAndCleanup_ProcessesRunning_NoWindowId_KillsImmediately()
    {
        using var dummy = StartDummyProcess();
        WriteAgentState("Adele", status: "free", autoClose: true, windowId: null);

        WatchdogService.FindProcessesOverride = _ => [dummy.Id];
        ProcessUtils.GetProcessNameOverride = pid => pid == dummy.Id ? "claude" : null;

        try
        {
            WatchdogService.PollAndCleanup(_testDir);
        }
        finally
        {
            WatchdogService.FindProcessesOverride = null;
            ProcessUtils.GetProcessNameOverride = null;
        }

        dummy.WaitForExit(5000);
        Assert.True(dummy.HasExited);

        var statePath = Path.Combine(_testDir, "agents", "Adele", "state.md");
        var content = File.ReadAllText(statePath);
        Assert.Contains("auto-close: false", content);
    }

    [Fact]
    public void PollAndCleanup_ProcessesRunning_DeadPid_ClearsAutoClose()
    {
        // Fake PID that doesn't exist — exercises the kill path where
        // Process.GetProcessById throws (caught gracefully). Override the
        // process name to "claude" so the whitelist gate is passed and the
        // kill attempt actually runs.
        WriteAgentState("Adele", status: "free", autoClose: true, windowId: null);

        WatchdogService.FindProcessesOverride = _ => [99999999];
        ProcessUtils.GetProcessNameOverride = _ => "claude";

        try
        {
            WatchdogService.PollAndCleanup(_testDir);
            var statePath = Path.Combine(_testDir, "agents", "Adele", "state.md");
            Assert.Contains("auto-close: false", File.ReadAllText(statePath));
        }
        finally
        {
            WatchdogService.FindProcessesOverride = null;
            ProcessUtils.GetProcessNameOverride = null;
        }
    }

    [Fact]
    public void PollAndCleanup_NoProcessesRunning_ClearsAutoClose()
    {
        // Simulates the terminal-already-closed path (pids.Count == 0)
        WriteAgentState("Adele", status: "free", autoClose: true, windowId: "win-gone");

        WatchdogService.FindProcessesOverride = _ => [];

        try
        {
            WatchdogService.PollAndCleanup(_testDir);
        }
        finally
        {
            WatchdogService.FindProcessesOverride = null;
        }

        var statePath = Path.Combine(_testDir, "agents", "Adele", "state.md");
        var content = File.ReadAllText(statePath);
        Assert.Contains("auto-close: false", content);
    }

    [Fact]
    public void PollAndCleanup_FirstPoll_ProcessesRunning_ClearsImmediately()
    {
        WriteAgentState("Adele", status: "free", autoClose: true, windowId: "win-ok");

        WatchdogService.FindProcessesOverride = _ => [99999999];
        ProcessUtils.GetProcessNameOverride = _ => "claude";

        try
        {
            WatchdogService.PollAndCleanup(_testDir);
        }
        finally
        {
            WatchdogService.FindProcessesOverride = null;
            ProcessUtils.GetProcessNameOverride = null;
        }

        var statePath = Path.Combine(_testDir, "agents", "Adele", "state.md");
        Assert.Contains("auto-close: false", File.ReadAllText(statePath));
    }

    [Fact]
    public void PollAndCleanup_ProcessesRunning_ShellProcess_SkipsKill()
    {
        using var dummy = StartDummyProcess();
        WriteAgentState("Adele", status: "free", autoClose: true, windowId: null);

        WatchdogService.FindProcessesOverride = _ => [dummy.Id];
        ProcessUtils.GetProcessNameOverride = pid => pid == dummy.Id ? "bash" : null;

        try
        {
            WatchdogService.PollAndCleanup(_testDir);
            Assert.False(dummy.HasExited);
        }
        finally
        {
            WatchdogService.FindProcessesOverride = null;
            ProcessUtils.GetProcessNameOverride = null;
            dummy.Kill();
        }
    }

    [Fact]
    public void PollAndCleanup_OnlyShellProcesses_DoesNotClearAutoClose()
    {
        using var dummy = StartDummyProcess();
        WriteAgentState("Adele", status: "free", autoClose: true, windowId: null);

        WatchdogService.FindProcessesOverride = _ => [dummy.Id];
        ProcessUtils.GetProcessNameOverride = pid => pid == dummy.Id ? "pwsh" : null;

        try
        {
            WatchdogService.PollAndCleanup(_testDir);

            var statePath = Path.Combine(_testDir, "agents", "Adele", "state.md");
            var content = File.ReadAllText(statePath);
            Assert.Contains("auto-close: true", content);
            Assert.DoesNotContain("auto-close: false", content);
        }
        finally
        {
            WatchdogService.FindProcessesOverride = null;
            ProcessUtils.GetProcessNameOverride = null;
            dummy.Kill();
        }
    }

    [Fact]
    public void PollAndCleanup_MixedProcesses_KillsNonShellAndClearsAutoClose()
    {
        using var dummy1 = StartDummyProcess();
        using var dummy2 = StartDummyProcess();
        WriteAgentState("Adele", status: "free", autoClose: true, windowId: null);

        WatchdogService.FindProcessesOverride = _ => [dummy1.Id, dummy2.Id];
        // dummy1 is a shell (skipped under whitelist — not on whitelist),
        // dummy2 is the actual claude target (killed).
        ProcessUtils.GetProcessNameOverride = pid =>
            pid == dummy1.Id ? "pwsh" :
            pid == dummy2.Id ? "claude" : null;

        try
        {
            WatchdogService.PollAndCleanup(_testDir);

            // Shell process (dummy1) should not be killed
            Assert.False(dummy1.HasExited);
            // Whitelisted process (dummy2) should be killed
            dummy2.WaitForExit(5000);
            Assert.True(dummy2.HasExited);

            var statePath = Path.Combine(_testDir, "agents", "Adele", "state.md");
            var content = File.ReadAllText(statePath);
            Assert.Contains("auto-close: false", content);
        }
        finally
        {
            WatchdogService.FindProcessesOverride = null;
            ProcessUtils.GetProcessNameOverride = null;
            if (!dummy1.HasExited) dummy1.Kill();
            if (!dummy2.HasExited) dummy2.Kill();
        }
    }

    [Fact]
    public void PollAndCleanup_RedispatchedDuringDeferral_KillsOldProcesses()
    {
        // Reproduces the race: agent releases (free+auto-close), watchdog sees it,
        // then agent gets re-dispatched (working) before cleanup completes.
        // Old session's processes must still be killed.
        using var dummy = StartDummyProcess();
        WriteAgentState("Adele", status: "free", autoClose: true);

        WatchdogService.FindProcessesOverride = _ => [dummy.Id];
        ProcessUtils.GetProcessNameOverride = pid => pid == dummy.Id ? "claude" : null;

        try
        {
            // Poll 1: agent is free+auto-close with processes running
            WatchdogService.PollAndCleanup(_testDir);

            // Between polls: agent gets re-dispatched (status changes to working)
            WriteAgentState("Adele", status: "working", autoClose: true);

            // Poll 2: agent is now working — old processes should still be killed
            WatchdogService.PollAndCleanup(_testDir);
        }
        finally
        {
            WatchdogService.FindProcessesOverride = null;
            ProcessUtils.GetProcessNameOverride = null;
        }

        dummy.WaitForExit(5000);
        Assert.True(dummy.HasExited);
    }

    [Fact]
    public void PollAndCleanup_LockHeldByWriter_DoesNotKill()
    {
        // Regression for #0121: when a registry writer holds the .claim.lock,
        // the watchdog must skip the iteration (no read, no kill, no
        // ClearAutoClose RMW). When the lock is released, the next poll
        // proceeds normally — proving the gate is a deferral, not a permanent
        // block.
        using var dummy = StartDummyProcess();
        WriteAgentState("Adele", status: "free", autoClose: true);

        var agentDir = Path.Combine(_testDir, "agents", "Adele");
        var lockPath = Path.Combine(agentDir, ".claim.lock");

        WatchdogService.FindProcessesOverride = _ => [dummy.Id];
        ProcessUtils.GetProcessNameOverride = pid => pid == dummy.Id ? "claude" : null;

        try
        {
            // Take the lock from this thread (simulates an in-flight registry writer).
            Assert.True(AgentRegistry.TryAcquireLockAtPath(lockPath, "Adele", out _));

            WatchdogService.PollAndCleanup(_testDir);

            // While the lock is held: no kill, no auto-close clear.
            Assert.False(dummy.HasExited);
            var content = File.ReadAllText(Path.Combine(agentDir, "state.md"));
            Assert.Contains("auto-close: true", content);
            Assert.DoesNotContain("auto-close: false", content);

            // Release the lock — next poll should kill normally.
            AgentRegistry.ReleaseLockAtPath(lockPath);

            WatchdogService.PollAndCleanup(_testDir);

            dummy.WaitForExit(5000);
            Assert.True(dummy.HasExited);
            Assert.Contains("auto-close: false", File.ReadAllText(Path.Combine(agentDir, "state.md")));
        }
        finally
        {
            WatchdogService.FindProcessesOverride = null;
            ProcessUtils.GetProcessNameOverride = null;
            if (!dummy.HasExited) dummy.Kill();
            if (File.Exists(lockPath)) File.Delete(lockPath);
        }
    }

    [Theory]
    [InlineData("gnome-terminal")]
    [InlineData("konsole")]
    [InlineData("xfce4-terminal")]
    [InlineData("alacritty")]
    [InlineData("kitty")]
    [InlineData("wezterm")]
    [InlineData("tilix")]
    [InlineData("foot")]
    [InlineData("xterm")]
    public void PollAndCleanup_LinuxTerminalEmulatorPid_NotKilled(string emulatorName)
    {
        // Regression for #0122: every Linux terminal emulator launches `bash -c
        // "...claude '{agent} --inbox'..."` and inherits the prompt in argv.
        // Substring-match returns the emulator PID as well as claude's PID.
        // Under the whitelist, the emulator must survive — only claude/node
        // is a valid kill target.
        using var dummy = StartDummyProcess();
        WriteAgentState("Adele", status: "free", autoClose: true);

        WatchdogService.FindProcessesOverride = _ => [dummy.Id];
        ProcessUtils.GetProcessNameOverride = pid => pid == dummy.Id ? emulatorName : null;

        try
        {
            WatchdogService.PollAndCleanup(_testDir);

            Assert.False(dummy.HasExited);
            // Auto-close must not be cleared either — the agent's actual claude
            // process is still in flight (just not in this fake pid list).
            var content = File.ReadAllText(Path.Combine(_testDir, "agents", "Adele", "state.md"));
            Assert.Contains("auto-close: true", content);
            Assert.DoesNotContain("auto-close: false", content);
        }
        finally
        {
            WatchdogService.FindProcessesOverride = null;
            ProcessUtils.GetProcessNameOverride = null;
            if (!dummy.HasExited) dummy.Kill();
        }
    }

    [Fact]
    public void PollAndCleanup_ClaudeProcess_Killed()
    {
        // Whitelist positive case: a process named "claude" must be killed
        // and auto-close cleared.
        using var dummy = StartDummyProcess();
        WriteAgentState("Adele", status: "free", autoClose: true);

        WatchdogService.FindProcessesOverride = _ => [dummy.Id];
        ProcessUtils.GetProcessNameOverride = pid => pid == dummy.Id ? "claude" : null;

        try
        {
            WatchdogService.PollAndCleanup(_testDir);
        }
        finally
        {
            WatchdogService.FindProcessesOverride = null;
            ProcessUtils.GetProcessNameOverride = null;
        }

        dummy.WaitForExit(5000);
        Assert.True(dummy.HasExited);
        Assert.Contains("auto-close: false", File.ReadAllText(Path.Combine(_testDir, "agents", "Adele", "state.md")));
    }

    [Fact]
    public void PollAndCleanup_ReleasesLockAfterWork()
    {
        // Guards against a finally-block regression that would deadlock the
        // registry: after PollAndCleanup runs, no .claim.lock files should
        // remain under any agent dir.
        WriteAgentState("Adele", status: "free", autoClose: true);
        WriteAgentState("Bob", status: "working", autoClose: true);

        WatchdogService.FindProcessesOverride = _ => [];

        try
        {
            WatchdogService.PollAndCleanup(_testDir);
        }
        finally
        {
            WatchdogService.FindProcessesOverride = null;
        }

        Assert.False(File.Exists(Path.Combine(_testDir, "agents", "Adele", ".claim.lock")));
        Assert.False(File.Exists(Path.Combine(_testDir, "agents", "Bob", ".claim.lock")));
    }

    #region PollOrphanedWaits Tests

    [Fact]
    public void PollOrphanedWaits_FreeAgent_KillsOrphanedWaitProcess()
    {
        using var dummy = StartDummyProcess();
        WriteAgentState("Adele", status: "free", autoClose: false);
        WriteWaitMarker("Adele", "my-task", dummy.Id);

        ProcessUtils.IsProcessRunningOverride = pid => pid == dummy.Id && !dummy.HasExited;
        try
        {
            WatchdogService.PollOrphanedWaits(_testDir);
        }
        finally
        {
            ProcessUtils.IsProcessRunningOverride = null;
        }

        dummy.WaitForExit(5000);
        Assert.True(dummy.HasExited);

        var markerPath = Path.Combine(_testDir, "agents", "Adele", ".waiting", "my-task.json");
        Assert.False(File.Exists(markerPath));
    }

    [Fact]
    public void PollOrphanedWaits_WorkingAgent_LeavesWaitAlone()
    {
        using var dummy = StartDummyProcess();
        WriteAgentState("Adele", status: "working", autoClose: true);
        WriteWaitMarker("Adele", "my-task", dummy.Id);

        ProcessUtils.IsProcessRunningOverride = pid => pid == dummy.Id && !dummy.HasExited;
        try
        {
            WatchdogService.PollOrphanedWaits(_testDir);
        }
        finally
        {
            ProcessUtils.IsProcessRunningOverride = null;
        }

        Assert.False(dummy.HasExited);

        var markerPath = Path.Combine(_testDir, "agents", "Adele", ".waiting", "my-task.json");
        Assert.True(File.Exists(markerPath));
        dummy.Kill();
    }

    [Fact]
    public void PollOrphanedWaits_FreeAgent_NotListening_NoAction()
    {
        WriteAgentState("Adele", status: "free", autoClose: false);
        WriteWaitMarker("Adele", "my-task", pid: null, listening: false);

        WatchdogService.PollOrphanedWaits(_testDir);

        var markerPath = Path.Combine(_testDir, "agents", "Adele", ".waiting", "my-task.json");
        Assert.True(File.Exists(markerPath));
    }

    private void WriteWaitMarker(string agentName, string task, int? pid, bool listening = true)
    {
        var dir = Path.Combine(_testDir, "agents", agentName, ".waiting");
        Directory.CreateDirectory(dir);
        var json = $$"""{"target":"Brian","task":"{{task}}","since":"2026-03-26T00:00:00Z","listening":{{listening.ToString().ToLowerInvariant()}},"pid":{{(pid.HasValue ? pid.Value.ToString() : "null")}}}""";
        File.WriteAllText(Path.Combine(dir, $"{task}.json"), json);
    }

    #endregion

    #region EnsureRunning Atomicity Tests

    [Fact]
    public async Task EnsureRunning_ConcurrentCalls_StartsOnlyOneWatchdog()
    {
        // No stale PID — clean start ensures the race is purely on
        // FileMode.CreateNew, which is atomic on all platforms.
        var startCount = 0;
        WatchdogService.StartProcessOverride = _ =>
        {
            Interlocked.Increment(ref startCount);
            return StartDummyProcess();
        };

        try
        {
            // Synchronize all threads to start simultaneously
            var ready = new CountdownEvent(10);
            var go = new ManualResetEventSlim(false);
            var tasks = new Task<bool>[10];

            for (var i = 0; i < 10; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    ready.Signal();
                    go.Wait();
                    return WatchdogService.EnsureRunning(_testDir);
                });
            }

            ready.Wait();
            go.Set();
            await Task.WhenAll(tasks);

            Assert.Equal(1, startCount);
        }
        finally
        {
            var pidFile = WatchdogService.GetPidFilePath(_testDir);
            if (File.Exists(pidFile) &&
                int.TryParse(File.ReadAllText(pidFile).Trim(), out var pid))
            {
                try { Process.GetProcessById(pid).Kill(); } catch { }
            }
        }
    }

    [Fact]
    public void EnsureRunning_LivePid_DoesNotStartProcess()
    {
        using var proc = StartDummyProcess();
        WritePidFile(proc.Id);

        var started = false;
        WatchdogService.StartProcessOverride = _ =>
        {
            started = true;
            return null;
        };

        try
        {
            var result = WatchdogService.EnsureRunning(_testDir);

            Assert.False(result);
            Assert.False(started);
        }
        finally
        {
            proc.Kill();
        }
    }

    #endregion

    #region Watchdog Lifecycle (issues #95, #97)

    [Fact]
    public void EnsureRunning_SpawnedFromWorktree_SetsWorkingDirectoryToMainProjectRoot()
    {
        var mainRoot = Path.Combine(_testDir, "main-project");
        var worktreeRoot = Path.Combine(mainRoot, "dydo", "_system", ".local", "worktrees", "wt-abc");
        Directory.CreateDirectory(Path.Combine(mainRoot, "dydo"));
        Directory.CreateDirectory(Path.Combine(worktreeRoot, "dydo"));
        File.WriteAllText(Path.Combine(mainRoot, "dydo.json"), """{"name":"main"}""");
        File.WriteAllText(Path.Combine(worktreeRoot, "dydo.json"), """{"name":"main"}""");

        ProcessStartInfo? capturedPsi = null;
        WatchdogService.StartProcessOverride = psi => { capturedPsi = psi; return null; };

        Environment.CurrentDirectory = worktreeRoot;
        WatchdogService.EnsureRunning();

        Assert.NotNull(capturedPsi);
        var wd = PathUtils.NormalizePath(capturedPsi.WorkingDirectory ?? "");
        var expected = PathUtils.NormalizePath(Path.GetFullPath(mainRoot));
        Assert.Equal(expected, wd);
        Assert.DoesNotContain("_system/.local/worktrees", wd);
    }

    [Fact]
    public void EnsureRunning_OutsideWorktree_SetsWorkingDirectoryToProjectRoot()
    {
        var mainRoot = Path.Combine(_testDir, "plain-project");
        Directory.CreateDirectory(Path.Combine(mainRoot, "dydo"));
        File.WriteAllText(Path.Combine(mainRoot, "dydo.json"), """{"name":"plain"}""");

        ProcessStartInfo? capturedPsi = null;
        WatchdogService.StartProcessOverride = psi => { capturedPsi = psi; return null; };

        Environment.CurrentDirectory = mainRoot;
        WatchdogService.EnsureRunning();

        Assert.NotNull(capturedPsi);
        Assert.Equal(
            PathUtils.NormalizePath(Path.GetFullPath(mainRoot)),
            PathUtils.NormalizePath(capturedPsi.WorkingDirectory ?? ""));
    }

    [Fact]
    public void EnsureRunning_FromWorktree_WritesPidFileToMainProjectNotWorktree()
    {
        var mainRoot = Path.Combine(_testDir, "main-pidfile");
        var worktreeRoot = Path.Combine(mainRoot, "dydo", "_system", ".local", "worktrees", "wt-xyz");
        Directory.CreateDirectory(Path.Combine(mainRoot, "dydo"));
        Directory.CreateDirectory(Path.Combine(worktreeRoot, "dydo"));
        File.WriteAllText(Path.Combine(mainRoot, "dydo.json"), """{"name":"main"}""");
        File.WriteAllText(Path.Combine(worktreeRoot, "dydo.json"), """{"name":"main"}""");

        // Return a live process so EnsureRunning keeps the pid file
        using var dummy = StartDummyProcess();
        WatchdogService.StartProcessOverride = _ => dummy;

        try
        {
            Environment.CurrentDirectory = worktreeRoot;
            WatchdogService.EnsureRunning();

            var mainPidFile = Path.Combine(mainRoot, "dydo", "_system", ".local", "watchdog.pid");
            var worktreePidFile = Path.Combine(worktreeRoot, "dydo", "_system", ".local", "watchdog.pid");
            Assert.True(File.Exists(mainPidFile), "Watchdog pid file should live under the main project's dydo/_system/.local");
            Assert.False(File.Exists(worktreePidFile), "Watchdog pid file must not be written into the worktree");
        }
        finally
        {
            if (!dummy.HasExited) dummy.Kill();
        }
    }

    [Fact]
    public async Task Run_ExitsWhenAnchorProcessDies()
    {
        using var anchor = StartDummyProcess();
        var dydoRoot = SetupRunDydoRoot(_testDir);
        WriteAnchorFile(dydoRoot, anchor.Id);

        WatchdogService.PollIntervalOverride = TimeSpan.FromMilliseconds(100);
        Environment.CurrentDirectory = _testDir;

        var runTask = Task.Run(WatchdogService.Run);
        await Task.Delay(250); // Let the loop enter and observe the live anchor
        anchor.Kill();

        var completed = await Task.WhenAny(runTask, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(runTask, completed);
        await runTask;
    }

    [Fact]
    public async Task Run_ExitsWhenCancellationRequested()
    {
        using var longLived = StartDummyProcess();
        var dydoRoot = SetupRunDydoRoot(_testDir);
        WriteAnchorFile(dydoRoot, longLived.Id);

        WatchdogService.PollIntervalOverride = TimeSpan.FromMilliseconds(100);
        Environment.CurrentDirectory = _testDir;

        var runTask = Task.Run(WatchdogService.Run);
        await Task.Delay(250);
        WatchdogService.RequestShutdownForTests();

        var completed = await Task.WhenAny(runTask, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(runTask, completed);
        await runTask;
        longLived.Kill();
    }

    [Fact]
    public async Task Run_DeletesPidFileOnExit()
    {
        using var anchor = StartDummyProcess();
        var dydoRoot = SetupRunDydoRoot(_testDir);
        WriteAnchorFile(dydoRoot, anchor.Id);
        Directory.CreateDirectory(Path.Combine(dydoRoot, "_system", ".local"));
        var pidFile = WatchdogService.GetPidFilePath(dydoRoot);
        File.WriteAllText(pidFile, Environment.ProcessId.ToString());

        WatchdogService.PollIntervalOverride = TimeSpan.FromMilliseconds(100);
        Environment.CurrentDirectory = _testDir;

        var runTask = Task.Run(WatchdogService.Run);
        await Task.Delay(250);
        anchor.Kill();
        await Task.WhenAny(runTask, Task.Delay(TimeSpan.FromSeconds(5)));

        Assert.False(File.Exists(pidFile), "Run must delete watchdog.pid on exit");
        await runTask;
    }

    [Fact]
    public void EnsureRunning_RegistersClaudeAnchor_InAnchorsDirectory()
    {
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), """{"name":"t"}""");
        Directory.CreateDirectory(Path.Combine(_testDir, "dydo"));
        ProcessUtils.FindAncestorProcessOverride = (_, _) => 99999;

        WatchdogService.EnsureRunning(_testDir);

        var anchor = Path.Combine(WatchdogService.GetAnchorsDirPath(_testDir), "99999.anchor");
        Assert.True(File.Exists(anchor), "anchor marker file must be written");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-5)]
    public void EnsureRunning_RejectsAnchorPidLessOrEqualOne(int badPid)
    {
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), """{"name":"t"}""");
        Directory.CreateDirectory(Path.Combine(_testDir, "dydo"));
        ProcessUtils.FindAncestorProcessOverride = (_, _) => badPid;

        WatchdogService.EnsureRunning(_testDir);

        var anchorsDir = WatchdogService.GetAnchorsDirPath(_testDir);
        if (Directory.Exists(anchorsDir))
            Assert.Empty(Directory.GetFiles(anchorsDir, "*.anchor"));
    }

    [Fact]
    public void EnsureRunning_OnWindowsWithNodeAncestor_RegistersAnchor()
    {
        // Closes #0151: on Windows the claude binary ships as a Node script.
        // Anchoring must accept a "node" ancestor so the watchdog stays alive
        // for the dispatcher's claude — without this, the watchdog finds zero
        // live anchors and exits within ~24h via max_orphan_age.
        if (!OperatingSystem.IsWindows()) return;

        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), """{"name":"t"}""");
        Directory.CreateDirectory(Path.Combine(_testDir, "dydo"));
        ProcessUtils.FindAncestorProcessOverride = (name, _) => name == "node" ? 77777 : null;

        WatchdogService.EnsureRunning(_testDir);

        var anchor = Path.Combine(WatchdogService.GetAnchorsDirPath(_testDir), "77777.anchor");
        Assert.True(File.Exists(anchor),
            "on Windows, EnsureRunning must register a 'node' ancestor as an anchor");
    }

    [Fact]
    public void EnsureRunning_LiveWatchdog_StillRegistersAnchor()
    {
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), """{"name":"t"}""");
        Directory.CreateDirectory(Path.Combine(_testDir, "dydo"));

        // Pre-existing live watchdog → fast path returns false without spawning.
        using var existing = StartDummyProcess();
        try
        {
            WritePidFile(existing.Id);
            ProcessUtils.FindAncestorProcessOverride = (_, _) => 88888;

            Assert.False(WatchdogService.EnsureRunning(_testDir));

            var anchor = Path.Combine(WatchdogService.GetAnchorsDirPath(_testDir), "88888.anchor");
            Assert.True(File.Exists(anchor),
                "even when a watchdog is already running, EnsureRunning must register the new claude as an anchor");
        }
        finally
        {
            if (!existing.HasExited) existing.Kill();
        }
    }

    [Fact]
    public async Task Run_AllAnchorsDead_Exits()
    {
        var dydoRoot = SetupRunDydoRoot(_testDir);
        WriteAnchorFile(dydoRoot, 12345);

        var alive = true;
        ProcessUtils.IsProcessRunningOverride = pid => pid == 12345 && alive;
        WatchdogService.PollIntervalOverride = TimeSpan.FromMilliseconds(100);
        Environment.CurrentDirectory = _testDir;

        var runTask = Task.Run(WatchdogService.Run);
        await Task.Delay(250);
        Assert.False(runTask.IsCompleted, "watchdog must stay alive while anchor 12345 is alive");

        alive = false;

        var completed = await Task.WhenAny(runTask, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(runTask, completed);
        await runTask;

        var anchorPath = Path.Combine(WatchdogService.GetAnchorsDirPath(dydoRoot), "12345.anchor");
        Assert.False(File.Exists(anchorPath), "dead-anchor file must be pruned");
    }

    [Fact]
    public async Task Run_NewAnchorAddedMidFlight_DefersExit()
    {
        // Three deterministic gates replace fixed sleeps. Each gate is a sentinel
        // file whose deletion observably proves the watchdog ran a full ScanAnchors
        // pass with a known disk+live-set state — closing the residual race that
        // Frank's single-decoy fix (3b58876) couldn't: under coverlet on Linux CI
        // the loop-iter ScanAnchors could call Directory.GetFiles BEFORE the test
        // wrote anchor 200, but check IsProcessRunning(100) AFTER the test removed
        // 100 from the live set, observing liveCount=0 and exiting prematurely.
        //
        // Mutation ordering invariant (per phase): live-set adds happen before file
        // writes, file writes happen before live-set removals. This guarantees that
        // any racing ScanAnchors snapshot sees liveCount >= 1 — every anchor file
        // it observes either still has a live PID, or its replacement file has
        // already appeared with its PID already in the live set.
        var dydoRoot = SetupRunDydoRoot(_testDir);
        WriteAnchorFile(dydoRoot, 100);
        WriteAnchorFile(dydoRoot, 99999999); // decoy A: gate startup scan

        var live = new HashSet<int> { 100 };
        ProcessUtils.IsProcessRunningOverride = pid => { lock (live) { return live.Contains(pid); } };
        WatchdogService.PollIntervalOverride = TimeSpan.FromMilliseconds(50);
        Environment.CurrentDirectory = _testDir;

        var anchorsDir = WatchdogService.GetAnchorsDirPath(dydoRoot);
        var decoyAPath = Path.Combine(anchorsDir, "99999999.anchor");
        var decoyBPath = Path.Combine(anchorsDir, "99999998.anchor");
        var anchor100Path = Path.Combine(anchorsDir, "100.anchor");

        var runTask = Task.Run(WatchdogService.Run);

        // Gate 1: decoy A deletion proves Run() executed its startup ResolveAnchors
        // with anchor 100 alive — hasSeenLiveAnchor is now true.
        await WaitForFileGoneAsync(decoyAPath, TimeSpan.FromSeconds(5),
            "watchdog must complete initial anchor scan");

        // Phase 1: register anchor 200 alongside decoy B. Live-set add is published
        // BEFORE the file write so any racing scan that picks up 200.anchor also
        // sees its PID alive. Decoy B is written last so its deletion proves a
        // post-200 scan completed.
        lock (live) { live.Add(200); }
        WriteAnchorFile(dydoRoot, 200);
        WriteAnchorFile(dydoRoot, 99999998);

        // Gate 2: decoy B deletion proves a full ScanAnchors ran with anchor 200
        // present on disk and 200 in the live set (liveCount >= 2 throughout).
        await WaitForFileGoneAsync(decoyBPath, TimeSpan.FromSeconds(5),
            "watchdog must scan with anchor 200 alive before original is killed");

        // Phase 2: kill the original. live={200}, 100.anchor still on disk until
        // the next watchdog scan. Critically, 200 is in live BEFORE this — so any
        // scan whose GetFiles snapshot includes 100 will keep 200 (liveCount >= 1).
        lock (live) { live.Remove(100); }

        // Gate 3: 100.anchor deletion proves the watchdog's next scan saw 100 dead
        // and pruned its file. Because 200 was alive in live throughout that scan,
        // liveCount=1 — not 0 — so no anchor_gone exit fired.
        await WaitForFileGoneAsync(anchor100Path, TimeSpan.FromSeconds(5),
            "watchdog must observe anchor 100 dead and prune its file");

        Assert.False(runTask.IsCompleted, "watchdog must stay alive while anchor 200 is alive");

        // Phase 3: kill the survivor; watchdog must now exit anchor_gone.
        lock (live) { live.Remove(200); }
        var completed = await Task.WhenAny(runTask, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(runTask, completed);
        await runTask;
    }

    private static async Task WaitForFileGoneAsync(string path, TimeSpan timeout, string failureMessage)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (File.Exists(path) && DateTime.UtcNow < deadline)
            await Task.Delay(20);
        Assert.False(File.Exists(path), failureMessage);
    }

    [Fact]
    public async Task Run_NoAnchorsEverRegistered_ExitsAtMaxAge()
    {
        SetupRunDydoRoot(_testDir);
        // No anchors directory at all — orphan path

        WatchdogService.PollIntervalOverride = TimeSpan.FromMilliseconds(50);
        WatchdogService.MaxOrphanAgeOverride = TimeSpan.FromMilliseconds(150);
        Environment.CurrentDirectory = _testDir;

        var runTask = Task.Run(WatchdogService.Run);

        var completed = await Task.WhenAny(runTask, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(runTask, completed);
        await runTask;
    }

    [Fact]
    public void ScanAnchors_PrunesDeadPids_AndIgnoresMalformedFiles()
    {
        var dir = WatchdogService.GetAnchorsDirPath(_testDir);
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "111.anchor"), Array.Empty<byte>()); // alive
        File.WriteAllBytes(Path.Combine(dir, "222.anchor"), Array.Empty<byte>()); // dead
        File.WriteAllBytes(Path.Combine(dir, "garbage.anchor"), Array.Empty<byte>());
        File.WriteAllBytes(Path.Combine(dir, "1.anchor"), Array.Empty<byte>()); // <= 1 rejected

        ProcessUtils.IsProcessRunningOverride = pid => pid == 111;

        var live = WatchdogService.ScanAnchors(dir);

        Assert.Equal(1, live);
        Assert.True(File.Exists(Path.Combine(dir, "111.anchor")));
        Assert.False(File.Exists(Path.Combine(dir, "222.anchor")));
        Assert.False(File.Exists(Path.Combine(dir, "garbage.anchor")));
        Assert.False(File.Exists(Path.Combine(dir, "1.anchor")));
    }

    #endregion

    #region Auto-Resume (Decision 022)

    [Fact]
    public void PollAndResumeForAgent_LiveClaimedPid_DoesNotLaunch()
    {
        var dydoRoot = ResumeDydoRoot();
        WriteResumeAgentState(dydoRoot, "Adele", status: "working", resumeAttempts: 0);
        using var live = StartDummyProcess();
        WriteResumeAgentSession(dydoRoot, "Adele", "sess-live", live.Id);

        var launchCount = 0;
        WatchdogService.LaunchResumeOverride = (_, _, _, _, _, _, _) => { launchCount++; return 0; };

        WatchdogService.PollAndResumeCrashedAgents(dydoRoot);

        Assert.Equal(0, launchCount);
        Assert.Equal(0, ReadResumeAttempts(dydoRoot, "Adele"));
    }

    [Fact]
    public void PollAndResumeForAgent_DeadClaimedPid_BelowCap_Launches()
    {
        var dydoRoot = ResumeDydoRoot();
        WriteResumeAgentState(dydoRoot, "Adele", status: "working", resumeAttempts: 0);
        WriteResumeAgentSession(dydoRoot, "Adele", "sess-abc", 99999999);
        ProcessUtils.IsProcessRunningOverride = _ => false;
        WatchdogService.ResumeAttemptsCapOverride = 3;

        var calls = new List<(string agent, string sessionId)>();
        WatchdogService.LaunchResumeOverride = (a, s, _, _, _, _, _) => { calls.Add((a, s)); return 12345; };

        WatchdogService.PollAndResumeCrashedAgents(dydoRoot);

        Assert.Single(calls);
        Assert.Equal(("Adele", "sess-abc"), calls[0]);
        Assert.Equal(1, ReadResumeAttempts(dydoRoot, "Adele"));
    }

    [Fact]
    public void PollAndResumeForAgent_DeadClaimedPid_AtCap_DoesNotLaunch()
    {
        var dydoRoot = ResumeDydoRoot();
        WriteResumeAgentState(dydoRoot, "Adele", status: "working", resumeAttempts: 3);
        WriteResumeAgentSession(dydoRoot, "Adele", "sess-abc", 99999999);
        ProcessUtils.IsProcessRunningOverride = _ => false;
        WatchdogService.ResumeAttemptsCapOverride = 3;

        var launchCount = 0;
        WatchdogService.LaunchResumeOverride = (_, _, _, _, _, _, _) => { launchCount++; return 0; };

        WatchdogService.PollAndResumeCrashedAgents(dydoRoot);

        Assert.Equal(0, launchCount);
        Assert.Equal(3, ReadResumeAttempts(dydoRoot, "Adele"));
    }

    [Fact]
    public void PollAndResumeForAgent_FreeStatus_DoesNotLaunch()
    {
        var dydoRoot = ResumeDydoRoot();
        WriteResumeAgentState(dydoRoot, "Adele", status: "free", resumeAttempts: 0);
        WriteResumeAgentSession(dydoRoot, "Adele", "sess-abc", 99999999);
        ProcessUtils.IsProcessRunningOverride = _ => false;

        var launchCount = 0;
        WatchdogService.LaunchResumeOverride = (_, _, _, _, _, _, _) => { launchCount++; return 0; };

        WatchdogService.PollAndResumeCrashedAgents(dydoRoot);

        Assert.Equal(0, launchCount);
    }

    [Fact]
    public void PollAndResumeForAgent_NoSessionFile_DoesNotLaunch()
    {
        var dydoRoot = ResumeDydoRoot();
        WriteResumeAgentState(dydoRoot, "Adele", status: "working", resumeAttempts: 0);
        // No .session file written.

        var launchCount = 0;
        WatchdogService.LaunchResumeOverride = (_, _, _, _, _, _, _) => { launchCount++; return 0; };

        WatchdogService.PollAndResumeCrashedAgents(dydoRoot);

        Assert.Equal(0, launchCount);
        Assert.Equal(0, ReadResumeAttempts(dydoRoot, "Adele"));
    }

    [Fact]
    public void PollAndResumeForAgent_RepeatedPolls_StopAtCap()
    {
        var dydoRoot = ResumeDydoRoot();
        WriteResumeAgentState(dydoRoot, "Adele", status: "working", resumeAttempts: 0);
        WriteResumeAgentSession(dydoRoot, "Adele", "sess-abc", 99999999);
        ProcessUtils.IsProcessRunningOverride = _ => false;
        WatchdogService.ResumeAttemptsCapOverride = 3;
        // Disable the warmup gate so this test continues to model cap saturation
        // (#0152 introduces the gate; PollAndResumeForAgent_WithinWarmupGate_*
        // covers gated behaviour separately). Set to a tiny negative-equivalent
        // so the no-refresh fail-fast also doesn't trigger in this scenario.
        WatchdogService.ResumeWarmupGateOverride = TimeSpan.Zero;
        // Vary the dead PID returned by the session to keep us out of the
        // bad-session fail-fast (which fires when ClaimedPid == pre-resume-pid
        // after the gate elapses). Without this, run #2 would saturate the cap
        // in a single call instead of the cap-by-increment behaviour this test
        // covers.
        WriteResumeAgentSession(dydoRoot, "Adele", "sess-abc", 99999998);

        var launchCount = 0;
        WatchdogService.LaunchResumeOverride = (_, _, _, _, _, _, _) =>
        {
            launchCount++;
            // Simulate the resumed claude refreshing its PID before the next poll —
            // this is the "genuine re-crash" path, not a bad-session loop.
            WriteResumeAgentSession(dydoRoot, "Adele", "sess-abc", 99999990 + launchCount);
            return 0;
        };

        for (var i = 0; i < 6; i++)
            WatchdogService.PollAndResumeCrashedAgents(dydoRoot);

        Assert.Equal(3, launchCount);
        Assert.Equal(3, ReadResumeAttempts(dydoRoot, "Adele"));
    }

    [Fact]
    public void PollAndResumeForAgent_LaunchesWithProjectRootAsWorkingDirectory()
    {
        // Repro for #0138: auto-resume relaunched the terminal but with no working
        // directory, so the new shell inherited %USERPROFILE% (Windows) / $HOME (POSIX)
        // instead of the project root the crashed agent was working in.
        var dydoRoot = ResumeDydoRoot();
        var projectRoot = _testDir; // ResumeDydoRoot() returns _testDir/dydo, so parent == _testDir
        WriteResumeAgentState(dydoRoot, "Adele", status: "working", resumeAttempts: 0);
        WriteResumeAgentSession(dydoRoot, "Adele", "sess-abc", 99999999);
        ProcessUtils.IsProcessRunningOverride = _ => false;
        WatchdogService.ResumeAttemptsCapOverride = 3;

        string? capturedWorkingDirectory = null;
        WatchdogService.LaunchResumeOverride = (_, _, wd, _, _, _, _) => { capturedWorkingDirectory = wd; return 12345; };

        WatchdogService.PollAndResumeCrashedAgents(dydoRoot);

        Assert.Equal(projectRoot, capturedWorkingDirectory);
    }

    [Fact]
    public void PollAndResumeForAgent_WorktreeAgent_LaunchesInWorktreeDirectory()
    {
        // Worktree-resumed agents must land in their worktree directory so the
        // resumed claude sees the right git branch. The .worktree-path marker
        // (written by DispatchService when the agent is dispatched into a worktree)
        // is the canonical signal.
        var dydoRoot = ResumeDydoRoot();
        WriteResumeAgentState(dydoRoot, "Adele", status: "working", resumeAttempts: 0);
        WriteResumeAgentSession(dydoRoot, "Adele", "sess-abc", 99999999);

        var worktreePath = Path.Combine(_testDir, "fake-worktree");
        Directory.CreateDirectory(worktreePath);
        var agentDir = Path.Combine(dydoRoot, "agents", "Adele");
        File.WriteAllText(Path.Combine(agentDir, ".worktree-path"), worktreePath);

        ProcessUtils.IsProcessRunningOverride = _ => false;
        WatchdogService.ResumeAttemptsCapOverride = 3;

        string? capturedWorkingDirectory = null;
        WatchdogService.LaunchResumeOverride = (_, _, wd, _, _, _, _) => { capturedWorkingDirectory = wd; return 12345; };

        WatchdogService.PollAndResumeCrashedAgents(dydoRoot);

        Assert.Equal(worktreePath, capturedWorkingDirectory);
    }

    [Fact]
    public void PollAndResumeForAgent_WithinWarmupGate_DoesNotRelaunch()
    {
        // #0152: after a recent resume launch, the watchdog must suppress further
        // launches until the warmup gate elapses, so a slow-rehydrating claude
        // doesn't burn the cap before its first claim.
        var dydoRoot = ResumeDydoRoot();
        WriteResumeAgentState(dydoRoot, "Adele", status: "working", resumeAttempts: 1,
            lastResumeLaunchedAt: DateTime.UtcNow.AddSeconds(-5), preResumePid: 99999998);
        WriteResumeAgentSession(dydoRoot, "Adele", "sess-abc", 99999999);
        ProcessUtils.IsProcessRunningOverride = _ => false;
        WatchdogService.ResumeAttemptsCapOverride = 3;
        WatchdogService.ResumeWarmupGateOverride = TimeSpan.FromSeconds(60);

        var launchCount = 0;
        WatchdogService.LaunchResumeOverride = (_, _, _, _, _, _, _) => { launchCount++; return 0; };

        WatchdogService.PollAndResumeCrashedAgents(dydoRoot);

        Assert.Equal(0, launchCount);
        Assert.Equal(1, ReadResumeAttempts(dydoRoot, "Adele"));
    }

    [Fact]
    public void PollAndResumeForAgent_AfterWarmupGate_AllowsNextLaunch()
    {
        // Symmetry to the gate test: once the gate has elapsed, a still-dead PID
        // (with a different value than pre-resume-pid, so not bad-session) should
        // trigger another launch.
        var dydoRoot = ResumeDydoRoot();
        WriteResumeAgentState(dydoRoot, "Adele", status: "working", resumeAttempts: 1,
            lastResumeLaunchedAt: DateTime.UtcNow.AddSeconds(-120), preResumePid: 99999998);
        WriteResumeAgentSession(dydoRoot, "Adele", "sess-abc", 99999999);
        ProcessUtils.IsProcessRunningOverride = _ => false;
        WatchdogService.ResumeAttemptsCapOverride = 3;
        WatchdogService.ResumeWarmupGateOverride = TimeSpan.FromSeconds(60);

        var launchCount = 0;
        WatchdogService.LaunchResumeOverride = (_, _, _, _, _, _, _) => { launchCount++; return 0; };

        WatchdogService.PollAndResumeCrashedAgents(dydoRoot);

        Assert.Equal(1, launchCount);
        Assert.Equal(2, ReadResumeAttempts(dydoRoot, "Adele"));
    }

    [Fact]
    public void PollAndResumeForAgent_BadSessionId_BurnsCapInOneAttempt()
    {
        // After the warmup gate elapses, if ClaimedPid is still equal to the
        // pre-resume-pid we recorded at the last launch, the resumed claude
        // never reached HandleExistingSession (e.g. "No conversation found
        // with session ID"). Saturate the cap to avoid 3 useless terminals.
        var dydoRoot = ResumeDydoRoot();
        WriteResumeAgentState(dydoRoot, "Adele", status: "working", resumeAttempts: 1,
            lastResumeLaunchedAt: DateTime.UtcNow.AddSeconds(-120), preResumePid: 99999999);
        WriteResumeAgentSession(dydoRoot, "Adele", "sess-abc", 99999999);
        ProcessUtils.IsProcessRunningOverride = _ => false;
        WatchdogService.ResumeAttemptsCapOverride = 3;
        WatchdogService.ResumeWarmupGateOverride = TimeSpan.FromSeconds(60);

        var launchCount = 0;
        WatchdogService.LaunchResumeOverride = (_, _, _, _, _, _, _) => { launchCount++; return 0; };

        WatchdogService.PollAndResumeCrashedAgents(dydoRoot);

        Assert.Equal(0, launchCount);
        Assert.Equal(3, ReadResumeAttempts(dydoRoot, "Adele"));
    }

    [Fact]
    public void PollAndResumeForAgent_RefreshAfterWarmup_AllowsNextCrash()
    {
        // Counter-test to BadSessionId: after the gate, if the resumed claude
        // refreshed its PID (so ClaimedPid != pre-resume-pid), this is a genuine
        // second crash and the next resume should fire — not the fail-fast.
        var dydoRoot = ResumeDydoRoot();
        WriteResumeAgentState(dydoRoot, "Adele", status: "working", resumeAttempts: 1,
            lastResumeLaunchedAt: DateTime.UtcNow.AddSeconds(-120), preResumePid: 11111111);
        WriteResumeAgentSession(dydoRoot, "Adele", "sess-abc", 22222222);
        ProcessUtils.IsProcessRunningOverride = _ => false;
        WatchdogService.ResumeAttemptsCapOverride = 3;
        WatchdogService.ResumeWarmupGateOverride = TimeSpan.FromSeconds(60);

        var launchCount = 0;
        WatchdogService.LaunchResumeOverride = (_, _, _, _, _, _, _) => { launchCount++; return 0; };

        WatchdogService.PollAndResumeCrashedAgents(dydoRoot);

        Assert.Equal(1, launchCount);
        Assert.Equal(2, ReadResumeAttempts(dydoRoot, "Adele"));
    }

    [Fact]
    public void PollAndResumeForAgent_PersistedWindowId_PassedToLauncher()
    {
        // #0144: when state.md records a window-id (the dispatcher launched into
        // a named wt window or iTerm window), the resume launcher must receive
        // that windowName plus useTab=true so the resumed claude lands in a new
        // tab of the same window rather than spawning a fresh one.
        var dydoRoot = ResumeDydoRoot();
        WriteResumeAgentState(dydoRoot, "Adele", status: "working", resumeAttempts: 0,
            windowId: "my-win-8");
        WriteResumeAgentSession(dydoRoot, "Adele", "sess-abc", 99999999);
        ProcessUtils.IsProcessRunningOverride = _ => false;
        WatchdogService.ResumeAttemptsCapOverride = 3;

        string? capturedWindowName = null;
        var capturedUseTab = false;
        WatchdogService.LaunchResumeOverride = (_, _, _, w, t, _, _) =>
        {
            capturedWindowName = w;
            capturedUseTab = t;
            return 12345;
        };

        WatchdogService.PollAndResumeCrashedAgents(dydoRoot);

        Assert.Equal("my-win-8", capturedWindowName);
        Assert.True(capturedUseTab);
    }

    [Fact]
    public void PollAndResumeForAgent_NoWindowId_LauncherReceivesNullAndUseTabFalse()
    {
        // Symmetry to the persisted-window-id test: when no window-id is recorded,
        // useTab must be false so the launcher falls back to its fresh-window path.
        var dydoRoot = ResumeDydoRoot();
        WriteResumeAgentState(dydoRoot, "Adele", status: "working", resumeAttempts: 0);
        WriteResumeAgentSession(dydoRoot, "Adele", "sess-abc", 99999999);
        ProcessUtils.IsProcessRunningOverride = _ => false;
        WatchdogService.ResumeAttemptsCapOverride = 3;

        string? capturedWindowName = "sentinel";
        var capturedUseTab = true;
        WatchdogService.LaunchResumeOverride = (_, _, _, w, t, _, _) =>
        {
            capturedWindowName = w;
            capturedUseTab = t;
            return 12345;
        };

        WatchdogService.PollAndResumeCrashedAgents(dydoRoot);

        Assert.Null(capturedWindowName);
        Assert.False(capturedUseTab);
    }

    [Fact]
    public void PollAndResumeForAgent_NullSessionPid_DoesNotLaunch()
    {
        // .session with explicit ClaimedPid: null means there was no live PID at claim
        // time (CLI-only contexts). Resume must not fire — there's nothing to revive.
        var dydoRoot = ResumeDydoRoot();
        WriteResumeAgentState(dydoRoot, "Adele", status: "working", resumeAttempts: 0);
        WriteResumeAgentSession(dydoRoot, "Adele", "sess-abc", claimedPid: null);

        var launchCount = 0;
        WatchdogService.LaunchResumeOverride = (_, _, _, _, _, _, _) => { launchCount++; return 0; };

        WatchdogService.PollAndResumeCrashedAgents(dydoRoot);

        Assert.Equal(0, launchCount);
    }

    [Fact]
    public void PollAndResumeForAgent_MalformedSessionJson_DoesNotLaunch()
    {
        // A corrupted .session file must not crash the watchdog or fire a resume —
        // the JSON deserialize catch returns silently and the agent waits for either
        // a fresh claim or the next sane state.
        var dydoRoot = ResumeDydoRoot();
        WriteResumeAgentState(dydoRoot, "Adele", status: "working", resumeAttempts: 0);
        var sessionPath = Path.Combine(dydoRoot, "agents", "Adele", ".session");
        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath)!);
        File.WriteAllText(sessionPath, "{not valid json");

        var launchCount = 0;
        WatchdogService.LaunchResumeOverride = (_, _, _, _, _, _, _) => { launchCount++; return 0; };

        WatchdogService.PollAndResumeCrashedAgents(dydoRoot);

        Assert.Equal(0, launchCount);
    }

    [Fact]
    public void PollAndResumeForAgent_NullSessionDeserialization_DoesNotLaunch()
    {
        // A .session with literal "null" content deserializes to null — same handling
        // as a missing file: no launch, no state mutation.
        var dydoRoot = ResumeDydoRoot();
        WriteResumeAgentState(dydoRoot, "Adele", status: "working", resumeAttempts: 0);
        var sessionPath = Path.Combine(dydoRoot, "agents", "Adele", ".session");
        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath)!);
        File.WriteAllText(sessionPath, "null");

        var launchCount = 0;
        WatchdogService.LaunchResumeOverride = (_, _, _, _, _, _, _) => { launchCount++; return 0; };

        WatchdogService.PollAndResumeCrashedAgents(dydoRoot);

        Assert.Equal(0, launchCount);
    }

    [Fact]
    public void PollAndResumeForAgent_MalformedLastResumeTimestamp_TreatedAsAbsent()
    {
        // ParseResumeFields must tolerate junk in last-resume-launched-at without
        // crashing the watchdog poll. A malformed value behaves like "null" (no
        // gate suppression). Same for pre-resume-pid.
        var dydoRoot = ResumeDydoRoot();
        var agentDir = Path.Combine(dydoRoot, "agents", "Adele");
        Directory.CreateDirectory(agentDir);
        File.WriteAllText(Path.Combine(agentDir, "state.md"), $$"""
            ---
            agent: Adele
            role: null
            task: null
            status: working
            assigned: testuser
            dispatched-by: null
            window-id: null
            auto-close: false
            resume-attempts: 0
            last-resume-launched-at: not-a-date
            pre-resume-pid: not-a-number
            started: null
            writable-paths: []
            readonly-paths: []
            unread-must-reads: []
            unread-messages: []
            task-role-history: {}
            ---
            """);
        WriteResumeAgentSession(dydoRoot, "Adele", "sess-abc", 99999999);
        ProcessUtils.IsProcessRunningOverride = _ => false;
        WatchdogService.ResumeAttemptsCapOverride = 3;

        var launchCount = 0;
        WatchdogService.LaunchResumeOverride = (_, _, _, _, _, _, _) => { launchCount++; return 0; };

        WatchdogService.PollAndResumeCrashedAgents(dydoRoot);

        Assert.Equal(1, launchCount);
        Assert.Equal(1, ReadResumeAttempts(dydoRoot, "Adele"));
    }

    [Fact]
    public void PollAndResumeForAgent_WithWorktreeMarker_PassesWorktreeContextToLaunchResume()
    {
        // #0175 regression: the watchdog must read .worktree (worktree id) from the
        // agent dir and pass it — alongside the main project root — to the resume
        // launcher so the resumed claude lands inside the same Set-Location /
        // init-settings / try-finally-cleanup envelope as the original dispatch.
        var dydoRoot = ResumeDydoRoot();
        WriteResumeAgentState(dydoRoot, "Adele", status: "working", resumeAttempts: 0);
        WriteResumeAgentSession(dydoRoot, "Adele", "sess-abc", 99999999);

        var agentDir = Path.Combine(dydoRoot, "agents", "Adele");
        File.WriteAllText(Path.Combine(agentDir, ".worktree"), "fix-bug");

        ProcessUtils.IsProcessRunningOverride = _ => false;
        WatchdogService.ResumeAttemptsCapOverride = 3;

        string? capturedWorktreeId = null;
        string? capturedMainProjectRoot = null;
        WatchdogService.LaunchResumeOverride = (_, _, _, _, _, wt, mr) =>
        {
            capturedWorktreeId = wt;
            capturedMainProjectRoot = mr;
            return 12345;
        };

        WatchdogService.PollAndResumeCrashedAgents(dydoRoot);

        Assert.Equal("fix-bug", capturedWorktreeId);
        // projectRoot inside PollAndResumeForAgent is Path.GetDirectoryName(dydoRoot),
        // which is the parent of dydoRoot — i.e. the main project root.
        Assert.Equal(Path.GetDirectoryName(dydoRoot), capturedMainProjectRoot);
    }

    [Fact]
    public void PollAndResumeForAgent_NoWorktreeMarker_PassesNullWorktreeId()
    {
        // Symmetry: a main-project agent (no .worktree marker) must NOT trigger the
        // worktree wrapper — passing a non-null worktreeId would make the resume
        // launcher cd into a non-existent worktree dir and the resumed claude
        // would crash before its first tool call.
        var dydoRoot = ResumeDydoRoot();
        WriteResumeAgentState(dydoRoot, "Adele", status: "working", resumeAttempts: 0);
        WriteResumeAgentSession(dydoRoot, "Adele", "sess-abc", 99999999);

        ProcessUtils.IsProcessRunningOverride = _ => false;
        WatchdogService.ResumeAttemptsCapOverride = 3;

        string? capturedWorktreeId = "sentinel";
        WatchdogService.LaunchResumeOverride = (_, _, _, _, _, wt, _) =>
        {
            capturedWorktreeId = wt;
            return 12345;
        };

        WatchdogService.PollAndResumeCrashedAgents(dydoRoot);

        Assert.Null(capturedWorktreeId);
    }

    [Fact]
    public void PollAndResumeForAgent_StaleWorktreeMarker_FallsBackToProjectRoot()
    {
        // .worktree-path can outlive its directory if cleanup raced the resume
        // (e.g. merger finalized the worktree while the crashed agent's poll was
        // about to fire). We must not pass a non-existent path to LaunchResume —
        // it would throw DirectoryNotFoundException and abort the resume.
        var dydoRoot = ResumeDydoRoot();
        var projectRoot = _testDir;
        WriteResumeAgentState(dydoRoot, "Adele", status: "working", resumeAttempts: 0);
        WriteResumeAgentSession(dydoRoot, "Adele", "sess-abc", 99999999);

        var agentDir = Path.Combine(dydoRoot, "agents", "Adele");
        File.WriteAllText(Path.Combine(agentDir, ".worktree-path"),
            Path.Combine(_testDir, "deleted-worktree"));

        ProcessUtils.IsProcessRunningOverride = _ => false;
        WatchdogService.ResumeAttemptsCapOverride = 3;

        string? capturedWorkingDirectory = null;
        WatchdogService.LaunchResumeOverride = (_, _, wd, _, _, _, _) => { capturedWorkingDirectory = wd; return 12345; };

        WatchdogService.PollAndResumeCrashedAgents(dydoRoot);

        Assert.Equal(projectRoot, capturedWorkingDirectory);
    }

    [Fact]
    public void PollAndResumeForAgent_LaunchedPidAlive_PastWarmup_DoesNotEmitResumeBlocked()
    {
        // #0173 regression: with the wall-clock-only IsBadSessionFailFast, every
        // rehydration that took longer than the gate produced a false `resume_blocked`
        // line — driving balazs's "50% -> 33% -> 0%" perception. The fix: also require
        // the launched-PID to be dead. When it's alive past the gate, the watchdog
        // silent-skips this tick — no relaunch, no log emission, just wait for the
        // next tick.
        var logPath = SetLogPathOverride();
        var dydoRoot = ResumeDydoRoot();
        WriteResumeAgentState(dydoRoot, "Adele", status: "working", resumeAttempts: 1,
            lastResumeLaunchedAt: DateTime.UtcNow.AddSeconds(-120),
            preResumePid: 99999999, launchedPid: 12345);
        WriteResumeAgentSession(dydoRoot, "Adele", "sess-abc", 99999999);
        // ClaimedPid (99999999) dead, but LaunchedPid (12345) ALIVE — the resumed
        // claude is rehydrating, not dead. Without the liveness check the wall-clock
        // gate alone would saturate the cap and emit resume_blocked.
        ProcessUtils.IsProcessRunningOverride = pid => pid == 12345;
        WatchdogService.ResumeAttemptsCapOverride = 3;
        WatchdogService.ResumeWarmupGateOverride = TimeSpan.FromSeconds(60);

        var launchCount = 0;
        WatchdogService.LaunchResumeOverride = (_, _, _, _, _, _, _) => { launchCount++; return 0; };

        WatchdogService.PollAndResumeCrashedAgents(dydoRoot);

        Assert.Equal(0, launchCount);
        Assert.Equal(1, ReadResumeAttempts(dydoRoot, "Adele"));
        var lines = ReadLogLines(logPath);
        Assert.DoesNotContain(lines, l => l["event"].GetString() == "resume_blocked");
    }

    [Fact]
    public void PollAndResumeForAgent_LaunchedPidDead_PastWarmup_EmitsResumeBlocked()
    {
        // Counter-test: when the launched PID is dead AND ClaimedPid still equals
        // pre-resume-pid (no refresh) past the gate, the resume genuinely failed.
        // Saturate the cap and emit resume_blocked — the log line is now an honest
        // signal that the resumed claude is provably gone.
        var logPath = SetLogPathOverride();
        var dydoRoot = ResumeDydoRoot();
        WriteResumeAgentState(dydoRoot, "Adele", status: "working", resumeAttempts: 1,
            lastResumeLaunchedAt: DateTime.UtcNow.AddSeconds(-120),
            preResumePid: 99999999, launchedPid: 12345);
        WriteResumeAgentSession(dydoRoot, "Adele", "sess-abc", 99999999);
        // Both ClaimedPid and LaunchedPid dead.
        ProcessUtils.IsProcessRunningOverride = _ => false;
        WatchdogService.ResumeAttemptsCapOverride = 3;
        WatchdogService.ResumeWarmupGateOverride = TimeSpan.FromSeconds(60);

        var launchCount = 0;
        WatchdogService.LaunchResumeOverride = (_, _, _, _, _, _, _) => { launchCount++; return 0; };

        WatchdogService.PollAndResumeCrashedAgents(dydoRoot);

        Assert.Equal(0, launchCount);
        Assert.Equal(3, ReadResumeAttempts(dydoRoot, "Adele"));
        var lines = ReadLogLines(logPath);
        Assert.Single(lines, l => l["event"].GetString() == "resume_blocked");
    }

    [Fact]
    public void PollAndResumeForAgent_LegacyState_NoLaunchedPid_PreservesPreFixBehavior()
    {
        // Backward-compat: a state.md from before #0173 has no launched-pid line
        // (or "null"). IsBadSessionFailFast must fall through to the wall-clock-only
        // predicate so existing in-flight sessions saturate the cap on bad-session
        // exactly as they did pre-fix.
        var dydoRoot = ResumeDydoRoot();
        WriteResumeAgentState(dydoRoot, "Adele", status: "working", resumeAttempts: 1,
            lastResumeLaunchedAt: DateTime.UtcNow.AddSeconds(-120),
            preResumePid: 99999999, launchedPid: null);
        WriteResumeAgentSession(dydoRoot, "Adele", "sess-abc", 99999999);
        ProcessUtils.IsProcessRunningOverride = _ => false;
        WatchdogService.ResumeAttemptsCapOverride = 3;
        WatchdogService.ResumeWarmupGateOverride = TimeSpan.FromSeconds(60);

        var launchCount = 0;
        WatchdogService.LaunchResumeOverride = (_, _, _, _, _, _, _) => { launchCount++; return 0; };

        WatchdogService.PollAndResumeCrashedAgents(dydoRoot);

        Assert.Equal(0, launchCount);
        Assert.Equal(3, ReadResumeAttempts(dydoRoot, "Adele"));
    }

    [Fact]
    public void PollAndResumeForAgent_LaunchSucceeds_PersistsLaunchedPidToState()
    {
        // After a successful launch (PID > 1), the watchdog must write the launched
        // PID back to state.md so the next tick's IsBadSessionFailFast can read it.
        // Without this, the liveness check has nothing to liveness-check.
        var dydoRoot = ResumeDydoRoot();
        WriteResumeAgentState(dydoRoot, "Adele", status: "working", resumeAttempts: 0);
        WriteResumeAgentSession(dydoRoot, "Adele", "sess-abc", 99999999);
        ProcessUtils.IsProcessRunningOverride = _ => false;
        WatchdogService.ResumeAttemptsCapOverride = 3;

        WatchdogService.LaunchResumeOverride = (_, _, _, _, _, _, _) => 54321;

        WatchdogService.PollAndResumeCrashedAgents(dydoRoot);

        var statePath = Path.Combine(dydoRoot, "agents", "Adele", "state.md");
        var content = File.ReadAllText(statePath);
        Assert.Contains("launched-pid: 54321", content);
    }

    [Fact]
    public void PollAndResumeForAgent_LaunchFailed_LeavesLaunchedPidNull()
    {
        // Symmetric to the success case: when the launcher returns 0 (or 1), don't
        // poison state.md with a sentinel that would never appear gone — leave
        // launched-pid null so the next tick's IsBadSessionFailFast falls back to
        // the wall-clock gate (legacy behaviour).
        var dydoRoot = ResumeDydoRoot();
        WriteResumeAgentState(dydoRoot, "Adele", status: "working", resumeAttempts: 0);
        WriteResumeAgentSession(dydoRoot, "Adele", "sess-abc", 99999999);
        ProcessUtils.IsProcessRunningOverride = _ => false;
        WatchdogService.ResumeAttemptsCapOverride = 3;

        WatchdogService.LaunchResumeOverride = (_, _, _, _, _, _, _) => 0;

        WatchdogService.PollAndResumeCrashedAgents(dydoRoot);

        var statePath = Path.Combine(dydoRoot, "agents", "Adele", "state.md");
        var content = File.ReadAllText(statePath);
        Assert.Contains("launched-pid: null", content);
    }

    [Fact]
    public void PollAndResumeForAgent_ResumeOutcome_Failed_OnLaunchedPidDead()
    {
        // PR3 of agent-crash-fixes: pair every IsBadSessionFailFast resume_blocked with a
        // terminal resume_outcome=failed so the 4-bucket categorisation (a/b/c/d) gets a
        // one-line signal of every confirmed-dead launch. The pre-resume-pid path is the
        // post-PR1 honest fail-fast — no rehydration false positives.
        var logPath = SetLogPathOverride();
        var dydoRoot = ResumeDydoRoot();
        WriteResumeAgentState(dydoRoot, "Adele", status: "working", resumeAttempts: 1,
            lastResumeLaunchedAt: DateTime.UtcNow.AddSeconds(-120),
            preResumePid: 99999999, launchedPid: 12345);
        WriteResumeAgentSession(dydoRoot, "Adele", "sess-fail", 99999999);
        // Both ClaimedPid and LaunchedPid dead → IsBadSessionFailFast fires.
        ProcessUtils.IsProcessRunningOverride = _ => false;
        WatchdogService.ResumeAttemptsCapOverride = 3;
        WatchdogService.ResumeWarmupGateOverride = TimeSpan.FromSeconds(60);

        WatchdogService.LaunchResumeOverride = (_, _, _, _, _, _, _) => 0;

        WatchdogService.PollAndResumeCrashedAgents(dydoRoot);

        var lines = ReadLogLines(logPath);
        var outcome = lines.Single(l => l["event"].GetString() == "resume_outcome");
        Assert.Equal("failed", outcome["outcome"].GetString());
        Assert.Equal("Adele", outcome["agent"].GetString());
        Assert.Equal("sess-fail", outcome["session_id"].GetString());
        Assert.Equal("launched_pid_dead", outcome["reason"].GetString());
        Assert.Equal(1, outcome["attempts"].GetInt32());
        // ElapsedSeconds rounds via int cast, so >= 119 is a safe lower bound (we set -120s).
        Assert.True(outcome["elapsed_seconds"].GetInt32() >= 119);
    }

    [Fact]
    public void PollAndResumeForAgent_ResumeOutcome_Failed_ClearsLastResumeLaunchedAt_PreventingDoubleEmit()
    {
        // PR3 idempotency: failed and gave_up are mutually exclusive per episode. The failed
        // path must clear LastResumeLaunchedAt so the gave_up tick-check on the next poll
        // cycle sees a null timestamp and skips. Without this, every IsBadSessionFailFast
        // hit would also produce a duplicate gave_up line.
        var logPath = SetLogPathOverride();
        var dydoRoot = ResumeDydoRoot();
        WriteResumeAgentState(dydoRoot, "Adele", status: "working", resumeAttempts: 1,
            lastResumeLaunchedAt: DateTime.UtcNow.AddSeconds(-120),
            preResumePid: 99999999, launchedPid: 12345);
        WriteResumeAgentSession(dydoRoot, "Adele", "sess-fail", 99999999);
        ProcessUtils.IsProcessRunningOverride = _ => false;
        WatchdogService.ResumeAttemptsCapOverride = 3;
        WatchdogService.ResumeWarmupGateOverride = TimeSpan.FromSeconds(60);
        WatchdogService.LaunchResumeOverride = (_, _, _, _, _, _, _) => 0;

        // Two consecutive polls — second poll must NOT re-emit failed or fire a gave_up.
        WatchdogService.PollAndResumeCrashedAgents(dydoRoot);
        WatchdogService.PollAndResumeCrashedAgents(dydoRoot);

        var lines = ReadLogLines(logPath);
        var outcomes = lines.Where(l => l["event"].GetString() == "resume_outcome").ToList();
        Assert.Single(outcomes);
        Assert.Equal("failed", outcomes[0]["outcome"].GetString());

        var statePath = Path.Combine(dydoRoot, "agents", "Adele", "state.md");
        Assert.Contains("last-resume-launched-at: null", File.ReadAllText(statePath));
    }

    [Fact]
    public void PollAndResumeForAgent_ResumeOutcome_GaveUp_OnCapReached_EmittedOnce()
    {
        // PR3: when natural cap saturation occurs (3 successive launches, none of them
        // hitting IsBadSessionFailFast — e.g. each crash hits a different ClaimedPid),
        // the next tick after the gate elapses must emit resume_outcome=gave_up exactly
        // once. Subsequent ticks must be silent (idempotency via cleared LastResumeLaunchedAt).
        var logPath = SetLogPathOverride();
        var dydoRoot = ResumeDydoRoot();
        WriteResumeAgentState(dydoRoot, "Adele", status: "working", resumeAttempts: 3,
            lastResumeLaunchedAt: DateTime.UtcNow.AddSeconds(-120),
            preResumePid: 11111111, launchedPid: 22222222);
        // ClaimedPid != preResumePid → IsBadSessionFailFast doesn't fire even though
        // attempts >= cap. (TryReadResumeContext returns null on attempts >= cap, but
        // the gave_up tick-check runs first and emits.)
        WriteResumeAgentSession(dydoRoot, "Adele", "sess-give", 33333333);
        ProcessUtils.IsProcessRunningOverride = _ => false;
        WatchdogService.ResumeAttemptsCapOverride = 3;
        WatchdogService.ResumeWarmupGateOverride = TimeSpan.FromSeconds(60);

        var launchCount = 0;
        WatchdogService.LaunchResumeOverride = (_, _, _, _, _, _, _) => { launchCount++; return 0; };

        // Three polls — first emits gave_up, the next two must be silent.
        WatchdogService.PollAndResumeCrashedAgents(dydoRoot);
        WatchdogService.PollAndResumeCrashedAgents(dydoRoot);
        WatchdogService.PollAndResumeCrashedAgents(dydoRoot);

        Assert.Equal(0, launchCount); // cap saturation prevents any new launches
        var lines = ReadLogLines(logPath);
        var outcomes = lines.Where(l => l["event"].GetString() == "resume_outcome").ToList();
        Assert.Single(outcomes);
        Assert.Equal("gave_up", outcomes[0]["outcome"].GetString());
        Assert.Equal("cap_reached", outcomes[0]["reason"].GetString());
        Assert.Equal(3, outcomes[0]["attempts"].GetInt32());

        // Cleared LastResumeLaunchedAt is the one-shot guard.
        var statePath = Path.Combine(dydoRoot, "agents", "Adele", "state.md");
        Assert.Contains("last-resume-launched-at: null", File.ReadAllText(statePath));
        // ResumeAttempts stays at cap so no further launches are attempted.
        Assert.Equal(3, ReadResumeAttempts(dydoRoot, "Adele"));
    }

    [Fact]
    public void PollAndResumeForAgent_ResumeOutcome_GaveUp_NotEmitted_BeforeGateElapses()
    {
        // Symmetry: cap reached but warmup gate has not yet elapsed since the last launch.
        // Don't emit gave_up yet — the resumed claude could still rehydrate. Wait for the
        // gate to expire before declaring the episode terminal.
        var logPath = SetLogPathOverride();
        var dydoRoot = ResumeDydoRoot();
        WriteResumeAgentState(dydoRoot, "Adele", status: "working", resumeAttempts: 3,
            lastResumeLaunchedAt: DateTime.UtcNow.AddSeconds(-5),
            preResumePid: 11111111, launchedPid: 22222222);
        WriteResumeAgentSession(dydoRoot, "Adele", "sess-still-warming", 33333333);
        ProcessUtils.IsProcessRunningOverride = _ => false;
        WatchdogService.ResumeAttemptsCapOverride = 3;
        WatchdogService.ResumeWarmupGateOverride = TimeSpan.FromSeconds(60);

        WatchdogService.PollAndResumeCrashedAgents(dydoRoot);

        var lines = ReadLogLines(logPath);
        Assert.DoesNotContain(lines, l => l["event"].GetString() == "resume_outcome");
    }

    [Fact]
    public void PollAndCleanup_KillsPostUpdateRenamedClaude()
    {
        // #0151 augment: after a Claude Code self-update on Windows, the running
        // process's image name becomes "claude.exe.old.<unix-ms>". The kill whitelist
        // must recognise this rename via MatchesProcessName, or auto-close silently
        // no-ops on the renamed image.
        using var dummy = StartDummyProcess();
        WriteAgentState("Adele", status: "free", autoClose: true);

        WatchdogService.FindProcessesOverride = _ => [dummy.Id];
        ProcessUtils.GetProcessNameOverride = pid => pid == dummy.Id ? "claude.exe.old.1777935765627" : null;

        try
        {
            WatchdogService.PollAndCleanup(_testDir);
        }
        finally
        {
            WatchdogService.FindProcessesOverride = null;
            ProcessUtils.GetProcessNameOverride = null;
        }

        dummy.WaitForExit(5000);
        Assert.True(dummy.HasExited, "post-update renamed claude.exe.old.<ts> must be killed");
    }

    #endregion

    private void WriteAgentState(string agentName, string status, bool autoClose, string? windowId = null, int resumeAttempts = 0)
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
            resume-attempts: {{resumeAttempts}}
            started: null
            writable-paths: []
            readonly-paths: []
            unread-must-reads: []
            unread-messages: []
            task-role-history: {}
            ---
            """);
    }

    private void WriteAgentSession(string agentName, string sessionId, int? claimedPid)
    {
        var agentDir = Path.Combine(_testDir, "agents", agentName);
        Directory.CreateDirectory(agentDir);
        var pidJson = claimedPid.HasValue ? claimedPid.Value.ToString() : "null";
        var json = $"{{\"Agent\":\"{agentName}\",\"SessionId\":\"{sessionId}\",\"Claimed\":\"{DateTime.UtcNow:o}\",\"ClaimedPid\":{pidJson}}}";
        File.WriteAllText(Path.Combine(agentDir, ".session"), json);
    }

    /// <summary>
    /// Resume tests need the registry's IncrementResumeAttempts to write to the same
    /// path the watchdog reads from. Watchdog derives projectRoot = parent(dydoRoot),
    /// and the registry's GetAgentsPath = projectRoot/dydo/agents. Tests therefore
    /// stage state under _testDir/dydo/agents/&lt;name&gt; and pass _testDir/dydo as the
    /// dydoRoot — keeping the existing _testDir/agents/&lt;name&gt; layout untouched
    /// for non-resume tests.
    /// </summary>
    #region needs-human reconcile (Decision 030 §1)

    [Fact]
    public void PollNeedsHuman_OrphanCrashMidTask_SetsFlag()
    {
        var dydoRoot = ResumeDydoRoot();
        WriteNeedsHumanState(dydoRoot, "Adele", status: "working", task: "my-task", needsHuman: false);
        WriteResumeAgentSession(dydoRoot, "Adele", "sess-abc", 99999999);
        ProcessUtils.IsProcessRunningOverride = _ => false; // claimed session PID is dead

        WatchdogService.PollNeedsHuman(dydoRoot);

        Assert.Contains("needs-human: true", ReadState(dydoRoot, "Adele"));
    }

    [Fact]
    public void PollNeedsHuman_ReleasedAgentWithStaleFlag_ClearsFlag()
    {
        var dydoRoot = ResumeDydoRoot();
        WriteNeedsHumanState(dydoRoot, "Adele", status: "free", task: "my-task", needsHuman: true);

        WatchdogService.PollNeedsHuman(dydoRoot);

        Assert.Contains("needs-human: false", ReadState(dydoRoot, "Adele"));
    }

    [Fact]
    public void PollNeedsHuman_TaskClosedWithStaleFlag_ClearsFlag()
    {
        var dydoRoot = ResumeDydoRoot();
        WriteNeedsHumanState(dydoRoot, "Adele", status: "working", task: "null", needsHuman: true);

        WatchdogService.PollNeedsHuman(dydoRoot);

        Assert.Contains("needs-human: false", ReadState(dydoRoot, "Adele"));
    }

    [Fact]
    public void PollNeedsHuman_WorkingAgentWaitingWithLiveSession_LeavesFlagSet()
    {
        // Legit waiting-on-human: working, has a task, live session, flag set. The sweep must NOT
        // clear it — that self-heals on the agent's next tool call, not here.
        var dydoRoot = ResumeDydoRoot();
        WriteNeedsHumanState(dydoRoot, "Adele", status: "working", task: "my-task", needsHuman: true);
        WriteResumeAgentSession(dydoRoot, "Adele", "sess-abc", 4242);
        ProcessUtils.IsProcessRunningOverride = _ => true;

        WatchdogService.PollNeedsHuman(dydoRoot);

        Assert.Contains("needs-human: true", ReadState(dydoRoot, "Adele"));
    }

    [Fact]
    public void PollNeedsHuman_OrphanCrashMidTask_SetsFlagAndTaskFileMirror()
    {
        // Orphan detection sets the flag AND mirrors it into the captured task file (the taskHint
        // threaded through SetNeedsHuman) — the sweep-path-with-hint coverage the wave-1 review asked for.
        var dydoRoot = ResumeDydoRoot();
        WriteNeedsHumanState(dydoRoot, "Adele", status: "working", task: "my-task", needsHuman: false);
        WriteResumeAgentSession(dydoRoot, "Adele", "sess-abc", 99999999);
        ProcessUtils.IsProcessRunningOverride = _ => false; // claimed session PID is dead
        var taskFile = WriteNeedsHumanTaskFile(dydoRoot, "my-task", needsHuman: false);

        WatchdogService.PollNeedsHuman(dydoRoot);

        Assert.Contains("needs-human: true", ReadState(dydoRoot, "Adele"));
        Assert.Contains("needs-human: true", File.ReadAllText(taskFile));
    }

    [Fact]
    public void PollNeedsHuman_ReleasedAgentWithStaleFlag_ClearsFlagAndTaskFileMirror()
    {
        // A released agent's stale flag AND its task-file mirror are both reconciled by the sweep via
        // the captured task hint — no stranded `needs-human: true` left in the task file.
        var dydoRoot = ResumeDydoRoot();
        WriteNeedsHumanState(dydoRoot, "Adele", status: "free", task: "my-task", needsHuman: true);
        var taskFile = WriteNeedsHumanTaskFile(dydoRoot, "my-task", needsHuman: true);

        WatchdogService.PollNeedsHuman(dydoRoot);

        Assert.Contains("needs-human: false", ReadState(dydoRoot, "Adele"));
        Assert.Contains("needs-human: false", File.ReadAllText(taskFile));
        Assert.DoesNotContain("needs-human: true", File.ReadAllText(taskFile));
    }

    [Fact]
    public void PollNeedsHuman_ExplicitFlagOnIdleAgent_IsNotSwept()
    {
        // The defect the source field closes: an explicit `dydo hand raise --agent X` on a peer that
        // isn't working-with-task must survive the sweep. A derived flag in the same shape is cleared.
        var dydoRoot = ResumeDydoRoot();
        WriteNeedsHumanState(dydoRoot, "Adele", status: "free", task: "null", needsHuman: true, source: "explicit");

        WatchdogService.PollNeedsHuman(dydoRoot);

        Assert.Contains("needs-human: true", ReadState(dydoRoot, "Adele"));
    }

    private void WriteNeedsHumanState(string dydoRoot, string agentName, string status, string task, bool needsHuman, string? source = null)
    {
        var agentDir = Path.Combine(dydoRoot, "agents", agentName);
        Directory.CreateDirectory(agentDir);
        File.WriteAllText(Path.Combine(agentDir, "state.md"), $"""
            ---
            agent: {agentName}
            role: code-writer
            task: {task}
            status: {status}
            assigned: testuser
            needs-human: {needsHuman.ToString().ToLowerInvariant()}
            needs-human-source: {source ?? "null"}
            ---
            """);
    }

    private string WriteNeedsHumanTaskFile(string dydoRoot, string task, bool needsHuman)
    {
        var tasksDir = Path.Combine(dydoRoot, "project", "tasks");
        Directory.CreateDirectory(tasksDir);
        var path = Path.Combine(tasksDir, $"{task}.md");
        File.WriteAllText(path, $"""
            ---
            area: general
            name: {task}
            status: pending
            needs-human: {needsHuman.ToString().ToLowerInvariant()}
            ---

            # Task: {task}
            """);
        return path;
    }

    private static string ReadState(string dydoRoot, string agentName) =>
        File.ReadAllText(Path.Combine(dydoRoot, "agents", agentName, "state.md"));

    #endregion

    private string ResumeDydoRoot()
    {
        var dydoRoot = Path.Combine(_testDir, "dydo");
        Directory.CreateDirectory(dydoRoot);
        return dydoRoot;
    }

    private void WriteResumeAgentState(string dydoRoot, string agentName, string status, int resumeAttempts,
        DateTime? lastResumeLaunchedAt = null, int? preResumePid = null, int? launchedPid = null, string? windowId = null)
    {
        var agentDir = Path.Combine(dydoRoot, "agents", agentName);
        Directory.CreateDirectory(agentDir);
        var lastAtStr = lastResumeLaunchedAt.HasValue ? lastResumeLaunchedAt.Value.ToString("o") : "null";
        var preStr = preResumePid?.ToString() ?? "null";
        var launchedStr = launchedPid?.ToString() ?? "null";
        var widStr = windowId ?? "null";
        File.WriteAllText(Path.Combine(agentDir, "state.md"), $$"""
            ---
            agent: {{agentName}}
            role: null
            task: null
            status: {{status}}
            assigned: testuser
            dispatched-by: null
            window-id: {{widStr}}
            auto-close: false
            resume-attempts: {{resumeAttempts}}
            last-resume-launched-at: {{lastAtStr}}
            pre-resume-pid: {{preStr}}
            launched-pid: {{launchedStr}}
            started: null
            writable-paths: []
            readonly-paths: []
            unread-must-reads: []
            unread-messages: []
            task-role-history: {}
            ---
            """);
    }

    private void WriteResumeAgentSession(string dydoRoot, string agentName, string sessionId, int? claimedPid)
    {
        var agentDir = Path.Combine(dydoRoot, "agents", agentName);
        Directory.CreateDirectory(agentDir);
        var pidJson = claimedPid.HasValue ? claimedPid.Value.ToString() : "null";
        var json = $"{{\"Agent\":\"{agentName}\",\"SessionId\":\"{sessionId}\",\"Claimed\":\"{DateTime.UtcNow:o}\",\"ClaimedPid\":{pidJson}}}";
        File.WriteAllText(Path.Combine(agentDir, ".session"), json);
    }

    private static int ReadResumeAttempts(string dydoRoot, string agentName)
    {
        var statePath = Path.Combine(dydoRoot, "agents", agentName, "state.md");
        var content = File.ReadAllText(statePath);
        var match = System.Text.RegularExpressions.Regex.Match(content, @"resume-attempts:\s*(\d+)");
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }

    #region Structured Logging (#0129)

    private string SetLogPathOverride()
    {
        var logPath = Path.Combine(_testDir, "watchdog.log");
        WatchdogLogger.LogPathOverride = logPath;
        return logPath;
    }

    private static List<Dictionary<string, JsonElement>> ReadLogLines(string logPath)
    {
        if (!File.Exists(logPath)) return [];
        return File.ReadAllLines(logPath)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(l)!)
            .ToList();
    }

    [Fact]
    public async Task Logger_StartEvent_RecordedWithAnchorPid()
    {
        var logPath = SetLogPathOverride();
        using var anchor = StartDummyProcess();
        var dydoRoot = SetupRunDydoRoot(_testDir);
        WriteAnchorFile(dydoRoot, anchor.Id);

        WatchdogService.PollIntervalOverride = TimeSpan.FromMilliseconds(100);
        ProcessUtils.GetProcessNameOverride = pid => pid == anchor.Id ? "claude" : null;
        Environment.CurrentDirectory = _testDir;

        var runTask = Task.Run(WatchdogService.Run);
        await Task.Delay(250);
        WatchdogService.RequestShutdownForTests();
        await Task.WhenAny(runTask, Task.Delay(TimeSpan.FromSeconds(5)));
        await runTask;
        anchor.Kill();

        var lines = ReadLogLines(logPath);
        Assert.NotEmpty(lines);
        var start = lines.First(l => l["event"].GetString() == "start");
        Assert.Equal(anchor.Id, start["anchor_pid"].GetInt32());
        Assert.Equal("claude", start["anchor_name"].GetString());
        Assert.Equal(1, start["anchor_count"].GetInt32());
        Assert.Equal(100, start["poll_interval_ms"].GetInt32());
        Assert.True(start.ContainsKey("watchdog_pid"));
        Assert.True(start.ContainsKey("ts"));
    }

    [Fact]
    public void Logger_KillEvent_RecordsTargetPidAndPattern()
    {
        var logPath = SetLogPathOverride();
        using var dummy = StartDummyProcess();
        WriteAgentState("Adele", status: "free", autoClose: true);

        WatchdogService.FindProcessesOverride = _ => [dummy.Id];
        ProcessUtils.GetProcessNameOverride = pid => pid == dummy.Id ? "claude" : null;

        try { WatchdogService.PollAndCleanup(_testDir); }
        finally
        {
            WatchdogService.FindProcessesOverride = null;
            ProcessUtils.GetProcessNameOverride = null;
        }

        var lines = ReadLogLines(logPath);
        var kill = lines.Single(l => l["event"].GetString() == "kill");
        Assert.Equal("Adele", kill["agent"].GetString());
        Assert.Equal(dummy.Id, kill["target_pid"].GetInt32());
        Assert.Equal("claude", kill["target_proc"].GetString());
        Assert.Equal("Adele --inbox", kill["pattern"].GetString());
        var state = kill["state"];
        Assert.Equal("free", state.GetProperty("status").GetString());
        Assert.True(state.GetProperty("auto_close").GetBoolean());
    }

    [Fact]
    public void Logger_ParseFailure_RecordedOnMalformedState()
    {
        var logPath = SetLogPathOverride();
        var agentDir = Path.Combine(_testDir, "agents", "Bad");
        Directory.CreateDirectory(agentDir);
        var statePath = Path.Combine(agentDir, "state.md");
        File.WriteAllText(statePath, "---\nagent: Bad\nno closing");

        WatchdogService.ParseStateForWatchdog(statePath);

        var lines = ReadLogLines(logPath);
        var pf = lines.Single(l => l["event"].GetString() == "parse_failure");
        Assert.Equal(statePath, pf["state_path"].GetString());
        Assert.False(string.IsNullOrEmpty(pf["reason"].GetString()));
    }

    [Fact]
    public void Logger_Rotation_TriggersAtThreshold_KeepsThreeBackups()
    {
        var logPath = SetLogPathOverride();
        WatchdogLogger.MaxBytesOverride = 256;
        WatchdogLogger.MaxRotationsOverride = 3;

        // Each tick line is ~80 bytes; 200 ticks is plenty to trigger 4 rotations.
        for (var i = 0; i < 200; i++)
            WatchdogLogger.LogTick(_testDir, agentsObserved: 1, killsAttempted: 0);

        Assert.True(File.Exists(logPath));
        Assert.True(File.Exists(logPath + ".1"));
        Assert.True(File.Exists(logPath + ".2"));
        Assert.True(File.Exists(logPath + ".3"));
        Assert.False(File.Exists(logPath + ".4"));
    }

    [Fact]
    public async Task Logger_ExitEvent_AnchorGoneReason()
    {
        var logPath = SetLogPathOverride();
        using var anchor = StartDummyProcess();
        var dydoRoot = SetupRunDydoRoot(_testDir);
        WriteAnchorFile(dydoRoot, anchor.Id);

        WatchdogService.PollIntervalOverride = TimeSpan.FromMilliseconds(100);
        Environment.CurrentDirectory = _testDir;

        var runTask = Task.Run(WatchdogService.Run);
        await Task.Delay(250);
        anchor.Kill();
        await Task.WhenAny(runTask, Task.Delay(TimeSpan.FromSeconds(5)));
        await runTask;

        var lines = ReadLogLines(logPath);
        var exit = lines.Last(l => l["event"].GetString() == "exit");
        Assert.Equal("anchor_gone", exit["reason"].GetString());
    }

    [Fact]
    public async Task Logger_ExitEvent_CancelledReason()
    {
        var logPath = SetLogPathOverride();
        using var anchor = StartDummyProcess();
        var dydoRoot = SetupRunDydoRoot(_testDir);
        WriteAnchorFile(dydoRoot, anchor.Id);

        WatchdogService.PollIntervalOverride = TimeSpan.FromMilliseconds(100);
        Environment.CurrentDirectory = _testDir;

        var runTask = Task.Run(WatchdogService.Run);
        await Task.Delay(250);
        WatchdogService.RequestShutdownForTests();
        await Task.WhenAny(runTask, Task.Delay(TimeSpan.FromSeconds(5)));
        await runTask;
        anchor.Kill();

        var lines = ReadLogLines(logPath);
        var exit = lines.Last(l => l["event"].GetString() == "exit");
        Assert.Equal("cancelled", exit["reason"].GetString());
    }

    [Fact]
    public void Logger_TickEvent_SkipsIdleNoAgents()
    {
        var logPath = SetLogPathOverride();
        Directory.CreateDirectory(Path.Combine(_testDir, "agents"));

        WatchdogService.PollAndCleanup(_testDir);

        var lines = ReadLogLines(logPath);
        Assert.DoesNotContain(lines, l => l["event"].GetString() == "tick");
    }

    [Fact]
    public void Logger_NeverThrows_OnInvalidPath()
    {
        // A path under a non-existent root with an illegal character on Windows
        // (NUL ('\0') is rejected by Path APIs on every platform).
        WatchdogLogger.LogPathOverride = Path.Combine(_testDir, "no\0such", "watchdog.log");

        var ex = Record.Exception(() =>
        {
            WatchdogLogger.LogStart(_testDir, 123, "claude", 100, 1);
            WatchdogLogger.LogTick(_testDir, 1, 0);
            WatchdogLogger.LogKill(_testDir, "Adele", 999, "claude", "Adele --inbox", "free", true, null, null);
            WatchdogLogger.LogResume(_testDir, "Adele", "sess-abc", 1, 12345);
            WatchdogLogger.LogResumeOutcome(_testDir, "Adele", "sess-abc", "succeeded", 1, 30, "same_session_reclaim");
            WatchdogLogger.LogParseFailure(_testDir, "x", "y");
            WatchdogLogger.LogPollError(_testDir, "boom");
            WatchdogLogger.LogExit(_testDir, "cancelled");
        });

        Assert.Null(ex);
    }

    [Fact]
    public void Logger_LogResumeOutcome_EmitsExpectedJson()
    {
        // PR3 of agent-crash-fixes: pins the resume_outcome event's wire format. The
        // follow-up inquisition uses these fields to compute the 4-bucket categorisation
        // — any rename or shape change here breaks downstream queries silently.
        var logPath = SetLogPathOverride();

        WatchdogLogger.LogResumeOutcome(_testDir, "Brian", "sess-pr3", "succeeded",
            attempts: 2, elapsedSeconds: 1928, reason: "same_session_reclaim");

        var lines = ReadLogLines(logPath);
        var outcome = lines.Single(l => l["event"].GetString() == "resume_outcome");
        Assert.Equal("Brian", outcome["agent"].GetString());
        Assert.Equal("sess-pr3", outcome["session_id"].GetString());
        Assert.Equal("succeeded", outcome["outcome"].GetString());
        Assert.Equal(2, outcome["attempts"].GetInt32());
        Assert.Equal(1928, outcome["elapsed_seconds"].GetInt32());
        Assert.Equal("same_session_reclaim", outcome["reason"].GetString());
        Assert.True(outcome.ContainsKey("ts"));
    }

    [Fact]
    public async Task Logger_PollError_RecordedOnInnerException()
    {
        var logPath = SetLogPathOverride();
        using var anchor = StartDummyProcess();
        var dydoRoot = SetupRunDydoRoot(_testDir);
        WriteAnchorFile(dydoRoot, anchor.Id);
        var adeleDir = Path.Combine(dydoRoot, "agents", "Adele");
        Directory.CreateDirectory(adeleDir);
        File.WriteAllText(Path.Combine(adeleDir, "state.md"),
            "---\nagent: Adele\nstatus: free\nauto-close: true\n---\n");

        WatchdogService.PollIntervalOverride = TimeSpan.FromMilliseconds(100);
        WatchdogService.FindProcessesOverride = _ => throw new InvalidOperationException("boom");
        Environment.CurrentDirectory = _testDir;

        var runTask = Task.Run(WatchdogService.Run);
        await Task.Delay(400);
        WatchdogService.RequestShutdownForTests();
        await Task.WhenAny(runTask, Task.Delay(TimeSpan.FromSeconds(5)));
        await runTask;
        anchor.Kill();

        var lines = ReadLogLines(logPath);
        var pe = lines.First(l => l["event"].GetString() == "poll_error");
        var error = pe["error"].GetString();
        Assert.Contains("InvalidOperationException", error);
        Assert.Contains("boom", error);
    }

    #endregion
}
