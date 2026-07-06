namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>A board view's grouping: the property <see cref="Type"/> (e.g. <c>select</c>), the
/// <see cref="PropertyId"/> to group columns by, and a required <see cref="Sort"/> (Notion rejects the
/// group_by without it).</summary>
public sealed class NotionViewGroupBy
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "select";

    [JsonPropertyName("property_id")]
    public string PropertyId { get; set; } = "";

    [JsonPropertyName("sort")]
    public NotionViewGroupSort Sort { get; set; } = new();
}
