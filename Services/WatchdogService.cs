namespace DynaDocs.Services;

using System.Diagnostics;
using DynaDocs.Sync.Notion;
using DynaDocs.Sync.Notion.Provisioning;
using DynaDocs.Utils;

/// <summary>
/// The Notion-sync daemon (ns-13, the DR-041 repurpose of the stripped watchdog). <c>start</c> spawns a detached
/// <c>watchdog run</c>, guarded by a pid file against a second instance; <c>stop</c> kills it; <c>run</c> is the
/// foreground loop that fires one cheap <see cref="NotionSpineDelta"/> tick every interval. The loop is single-
/// flight (a tick never overlaps or queues behind a running one), never dies on a sync/API error (it logs and
/// retries next tick), and dies only on a startup config error. Every process/clock touch-point is injectable so
/// the whole lifecycle is unit-testable without spawning a real process or waiting real seconds.
/// </summary>
public sealed class WatchdogService
{
    public const int DefaultIntervalSeconds = 15;
    public const int MinIntervalSeconds = 5;

    /// <summary>Full remote-archive census cadence (ns-13): every 240 ticks ≈ one hour at the 15s default. A census
    /// paginates ids/stamps only (no body reads) to surface pages archived in Notion, which a filtered fast tick
    /// cannot see.</summary>
    public const int DefaultCensusInterval = 240;

    /// <summary>Provision-probe cadence: re-validate each type's database still exists every 20 ticks (and on the
    /// first tick, and immediately after any tick error), not every tick — the recorded ids are read locally otherwise.</summary>
    public const int ProvisionProbeInterval = 20;

    /// <summary>Activity-lease default (watchdog-autostart-lease): the daemon self-exits after this many minutes with
    /// no guard-refreshed activity stamp — it runs while someone works in the project and dies an hour after work
    /// stops, resurrecting on the next session's first tool call. <c>--lease 0</c> disables expiry.</summary>
    public const int DefaultLeaseMinutes = 60;

    // Process/kill seams so start/stop are testable without touching real OS processes.
    internal static Func<int, bool>? IsProcessAliveOverride { get; set; }
    internal static Action<IReadOnlyList<string>>? SpawnOverride { get; set; }
    internal static Action<int>? KillOverride { get; set; }

    // Clock/stamp seams so the lease loop is testable without real sleeps or a real stamp file.
    internal static Func<string, DateTime>? ActivityStampReadOverride { get; set; }

    private int _inFlight;

    /// <summary>Clamp an interval to the 5s floor (ns-13): sub-5s ticks would hammer Notion's rate limit for no gain.</summary>
    public static int ClampInterval(int seconds) => Math.Max(MinIntervalSeconds, seconds);

    public static string PidPath(string dydoRoot) =>
        Path.Combine(dydoRoot, "_system", ".local", "watchdog.pid");

    /// <summary>The guard-refreshed activity stamp the daemon leases against (watchdog-autostart-lease): its
    /// last-write time is the daemon's proof someone is still working in the project.</summary>
    public static string ActivityStampPath(string dydoRoot) =>
        Path.Combine(dydoRoot, "_system", ".local", "watchdog-activity");

    /// <summary>The suppress marker a <c>stop</c> writes (watchdog-autostart-lease): while it exists, auto-start
    /// stays honored-off — stop means stop — until the next manual <c>start</c> clears it. A crashed daemon leaves
    /// no marker, so crashes still self-heal.</summary>
    public static string HoldPath(string dydoRoot) =>
        Path.Combine(dydoRoot, "_system", ".local", "watchdog.hold");

    private static bool IsProcessAlive(int pid)
    {
        if (IsProcessAliveOverride is { } probe)
            return probe(pid);
        try
        {
            using var p = Process.GetProcessById(pid);
            return !p.HasExited;
        }
        catch
        {
            return false; // no such process — a dead/stale pid
        }
    }

    /// <summary>Claim the pid file exclusively (ns-13 F5): create it with <see cref="FileMode.CreateNew"/> so two
    /// racing daemons cannot both succeed. If the create loses because a file already exists, probe its pid — a live
    /// one wins (return false), a dead/stale one is deleted and the create retried. Returns whether this process now
    /// owns the pid file.</summary>
    private static bool TryAcquirePid(string pidPath)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                using var fs = new FileStream(pidPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                using var writer = new StreamWriter(fs);
                writer.Write(Environment.ProcessId);
                return true;
            }
            catch (IOException)
            {
                // The file exists. A live owner wins; a dead/stale pid is cleared and the create retried.
                if (int.TryParse(SafeRead(pidPath), out var pid) && IsProcessAlive(pid))
                    return false;
                try { File.Delete(pidPath); } catch { /* another racer may have deleted it — retry the create */ }
            }
        }
        return false;
    }

    private static string SafeRead(string path)
    {
        try { return File.ReadAllText(path).Trim(); } catch { return ""; }
    }

    /// <summary>Spawn a detached <c>watchdog run</c>. Refuses when a live daemon already holds the pid file (naming
    /// the pid); a dead/stale pid file is deleted and start proceeds. A config error is reported and start aborts.
    /// A manual start clears any suppress marker (watchdog-autostart-lease) — a human explicitly wants the daemon
    /// back — and touches the activity stamp so the fresh daemon begins with a full lease.</summary>
    public static int Start(
        string dydoRoot, int intervalSeconds, int censusInterval, string? configError, TextWriter output,
        int leaseMinutes = DefaultLeaseMinutes)
    {
        if (configError != null)
        {
            output.WriteLine($"watchdog: {configError} Not starting.");
            return ExitCodes.ValidationErrors;
        }

        TryDeleteHold(dydoRoot); // manual start lifts a prior stop's suppression

        var pidPath = PidPath(dydoRoot);
        if (File.Exists(pidPath))
        {
            if (int.TryParse(File.ReadAllText(pidPath).Trim(), out var existing) && IsProcessAlive(existing))
            {
                output.WriteLine($"watchdog: already running (pid {existing}).");
                return ExitCodes.ToolError;
            }
            File.Delete(pidPath); // stale — the process is gone
        }

        var interval = ClampInterval(intervalSeconds);
        TouchActivityStamp(dydoRoot);
        Spawn(interval, censusInterval, leaseMinutes);
        output.WriteLine($"watchdog: started (sync every {interval}s).");
        return ExitCodes.Success;
    }

    /// <summary>Guard-triggered auto-start (watchdog-autostart-lease). Spawns the daemon QUIETLY, and only when it
    /// SHOULD run: no suppress marker (a prior <c>stop</c> stays honored until a manual <c>start</c>), Notion actually
    /// configured (the same gate the manual start uses), sync already provisioned in THIS project (an implicit
    /// trigger only resumes a sync explicitly begun here — it never initiates first contact), and no live daemon
    /// already holding the pid. Every failed precondition is a silent skip — a guard call must not chatter — while a
    /// real spawn emits one stderr NOTICE.</summary>
    public static void AutoStart(string dydoRoot)
    {
        if (File.Exists(HoldPath(dydoRoot)))
            return;

        var config = new ConfigService();
        var token = NotionTokenResolver.Resolve(config.LoadConfig(), config.GetProjectRoot(), config.GetDydoRoot());
        if (NotionSyncService.DaemonConfigError(token, config) != null)
            return;

        if (!HasProvisionEvidence(dydoRoot))
            return;

        var pidPath = PidPath(dydoRoot);
        if (File.Exists(pidPath) && int.TryParse(SafeRead(pidPath), out var pid) && IsProcessAlive(pid))
            return;

        TouchActivityStamp(dydoRoot);
        Spawn(DefaultIntervalSeconds, DefaultCensusInterval, DefaultLeaseMinutes);
        Console.Error.WriteLine("NOTICE: dydo watchdog auto-started (Notion sync)");
    }

    /// <summary>Whether this project has on-disk proof that a Notion sync was explicitly begun here: the provision
    /// registry directory holds at least one <c>provision*.json</c> (the legacy project-scoped <c>provision.json</c>
    /// or a parent-scoped <c>provision-&lt;hash&gt;.json</c>), written only after a successful provision/sync. The
    /// env-fallback config gate alone would let a global parent-page reach any project; this keeps the implicit
    /// auto-start from ever initiating first contact — a never-synced project is never auto-started.</summary>
    public static bool HasProvisionEvidence(string dydoRoot)
    {
        var dir = Path.GetDirectoryName(NotionProvisioner.PathFor(dydoRoot))!;
        return Directory.Exists(dir) && Directory.EnumerateFiles(dir, "provision*.json").Any();
    }

    /// <summary>Spawn a detached <c>watchdog run</c> with the given cadence and lease. The live daemon runs from the
    /// installed AOT binary, so <see cref="Environment.ProcessPath"/> IS dydo (DR-041).
    ///
    /// The daemon MUST NOT inherit the caller's stdio handles: an auto-start fires from the guard hook, whose
    /// stdout/stderr are pipes the hook runner reads. A daemon holding those pipe handles keeps them open for its
    /// whole (long) life, so a reader waiting for pipe EOF stalls the triggering tool call until the hook times out
    /// (observed in ns-13 live bring-up: a `watchdog start` wrapper hung until timeout while the daemon lived on).
    /// On Windows, ShellExecuteEx (<c>UseShellExecute = true</c>) launches without inheriting the caller's handles —
    /// empirically the caller's pipe reaches EOF at the guard's exit, not the daemon's death; merely redirecting the
    /// child's streams does NOT fix it (the original inheritable handles still leak into the child's handle table).
    /// On POSIX, ShellExecuteEx doesn't exist and <c>UseShellExecute = true</c> means "open via the desktop handler"
    /// (wrong for launching our binary with args), so we redirect the child's std streams to fresh pipes and close
    /// our ends — the daemon never holds the guard's stdio, and the guard's own streams reach EOF at its exit.</summary>
    private static void Spawn(int intervalSeconds, int censusInterval, int leaseMinutes)
    {
        var args = new List<string>
        {
            "watchdog", "run", "--interval", intervalSeconds.ToString(),
            "--census-interval", censusInterval.ToString(), "--lease", leaseMinutes.ToString(),
        };
        if (SpawnOverride is { } spawn)
        {
            spawn(args);
            return;
        }

        var psi = new ProcessStartInfo { FileName = Environment.ProcessPath! };
        foreach (var a in args) psi.ArgumentList.Add(a);

        if (OperatingSystem.IsWindows())
        {
            psi.UseShellExecute = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            Process.Start(psi);
            return;
        }

        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.RedirectStandardInput = true;
        var proc = Process.Start(psi)!;
        proc.StandardOutput.Dispose();
        proc.StandardError.Dispose();
        proc.StandardInput.Dispose();
    }

    private static void TouchActivityStamp(string dydoRoot) => WriteLocal(ActivityStampPath(dydoRoot));
    private static void WriteHold(string dydoRoot) => WriteLocal(HoldPath(dydoRoot));

    private static void WriteLocal(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, DateTime.UtcNow.ToString("O"));
    }

    private static void TryDeleteHold(string dydoRoot)
    {
        try { File.Delete(HoldPath(dydoRoot)); } catch { /* best-effort — a missing marker is the goal */ }
    }

    private static DateTime ReadActivityStamp(string dydoRoot)
    {
        if (ActivityStampReadOverride is { } read)
            return read(dydoRoot);
        var path = ActivityStampPath(dydoRoot);
        return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
    }

    /// <summary>Kill the running daemon and clear the pid file, then write the suppress marker so auto-start stays
    /// honored-off until the next manual <c>start</c> (watchdog-autostart-lease — stop means stop). A dead pid (kill
    /// throws) is fine — the file is deleted regardless. Running <c>stop</c> when nothing is live still writes the
    /// marker: invoking stop is always a deliberate "keep it off" signal.</summary>
    public static int Stop(string dydoRoot, TextWriter output)
    {
        // Write the suppress marker FIRST — before the kill and pid-delete. Then "stop means stop" holds even if an
        // auto-start races in the same millisecond (the daemon's per-tick hold check catches it) and even if the
        // kill silently fails (access-denied is swallowed below) — the surviving process still exits on its next tick.
        WriteHold(dydoRoot);

        var pidPath = PidPath(dydoRoot);
        var wasRunning = File.Exists(pidPath);

        if (wasRunning)
        {
            if (int.TryParse(File.ReadAllText(pidPath).Trim(), out var pid))
            {
                try
                {
                    if (KillOverride is { } kill)
                        kill(pid);
                    else
                    {
                        using var p = Process.GetProcessById(pid);
                        p.Kill();
                    }
                }
                catch
                {
                    // Already dead — nothing to kill; still clear the pid file below.
                }
            }
            File.Delete(pidPath);
        }

        output.WriteLine(wasRunning
            ? "watchdog: stopped. Auto-start suppressed until the next `dydo watchdog start`."
            : "watchdog: not running. Auto-start suppressed until the next `dydo watchdog start`.");
        return ExitCodes.Success;
    }

    /// <summary>Run one tick under the single-flight guard: if a tick is already in flight (a timer/re-entry firing
    /// during a running tick), skip it — log one line, never queue — and return null. Otherwise run
    /// <paramref name="body"/> to completion. In-process re-entrancy only; the pid file covers cross-process.</summary>
    public NotionDeltaTickResult? Tick(Func<NotionDeltaTickResult> body, string dydoRoot)
    {
        if (Interlocked.CompareExchange(ref _inFlight, 1, 0) != 0)
        {
            WatchdogLogger.LogTickSkipped(dydoRoot);
            return null;
        }
        try
        {
            return body();
        }
        finally
        {
            Interlocked.Exchange(ref _inFlight, 0);
        }
    }

    /// <summary>The foreground daemon loop. Writes its own pid on entry, deletes it on clean exit. Each iteration
    /// runs one guarded tick, logs one summary line, then waits the interval. A config error refuses to start
    /// (dies); any tick error is logged and the loop continues (retries next tick). After each tick the activity
    /// lease is checked (watchdog-autostart-lease): when the guard has not refreshed the activity stamp for
    /// <paramref name="leaseMinutes"/>, the daemon exits cleanly (<c>leaseMinutes == 0</c> disables expiry). Test
    /// seams: <paramref name="tick"/> supplies the sync (census, validateProvisioning) → result;
    /// <paramref name="keepRunning"/> bounds the loop (production returns true forever); <paramref name="wait"/>
    /// replaces the real sleep; <paramref name="utcNow"/> replaces the clock.</summary>
    public int Run(
        string dydoRoot, int intervalSeconds, int censusInterval, int probeInterval, string? configError,
        Func<bool, bool, NotionDeltaTickResult> tick, Func<int, bool> keepRunning, Action<TimeSpan> wait, TextWriter output,
        int leaseMinutes = 0, Func<DateTime>? utcNow = null)
    {
        if (configError != null)
        {
            output.WriteLine($"watchdog: {configError} Refusing to start.");
            return ExitCodes.ValidationErrors;
        }

        var clock = utcNow ?? (() => DateTime.UtcNow);
        var lease = TimeSpan.FromMinutes(leaseMinutes);
        // A missing/stale stamp never insta-kills a freshly (manually) started daemon: the baseline seeds the lease.
        var processStartBaseline = clock();

        var interval = TimeSpan.FromSeconds(ClampInterval(intervalSeconds));
        var pidPath = PidPath(dydoRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(pidPath)!);
        // `run` performs the SAME liveness guard `start` does before claiming the pid, so a directly-invoked (or
        // raced) second `run` refuses instead of overwriting a live daemon's pid file (ns-13 F5).
        if (!TryAcquirePid(pidPath))
        {
            output.WriteLine("watchdog: already running. Refusing to start a second instance.");
            return ExitCodes.ToolError;
        }

        var tickNumber = 0;
        var revalidate = true; // probe provisioning on the first tick, then every probeInterval and after errors
        try
        {
            while (keepRunning(tickNumber))
            {
                tickNumber++;
                var census = censusInterval > 0 && tickNumber % censusInterval == 0;
                var validate = revalidate || (probeInterval > 0 && tickNumber % probeInterval == 0);
                var started = Stopwatch.GetTimestamp();
                try
                {
                    var result = Tick(() => tick(census, validate), dydoRoot);
                    if (result != null)
                    {
                        revalidate = false;
                        var ms = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                        WatchdogLogger.LogSyncTick(dydoRoot, result.Reconciled, result.Created, result.Updated,
                            result.Archived, result.Conflicts, result.FuseTrips, result.Requests, ms, result.Quiet, result.Census);
                    }
                }
                catch (Exception e)
                {
                    // Never die on a sync/API error — log loudly and retry next tick, re-probing provisioning.
                    WatchdogLogger.LogTickError(dydoRoot, e.Message);
                    revalidate = true;
                }

                // Honor a suppress marker written mid-run (a racing or kill-failed `stop`): one stat per tick, exit
                // cleanly. Checked before the lease so an explicit stop always wins.
                if (File.Exists(HoldPath(dydoRoot)))
                {
                    WatchdogLogger.LogHoldHonored(dydoRoot);
                    break;
                }

                if (leaseMinutes > 0)
                {
                    var lastActivity = Max(ReadActivityStamp(dydoRoot), processStartBaseline);
                    if (clock() - lastActivity > lease)
                    {
                        WatchdogLogger.LogLeaseExpired(dydoRoot, leaseMinutes);
                        break;
                    }
                }

                wait(interval);
            }
        }
        finally
        {
            try { File.Delete(pidPath); } catch { /* best-effort cleanup */ }
        }
        return ExitCodes.Success;
    }

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
}
