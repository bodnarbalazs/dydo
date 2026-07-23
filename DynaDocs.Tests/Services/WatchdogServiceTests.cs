namespace DynaDocs.Tests.Services;

using DynaDocs.Services;
using DynaDocs.Sync.Notion;
using DynaDocs.Utils;

/// <summary>The Notion-sync daemon lifecycle (ns-13): pid-file stale detection, detached spawn, stop, the
/// single-flight tick guard, interval floor, config-refusal, and the loop's census/probe cadence — all exercised
/// through the injectable process/clock seams, so nothing spawns a real process or waits real seconds.</summary>
[Collection("Integration")]
public sealed class WatchdogServiceTests : IDisposable
{
    private readonly string _dydoRoot;

    public WatchdogServiceTests()
    {
        _dydoRoot = Path.Combine(Path.GetTempPath(), "dydo-wd-" + Guid.NewGuid().ToString("N")[..8], "dydo");
        Directory.CreateDirectory(Path.GetDirectoryName(WatchdogService.PidPath(_dydoRoot))!);
        WatchdogLogger.LogPathOverride = Path.Combine(_dydoRoot, "watchdog.log");
    }

    public void Dispose()
    {
        WatchdogService.IsProcessAliveOverride = null;
        WatchdogService.SpawnOverride = null;
        WatchdogService.KillOverride = null;
        WatchdogService.ActivityStampReadOverride = null;
        WatchdogLogger.LogPathOverride = null;
        var parent = Directory.GetParent(_dydoRoot)!.FullName;
        if (Directory.Exists(parent)) Directory.Delete(parent, true);
    }

    [Fact]
    public void ClampInterval_EnforcesFiveSecondFloor()
    {
        Assert.Equal(5, WatchdogService.ClampInterval(2));
        Assert.Equal(5, WatchdogService.ClampInterval(5));
        Assert.Equal(30, WatchdogService.ClampInterval(30));
    }

    [Fact]
    public void Start_NoPidFile_SpawnsRunWithClampedInterval()
    {
        IReadOnlyList<string>? spawned = null;
        WatchdogService.SpawnOverride = args => spawned = args;
        var output = new StringWriter();

        var code = WatchdogService.Start(_dydoRoot, intervalSeconds: 2, censusInterval: 240, configError: null, output);

        Assert.Equal(ExitCodes.Success, code);
        Assert.NotNull(spawned);
        Assert.Equal(
            new[] { "watchdog", "run", "--interval", "5", "--census-interval", "240", "--lease", "60" },
            spawned);
        Assert.Contains("started", output.ToString());
    }

    [Fact]
    public void Start_LivePid_Refuses_NamingThePid()
    {
        File.WriteAllText(WatchdogService.PidPath(_dydoRoot), "4242");
        WatchdogService.IsProcessAliveOverride = pid => pid == 4242;
        var spawned = false;
        WatchdogService.SpawnOverride = _ => spawned = true;
        var output = new StringWriter();

        var code = WatchdogService.Start(_dydoRoot, 15, 240, null, output);

        Assert.Equal(ExitCodes.ToolError, code);
        Assert.Contains("4242", output.ToString());
        Assert.False(spawned);
    }

    [Fact]
    public void Start_StalePid_DeletesFileAndSpawns()
    {
        File.WriteAllText(WatchdogService.PidPath(_dydoRoot), "9999");
        WatchdogService.IsProcessAliveOverride = _ => false; // the recorded pid is dead
        var spawned = false;
        WatchdogService.SpawnOverride = _ => spawned = true;

        var code = WatchdogService.Start(_dydoRoot, 15, 240, null, new StringWriter());

        Assert.Equal(ExitCodes.Success, code);
        Assert.True(spawned);
        Assert.False(File.Exists(WatchdogService.PidPath(_dydoRoot))); // stale file removed
    }

    [Fact]
    public void Start_ConfigError_RefusesWithoutSpawning()
    {
        var spawned = false;
        WatchdogService.SpawnOverride = _ => spawned = true;
        var output = new StringWriter();

        var code = WatchdogService.Start(_dydoRoot, 15, 240, "not configured — set the token.", output);

        Assert.Equal(ExitCodes.ValidationErrors, code);
        Assert.False(spawned);
        Assert.Contains("not configured", output.ToString());
    }

    [Fact]
    public void Stop_KillsRecordedPid_AndDeletesFile()
    {
        File.WriteAllText(WatchdogService.PidPath(_dydoRoot), "7777");
        int? killed = null;
        WatchdogService.KillOverride = pid => killed = pid;
        var output = new StringWriter();

        var code = WatchdogService.Stop(_dydoRoot, output);

        Assert.Equal(ExitCodes.Success, code);
        Assert.Equal(7777, killed);
        Assert.False(File.Exists(WatchdogService.PidPath(_dydoRoot)));
        Assert.Contains("stopped", output.ToString());
    }

    [Fact]
    public void Stop_NoPidFile_ReportsNotRunning()
    {
        var output = new StringWriter();
        var code = WatchdogService.Stop(_dydoRoot, output);

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("not running", output.ToString());
    }

    [Fact]
    public void Run_ConfigError_RefusesToStart()
    {
        var ticked = false;
        var code = new WatchdogService().Run(
            _dydoRoot, 15, 240, 20, configError: "no parent page.",
            tick: (_, _) => { ticked = true; return NotionDeltaTickResult.Empty(false); },
            keepRunning: _ => true, wait: _ => { }, output: new StringWriter());

        Assert.Equal(ExitCodes.ValidationErrors, code);
        Assert.False(ticked); // dies before the loop
        Assert.False(File.Exists(WatchdogService.PidPath(_dydoRoot)));
    }

    [Fact]
    public void Run_WritesPidWhileLooping_DeletesOnExit_AndLogsOnePerTick()
    {
        var pidSeenDuringLoop = false;
        var ticks = 0;
        new WatchdogService().Run(
            _dydoRoot, 15, 240, 20, configError: null,
            tick: (_, _) => { ticks++; pidSeenDuringLoop |= File.Exists(WatchdogService.PidPath(_dydoRoot)); return NotionDeltaTickResult.Empty(false); },
            keepRunning: done => done < 3, wait: _ => { }, output: new StringWriter());

        Assert.Equal(3, ticks);
        Assert.True(pidSeenDuringLoop);                                        // pid file present during the loop
        Assert.False(File.Exists(WatchdogService.PidPath(_dydoRoot)));         // and cleaned up on exit
        Assert.Equal(3, File.ReadAllLines(WatchdogLogger.LogPathOverride!).Count(l => l.Contains("\"sync_tick\"")));
    }

    [Fact]
    public void Run_TickThrows_LoopSurvivesAndRetries()
    {
        var ticks = 0;
        new WatchdogService().Run(
            _dydoRoot, 15, 240, 20, configError: null,
            tick: (_, _) =>
            {
                ticks++;
                if (ticks == 1) throw new InvalidOperationException("boom");
                return NotionDeltaTickResult.Empty(false);
            },
            keepRunning: done => done < 3, wait: _ => { }, output: new StringWriter());

        Assert.Equal(3, ticks); // the throw did not kill the loop
        var log = File.ReadAllText(WatchdogLogger.LogPathOverride!);
        Assert.Contains("tick_error", log);
        Assert.Contains("boom", log);
    }

    [Fact]
    public void Run_CensusEveryNthTick_AndProbesProvisioningOnScheduledTicks()
    {
        var censuses = new List<bool>();
        var validates = new List<bool>();
        new WatchdogService().Run(
            _dydoRoot, 15, censusInterval: 2, probeInterval: 2, configError: null,
            tick: (census, validate) => { censuses.Add(census); validates.Add(validate); return NotionDeltaTickResult.Empty(census); },
            keepRunning: done => done < 4, wait: _ => { }, output: new StringWriter());

        Assert.Equal(new[] { false, true, false, true }, censuses);  // census on ticks 2 and 4
        Assert.Equal(new[] { true, true, false, true }, validates);  // probe on tick 1 (first), 2 and 4 (every 2)
    }

    [Fact]
    public void Run_LivePidExists_RefusesSecondInstance()
    {
        // ns-13 F5: a directly-invoked (or raced) second `run` must probe liveness and refuse, not overwrite a live
        // daemon's pid file. The exclusive-create acquire fails against a live pid.
        File.WriteAllText(WatchdogService.PidPath(_dydoRoot), "4242");
        WatchdogService.IsProcessAliveOverride = pid => pid == 4242;
        var ticked = false;

        var code = new WatchdogService().Run(
            _dydoRoot, 15, 240, 20, configError: null,
            tick: (_, _) => { ticked = true; return NotionDeltaTickResult.Empty(false); },
            keepRunning: _ => true, wait: _ => { }, output: new StringWriter());

        Assert.Equal(ExitCodes.ToolError, code);
        Assert.False(ticked);                                              // never entered the loop
        Assert.Equal("4242", File.ReadAllText(WatchdogService.PidPath(_dydoRoot))); // the live owner's pid was left intact
    }

    [Fact]
    public void Tick_SingleFlight_SkipsAReentrantTick_NeverQueues()
    {
        var svc = new WatchdogService();
        var inside = new ManualResetEventSlim();
        var release = new ManualResetEventSlim();

        NotionDeltaTickResult? first = null;
        var worker = new Thread(() => first = svc.Tick(() =>
        {
            inside.Set();
            release.Wait();
            return new NotionDeltaTickResult(1, 0, 0, 0, 0, 1, false);
        }, _dydoRoot));
        worker.Start();

        Assert.True(inside.Wait(TimeSpan.FromSeconds(5)));
        // A second tick fired while the first is still running is SKIPPED, not queued.
        var second = svc.Tick(() => new NotionDeltaTickResult(9, 9, 9, 0, 0, 9, false), _dydoRoot);
        Assert.Null(second);

        release.Set();
        worker.Join(TimeSpan.FromSeconds(5));
        Assert.NotNull(first);
        Assert.Contains("tick_skipped", File.ReadAllText(WatchdogLogger.LogPathOverride!));
    }

    // ── Suppress marker (watchdog-autostart-lease): stop means stop ──────────────────────────────

    [Fact]
    public void Stop_WritesHoldMarker_SuppressingAutoStart()
    {
        File.WriteAllText(WatchdogService.PidPath(_dydoRoot), "7777");
        WatchdogService.KillOverride = _ => { };
        var output = new StringWriter();

        var code = WatchdogService.Stop(_dydoRoot, output);

        Assert.Equal(ExitCodes.Success, code);
        Assert.True(File.Exists(WatchdogService.HoldPath(_dydoRoot)));
        Assert.Contains("suppressed", output.ToString());
    }

    [Fact]
    public void Stop_NotRunning_StillWritesHoldMarker()
    {
        // Invoking stop is always a deliberate "keep it off" signal, even when nothing is live.
        var code = WatchdogService.Stop(_dydoRoot, new StringWriter());

        Assert.Equal(ExitCodes.Success, code);
        Assert.True(File.Exists(WatchdogService.HoldPath(_dydoRoot)));
    }

    [Fact]
    public void Stop_KillThrows_StillWritesHoldMarker()
    {
        // The marker is written BEFORE the kill, so a silently-failing kill (access-denied etc.) still leaves the
        // suppression in place — the surviving daemon exits on its next per-tick hold check.
        File.WriteAllText(WatchdogService.PidPath(_dydoRoot), "7777");
        WatchdogService.KillOverride = _ => throw new UnauthorizedAccessException("denied");

        var code = WatchdogService.Stop(_dydoRoot, new StringWriter());

        Assert.Equal(ExitCodes.Success, code);
        Assert.True(File.Exists(WatchdogService.HoldPath(_dydoRoot)));
    }

    [Fact]
    public void Run_HoldMarkerAppearsMidRun_ExitsCleanly_AndLogsIt()
    {
        var ticks = 0;
        new WatchdogService().Run(
            _dydoRoot, 15, 240, 20, configError: null,
            tick: (_, _) =>
            {
                ticks++;
                if (ticks == 2) // a `stop` lands mid-run
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(WatchdogService.HoldPath(_dydoRoot))!);
                    File.WriteAllText(WatchdogService.HoldPath(_dydoRoot), "stop");
                }
                return NotionDeltaTickResult.Empty(false);
            },
            keepRunning: done => done < 10, wait: _ => { }, output: new StringWriter());

        Assert.Equal(2, ticks); // honored the marker after tick 2 — never reached tick 3
        Assert.Contains("hold_honored", File.ReadAllText(WatchdogLogger.LogPathOverride!));
        Assert.False(File.Exists(WatchdogService.PidPath(_dydoRoot))); // pid cleaned on exit
    }

    [Fact]
    public void Start_ClearsHoldMarker_BeforeSpawning()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(WatchdogService.HoldPath(_dydoRoot))!);
        File.WriteAllText(WatchdogService.HoldPath(_dydoRoot), "held");
        WatchdogService.SpawnOverride = _ => { };

        WatchdogService.Start(_dydoRoot, 15, 240, configError: null, new StringWriter());

        Assert.False(File.Exists(WatchdogService.HoldPath(_dydoRoot))); // a manual start lifts the suppression
    }

    [Fact]
    public void Start_TouchesActivityStamp_SoFreshDaemonHasFullLease()
    {
        WatchdogService.SpawnOverride = _ => { };

        WatchdogService.Start(_dydoRoot, 15, 240, configError: null, new StringWriter());

        Assert.True(File.Exists(WatchdogService.ActivityStampPath(_dydoRoot)));
    }

    // ── Lease expiry (watchdog-autostart-lease) ─────────────────────────────────────────────────

    [Fact]
    public void Run_LeaseExpires_ExitsCleanly_AndLogsIt()
    {
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var calls = 0;
        // No stamp file → the lease measures from the process-start baseline (calls==0). The next clock
        // read is 70 minutes on, past the 60-minute lease.
        Func<DateTime> clock = () => baseTime.AddMinutes(70 * calls++);
        var ticks = 0;

        var code = new WatchdogService().Run(
            _dydoRoot, 15, 240, 20, configError: null,
            tick: (_, _) => { ticks++; return NotionDeltaTickResult.Empty(false); },
            keepRunning: _ => true, wait: _ => { }, output: new StringWriter(),
            leaseMinutes: 60, utcNow: clock);

        Assert.Equal(ExitCodes.Success, code);
        Assert.Equal(1, ticks); // exits after the first tick's lease check
        Assert.False(File.Exists(WatchdogService.PidPath(_dydoRoot)));
        Assert.Contains("lease_expired", File.ReadAllText(WatchdogLogger.LogPathOverride!));
    }

    [Fact]
    public void Run_LeaseZero_NeverExpires()
    {
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var calls = 0;
        Func<DateTime> clock = () => baseTime.AddDays(calls++); // wildly advancing — irrelevant when lease is off
        var ticks = 0;

        new WatchdogService().Run(
            _dydoRoot, 15, 240, 20, configError: null,
            tick: (_, _) => { ticks++; return NotionDeltaTickResult.Empty(false); },
            keepRunning: done => done < 3, wait: _ => { }, output: new StringWriter(),
            leaseMinutes: 0, utcNow: clock);

        Assert.Equal(3, ticks); // only keepRunning ends the loop — the lease is disabled
        Assert.DoesNotContain("lease_expired", File.ReadAllText(WatchdogLogger.LogPathOverride!));
    }

    [Fact]
    public void Run_FreshStamp_KeepsDaemonAlive_DespiteAdvancingClock()
    {
        // Wall-clock time leaps 1000 min between ticks — the process-start baseline goes stale immediately — yet the
        // guard keeps refreshing the stamp to the current time, so lastActivity stays current and the lease never
        // lapses. (The stamp read models that refresh: it advances the shared clock the check then reads.)
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Func<DateTime> clock = () => now;
        WatchdogService.ActivityStampReadOverride = _ => { now = now.AddMinutes(1000); return now; };
        var ticks = 0;

        new WatchdogService().Run(
            _dydoRoot, 15, 240, 20, configError: null,
            tick: (_, _) => { ticks++; return NotionDeltaTickResult.Empty(false); },
            keepRunning: done => done < 3, wait: _ => { }, output: new StringWriter(),
            leaseMinutes: 60, utcNow: clock);

        Assert.Equal(3, ticks); // a fresh stamp holds the lease open; only keepRunning ends it
    }

    [Fact]
    public void Run_MissingStamp_UsesProcessStartBaseline_NoInstaExit()
    {
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var calls = 0;
        Func<DateTime> clock = () => baseTime.AddSeconds(calls++); // barely advances — well within the lease
        var ticks = 0;

        new WatchdogService().Run(
            _dydoRoot, 15, 240, 20, configError: null,
            tick: (_, _) => { ticks++; return NotionDeltaTickResult.Empty(false); },
            keepRunning: done => done < 2, wait: _ => { }, output: new StringWriter(),
            leaseMinutes: 60, utcNow: clock);

        Assert.Equal(2, ticks); // a missing stamp does not insta-kill a freshly started daemon
        Assert.DoesNotContain("lease_expired", File.ReadAllText(WatchdogLogger.LogPathOverride!));
    }
}
