namespace DynaDocs.Sync.Notion.Provisioning;

using System.Text.Json.Serialization;

/// <summary>On-disk shape of the provision-state file: the recorded ids for each provisioned type.</summary>
public sealed class NotionProvisionState
{
    [JsonPropertyName("types")]
    public List<NotionProvisionedType> Types { get; set; } = [];
}
