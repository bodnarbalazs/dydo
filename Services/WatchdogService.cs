namespace DynaDocs.Services;

using System.Diagnostics;
using DynaDocs.Models;
using DynaDocs.Utils;

public static class WatchdogService
{
    internal static readonly HashSet<string> ShellProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "powershell", "pwsh", "bash", "sh", "cmd", "zsh",
        "fish", "dash", "tcsh", "csh", "nu", "ksh"
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
    /// When set, Run()'s polling loop waits for this interval instead of the default 10s.
    /// Test hook to keep behavioural assertions fast.
    /// </summary>
    internal static TimeSpan? PollIntervalOverride { get; set; }

    /// <summary>
    /// When set, Run() exits the orphan path (no anchors ever registered) after this
    /// interval instead of the default 24h. Test hook — mirrors PollIntervalOverride.
    /// </summary>
    internal static TimeSpan? MaxOrphanAgeOverride { get; set; }

    private static readonly TimeSpan MaxOrphanAge = TimeSpan.FromHours(24);

    private static CancellationTokenSource? _shutdownCts;

    /// <summary>
    /// Test hook: signals the active Run() loop to cancel without raising a real
    /// OS signal (which would terminate the test host).
    /// </summary>
    internal static void RequestShutdownForTests() => _shutdownCts?.Cancel();

    public static string GetPidFilePath(string dydoRoot) =>
        Path.Combine(dydoRoot, "_system", ".local", "watchdog.pid");

    public static string GetAnchorsDirPath(string dydoRoot) =>
        Path.Combine(dydoRoot, "_system", ".local", "watchdog-anchors");

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

        // Register this dispatcher's claude ancestor (if any) before any short-circuit,
        // so a watchdog already running for an earlier dispatcher gains coverage of THIS
        // claude as well. Closes #0127.
        RegisterAnchor(dydoRoot, ProcessUtils.FindAncestorProcess("claude"));

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

    // PID <= 1 is init/System; never write that — it would never appear gone and would
    // defeat the orphan-cap entirely. Closes the anchor-write half of #0128.
    internal static void RegisterAnchor(string dydoRoot, int? anchorPid)
    {
        if (!anchorPid.HasValue || anchorPid.Value <= 1) return;
        try
        {
            var dir = GetAnchorsDirPath(dydoRoot);
            Directory.CreateDirectory(dir);
            // The PID is in the filename; content is unused. Concurrent dispatchers writing
            // the same {pid}.anchor are race-free (truncate-or-create).
            File.WriteAllBytes(Path.Combine(dir, $"{anchorPid.Value}.anchor"), Array.Empty<byte>());
        }
        catch { /* best-effort; null-anchor cap protects orphan path anyway */ }
    }

    /// <summary>
    /// Lists anchor marker files under the anchors directory, deletes the ones whose
    /// PIDs are gone or malformed, and returns the count of still-live anchors.
    /// </summary>
    internal static int ScanAnchors(string anchorsDir)
    {
        if (!Directory.Exists(anchorsDir)) return 0;
        var liveCount = 0;
        foreach (var file in Directory.GetFiles(anchorsDir, "*.anchor"))
        {
            if (!int.TryParse(Path.GetFileNameWithoutExtension(file), out var pid) || pid <= 1)
            {
                try { File.Delete(file); } catch { }
                continue;
            }
            if (ProcessUtils.IsProcessRunning(pid)) { liveCount++; continue; }
            try { File.Delete(file); } catch { }
        }
        return liveCount;
    }

    // Returns the lowest-PID live anchor (if any) along with the total live count. The
    // primary PID is what the structured log records; the count is the new field.
    private static (int? primaryPid, string? primaryName, int count) ResolveAnchors(string anchorsDir)
    {
        var liveCount = ScanAnchors(anchorsDir);
        if (liveCount == 0) return (null, null, 0);

        int? lowest = null;
        foreach (var file in Directory.GetFiles(anchorsDir, "*.anchor"))
        {
            if (!int.TryParse(Path.GetFileNameWithoutExtension(file), out var pid) || pid <= 1) continue;
            if (!ProcessUtils.IsProcessRunning(pid)) continue;
            if (lowest == null || pid < lowest) lowest = pid;
        }
        return (lowest, lowest.HasValue ? ProcessUtils.GetProcessName(lowest.Value) : null, liveCount);
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
        var anchorsDir = GetAnchorsDirPath(dydoRoot);
        using var cts = new CancellationTokenSource();
        _shutdownCts = cts;

        void OnProcessExit(object? s, EventArgs e) => cts.Cancel();
        void OnCancelKeyPress(object? s, ConsoleCancelEventArgs e) { e.Cancel = true; cts.Cancel(); }

        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        Console.CancelKeyPress += OnCancelKeyPress;

        var pollInterval = PollIntervalOverride ?? TimeSpan.FromSeconds(10);
        var maxOrphanAge = MaxOrphanAgeOverride ?? MaxOrphanAge;
        var startedAt = DateTime.UtcNow;
        var hasSeenLiveAnchor = false;

        var (primaryPid, primaryName, anchorCount) = ResolveAnchors(anchorsDir);
        if (anchorCount > 0) hasSeenLiveAnchor = true;
        WatchdogLogger.LogStart(dydoRoot, primaryPid, primaryName, (int)pollInterval.TotalMilliseconds, anchorCount);

        var exitReason = "cancelled";
        try
        {
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    if (cts.Token.WaitHandle.WaitOne(pollInterval)) { exitReason = "cancelled"; break; }

                    var liveAnchorCount = ScanAnchors(anchorsDir);
                    if (liveAnchorCount > 0) hasSeenLiveAnchor = true;

                    // Exit if all tracked claude anchors are gone (#0127), or if we've been
                    // running orphaned (no anchor ever registered) past the max-age ceiling
                    // (#0126). The cap also protects against a future bug where every
                    // dispatcher fails to register: we never run forever.
                    if (hasSeenLiveAnchor && liveAnchorCount == 0) { exitReason = "anchor_gone"; break; }
                    if (!hasSeenLiveAnchor && DateTime.UtcNow - startedAt >= maxOrphanAge) { exitReason = "max_orphan_age"; break; }

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
            catch (Exception ex)
            {
                // Anything escaping the inner poll-error catch (WaitHandle disposal mid-shutdown,
                // OOM bubbling out of IsProcessRunning, etc.) records as error:* so LogExit
                // distinguishes a crash from a clean cancel.
                exitReason = "error:" + ex.GetType().Name;
                throw;
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
            killsAttempted += PollAndCleanupForAgent(dydoRoot, agentDir);

        if (agentDirs.Length > 0 || killsAttempted > 0)
            WatchdogLogger.LogTick(dydoRoot, agentDirs.Length, killsAttempted);
    }

    private static int PollAndCleanupForAgent(string dydoRoot, string agentDir)
    {
        var statePath = Path.Combine(agentDir, "state.md");
        if (!File.Exists(statePath)) return 0;

        // Hold the same per-agent lock the registry uses for Reserve/Release/SetDispatchMetadata.
        // If the lock is held by a live writer, skip this iteration and try again on the next
        // poll. Closes #0121 Window A (stale-decision kill — registry write completes before
        // we read state) and Window B (ClearAutoClose RMW cannot interleave with WriteStateFile).
        var lockPath = Path.Combine(agentDir, ".claim.lock");
        var agentDirName = Path.GetFileName(agentDir);
        if (!AgentRegistry.TryAcquireLockAtPath(lockPath, agentDirName, out _)) return 0;

        try
        {
            var (autoClose, isFree, agentName, _) = ParseStateForWatchdog(statePath);
            if (!autoClose || !isFree || agentName == null) return 0;

            var (dispatchedBy, since) = ReadStateContext(statePath);

            // Load-bearing: the trailing " --inbox" suffix is collision-safety. The match
            // runs as a substring on each process command line (wmic LIKE / ps -eo args),
            // so without the trailing token "Jack" would prefix-match a hypothetical
            // "Jacky" agent. Anyone changing the dispatch prompt format MUST preserve a
            // trailing token that no other agent's prompt could legally start with.
            // Regression: ProcessUtilsTests.ParsePsEoPidArgs_PrefixCollision_NotMatched.
            var pattern = $"{agentName} --inbox";
            var pids = FindProcessesOverride != null
                ? FindProcessesOverride(pattern)
                : ProcessUtils.FindProcessesByCommandLine(pattern);

            var killsAttempted = KillClaudeProcesses(dydoRoot, agentName, pattern, pids, dispatchedBy, since);

            // Same retry semantics as before: clear auto-close only when we actually killed
            // a whitelisted target, or when there are no candidates left at all. When matches
            // exist but none are claude/node (terminal still tearing down), defer to the
            // next poll.
            if (killsAttempted > 0 || pids.Count == 0)
                ClearAutoClose(statePath);

            return killsAttempted;
        }
        finally
        {
            AgentRegistry.ReleaseLockAtPath(lockPath);
        }
    }

    // Whitelist target: claude (Linux/Mac) or node (Windows). Every other matching PID
    // — terminal emulators that carry the prompt in their argv (gnome-terminal,
    // konsole, alacritty, kitty, …), bash wrappers, editors that happen to substring-
    // match — is fail-closed protected. Closes #0122.
    private static int KillClaudeProcesses(string dydoRoot, string agentName, string pattern,
                                           List<int> pids, string? dispatchedBy, string? since)
    {
        var killsAttempted = 0;
        foreach (var pid in pids)
        {
            try
            {
                var procName = ProcessUtils.GetProcessName(pid);
                if (procName == null || !ClaudeProcessNames.Contains(procName))
                    continue;
                killsAttempted++;
                WatchdogLogger.LogKill(dydoRoot, agentName, pid, procName, pattern,
                    status: "free", autoClose: true, dispatchedBy, since);
                using var proc = Process.GetProcessById(pid);
                proc.Kill();
            }
            catch { }
        }
        return killsAttempted;
    }

    internal static (string? dispatchedBy, string? since) ReadStateContext(string statePath)
    {
        // Inline catch keeps a single agent's IO failure (sharing-violation, file vanished
        // mid-poll) from aborting the surrounding foreach over agentDirs and skipping every
        // other agent for this tick. The outer poll-error catch would only fire on the next
        // iteration after an early break.
        try
        {
            var fields = FrontmatterParser.ParseFields(File.ReadAllText(statePath));
            var db = fields?.GetValueOrDefault("dispatched-by");
            var since = fields?.GetValueOrDefault("started");
            return (db == "null" ? null : db, since == "null" ? null : since);
        }
        catch { return (null, null); }
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
