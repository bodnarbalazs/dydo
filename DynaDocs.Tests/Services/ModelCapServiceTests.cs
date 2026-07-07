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

    public ModelCapServiceTests()
    {
        _projectRoot = Path.Combine(Path.GetTempPath(), "dydo-modelcap-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_projectRoot);
        // Stub the re-sync so cap/uncap surgery doesn't emit native agent files during tests.
        ModelCapService.ResyncOverride = () => { _resyncCalls++; return 0; };
        SaveConfig(ConfigFactory.CreateDefault("balazs"));
    }

    public void Dispose()
    {
        ModelCapService.ResyncOverride = null;
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
        ModelCapService.Cap("claude-fable-5", DateTimeOffset.Now.AddHours(-1), null, TextWriter.Null, TextWriter.Null, _projectRoot);
        _resyncCalls = 0;

        var restored = ModelCapService.RestoreExpired(DateTimeOffset.UtcNow, _projectRoot);

        Assert.Equal(1, restored);
        Assert.Equal(1, _resyncCalls);
        Assert.Equal("claude-fable-5", LoadConfig().Models!.Tiers["anthropic"]["strong"]);
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
    public void PollModelCaps_RestoresExpiredCapThroughWatchdogEntry()
    {
        ModelCapService.Cap("claude-fable-5", DateTimeOffset.Now.AddMinutes(-5), null, TextWriter.Null, TextWriter.Null, _projectRoot);

        WatchdogService.PollModelCaps(Path.Combine(_projectRoot, "dydo"));

        Assert.Equal("claude-fable-5", LoadConfig().Models!.Tiers["anthropic"]["strong"]);
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
    public void ParseUntil_YearOmitted_AssumesCurrentYear()
    {
        var parsed = ModelCapService.ParseUntil("07-13 09:00");

        Assert.NotNull(parsed);
        Assert.Equal(DateTime.Now.Year, parsed.Value.Year);
        Assert.Equal(7, parsed.Value.Month);
        Assert.Equal(13, parsed.Value.Day);
    }

    [Fact]
    public void ParseUntil_Garbage_ReturnsNull()
    {
        Assert.Null(ModelCapService.ParseUntil("not a date"));
    }

    private ModelCap ReadMarker(string model) =>
        JsonSerializer.Deserialize(File.ReadAllText(MarkerPath(model)), DydoDefaultJsonContext.Default.ModelCap)!;
}
