namespace DynaDocs.Sync.Notion.Provisioning;

using System.Text.Json.Serialization;

/// <summary>The recorded Notion ids for one provisioned object type, persisted so provisioning is
/// idempotent across runs (the dydo binary writes this at runtime under the gitignored .local tree).</summary>
public sealed class NotionProvisionedType
{
    [JsonPropertyName("objectType")]
    public required string ObjectType { get; set; }

    [JsonPropertyName("databaseId")]
    public required string DatabaseId { get; set; }

    [JsonPropertyName("dataSourceId")]
    public required string DataSourceId { get; set; }
}
