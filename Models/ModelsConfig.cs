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
}
