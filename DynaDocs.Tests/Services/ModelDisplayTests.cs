namespace DynaDocs.Tests.Services;

using DynaDocs.Models;
using DynaDocs.Services;

/// <summary>
/// The one shared model display-name resolver (c1-6): known id → display name, unknown id
/// verbatim, vendor only as a fallback when the model is unknown, and absent/empty config maps
/// falling back to the shipped defaults.
/// </summary>
public class ModelDisplayTests
{
    [Fact]
    public void Resolve_KnownId_ReturnsDisplayName()
    {
        var models = ConfigFactory.CreateDefaultModels();

        Assert.Equal("Opus 4.8", ModelDisplay.Resolve("claude-opus-4-8", models));
    }

    [Fact]
    public void Resolve_UnknownId_PassesThroughVerbatim()
    {
        var models = ConfigFactory.CreateDefaultModels();

        Assert.Equal("some-unmapped-model-9", ModelDisplay.Resolve("some-unmapped-model-9", models));
    }

    [Fact]
    public void Resolve_AbsentMap_FallsBackToShippedDefaults()
    {
        // A dydo.json written before the display-names key existed deserializes to an empty map.
        var models = new ModelsConfig();

        Assert.Equal("Gpt-5.6 Sol", ModelDisplay.Resolve("gpt-5-codex", models));
    }

    [Fact]
    public void Resolve_NullConfig_FallsBackToShippedDefaults()
    {
        Assert.Equal("Fable 5", ModelDisplay.Resolve("claude-fable-5", null));
    }

    [Fact]
    public void Resolve_ConfiguredNonEmptyMap_IsAuthoritative()
    {
        var models = new ModelsConfig
        {
            DisplayNames = new Dictionary<string, string> { ["custom-id"] = "Custom Name" }
        };

        // The configured map wins...
        Assert.Equal("Custom Name", ModelDisplay.Resolve("custom-id", models));
        // ...and ids not in it pass through verbatim rather than falling to the shipped defaults.
        Assert.Equal("claude-opus-4-8", ModelDisplay.Resolve("claude-opus-4-8", models));
    }

    [Fact]
    public void ResolveOrVendor_KnownModel_ReturnsDisplayName()
    {
        Assert.Equal("Opus 4.8", ModelDisplay.ResolveOrVendor("claude-opus-4-8", "claude", null));
    }

    [Fact]
    public void ResolveOrVendor_UnknownModel_FallsBackToVendor()
    {
        Assert.Equal("codex", ModelDisplay.ResolveOrVendor("unknown", "codex", null));
        Assert.Equal("claude", ModelDisplay.ResolveOrVendor(null, "claude", null));
    }

    [Fact]
    public void EffectiveDisplayNames_EmptyMap_ReturnsShippedDefaults()
    {
        var effective = ModelDisplay.EffectiveDisplayNames(new ModelsConfig());

        Assert.Same(ConfigFactory.DefaultDisplayNames, effective);
    }

    [Fact]
    public void EffectiveDisplayNames_NonEmptyMap_ReturnsConfigured()
    {
        var configured = new Dictionary<string, string> { ["x"] = "X" };
        var effective = ModelDisplay.EffectiveDisplayNames(new ModelsConfig { DisplayNames = configured });

        Assert.Same(configured, effective);
    }
}
