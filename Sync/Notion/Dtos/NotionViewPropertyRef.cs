namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>One column in a view's ordered <c>properties</c> list: the Notion <see cref="PropertyId"/> and
/// whether it is <see cref="Visible"/>. Listing every property in the intended order — compute-only helpers
/// as <c>visible: false</c> — is how the provisioner sets both column order and hides.</summary>
public sealed class NotionViewPropertyRef
{
    [JsonPropertyName("property_id")]
    public string PropertyId { get; set; } = "";

    [JsonPropertyName("visible")]
    public bool Visible { get; set; } = true;
}
