namespace DynaDocs.Services;

using System.Diagnostics;
using DynaDocs.Sync.Notion;
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

    // Process/kill seams so start/stop are testable without touching real OS processes.
    internal static Func<int, bool>? IsProcessAliveOverride { get; set; }
    internal static Action<IReadOnlyList<string>>? SpawnOverride { get; set; }
    internal static Action<int>? KillOverride { get; set; }

    private int _inFlight;

    /// <summary>Clamp an interval to the 5s floor (ns-13): sub-5s ticks would hammer Notion's rate limit for no gain.</summary>
    public static int ClampInterval(int seconds) => Math.Max(MinIntervalSeconds, seconds);

    public static string PidPath(string dydoRoot) =>
        Path.Combine(dydoRoot, "_system", ".local", "watchdog.pid");

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
    /// the pid); a dead/stale pid file is deleted and start proceeds. A config error is reported and start aborts.</summary>
    public static int Start(string dydoRoot, int intervalSeconds, int censusInterval, string? configError, TextWriter output)
    {
        if (configError != null)
        {
            output.WriteLine($"watchdog: {configError} Not starting.");
            return ExitCodes.ValidationErrors;
        }

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
        var args = new List<string> { "watchdog", "run", "--interval", interval.ToString(), "--census-interval", censusInterval.ToString() };
        if (SpawnOverride is { } spawn)
        {
            spawn(args);
        }
        else
        {
            // The live daemon runs from the installed AOT binary, so ProcessPath IS dydo (DR-041).
            var psi = new ProcessStartInfo { FileName = Environment.ProcessPath!, UseShellExecute = false, CreateNoWindow = true };
            foreach (var a in args) psi.ArgumentList.Add(a);
            Process.Start(psi);
        }
        output.WriteLine($"watchdog: started (sync every {interval}s).");
        return ExitCodes.Success;
    }

    /// <summary>Kill the running daemon and clear the pid file. A dead pid (kill throws) is fine — the file is
    /// deleted regardless. A missing pid file reports not-running and exits clean.</summary>
    public static int Stop(string dydoRoot, TextWriter output)
    {
        var pidPath = PidPath(dydoRoot);
        if (!File.Exists(pidPath))
        {
            output.WriteLine("watchdog: not running.");
            return ExitCodes.Success;
        }

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
        output.WriteLine("watchdog: stopped.");
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
    /// (dies); any tick error is logged and the loop continues (retries next tick). Test seams: <paramref name="tick"/>
    /// supplies the sync (census, validateProvisioning) → result; <paramref name="keepRunning"/> bounds the loop
    /// (production returns true forever); <paramref name="wait"/> replaces the real sleep.</summary>
    public int Run(
        string dydoRoot, int intervalSeconds, int censusInterval, int probeInterval, string? configError,
        Func<bool, bool, NotionDeltaTickResult> tick, Func<int, bool> keepRunning, Action<TimeSpan> wait, TextWriter output)
    {
        if (configError != null)
        {
            output.WriteLine($"watchdog: {configError} Refusing to start.");
            return ExitCodes.ValidationErrors;
        }

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
                wait(interval);
            }
        }
        finally
        {
            try { File.Delete(pidPath); } catch { /* best-effort cleanup */ }
        }
        return ExitCodes.Success;
    }
}
