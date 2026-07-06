namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>A select-column condition: <see cref="Equals"/> or <see cref="DoesNotEqual"/> an option value
/// (only one set). Drives the "Open = status does_not_equal resolved" style filters.</summary>
public sealed class NotionViewTextCondition
{
    [JsonPropertyName("equals")]
    public string? EqualsValue { get; set; }

    [JsonPropertyName("does_not_equal")]
    public string? DoesNotEqual { get; set; }
}
