namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>The <c>formula</c> config in a create/update property schema (DR 029/030): a Notion
/// expression string, computed at the edge and never stored in the repo (view-only).</summary>
public sealed class NotionFormulaSchema
{
    [JsonPropertyName("expression")]
    public string Expression { get; set; } = "";
}
