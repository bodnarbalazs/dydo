namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>Body for PATCH /v1/pages/{id}: update properties and/or archive the page. Write-only (never
/// deserialized), so a custom write converter is safe — it merges the typed <see cref="Properties"/> with the
/// explicit-null <see cref="PropertyClears"/> under one <c>properties</c> object (issue 0299, F5): a clear needs
/// <c>{"select": null}</c> / <c>{"date": null}</c> / <c>{"rich_text": []}</c>, which the source generator's
/// WhenWritingNull would otherwise omit.</summary>
[JsonConverter(typeof(NotionPageUpdateRequestConverter))]
public sealed class NotionPageUpdateRequest
{
    public Dictionary<string, NotionPropertyValue>? Properties { get; set; }

    /// <summary>Property name → its Notion type, for properties this update must explicitly CLEAR — the wire shape
    /// the source-gen typed value cannot emit (an explicit null the WhenWritingNull default drops). Merged into the
    /// <c>properties</c> object by <see cref="NotionPageUpdateRequestConverter"/>.</summary>
    public Dictionary<string, string>? PropertyClears { get; set; }

    public bool? Archived { get; set; }
}
