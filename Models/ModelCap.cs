namespace DynaDocs.Models;

using System.Text.Json.Serialization;

/// <summary>
/// A local, gitignored record that a model is temporarily capped (issue #214): its tier
/// bindings were rebound to <see cref="Fallback"/> until <see cref="Until"/>, at which point
/// the watchdog (or an explicit <c>dydo model uncap</c>) restores them. One marker per capped
/// model, stored at <c>_system/.local/model-caps/&lt;model&gt;.json</c>.
/// </summary>
public class ModelCap
{
    /// <summary>The capped (unavailable) model id, e.g. <c>claude-fable-5</c>.</summary>
    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    /// <summary>The model its tiers were rebound to for the duration of the cap.</summary>
    [JsonPropertyName("fallback")]
    public string Fallback { get; set; } = "";

    /// <summary>When the cap lifts — the user-stated reset time from the spend-limit error.</summary>
    [JsonPropertyName("until")]
    public DateTimeOffset Until { get; set; }

    /// <summary>
    /// The exact tier bindings rebound from <see cref="Model"/> to <see cref="Fallback"/>, so
    /// restore puts precisely those back and never touches tiers the cap didn't move.
    /// </summary>
    [JsonPropertyName("reboundTiers")]
    public List<ModelCapBinding> ReboundTiers { get; set; } = new();
}
