namespace DynaDocs.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Model-tier abstraction (Decision 028): roles declare an abstract tier
/// (strong / standard / light), a per-vendor mapping binds tiers to concrete
/// model ids, and <c>dydo sync</c> resolves role → tier → model when emitting
/// native agent definitions. An absent section means everything inherits the
/// session model.
/// </summary>
public class ModelsConfig
{
    /// <summary>Per-vendor tier bindings, e.g. tiers["anthropic"]["strong"] = "claude-fable-5".</summary>
    [JsonPropertyName("tiers")]
    public Dictionary<string, Dictionary<string, string>> Tiers { get; set; } = new();

    /// <summary>Role → tier map. Vendor-agnostic; never names a concrete model.</summary>
    [JsonPropertyName("roles")]
    public Dictionary<string, string> Roles { get; set; } = new();

    /// <summary>
    /// Optional role → reasoning-effort map (Decision 028 §4): tier picks the brain,
    /// effort picks how hard it thinks. Only emitted for roles with a resolved model.
    /// </summary>
    [JsonPropertyName("efforts")]
    public Dictionary<string, string> Efforts { get; set; } = new();

    /// <summary>
    /// Optional vendor-agnostic fallback model (issue #214): the second-line model that
    /// <c>dydo model cap</c> rebinds a capped model's tiers to when no explicit
    /// <c>--fallback</c> is given. A declared safe target for any provider outage — a spend
    /// cap, a capacity error, a provider being down — kept in the abstraction we already own
    /// rather than a runtime failover interceptor.
    /// </summary>
    [JsonPropertyName("fallback")]
    public string? Fallback { get; set; }

    /// <summary>
    /// Optional model-id → human display-name map (c1-6): provenance surfaces render the
    /// exact model (<c>Opus 4.8</c>, <c>Fable 5</c>, <c>Gpt-5.6 Sol</c>) instead of a bare
    /// vendor. An ABSENT or EMPTY map resolves to the shipped defaults
    /// (<see cref="Services.ConfigFactory.DefaultDisplayNames"/>) via
    /// <see cref="Services.ModelDisplay"/> — the same absent-section-defaults contract as
    /// <see cref="CodexDispatchConfig"/> — so the defaults take effect even for a dydo.json
    /// written before this key existed. A configured non-empty map is authoritative; ids not
    /// in the effective map pass through verbatim.
    /// </summary>
    [JsonPropertyName("display-names")]
    public Dictionary<string, string> DisplayNames { get; set; } = new();
}
