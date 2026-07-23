namespace DynaDocs.Tests.Services;

using DynaDocs.Commands;
using DynaDocs.Services;
using DynaDocs.Sync.Notion;

/// <summary>Guard-triggered auto-start with an activity lease (watchdog-autostart-lease): the throttled activity
/// stamp in the guard hot path, and the daemon's spawn decision (Notion configured, no suppress marker, no live
/// daemon). Exercised through the SpawnOverride/liveness seams so nothing spawns a real process; a project with no
/// dydo.json above it makes the "not connected" gate deterministic regardless of the machine's Notion env.</summary>
[Collection("Integration")]
public sealed class WatchdogAutoStartTests : IDisposable
{
    private readonly string _root;
    private readonly string _dydoRoot;
    private readonly string _savedCwd;
    private readonly string? _savedToken;

    public WatchdogAutoStartTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "dydo-autostart-" + Guid.NewGuid().ToString("N")[..8]);
        _dydoRoot = Path.Combine(_root, "dydo");
        Directory.CreateDirectory(Path.Combine(_dydoRoot, "_system", ".local"));

        _savedCwd = Directory.GetCurrentDirectory();
        _savedToken = Environment.GetEnvironmentVariable(NotionTokenResolver.TokenEnvVar);
        Directory.SetCurrentDirectory(_root);
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_savedCwd);
        Environment.SetEnvironmentVariable(NotionTokenResolver.TokenEnvVar, _savedToken);
        WatchdogService.SpawnOverride = null;
        WatchdogService.IsProcessAliveOverride = null;
        if (Directory.Exists(_root)) { try { Directory.Delete(_root, true); } catch { /* best-effort */ } }
    }

    // Notion configured: a parentPageId in dydo.json (short-circuits any env/registry parent) plus a resolvable
    // token. DaemonConfigError then returns null — the daemon has everything it needs.
    private void MakeConnected()
    {
        File.WriteAllText(Path.Combine(_root, "dydo.json"),
            """{ "version": 1, "structure": { "root": "dydo" }, "notion": { "parentPageId": "page-abc" } }""");
        Environment.SetEnvironmentVariable(NotionTokenResolver.TokenEnvVar, "test-token");
    }

    // On-disk proof a sync was explicitly begun in this project (precondition (d)): a provision*.json in the
    // provision registry directory. Named path defaults to the parent-scoped variant.
    private void SeedProvision(string fileName = "provision-abc12345.json")
    {
        var dir = Path.Combine(_dydoRoot, "_system", ".local", "notion");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, fileName), "{}");
    }

    private void StaleStamp() =>
        File.SetLastWriteTimeUtc(WriteStampNow(), DateTime.UtcNow.AddMinutes(-10)); // older than the 5-min throttle

    private string WriteStampNow()
    {
        var stamp = WatchdogService.ActivityStampPath(_dydoRoot);
        File.WriteAllText(stamp, DateTime.UtcNow.ToString("O"));
        return stamp;
    }

    // ── WatchdogService.AutoStart — the spawn decision ──────────────────────────────────────────

    [Fact]
    public void AutoStart_Connected_Provisioned_NoMarker_NoLivePid_Spawns()
    {
        MakeConnected();
        SeedProvision();
        IReadOnlyList<string>? spawned = null;
        WatchdogService.SpawnOverride = a => spawned = a;

        WatchdogService.AutoStart(_dydoRoot);

        Assert.NotNull(spawned);
        Assert.Equal(
            new[] { "watchdog", "run", "--interval", "15", "--census-interval", "240", "--lease", "60" },
            spawned);
        Assert.True(File.Exists(WatchdogService.ActivityStampPath(_dydoRoot))); // stamp touched at spawn
    }

    [Fact]
    public void AutoStart_LegacyProvisionJson_Spawns()
    {
        // The legacy project-scoped provision.json counts as evidence just like a parent-scoped variant.
        MakeConnected();
        SeedProvision("provision.json");
        var spawned = false;
        WatchdogService.SpawnOverride = _ => spawned = true;

        WatchdogService.AutoStart(_dydoRoot);

        Assert.True(spawned);
    }

    [Fact]
    public void AutoStart_NoProvisionEvidence_Skips()
    {
        // All other preconditions green, but this project never provisioned a sync → auto-start never initiates
        // first contact.
        MakeConnected();
        var spawned = false;
        WatchdogService.SpawnOverride = _ => spawned = true;

        WatchdogService.AutoStart(_dydoRoot);

        Assert.False(spawned);
    }

    [Fact]
    public void AutoStart_HoldMarker_Skips()
    {
        MakeConnected();
        File.WriteAllText(WatchdogService.HoldPath(_dydoRoot), "held");
        var spawned = false;
        WatchdogService.SpawnOverride = _ => spawned = true;

        WatchdogService.AutoStart(_dydoRoot);

        Assert.False(spawned); // stop means stop — suppressed until a manual start
    }

    [Fact]
    public void AutoStart_LivePid_Skips()
    {
        MakeConnected();
        File.WriteAllText(WatchdogService.PidPath(_dydoRoot), "4242");
        WatchdogService.IsProcessAliveOverride = pid => pid == 4242;
        var spawned = false;
        WatchdogService.SpawnOverride = _ => spawned = true;

        WatchdogService.AutoStart(_dydoRoot);

        Assert.False(spawned); // a daemon already holds the pid
    }

    [Fact]
    public void AutoStart_NotConnected_Skips()
    {
        // No dydo.json anywhere above cwd → the daemon config gate fails ("no dydo.json"), regardless of any
        // Notion token/parent-page the machine happens to have set.
        var spawned = false;
        WatchdogService.SpawnOverride = _ => spawned = true;

        WatchdogService.AutoStart(_dydoRoot);

        Assert.False(spawned);
    }

    // ── GuardCommand.AutoStartWatchdogIfDue — the throttled hot path ─────────────────────────────

    [Fact]
    public void Guard_FreshStamp_DoesNotAttemptSpawn()
    {
        MakeConnected();
        WriteStampNow(); // younger than the throttle window → short-circuits before any precondition/spawn work
        var spawned = false;
        WatchdogService.SpawnOverride = _ => spawned = true;

        GuardCommand.AutoStartWatchdogIfDue();

        Assert.False(spawned);
    }

    [Fact]
    public void Guard_StaleStamp_AllPreconditions_Spawns()
    {
        MakeConnected();
        SeedProvision();
        StaleStamp();
        var spawned = false;
        WatchdogService.SpawnOverride = _ => spawned = true;

        GuardCommand.AutoStartWatchdogIfDue();

        Assert.True(spawned);
        // the stale stamp was refreshed to now
        Assert.True((DateTime.UtcNow - File.GetLastWriteTimeUtc(WatchdogService.ActivityStampPath(_dydoRoot))).TotalMinutes < 5);
    }

    [Fact]
    public void Guard_MissingStamp_Spawns()
    {
        MakeConnected(); // no stamp written at all → due for refresh + auto-start
        SeedProvision();
        var spawned = false;
        WatchdogService.SpawnOverride = _ => spawned = true;

        GuardCommand.AutoStartWatchdogIfDue();

        Assert.True(spawned);
    }

    [Fact]
    public void Guard_SpawnThrows_ExceptionSwallowed_GuardNeverBreaks()
    {
        MakeConnected();
        SeedProvision();
        StaleStamp();
        WatchdogService.SpawnOverride = _ => throw new InvalidOperationException("boom");

        var ex = Record.Exception(GuardCommand.AutoStartWatchdogIfDue);

        Assert.Null(ex); // the guard must never break or block on auto-start
    }
}
