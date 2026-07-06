namespace DynaDocs.Sync.Model;

using System.Text.Json.Serialization;

/// <summary>A single-property view filter (slice v1): the <see cref="Property"/> by name, an
/// <see cref="Operator"/> (<c>equals</c> | <c>does_not_equal</c>), and the <see cref="Value"/> to compare.
/// The provisioner builds the Notion filter body appropriate to the property's type (a <c>select</c>,
/// <c>checkbox</c>, or boolean <c>formula</c> condition) from the live schema.</summary>
public sealed class SyncViewFilter
{
    [JsonPropertyName("property")]
    public string Property { get; set; } = "";

    [JsonPropertyName("operator")]
    public string Operator { get; set; } = "equals";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";
}
