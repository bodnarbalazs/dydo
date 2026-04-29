namespace DynaDocs.Services;

using System.Diagnostics;
using DynaDocs.Models;
using DynaDocs.Utils;

public static class WatchdogService
{
    internal static readonly HashSet<string> ShellProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "powershell", "pwsh", "bash", "sh", "cmd", "zsh"
    };

    // Targets the watchdog is allowed to kill. Whitelist (not skip-list) so every other
    // process — terminal emulators, editors, anything future — is fail-closed protected.
    // Linux/Mac claude binary is "claude"; on Windows it ships as a Node script so the
    // resolved process name is "node".
    internal static readonly HashSet<string> ClaudeProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "claude", "node"
    };

    /// <summary>
    /// When set, EnsureRunning uses this instead of Process.Start.
    /// Enables testing without spawning real watchdog processes.
    /// </summary>
    internal static Func<ProcessStartInfo, Process?>? StartProcessOverride { get; set; }

    /// <summary>
    /// When set, PollAndCleanup uses this instead of ProcessUtils.FindProcessesByCommandLine.
    /// Enables testing the processes-still-running paths without real process scanning.
    /// </summary>
    internal static Func<string, List<int>>? FindProcessesOverride { get; set; }

    /// <summary>
    /// When set, Run() uses this to determine the anchor process PID instead of walking
    /// the real parent chain. Test hook.
    /// </summary>
    internal static Func<int?>? GetParentPidOverride { get; set; }

    /// <summary>
    /// When set, Run()'s polling loop waits for this interval instead of the default 10s.
    /// Test hook to keep behavioural assertions fast.
    /// </summary>
    internal static TimeSpan? PollIntervalOverride { get; set; }

    private static CancellationTokenSource? _shutdownCts;

    /// <summary>
    /// Test hook: signals the active Run() loop to cancel without raising a real
    /// OS signal (which would terminate the test host).
    /// </summary>
    internal static void RequestShutdownForTests() => _shutdownCts?.Cancel();

    public static string GetPidFilePath(string dydoRoot) =>
        Path.Combine(dydoRoot, "_system", ".local", "watchdog.pid");

    /// <summary>
    /// Starts the watchdog if not already running. Called automatically by DispatchCommand
    /// when --auto-close is set. Idempotent: multiple calls are safe.
    /// Resolves to the MAIN project root — dispatches from inside a worktree
    /// do not spawn a second, worktree-scoped watchdog.
    /// Returns true if a new watchdog was started, false if one was already running.
    /// </summary>
    public static bool EnsureRunning()
    {
        var mainProjectRoot = PathUtils.FindMainProjectRoot();
        var mainDydoRoot = PathUtils.FindMainDydoRoot() ?? ".";
        return EnsureRunning(mainDydoRoot, mainProjectRoot);
    }

    public static bool EnsureRunning(string dydoRoot) =>
        EnsureRunning(dydoRoot, Path.GetDirectoryName(Path.GetFullPath(dydoRoot)));

    private static bool EnsureRunning(string dydoRoot, string? workingDirectory)
    {
        var pidFile = GetPidFilePath(dydoRoot);
        PathUtils.EnsureLocalDirExists(dydoRoot);

        // Fast path: live watchdog already running
        if (File.Exists(pidFile))
        {
            try
            {
                if (int.TryParse(File.ReadAllText(pidFile).Trim(), out var existingPid) &&
                    ProcessUtils.IsProcessRunning(existingPid))
                    return false;
            }
            catch { return false; } // File locked — another thread/process is handling startup
            // Stale PID — remove so we can re-create atomically
            try { File.Delete(pidFile); } catch { return false; }
        }

        // Atomic creation: FileMode.CreateNew fails if another process created it first
        FileStream stream;
        try
        {
            stream = new FileStream(pidFile, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        }
        catch (IOException)
        {
            return false; // Another process won the race
        }

        try
        {
            var dydoPath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(dydoPath))
            {
                stream.Close();
                try { File.Delete(pidFile); } catch { }
                return false;
            }

            var psi = new ProcessStartInfo(dydoPath, "watchdog run")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                // Pin CWD to the main project root so the spawned watchdog never holds a
                // directory handle inside a worktree — otherwise Windows blocks worktree deletion.
                WorkingDirectory = workingDirectory ?? ""
            };

            // Resolve the anchor here (in the still-alive dispatcher) rather than inside the
            // watchdog — its immediate parent is this short-lived dispatcher, so walking from
            // the watchdog would anchor on a dead PID. Pass the claude-session PID via env var.
            var anchor = ProcessUtils.FindAncestorProcess("claude");
            if (anchor.HasValue)
                psi.Environment["DYDO_WATCHDOG_ANCHOR_PID"] = anchor.Value.ToString();

            var proc = StartProcessOverride != null ? StartProcessOverride(psi) : Process.Start(psi);
            if (proc == null)
            {
                stream.Close();
                try { File.Delete(pidFile); } catch { }
                return false;
            }

            var pidBytes = System.Text.Encoding.UTF8.GetBytes(proc.Id.ToString());
            stream.Write(pidBytes, 0, pidBytes.Length);
            stream.Flush();
            return true;
        }
        catch
        {
            stream.Close();
            try { File.Delete(pidFile); } catch { }
            return false;
        }
        finally
        {
            stream.Close();
        }
    }

    /// <summary>
    /// Stops the watchdog process.
    /// Returns true if a running watchdog was stopped, false if none was running.
    /// </summary>
    public static bool Stop() => Stop(PathUtils.FindMainDydoRoot() ?? ".");

    public static bool Stop(string dydoRoot)
    {
        var pidFile = GetPidFilePath(dydoRoot);
        if (!File.Exists(pidFile)) return false;

        var stopped = false;
        try
        {
            if (int.TryParse(File.ReadAllText(pidFile).Trim(), out var pid) &&
                ProcessUtils.IsProcessRunning(pid))
            {
                using var proc = Process.GetProcessById(pid);
                proc.Kill();
                proc.WaitForExit(5000);
                stopped = true;
            }
        }
        catch { }

        try { File.Delete(pidFile); } catch { }
        return stopped;
    }

    /// <summary>
    /// The watchdog polling loop. Runs as a background process, scanning for released
    /// auto-close agents and killing their claude processes.
    /// Exits gracefully on: cancellation (ProcessExit / CancelKeyPress), or when the
    /// spawning anchor process is gone. The pid file is deleted in a finally block so
    /// a clean shutdown never leaves residue — the gap that prior `taskkill`
    /// remediations kept papering over.
    /// </summary>
    public static void Run()
    {
        var dydoRoot = PathUtils.FindMainDydoRoot();
        if (dydoRoot == null) return;

        var pidFile = GetPidFilePath(dydoRoot);
        using var cts = new CancellationTokenSource();
        _shutdownCts = cts;

        void OnProcessExit(object? s, EventArgs e) => cts.Cancel();
        void OnCancelKeyPress(object? s, ConsoleCancelEventArgs e) { e.Cancel = true; cts.Cancel(); }

        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        Console.CancelKeyPress += OnCancelKeyPress;

        // Anchor precedence: test override > env var set by dispatcher > none.
        // Never fall back to GetParentPid(Environment.ProcessId) — the watchdog's real parent
        // is the short-lived dispatcher, which dies within seconds and falsely trips the exit.
        int? anchorPid = GetParentPidOverride != null
            ? GetParentPidOverride()
            : int.TryParse(Environment.GetEnvironmentVariable("DYDO_WATCHDOG_ANCHOR_PID"), out var envPid) ? envPid : null;
        var pollInterval = PollIntervalOverride ?? TimeSpan.FromSeconds(10);

        var anchorName = anchorPid.HasValue ? ProcessUtils.GetProcessName(anchorPid.Value) : null;
        WatchdogLogger.LogStart(dydoRoot, anchorPid, anchorName, (int)pollInterval.TotalMilliseconds);

        var exitReason = "cancelled";
        try
        {
            while (!cts.IsCancellationRequested)
            {
                if (cts.Token.WaitHandle.WaitOne(pollInterval)) { exitReason = "cancelled"; break; }

                if (anchorPid.HasValue && !ProcessUtils.IsProcessRunning(anchorPid.Value)) { exitReason = "anchor_gone"; break; }

                try
                {
                    PollAndCleanup(dydoRoot);
                    PollQueues(dydoRoot);
                    PollOrphanedWaits(dydoRoot);
                }
                catch (Exception ex)
                {
                    WatchdogLogger.LogPollError(dydoRoot, ex.GetType().Name + ": " + ex.Message);
                }
            }
        }
        finally
        {
            WatchdogLogger.LogExit(dydoRoot, exitReason);
            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
            Console.CancelKeyPress -= OnCancelKeyPress;
            _shutdownCts = null;
            try { File.Delete(pidFile); } catch { }
        }
    }

    public static void PollAndCleanup(string dydoRoot)
    {
        var agentsDir = Path.Combine(dydoRoot, "agents");
        if (!Directory.Exists(agentsDir)) return;

        var agentDirs = Directory.GetDirectories(agentsDir);
        var killsAttempted = 0;

        foreach (var agentDir in agentDirs)
        {
            var statePath = Path.Combine(agentDir, "state.md");
            if (!File.Exists(statePath)) continue;

            // Hold the same per-agent lock the registry uses for Reserve/Release/SetDispatchMetadata.
            // If the lock is held by a live writer, skip this iteration and try again on the next
            // poll. Closes #0121 Window A (stale-decision kill — registry write completes before
            // we read state) and Window B (ClearAutoClose RMW cannot interleave with WriteStateFile).
            var lockPath = Path.Combine(agentDir, ".claim.lock");
            var agentDirName = Path.GetFileName(agentDir);
            if (!AgentRegistry.TryAcquireLockAtPath(lockPath, agentDirName, out _)) continue;

            try
            {
                var (autoClose, isFree, agentName, _) = ParseStateForWatchdog(statePath);
                if (!autoClose || !isFree || agentName == null) continue;

                var (dispatchedBy, since) = ReadStateContext(statePath);

                var pattern = $"{agentName} --inbox";
                var pids = FindProcessesOverride != null
                    ? FindProcessesOverride(pattern)
                    : ProcessUtils.FindProcessesByCommandLine(pattern);

                // Whitelist target: claude (Linux/Mac) or node (Windows). Every other matching PID
                // — terminal emulators that carry the prompt in their argv (gnome-terminal,
                // konsole, alacritty, kitty, …), bash wrappers, editors that happen to substring-
                // match — is fail-closed protected. Closes #0122.
                var killedOrAttempted = false;
                foreach (var pid in pids)
                {
                    try
                    {
                        var procName = ProcessUtils.GetProcessName(pid);
                        if (procName == null || !ClaudeProcessNames.Contains(procName))
                            continue;
                        killedOrAttempted = true;
                        killsAttempted++;
                        WatchdogLogger.LogKill(dydoRoot, agentName, pid, procName, pattern,
                            status: "free", autoClose: true, dispatchedBy, since);
                        using var proc = Process.GetProcessById(pid);
                        proc.Kill();
                    }
                    catch { }
                }

                // Same retry semantics as before: clear auto-close only when we actually killed
                // a whitelisted target, or when there are no candidates left at all. When matches
                // exist but none are claude/node (terminal still tearing down), defer to the
                // next poll.
                if (killedOrAttempted || pids.Count == 0)
                    ClearAutoClose(statePath);
            }
            finally
            {
                AgentRegistry.ReleaseLockAtPath(lockPath);
            }
        }

        if (agentDirs.Length > 0 || killsAttempted > 0)
            WatchdogLogger.LogTick(dydoRoot, agentDirs.Length, killsAttempted);
    }

    internal static (string? dispatchedBy, string? since) ReadStateContext(string statePath)
    {
        var fields = FrontmatterParser.ParseFields(File.ReadAllText(statePath));
        var db = fields?.GetValueOrDefault("dispatched-by");
        var since = fields?.GetValueOrDefault("started");
        return (db == "null" ? null : db, since == "null" ? null : since);
    }

    internal static void ClearAutoClose(string statePath)
    {
        try
        {
            var content = File.ReadAllText(statePath);
            var updated = content.Replace("auto-close: true", "auto-close: false");
            if (updated != content)
                File.WriteAllText(statePath, updated);
        }
        catch { }
    }

    /// <summary>
    /// Detects stale active queue entries (dead PIDs) and promotes the next pending item.
    /// Also cleans up empty transient queues.
    /// </summary>
    public static void PollQueues(string dydoRoot)
    {
        var configService = new ConfigService();
        var config = configService.LoadConfig();
        var queueService = new QueueService(dydoRoot, config);

        // Stale active detection
        var staleEntries = queueService.FindStaleActiveEntries();

        // On Windows, wt.exe exits immediately after spawning a tab — the PID in
        // _active.json dies even though the terminal is still running. Check agent
        // state before clearing: a working/reviewing/dispatched agent is not stale.
        AgentRegistry? registry = null;
        foreach (var (queueName, entry) in staleEntries)
        {
            registry ??= new AgentRegistry(configService.GetProjectRoot());
            var agentState = registry.GetAgentState(entry.Agent);
            if (agentState?.Status is AgentStatus.Working or AgentStatus.Reviewing or AgentStatus.Dispatched or AgentStatus.Queued)
                continue;

            queueService.ClearActive(queueName);
            var next = queueService.DequeueNext(queueName);
            if (next != null)
            {
                queueService.ClearQueuedMarker(next.Agent);
                var projectRoot = next.WorkingDirOverride ?? next.MainProjectRoot ?? Utils.PathUtils.FindProjectRoot();
                var pid = TerminalLauncher.LaunchNewTerminal(next.Agent, projectRoot, next.LaunchInTab,
                    next.AutoClose, next.WorktreeId, next.WindowName, next.CleanupWorktreeId, next.MainProjectRoot);
                queueService.SetActive(queueName, next.Agent, next.Task, pid);
            }
            else
            {
                queueService.CleanupIfEmptyTransient(queueName);
            }
        }

        // Transient queue cleanup
        queueService.CleanupAllEmptyTransient();
    }

    /// <summary>
    /// Scans wait markers for free agents and kills orphaned dydo wait processes.
    /// A free agent should never have a live wait process — if one exists, the session
    /// died without proper cleanup.
    /// </summary>
    public static void PollOrphanedWaits(string dydoRoot)
    {
        var agentsDir = Path.Combine(dydoRoot, "agents");
        if (!Directory.Exists(agentsDir)) return;

        foreach (var agentDir in Directory.GetDirectories(agentsDir))
        {
            var statePath = Path.Combine(agentDir, "state.md");
            if (!File.Exists(statePath)) continue;

            var (_, isFree, _, _) = ParseStateForWatchdog(statePath);
            if (!isFree) continue;

            var waitDir = Path.Combine(agentDir, ".waiting");
            if (!Directory.Exists(waitDir)) continue;

            foreach (var file in Directory.GetFiles(waitDir, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    if (!ParseListeningFromJson(json)) continue;
                    var pid = ParsePidFromJson(json);
                    if (pid == null || !ProcessUtils.IsProcessRunning(pid.Value)) continue;

                    try { using var proc = Process.GetProcessById(pid.Value); proc.Kill(); }
                    catch { }

                    File.Delete(file);
                }
                catch { }
            }

            // Remove empty .waiting directory
            try
            {
                if (Directory.Exists(waitDir) && Directory.GetFiles(waitDir).Length == 0)
                    Directory.Delete(waitDir);
            }
            catch { }
        }
    }

    internal static bool ParseListeningFromJson(string json)
    {
        var idx = json.IndexOf("\"listening\"", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return false;
        var colon = json.IndexOf(':', idx + 11);
        if (colon < 0) return false;
        var valueStart = json.AsSpan()[(colon + 1)..].TrimStart();
        return valueStart.StartsWith("true", StringComparison.OrdinalIgnoreCase);
    }

    internal static int? ParsePidFromJson(string json)
    {
        var idx = json.IndexOf("\"pid\"", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var colon = json.IndexOf(':', idx + 5);
        if (colon < 0) return null;
        var rest = json.AsSpan()[(colon + 1)..].TrimStart();
        if (rest.StartsWith("null", StringComparison.OrdinalIgnoreCase)) return null;
        var end = 0;
        while (end < rest.Length && char.IsDigit(rest[end])) end++;
        return end > 0 && int.TryParse(rest[..end], out var pid) ? pid : null;
    }

    internal static (bool autoClose, bool isFree, string? agentName, string? windowId) ParseStateForWatchdog(string statePath)
    {
        try
        {
            var content = File.ReadAllText(statePath);
            var fields = FrontmatterParser.ParseFields(content);
            if (fields == null)
            {
                WatchdogLogger.LogParseFailure(GetDydoRootForLog(statePath), statePath, "no frontmatter");
                return (false, false, null, null);
            }

            fields.TryGetValue("agent", out var agentName);
            var isFree = fields.TryGetValue("status", out var status) && status == "free";
            var autoClose = fields.TryGetValue("auto-close", out var ac) && ac == "true";
            string? windowId = null;
            if (fields.TryGetValue("window-id", out var wid) && wid is not ("null" or ""))
                windowId = wid;

            return (autoClose, isFree, agentName, windowId);
        }
        catch (Exception ex)
        {
            WatchdogLogger.LogParseFailure(GetDydoRootForLog(statePath), statePath, ex.GetType().Name + ": " + ex.Message);
            return (false, false, null, null);
        }
    }

    internal static string GetDydoRootForLog(string statePath)
    {
        // .../<dydoRoot>/agents/<agent>/state.md → grandparent of agentDir
        var agentDir = Path.GetDirectoryName(statePath);
        var agentsDir = Path.GetDirectoryName(agentDir);
        return Path.GetDirectoryName(agentsDir) ?? "";
    }
}
