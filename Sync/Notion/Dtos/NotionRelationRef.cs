namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>One related page reference inside a relation property value: <c>{ "id": &lt;page_id&gt; }</c>.</summary>
public sealed class NotionRelationRef
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
}
