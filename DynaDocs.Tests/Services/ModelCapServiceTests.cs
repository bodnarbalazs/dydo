namespace DynaDocs.Tests.Services;

using System.Text.Json;
using DynaDocs.Models;
using DynaDocs.Serialization;
using DynaDocs.Services;
using Xunit;

public class ModelCapServiceTests : IDisposable
{
    private readonly string _projectRoot;
    private int _resyncCalls;
    private string? _lastResyncRoot;

    public ModelCapServiceTests()
    {
        _projectRoot = Path.Combine(Path.GetTempPath(), "dydo-modelcap-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_projectRoot);
        // Stub the re-sync so cap/uncap surgery doesn't emit native agent files during tests, and
        // capture the project root it was handed so we can assert the re-sync targets the right tree.
        ModelCapService.ResyncOverride = pr => { _resyncCalls++; _lastResyncRoot = pr; return 0; };
        SaveConfig(ConfigFactory.CreateDefault("balazs"));
    }

    public void Dispose()
    {
        ModelCapService.ResyncOverride = null;
        WatchdogLogger.LogPathOverride = null;
        try { Directory.Delete(_projectRoot, true); } catch { }
    }

    private void SaveConfig(DydoConfig config) =>
        new ConfigService().SaveConfig(config, Path.Combine(_projectRoot, "dydo.json"));

    private DydoConfig LoadConfig() =>
        new ConfigService().LoadConfig(_projectRoot)!;

    private string MarkerPath(string model) =>
        Path.Combine(_projectRoot, "dydo", "_system", ".local", "model-caps", model + ".json");

    private static DateTimeOffset Future => DateTimeOffset.Now.AddHours(6);

    [Fact]
    public void DefaultModels_DeclareSonnetFallback()
    {
        Assert.Equal("claude-sonnet-5", ConfigFactory.CreateDefaultModels().Fallback);
    }

    [Fact]
    public void Cap_RebindsMatchingTier_WritesMarker_ReSyncs()
    {
        var code = ModelCapService.Cap("claude-fable-5", Future, null, TextWriter.Null, TextWriter.Null, _projectRoot);

        Assert.Equal(0, code);
        Assert.Equal(1, _resyncCalls);

        var tiers = LoadConfig().Models!.Tiers["anthropic"];
        Assert.Equal("claude-sonnet-5", tiers["strong"]);   // rebound to the declared fallback
        Assert.Equal("claude-opus-4-8", tiers["standard"]); // untouched

        var marker = ReadMarker("claude-fable-5");
        Assert.Equal("claude-fable-5", marker.Model);
        Assert.Equal("claude-sonnet-5", marker.Fallback);
        Assert.Contains(marker.ReboundTiers, b => b.Vendor == "anthropic" && b.Tier == "strong");
        Assert.DoesNotContain(marker.ReboundTiers, b => b.Tier == "standard");
    }

    [Fact]
    public void Cap_ExplicitFallback_OverridesDeclaredDefault()
    {
        ModelCapService.Cap("claude-fable-5", Future, "claude-opus-4-8", TextWriter.Null, TextWriter.Null, _projectRoot);

        Assert.Equal("claude-opus-4-8", LoadConfig().Models!.Tiers["anthropic"]["strong"]);
        Assert.Equal("claude-opus-4-8", ReadMarker("claude-fable-5").Fallback);
    }

    [Fact]
    public void Cap_NoTierBindsModel_IsRejected()
    {
        var code = ModelCapService.Cap("no-such-model", Future, null, TextWriter.Null, TextWriter.Null, _projectRoot);

        Assert.Equal(1, code);
        Assert.False(File.Exists(MarkerPath("no-such-model")));
        Assert.Equal(0, _resyncCalls);
    }

    [Fact]
    public void Cap_FallbackEqualsModel_IsRejected()
    {
        var code = ModelCapService.Cap("claude-fable-5", Future, "claude-fable-5", TextWriter.Null, TextWriter.Null, _projectRoot);

        Assert.Equal(1, code);
        Assert.Equal(0, _resyncCalls);
    }

    [Fact]
    public void Cap_NoFallbackDeclaredOrGiven_IsRejected()
    {
        var config = ConfigFactory.CreateDefault("balazs");
        config.Models!.Fallback = null;
        SaveConfig(config);

        var code = ModelCapService.Cap("claude-fable-5", Future, null, TextWriter.Null, TextWriter.Null, _projectRoot);

        Assert.Equal(1, code);
        Assert.Equal(0, _resyncCalls);
    }

    [Fact]
    public void Cap_NoModelsSection_IsRejected()
    {
        var config = ConfigFactory.CreateDefault("balazs");
        config.Models = null;
        SaveConfig(config);

        var code = ModelCapService.Cap("claude-fable-5", Future, "x", TextWriter.Null, TextWriter.Null, _projectRoot);

        Assert.Equal(1, code);
    }

    [Fact]
    public void ReCap_WithNewFallback_PreservesRestoreRecord()
    {
        ModelCapService.Cap("claude-fable-5", Future, null, TextWriter.Null, TextWriter.Null, _projectRoot);
        // Second cap: the tiers already point at the first fallback, so only the marker knows what to restore.
        ModelCapService.Cap("claude-fable-5", Future, "claude-opus-4-8", TextWriter.Null, TextWriter.Null, _projectRoot);

        Assert.Equal("claude-opus-4-8", LoadConfig().Models!.Tiers["anthropic"]["strong"]);
        var marker = ReadMarker("claude-fable-5");
        Assert.Contains(marker.ReboundTiers, b => b.Vendor == "anthropic" && b.Tier == "strong");

        ModelCapService.Uncap("claude-fable-5", TextWriter.Null, TextWriter.Null, _projectRoot);
        Assert.Equal("claude-fable-5", LoadConfig().Models!.Tiers["anthropic"]["strong"]);
    }

    [Fact]
    public void Uncap_RestoresBindings_DeletesMarker_ReSyncs()
    {
        ModelCapService.Cap("claude-fable-5", Future, null, TextWriter.Null, TextWriter.Null, _projectRoot);
        _resyncCalls = 0;

        var code = ModelCapService.Uncap("claude-fable-5", TextWriter.Null, TextWriter.Null, _projectRoot);

        Assert.Equal(0, code);
        Assert.Equal(1, _resyncCalls);
        Assert.Equal("claude-fable-5", LoadConfig().Models!.Tiers["anthropic"]["strong"]);
        Assert.False(File.Exists(MarkerPath("claude-fable-5")));
    }

    [Fact]
    public void Uncap_NoActiveCap_IsRejected()
    {
        var code = ModelCapService.Uncap("claude-fable-5", TextWriter.Null, TextWriter.Null, _projectRoot);

        Assert.Equal(1, code);
        Assert.Equal(0, _resyncCalls);
    }

    [Fact]
    public void RestoreExpired_RestoresPastDueCap_ReSyncsOnce()
    {
        // Cap with a future reset, then advance the passed-in "now" past it — Cap rejects a past
        // --until, so the expiry is modelled by moving the clock forward, not by capping into the past.
        var until = DateTimeOffset.Now.AddHours(1);
        ModelCapService.Cap("claude-fable-5", until, null, TextWriter.Null, TextWriter.Null, _projectRoot);
        _resyncCalls = 0;

        var restored = ModelCapService.RestoreExpired(until.AddMinutes(1), _projectRoot);

        Assert.Equal(1, restored);
        Assert.Equal(1, _resyncCalls);
        Assert.Equal("claude-fable-5", LoadConfig().Models!.Tiers["anthropic"]["strong"]);
        Assert.False(File.Exists(MarkerPath("claude-fable-5")));
    }

    [Fact]
    public void Cap_PastUntil_IsRejected()
    {
        var code = ModelCapService.Cap("claude-fable-5", DateTimeOffset.Now.AddHours(-1), null,
            TextWriter.Null, TextWriter.Null, _projectRoot);

        Assert.Equal(1, code);
        Assert.Equal(0, _resyncCalls);
        Assert.False(File.Exists(MarkerPath("claude-fable-5")));
    }

    [Fact]
    public void RestoreExpired_LeavesFutureCapUntouched()
    {
        ModelCapService.Cap("claude-fable-5", Future, null, TextWriter.Null, TextWriter.Null, _projectRoot);
        _resyncCalls = 0;

        var restored = ModelCapService.RestoreExpired(DateTimeOffset.UtcNow, _projectRoot);

        Assert.Equal(0, restored);
        Assert.Equal(0, _resyncCalls);
        Assert.Equal("claude-sonnet-5", LoadConfig().Models!.Tiers["anthropic"]["strong"]);
        Assert.True(File.Exists(MarkerPath("claude-fable-5")));
    }

    [Fact]
    public void RestoreExpired_NoCaps_IsCheapNoOp()
    {
        var restored = ModelCapService.RestoreExpired(DateTimeOffset.UtcNow, _projectRoot);

        Assert.Equal(0, restored);
        Assert.Equal(0, _resyncCalls);
    }

    [Fact]
    public void RestoreExpired_RestoresExpiredCapFromDydoRoot()
    {
        // RestoreExpired compares against the supplied now, so seed an already-past marker directly
        // rather than through Cap (which rejects a past --until). This is the restore the watchdog
        // used to drive per-tick; the watchdog poll was stripped in the 2.1.0 campaign (DR-041).
        SeedExpiredCap("claude-fable-5", "claude-sonnet-5", "anthropic", "strong");

        ModelCapService.RestoreExpired(DateTimeOffset.UtcNow, Path.Combine(_projectRoot, "dydo"));

        Assert.Equal("claude-fable-5", LoadConfig().Models!.Tiers["anthropic"]["strong"]);
        Assert.False(File.Exists(MarkerPath("claude-fable-5")));
    }

    // Simulates a cap already applied and now past-due: rebind the tier and drop a past-due marker,
    // bypassing Cap()'s future-until guard so the watchdog restore path can be exercised directly.
    private void SeedExpiredCap(string model, string fallback, string vendor, string tier)
    {
        var config = LoadConfig();
        config.Models!.Tiers[vendor][tier] = fallback;
        SaveConfig(config);

        var cap = new ModelCap
        {
            Model = model,
            Fallback = fallback,
            Until = DateTimeOffset.Now.AddMinutes(-5),
            ReboundTiers = new List<ModelCapBinding> { new() { Vendor = vendor, Tier = tier } },
        };
        Directory.CreateDirectory(Path.GetDirectoryName(MarkerPath(model))!);
        File.WriteAllText(MarkerPath(model),
            JsonSerializer.Serialize(cap, DydoDefaultJsonContext.Default.ModelCap));
    }

    [Theory]
    [InlineData("2026-07-13 09:00", 2026, 7, 13, 9, 0)]
    [InlineData("2026-07-13 09:30:00", 2026, 7, 13, 9, 30)]
    public void ParseUntil_ReadsFullTimestamp(string input, int y, int mo, int d, int h, int mi)
    {
        var parsed = ModelCapService.ParseUntil(input);

        Assert.NotNull(parsed);
        Assert.Equal(new DateTime(y, mo, d, h, mi, 0), parsed.Value.DateTime);
    }

    [Fact]
    public void ParseUntil_YearOmitted_PreservesMonthDayAndNeverResolvesToThePast()
    {
        // Dec 31 keeps its month/day through any rollover, and a year-omitted reset must always land
        // in the future — even across a Dec->Jan boundary (the cap-defeating bug the review caught).
        var parsed = ModelCapService.ParseUntil("12-31 23:59");

        Assert.NotNull(parsed);
        Assert.Equal(12, parsed.Value.Month);
        Assert.Equal(31, parsed.Value.Day);
        Assert.True(parsed.Value > DateTimeOffset.Now);
    }

    [Fact]
    public void ParseUntil_YearOmitted_RollsForwardWhenDateAlreadyPast()
    {
        // Jan 1 00:00 of the current year is (essentially always) already past, so it must roll to
        // next year rather than resolve to a past instant that the watchdog would instantly restore.
        var parsed = ModelCapService.ParseUntil("01-01 00:00");

        Assert.NotNull(parsed);
        Assert.True(parsed.Value > DateTimeOffset.Now, $"expected a future instant, got {parsed}");
    }

    [Fact]
    public void ParseUntil_Garbage_ReturnsNull()
    {
        Assert.Null(ModelCapService.ParseUntil("not a date"));
    }

    [Fact]
    public void ParseUntil_IsoString_ParsedByLastResort()
    {
        // A 'T'-separated ISO stamp matches none of the explicit formats, so the generous
        // last-resort DateTime.TryParse must still read it.
        var parsed = ModelCapService.ParseUntil("2027-01-15T09:30");

        Assert.NotNull(parsed);
        Assert.Equal(new DateTime(2027, 1, 15, 9, 30, 0), parsed.Value.DateTime);
    }

    [Fact]
    public void Cap_ThreadsResolvedProjectRootToResync()
    {
        // Finding 2: the re-sync must target the project the marker was written under, resolved from
        // startPath — not the process CWD.
        ModelCapService.Cap("claude-fable-5", Future, null, TextWriter.Null, TextWriter.Null, _projectRoot);

        Assert.NotNull(_lastResyncRoot);
        Assert.Equal(Path.GetFullPath(_projectRoot), Path.GetFullPath(_lastResyncRoot!));
    }

    [Fact]
    public void RestoreExpired_LogsModelCapRestoreEvent()
    {
        // Finding 3: a watchdog auto-restore emits a model_cap_restored line carrying the model + fallback.
        var logPath = Path.Combine(_projectRoot, "watchdog.log");
        WatchdogLogger.LogPathOverride = logPath;

        var until = DateTimeOffset.Now.AddHours(1);
        ModelCapService.Cap("claude-fable-5", until, "claude-sonnet-5", TextWriter.Null, TextWriter.Null, _projectRoot);
        ModelCapService.RestoreExpired(until.AddMinutes(1), _projectRoot);

        var log = File.ReadAllText(logPath);
        Assert.Contains("\"event\":\"model_cap_restored\"", log);
        Assert.Contains("\"model\":\"claude-fable-5\"", log);
        Assert.Contains("\"fallback\":\"claude-sonnet-5\"", log);
    }

    [Fact]
    public void Cap_WithoutResyncOverride_RunsRealSyncAgainstProjectRoot()
    {
        // Covers the real re-sync fallback (SyncCommand.Execute) end to end and proves the CWD
        // decoupling from Finding 2: sync emits native agents under the capped project, not the CWD.
        ModelCapService.ResyncOverride = null;

        var code = ModelCapService.Cap("claude-fable-5", Future, null, TextWriter.Null, TextWriter.Null, _projectRoot);

        Assert.Equal(0, code);
        Assert.Equal("claude-sonnet-5", LoadConfig().Models!.Tiers["anthropic"]["strong"]);
        Assert.True(File.Exists(Path.Combine(_projectRoot, ".claude", "agents", "reviewer.md")));
    }

    [Fact]
    public void Cap_NotInDydoProject_IsRejected()
    {
        var empty = NewEmptyDir();
        try
        {
            var code = ModelCapService.Cap("claude-fable-5", Future, "claude-sonnet-5",
                TextWriter.Null, TextWriter.Null, empty);
            Assert.Equal(2, code); // ToolError — no dydo.json found
        }
        finally { TryDeleteDir(empty); }
    }

    [Fact]
    public void Uncap_NotInDydoProject_IsRejected()
    {
        var empty = NewEmptyDir();
        try
        {
            var code = ModelCapService.Uncap("claude-fable-5", TextWriter.Null, TextWriter.Null, empty);
            Assert.Equal(2, code);
        }
        finally { TryDeleteDir(empty); }
    }

    [Fact]
    public void Uncap_CorruptMarker_TreatedAsNoActiveCap()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(MarkerPath("claude-fable-5"))!);
        File.WriteAllText(MarkerPath("claude-fable-5"), "{ not valid json");

        var code = ModelCapService.Uncap("claude-fable-5", TextWriter.Null, TextWriter.Null, _projectRoot);

        Assert.Equal(1, code); // LoadMarker swallows the parse error -> null -> "no active cap"
    }

    [Fact]
    public void Uncap_MarkerPresentButNoModelsSection_IsRejected()
    {
        SeedExpiredCap("claude-fable-5", "claude-sonnet-5", "anthropic", "strong");
        var config = LoadConfig();
        config.Models = null;
        SaveConfig(config);

        var code = ModelCapService.Uncap("claude-fable-5", TextWriter.Null, TextWriter.Null, _projectRoot);

        Assert.Equal(1, code);
    }

    private static string NewEmptyDir()
    {
        var d = Path.Combine(Path.GetTempPath(), "dydo-noproj-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(d);
        return d;
    }

    private static void TryDeleteDir(string d)
    {
        try { Directory.Delete(d, true); } catch { }
    }

    private ModelCap ReadMarker(string model) =>
        JsonSerializer.Deserialize(File.ReadAllText(MarkerPath(model)), DydoDefaultJsonContext.Default.ModelCap)!;
}
