namespace DynaDocs.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Model-tier abstraction (Decision 028): agents declare an abstract tier
/// (strong / standard / light), a per-vendor mapping binds tiers to concrete
/// model ids, and <c>dydo sync</c> resolves agent → tier → model when emitting
/// native agent definitions. An absent section means everything inherits the
/// session model.
/// </summary>
public class ModelsConfig
{
    /// <summary>Per-vendor tier bindings, e.g. tiers["anthropic"]["strong"] = "claude-fable-5".</summary>
    [JsonPropertyName("tiers")]
    public Dictionary<string, Dictionary<string, string>> Tiers { get; set; } = new();

    /// <summary>
    /// Spawned agent → tier map: each key is an <c>emit: agent</c> skill template's name.
    /// Vendor-agnostic; never names a concrete model.
    /// </summary>
    [JsonPropertyName("agents")]
    public Dictionary<string, string> Agents { get; set; } = new();

    /// <summary>
    /// Optional role → reasoning-effort map (Decision 028 §4): tier picks the brain,
    /// effort picks how hard it thinks. Only emitted for roles with a resolved model.
    /// </summary>
    [JsonPropertyName("efforts")]
    public Dictionary<string, string> Efforts { get; set; } = new();
}
