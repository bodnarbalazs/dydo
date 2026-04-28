namespace DynaDocs.Tests.Services;

using System.Diagnostics;
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
        WatchdogService.GetParentPidOverride = null;
        WatchdogService.PollIntervalOverride = null;
        ProcessUtils.GetProcessNameOverride = null;
        ProcessUtils.IsProcessRunningOverride = null;
        ProcessUtils.FindAncestorProcessOverride = null;
        Environment.SetEnvironmentVariable("DYDO_WATCHDOG_ANCHOR_PID", null);
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
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), """{"name":"t"}""");
        Directory.CreateDirectory(Path.Combine(_testDir, "dydo"));

        WatchdogService.GetParentPidOverride = () => anchor.Id;
        WatchdogService.PollIntervalOverride = TimeSpan.FromMilliseconds(100);
        Environment.CurrentDirectory = _testDir;

        var runTask = Task.Run(WatchdogService.Run);
        await Task.Delay(250); // Let the loop enter and read the anchor PID
        anchor.Kill();

        var completed = await Task.WhenAny(runTask, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(runTask, completed);
        await runTask;
    }

    [Fact]
    public async Task Run_ExitsWhenCancellationRequested()
    {
        using var longLived = StartDummyProcess();
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), """{"name":"t"}""");
        Directory.CreateDirectory(Path.Combine(_testDir, "dydo"));

        WatchdogService.GetParentPidOverride = () => longLived.Id;
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
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), """{"name":"t"}""");
        var dydoRoot = Path.Combine(_testDir, "dydo");
        Directory.CreateDirectory(Path.Combine(dydoRoot, "_system", ".local"));
        var pidFile = WatchdogService.GetPidFilePath(dydoRoot);
        File.WriteAllText(pidFile, Environment.ProcessId.ToString());

        WatchdogService.GetParentPidOverride = () => anchor.Id;
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
    public void EnsureRunning_PassesClaudeAncestorPidToChildViaEnv()
    {
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), """{"name":"t"}""");
        Directory.CreateDirectory(Path.Combine(_testDir, "dydo"));

        ProcessStartInfo? capturedPsi = null;
        WatchdogService.StartProcessOverride = psi => { capturedPsi = psi; return null; };
        ProcessUtils.FindAncestorProcessOverride = (_, _) => 99999;

        WatchdogService.EnsureRunning(_testDir);

        Assert.NotNull(capturedPsi);
        Assert.True(capturedPsi.Environment.TryGetValue("DYDO_WATCHDOG_ANCHOR_PID", out var envValue));
        Assert.Equal("99999", envValue);
    }

    [Fact]
    public async Task Run_ReadsAnchorPidFromEnvironmentWhenOverrideNull_DoesNotFallBackToParentPid()
    {
        using var anchor = StartDummyProcess();
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), """{"name":"t"}""");
        Directory.CreateDirectory(Path.Combine(_testDir, "dydo"));

        Environment.SetEnvironmentVariable("DYDO_WATCHDOG_ANCHOR_PID", anchor.Id.ToString());
        WatchdogService.GetParentPidOverride = null;
        WatchdogService.PollIntervalOverride = TimeSpan.FromMilliseconds(100);
        Environment.CurrentDirectory = _testDir;

        var runTask = Task.Run(WatchdogService.Run);
        await Task.Delay(250);
        Assert.False(runTask.IsCompleted);

        anchor.Kill();

        var completed = await Task.WhenAny(runTask, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(runTask, completed);
        await runTask;
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
}
