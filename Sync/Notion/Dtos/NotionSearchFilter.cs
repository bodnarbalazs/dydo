namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>The <c>filter</c> of a POST /v1/search body — here, restrict to data sources.</summary>
public sealed class NotionSearchFilter
{
    [JsonPropertyName("property")]
    public string Property { get; set; } = "object";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "data_source";
}
