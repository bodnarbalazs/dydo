namespace DynaDocs.Models;

using System.Text.Json.Serialization;

/// <summary>
/// One (vendor, tier) coordinate in <see cref="ModelsConfig.Tiers"/> that a
/// <see cref="ModelCap"/> rebound to the fallback — the unit of restore.
/// </summary>
public class ModelCapBinding
{
    [JsonPropertyName("vendor")]
    public string Vendor { get; set; } = "";

    [JsonPropertyName("tier")]
    public string Tier { get; set; } = "";
}
