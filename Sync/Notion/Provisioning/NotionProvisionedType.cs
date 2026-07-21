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

    /// <summary>The <c>notionTitle</c> this data source was last provisioned/renamed to. The additive
    /// model-evolution pass (ns-11) compares it against the current model to rename the board exactly once
    /// when the title changed — no live read needed, no re-mint. Empty on a record written before ns-11; the
    /// additive pass seeds it from the model without a rename (the live board already carries that title).</summary>
    [JsonPropertyName("notionTitle")]
    public string NotionTitle { get; set; } = "";

    /// <summary>Whether this type's rollup/formula post-pass has run. A database create is recorded the
    /// instant it succeeds (mid-provision-failure safety), but its rollups and deferred formulas are PATCHed
    /// only in the later post-pass — which a mid-run throw can skip entirely. Persisting completion per type
    /// lets a retry re-run the post-pass for recorded-but-unpassed types, so a reused parent's attention-layer
    /// rollups/formulas are never silently missing (review R2-1).</summary>
    [JsonPropertyName("postPassDone")]
    public bool PostPassDone { get; set; }
}
