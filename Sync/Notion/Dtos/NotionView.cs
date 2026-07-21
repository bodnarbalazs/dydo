namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>A single view retrieved by id (GET /v1/views/{id}, ns-12 live): unlike the bare
/// <see cref="NotionViewRef"/> the list endpoint returns, this carries the <c>name</c> and <c>type</c>. The
/// CreateView recovery retrieves each listed view to match one by name before re-creating (ns-5).</summary>
public sealed class NotionView
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}
