namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>A checkbox condition: whether the box <see cref="Equals"/> true or false. Nullable so an unset
/// condition is omitted rather than sent as false.</summary>
public sealed class NotionViewCheckboxCondition
{
    [JsonPropertyName("equals")]
    public bool? EqualsValue { get; set; }
}
