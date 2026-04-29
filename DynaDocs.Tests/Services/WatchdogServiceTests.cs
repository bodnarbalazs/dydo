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
        var dydoRoot = SetupRunDydoRoot(_testDir);
        WriteAnchorFile(dydoRoot, 100);

        var live = new HashSet<int> { 100 };
        ProcessUtils.IsProcessRunningOverride = pid => { lock (live) { return live.Contains(pid); } };
        WatchdogService.PollIntervalOverride = TimeSpan.FromMilliseconds(100);
        Environment.CurrentDirectory = _testDir;

        var runTask = Task.Run(WatchdogService.Run);
        await Task.Delay(150);

        // Second dispatcher arrives, registers a new anchor; first claude dies.
        WriteAnchorFile(dydoRoot, 200);
        lock (live) { live.Add(200); live.Remove(100); }

        await Task.Delay(400);
        Assert.False(runTask.IsCompleted, "watchdog must stay alive while ANY anchor is alive");

        lock (live) { live.Remove(200); }
        var completed = await Task.WhenAny(runTask, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(runTask, completed);
        await runTask;
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
            WatchdogLogger.LogParseFailure(_testDir, "x", "y");
            WatchdogLogger.LogPollError(_testDir, "boom");
            WatchdogLogger.LogExit(_testDir, "cancelled");
        });

        Assert.Null(ex);
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
