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
    /// When set, EnsureRunning uses this instead of scanning process command lines to decide
    /// whether the pidfile PID is a live watchdog. Test hook.
    /// </summary>
    internal static Func<int, bool>? IsWatchdogProcessOverride { get; set; }

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

    /// <summary>
    /// When set, PollAndResumeForAgent uses this instead of TerminalLauncher.LaunchResumeTerminal.
    /// Test hook — receives (agentName, sessionId, workingDirectory, windowName, useTab,
    /// worktreeId, mainProjectRoot), returns the (faked) launched PID. The worktreeId/
    /// mainProjectRoot pair carries the worktree context so the resume launcher can wrap
    /// the resumed claude in the same Set-Location / init-settings / cleanup envelope as
    /// the original dispatch (Finding #4 of agent-crashes inquisition; closes #0175).
    /// </summary>
    internal static Func<string, string, string?, string?, bool, string?, string?, int>? LaunchResumeOverride { get; set; }

    /// <summary>
    /// When set, PollAndResumeForAgent uses this cap instead of ResumeAttemptsCap.
    /// Test hook — lets retry-cap tests run with cap=1 or cap=2 for fewer iterations.
    /// </summary>
    internal static int? ResumeAttemptsCapOverride { get; set; }

    /// <summary>
    /// Per Decision 022: bounded respawns absorb transient crashes; cap exits a
    /// poisoned-state crash-resume-crash loop within ~2 polling intervals after hit.
    /// </summary>
    internal const int ResumeAttemptsCap = 3;

    /// <summary>
    /// Belt-and-braces ceiling between resume launches for the same agent. The
    /// authoritative liveness signal is <see cref="IsBadSessionFailFast"/>'s
    /// launched-PID check; this gate exists to bound how often we rapid-fire
    /// respawn the same dead session in pathological cases. Raised from 60s to
    /// 5min after #0173 observed multi-minute rehydrations on long conversations
    /// (rehydration deltas of 8/32/10 minutes in audit data). Closes #0152, #0173.
    /// </summary>
    internal static readonly TimeSpan ResumeWarmupGate = TimeSpan.FromMinutes(5);

    /// <summary>Test hook — shrink the warmup gate so cap-race tests stay fast.</summary>
    internal static TimeSpan? ResumeWarmupGateOverride { get; set; }

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
        // claude as well. Closes #0127. Uses FindClaudeAncestor to also accept the
        // "node" parent name on Windows where claude ships as a Node script (#0151).
        RegisterAnchor(dydoRoot, ProcessUtils.FindClaudeAncestor());

        // Fast path: live watchdog already running
        if (File.Exists(pidFile))
        {
            try
            {
                if (int.TryParse(File.ReadAllText(pidFile).Trim(), out var existingPid) &&
                    ProcessUtils.IsProcessRunning(existingPid) &&
                    IsWatchdogProcess(existingPid))
                    return false;
            }
            catch { return false; } // File locked — another thread/process is handling startup
            // Stale or recycled PID — remove so we can re-create atomically
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
            // #0197 (F13): scrub inherited DYDO_AGENT so the watchdog never inherits an
            // identity. The watchdog is a long-lived background process that must operate
            // on every claimed agent's behalf, not as any single agent — leaving DYDO_AGENT
            // set lets its later dydo subprocesses (e.g. resume launches) impersonate.
            psi.Environment.Remove("DYDO_AGENT");

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

    // A pidfile PID counts as a live watchdog only if its command line is a "watchdog run"
    // invocation. Bare IsProcessRunning is not enough: when the watchdog dies its PID is
    // quickly recycled on Windows (observed: an npx/cmd process reusing it), and the recycled
    // process would masquerade as a live watchdog, so EnsureRunning would return false and
    // NEVER respawn — silently disabling --auto-close until someone noticed lingering tabs.
    // Matching the command line defeats PID reuse.
    internal static bool IsWatchdogProcess(int pid)
    {
        if (IsWatchdogProcessOverride != null) return IsWatchdogProcessOverride(pid);
        return ProcessUtils.FindProcessesByCommandLine("watchdog run").Contains(pid);
    }

    /// <summary>
    /// Helper for the agent-claim anchor write: resolves the MAIN dydo root
    /// (never a worktree) via <see cref="PathUtils.FindMainDydoRoot"/> and
    /// delegates to <see cref="RegisterAnchor"/>. <see cref="EnsureRunning()"/>
    /// has its own equivalent path — it calls <see cref="PathUtils.FindMainDydoRoot"/>
    /// directly and invokes <see cref="RegisterAnchor"/>, so the main-vs-worktree
    /// rule is upheld at each call site independently rather than enforced
    /// through a shared chokepoint. Any new anchor-registration site MUST
    /// resolve the main dydo root the same way. Closes #0174 — previously the
    /// claim-time callsite resolved its own dydo root via
    /// <c>_configService.GetDydoRoot</c>, which lands inside a worktree when
    /// the claimer's basepath is a worktree.
    /// <paramref name="startPath"/> seeds the worktree-walkback search; pass the
    /// caller's basepath so test fixtures with synthetic project roots resolve
    /// against the fixture rather than the host process CWD.
    /// </summary>
    public static void RegisterMainAnchor(int? anchorPid, string? startPath = null)
    {
        var mainDydoRoot = PathUtils.FindMainDydoRoot(startPath);
        if (mainDydoRoot == null) return;
        RegisterAnchor(mainDydoRoot, anchorPid);
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
                        PollOrphanedWaits(dydoRoot);
                        PollAndResumeCrashedAgents(dydoRoot);
                        PollNeedsHuman(dydoRoot);
                        PollModelCaps(dydoRoot);
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

            var killsAttempted = KillAgentHostProcesses(dydoRoot, agentName, pattern, pids, dispatchedBy, since);

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

    /// <summary>
    /// Per-tick pass: detects agents whose claimed claude PID is dead while state is still
    /// working, and re-launches them via the resume launcher up to ResumeAttemptsCap.
    /// Decision 022.
    /// </summary>
    public static void PollAndResumeCrashedAgents(string dydoRoot)
    {
        var agentsDir = Path.Combine(dydoRoot, "agents");
        if (!Directory.Exists(agentsDir)) return;

        foreach (var agentDir in Directory.GetDirectories(agentsDir))
            PollAndResumeForAgent(dydoRoot, agentDir);
    }

    private sealed record ResumeContext(
        string AgentName,
        string SessionId,
        int ClaimedPid,
        DateTime? LastResumeAt,
        int Attempts,
        int? PreResumePid,
        int? LaunchedPid,
        string? WindowId,
        int Cap,
        TimeSpan Gate);

    private sealed record GaveUpContext(
        string AgentName,
        string SessionId,
        int Attempts,
        int Cap,
        int ElapsedSeconds);

    private static void PollAndResumeForAgent(string dydoRoot, string agentDir)
    {
        var projectRoot = Path.GetDirectoryName(dydoRoot) ?? Directory.GetCurrentDirectory();

        // PR3 of agent-crash-fixes: emit resume_outcome=gave_up at most once per episode when
        // the cap was reached on the natural increment path (no IsBadSessionFailFast). Run
        // before TryReadResumeContext, which would otherwise return null on cap-saturated
        // agents and skip the rest of the poll. SaturateResumeAttempts clears
        // LastResumeLaunchedAt so the next tick's predicate is false until a new launch.
        var giveUpCtx = TryReadGaveUpContext(agentDir);
        if (giveUpCtx != null)
        {
            WatchdogLogger.LogResumeOutcome(dydoRoot, giveUpCtx.AgentName, giveUpCtx.SessionId,
                outcome: "gave_up", attempts: giveUpCtx.Attempts,
                elapsedSeconds: giveUpCtx.ElapsedSeconds, reason: "cap_reached");
            new AgentRegistry(projectRoot).SaturateResumeAttempts(giveUpCtx.AgentName, giveUpCtx.Cap);
        }

        var ctx = TryReadResumeContext(agentDir);
        if (ctx == null) return;

        // Drop the lock before delegating to the registry — IncrementResumeAttempts
        // takes its own lock; the launcher must NOT hold the agent's claim lock or
        // the resumed claude would block on its first claim.
        var registry = new AgentRegistry(projectRoot);

        if (IsBadSessionFailFast(ctx))
        {
            registry.SaturateResumeAttempts(ctx.AgentName, ctx.Cap);
            WatchdogLogger.LogResumeBlocked(dydoRoot, ctx.AgentName, ctx.SessionId,
                reason: "no_refresh_after_warmup", preResumePid: ctx.PreResumePid!.Value);

            // PR3: pair every resume_blocked with a terminal resume_outcome=failed event so
            // the 4-bucket categorisation gets a one-line signal of every confirmed-dead
            // launch. SaturateResumeAttempts above also cleared LastResumeLaunchedAt, so
            // gave_up's tick-check on the next poll won't double-fire — failed and gave_up
            // are mutually exclusive per episode.
            var elapsed = (int)(DateTime.UtcNow - ctx.LastResumeAt!.Value).TotalSeconds;
            WatchdogLogger.LogResumeOutcome(dydoRoot, ctx.AgentName, ctx.SessionId,
                outcome: "failed", attempts: ctx.Attempts,
                elapsedSeconds: elapsed, reason: "launched_pid_dead");
            return;
        }

        // Silent-skip: warmup elapsed but the launched claude is still alive (slow
        // rehydration on long conversations — 8/32/10-minute deltas observed in
        // #0173 audit). Don't emit resume_blocked, don't relaunch, just wait. The
        // log line is reserved for confirmed-dead launches per the locked decision
        // on #0173 (Q3 silent-skip).
        if (IsLaunchedClaudeStillAlive(ctx))
            return;

        var newCount = registry.IncrementResumeAttempts(ctx.AgentName, ctx.ClaimedPid);
        if (newCount < 0) return; // lock contention — try again next tick

        var workingDirectory = ResolveResumeWorkingDirectory(agentDir, projectRoot);
        // #0144: route the resume back into the dispatcher's window/tab when the
        // dispatcher recorded a window-id. useTab is implied by windowId being non-null:
        // a recorded windowId means the dispatcher used a window-name (Windows) or
        // iTerm window id (Mac), so resume into it as a new tab.
        var useTab = ctx.WindowId != null;

        // #0175: thread the worktree context through to the resume launcher so the
        // resumed claude is wrapped in the same Set-Location / init-settings / try-
        // finally-cleanup envelope as the original dispatch. Without this the resume
        // tab lacks junctions and never runs `dydo worktree cleanup` on release.
        var worktreeId = ResolveResumeWorktreeId(agentDir);
        var mainProjectRoot = projectRoot;

        var launchedPid = LaunchResumeOverride != null
            ? LaunchResumeOverride(ctx.AgentName, ctx.SessionId, workingDirectory, ctx.WindowId, useTab, worktreeId, mainProjectRoot)
            : TerminalLauncher.LaunchResumeTerminal(ctx.AgentName, ctx.SessionId, workingDirectory, ctx.WindowId, useTab, worktreeId, mainProjectRoot);

        // PID > 1 only — 0/1 mean launch failed or returned an init-like sentinel.
        // Leaving LaunchedPid null in that case lets the next tick's liveness check
        // fall back to the wall-clock gate (legacy behaviour).
        if (launchedPid > 1)
            registry.RecordResumeLaunch(ctx.AgentName, launchedPid);

        WatchdogLogger.LogResume(dydoRoot, ctx.AgentName, ctx.SessionId, newCount, launchedPid);
    }

    /// <summary>
    /// Reads the per-agent resume preconditions under the per-agent .claim.lock and
    /// returns a context record when a resume is warranted, or null when any gating
    /// check rules it out (status != working, cap saturated, missing/dead-pid session,
    /// inside warmup window). Lock is released before this method returns.
    /// </summary>
    private static ResumeContext? TryReadResumeContext(string agentDir)
    {
        var statePath = Path.Combine(agentDir, "state.md");
        if (!File.Exists(statePath)) return null;

        var lockPath = Path.Combine(agentDir, ".claim.lock");
        var agentName = Path.GetFileName(agentDir);
        if (!AgentRegistry.TryAcquireLockAtPath(lockPath, agentName, out _)) return null;

        try
        {
            var cap = ResumeAttemptsCapOverride ?? ResumeAttemptsCap;
            var gate = ResumeWarmupGateOverride ?? ResumeWarmupGate;

            var (status, attempts, lastResumeAt, preResumePid, launchedPid, windowId) = ParseResumeFields(statePath);
            if (status != "working") return null;
            if (attempts >= cap) return null;

            var session = TryReadSession(Path.Combine(agentDir, ".session"));
            if (session == null) return null;
            if (session.SessionId == null) return null;
            if (session.ClaimedPid is not { } pid) return null;
            if (ProcessUtils.IsProcessRunning(pid)) return null;

            // Suppress re-fires during the resumed claude's warmup. Closes #0152.
            if (lastResumeAt.HasValue && DateTime.UtcNow - lastResumeAt.Value < gate)
                return null;

            return new ResumeContext(agentName, session.SessionId, pid, lastResumeAt,
                attempts, preResumePid, launchedPid, windowId, cap, gate);
        }
        finally
        {
            AgentRegistry.ReleaseLockAtPath(lockPath);
        }
    }

    /// <summary>
    /// PR3 of agent-crash-fixes: detects an agent whose resume episode has terminated by
    /// natural cap saturation and is ready for a one-shot <c>resume_outcome=gave_up</c>
    /// emission. Predicate: status=working AND attempts &gt;= cap AND LastResumeLaunchedAt
    /// is set AND the warmup gate has elapsed since that launch. The bad-session-fail-fast
    /// path emits its own <c>failed</c> outcome and clears LastResumeLaunchedAt, so a
    /// failed-then-gave_up double-fire cannot happen.
    /// </summary>
    private static GaveUpContext? TryReadGaveUpContext(string agentDir)
    {
        var statePath = Path.Combine(agentDir, "state.md");
        if (!File.Exists(statePath)) return null;

        var lockPath = Path.Combine(agentDir, ".claim.lock");
        var agentName = Path.GetFileName(agentDir);
        if (!AgentRegistry.TryAcquireLockAtPath(lockPath, agentName, out _)) return null;

        try
        {
            var cap = ResumeAttemptsCapOverride ?? ResumeAttemptsCap;
            var gate = ResumeWarmupGateOverride ?? ResumeWarmupGate;

            var (status, attempts, lastResumeAt, _, _, _) = ParseResumeFields(statePath);
            if (status != "working") return null;
            if (attempts < cap) return null;
            if (lastResumeAt == null) return null;
            if (DateTime.UtcNow - lastResumeAt.Value < gate) return null;

            var session = TryReadSession(Path.Combine(agentDir, ".session"));
            if (session?.SessionId == null) return null;

            var elapsed = (int)(DateTime.UtcNow - lastResumeAt.Value).TotalSeconds;
            return new GaveUpContext(agentName, session.SessionId, attempts, cap, elapsed);
        }
        finally
        {
            AgentRegistry.ReleaseLockAtPath(lockPath);
        }
    }

    private static Models.AgentSession? TryReadSession(string sessionPath)
    {
        if (!File.Exists(sessionPath)) return null;
        try
        {
            var json = File.ReadAllText(sessionPath);
            return System.Text.Json.JsonSerializer.Deserialize(
                json, Serialization.DydoDefaultJsonContext.Default.AgentSession);
        }
        catch { return null; }
    }

    /// <summary>
    /// True when the warmup window has elapsed AND the live ClaimedPid still equals
    /// the PID we observed at the last launch AND the launched resume terminal's
    /// claude PID is dead — meaning the resumed claude never refreshed its PID and
    /// is provably gone. The launched-PID liveness check distinguishes "still
    /// rehydrating" (alive but slow) from "genuinely failed" (dead) — the false
    /// positive that drove #0173. Saturating the cap here turns repeated redundant
    /// terminals into 1. Legacy state.md without launched-pid (null) falls through
    /// to the prior wall-clock-only behaviour for backward compatibility.
    /// </summary>
    private static bool IsBadSessionFailFast(ResumeContext ctx) =>
        ctx.LastResumeAt.HasValue
            && DateTime.UtcNow - ctx.LastResumeAt.Value >= ctx.Gate
            && ctx.PreResumePid.HasValue
            && ctx.ClaimedPid == ctx.PreResumePid.Value
            && (!ctx.LaunchedPid.HasValue || !ProcessUtils.IsProcessRunning(ctx.LaunchedPid.Value));

    /// <summary>
    /// True when the warmup window has elapsed but the launched claude PID is
    /// still alive — i.e. rehydration is taking longer than the gate but has not
    /// failed. Used to silent-skip the resume_blocked log emission per the
    /// #0173 locked decision (Q3): the log line is reserved for confirmed-dead
    /// launches, so a slow rehydration produces no log line at all (next tick
    /// re-checks; either it eventually claims, or the launched PID dies and
    /// IsBadSessionFailFast fires).
    /// </summary>
    private static bool IsLaunchedClaudeStillAlive(ResumeContext ctx) =>
        ctx.LastResumeAt.HasValue
            && DateTime.UtcNow - ctx.LastResumeAt.Value >= ctx.Gate
            && ctx.LaunchedPid.HasValue
            && ProcessUtils.IsProcessRunning(ctx.LaunchedPid.Value);

    private static string ResolveResumeWorkingDirectory(string agentDir, string projectRoot)
    {
        var worktreeMarker = Path.Combine(agentDir, ".worktree-path");
        if (!File.Exists(worktreeMarker)) return projectRoot;
        try
        {
            var path = File.ReadAllText(worktreeMarker).Trim();
            return Directory.Exists(path) ? path : projectRoot;
        }
        catch { return projectRoot; }
    }

    // The .worktree marker stores the worktree id (e.g. "implement-pr2" or
    // "parent/child"); set by DispatchService at worktree-creation time. Returns
    // null when the agent is not in a worktree, so the resume launcher's worktree
    // wrapper is bypassed for main-project claims.
    private static string? ResolveResumeWorktreeId(string agentDir)
    {
        var marker = Path.Combine(agentDir, ".worktree");
        if (!File.Exists(marker)) return null;
        try
        {
            var id = File.ReadAllText(marker).Trim();
            return string.IsNullOrEmpty(id) ? null : id;
        }
        catch { return null; }
    }

    private static (string? status, int attempts, DateTime? lastResumeAt, int? preResumePid, int? launchedPid, string? windowId)
        ParseResumeFields(string statePath)
    {
        try
        {
            var fields = FrontmatterParser.ParseFields(File.ReadAllText(statePath));
            if (fields == null) return (null, 0, null, null, null, null);
            fields.TryGetValue("status", out var status);
            return (
                status,
                ParseIntField(fields, "resume-attempts", 0),
                ParseTimestampField(fields, "last-resume-launched-at"),
                ParseNullableIntField(fields, "pre-resume-pid"),
                ParseNullableIntField(fields, "launched-pid"),
                ParseNonNullStringField(fields, "window-id"));
        }
        catch { return (null, 0, null, null, null, null); }
    }

    private static int ParseIntField(Dictionary<string, string> fields, string key, int defaultValue)
    {
        if (fields.TryGetValue(key, out var v) && int.TryParse(v, out var n)) return n;
        return defaultValue;
    }

    private static int? ParseNullableIntField(Dictionary<string, string> fields, string key)
    {
        if (!fields.TryGetValue(key, out var v)) return null;
        if (IsNullSentinel(v)) return null;
        return int.TryParse(v, out var n) ? n : null;
    }

    private static DateTime? ParseTimestampField(Dictionary<string, string> fields, string key)
    {
        if (!fields.TryGetValue(key, out var v)) return null;
        if (IsNullSentinel(v)) return null;
        return DateTime.TryParse(v, null, System.Globalization.DateTimeStyles.RoundtripKind, out var ts)
            ? ts : null;
    }

    private static string? ParseNonNullStringField(Dictionary<string, string> fields, string key)
    {
        if (!fields.TryGetValue(key, out var v)) return null;
        return IsNullSentinel(v) ? null : v;
    }

    private static bool IsNullSentinel(string? v) => v is null or "null" or "";

    // Whitelist target: claude (Linux/Mac, including the post-update
    // "claude.exe.old.<unix-ms>" rename on Windows) or node (Windows, where
    // claude/codex may ship as Node scripts), or codex. Every other matching PID — terminal
    // emulators that carry the prompt in their argv (gnome-terminal, konsole,
    // alacritty, kitty, …), bash wrappers, editors that happen to substring-
    // match — is fail-closed protected. Routing through MatchesProcessName
    // keeps the kill-side and anchor-side rename coverage in lockstep
    // (closes #0122, augments #0151).
    private static int KillAgentHostProcesses(string dydoRoot, string agentName, string pattern,
                                              List<int> pids, string? dispatchedBy, string? since)
    {
        var killsAttempted = 0;
        foreach (var pid in pids)
        {
            try
            {
                var procName = ProcessUtils.GetProcessName(pid);
                if (procName == null) continue;
                if (!ProcessUtils.MatchesProcessName(procName, "claude") &&
                    !ProcessUtils.MatchesProcessName(procName, "codex") &&
                    !ProcessUtils.MatchesProcessName(procName, "node"))
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

    /// <summary>
    /// Per-tick restore of expired model caps (issue #214): once a <c>dydo model cap</c>'s reset time
    /// passes, rebind its tiers back to the original model and re-sync. Extends the existing reconcile
    /// rather than adding a daemon; a no-op on ticks with no expired (or no) caps.
    /// </summary>
    public static void PollModelCaps(string dydoRoot) =>
        ModelCapService.RestoreExpired(DateTimeOffset.UtcNow, dydoRoot);

    /// <summary>
    /// Per-tick reconcile of the derived needs-human attention flag (Decision 030 §1). SETs it on a
    /// crash-mid-task orphan (status=working with an in-flight task, but the claimed session PID is
    /// dead — a dead session mid-task also needs the human). CLEARs it once the cause disappears (the
    /// agent released, or its task closed). A live working agent with a task is left untouched: a
    /// legitimately-waiting flag self-clears on the agent's next guarded tool call, not here.
    /// </summary>
    public static void PollNeedsHuman(string dydoRoot)
    {
        var agentsDir = Path.Combine(dydoRoot, "agents");
        if (!Directory.Exists(agentsDir)) return;

        var projectRoot = Path.GetDirectoryName(dydoRoot) ?? Directory.GetCurrentDirectory();
        AgentRegistry? registry = null;

        foreach (var agentDir in Directory.GetDirectories(agentsDir))
        {
            var (target, task) = ReconcileNeedsHumanForAgent(agentDir);
            if (target == null) continue;
            registry ??= new AgentRegistry(projectRoot);
            // Pass the task captured from state here (before nulling by any concurrent Release) so the
            // task-file mirror can still be reconciled even if SetNeedsHuman finds state.Task == null.
            registry.SetNeedsHuman(Path.GetFileName(agentDir), target.Value, task);
        }
    }

    // Returns (the needs-human value the watchdog should write for this agent, or null to leave it
    // as-is; the agent's task name captured from state for the task-file mirror, or null if none).
    private static (bool? target, string? task) ReconcileNeedsHumanForAgent(string agentDir)
    {
        var statePath = Path.Combine(agentDir, "state.md");
        if (!File.Exists(statePath)) return (null, null);
        try
        {
            var fields = FrontmatterParser.ParseFields(File.ReadAllText(statePath));
            if (fields == null) return (null, null);

            var working = fields.GetValueOrDefault("status") == "working";
            var task = fields.GetValueOrDefault("task");
            var hasTask = !string.IsNullOrEmpty(task) && task != "null";
            var needsHuman = fields.GetValueOrDefault("needs-human") == "true";
            var isExplicit = fields.GetValueOrDefault("needs-human-source") == "explicit";
            var taskName = hasTask ? task : null;

            // Crash mid-task: a working agent holding a task whose claimed session PID is gone. Always return
            // the SET (not null even when the flag is already true) so SetNeedsHuman still reconciles the
            // task-file mirror — a crashed agent's mirror may be stale or missing, and the state write is
            // skipped anyway when the flag is unchanged, so the only added work is the mirror repair (finding 8).
            if (working && hasTask && !IsClaimedSessionAlive(agentDir))
                return (true, taskName);

            // Cause disappeared: released (no longer working) or the task closed. Only DERIVED flags
            // self-heal here (Decision 030 §1). An EXPLICIT `dydo hand raise` — e.g. an orchestrator
            // flagging an idle peer that is not a working-with-task agent — is deliberately sticky and
            // is cleared only by `dydo hand lower` or by agent release (which clears flag + mirror
            // directly). The machine sweep must never erase a human-directed raise.
            if (needsHuman && !isExplicit && (!working || !hasTask))
                return (false, taskName);

            return (null, taskName);
        }
        catch { return (null, null); }
    }

    private static bool IsClaimedSessionAlive(string agentDir)
    {
        var session = TryReadSession(Path.Combine(agentDir, ".session"));
        return session?.ClaimedPid is { } pid && ProcessUtils.IsProcessRunning(pid);
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
