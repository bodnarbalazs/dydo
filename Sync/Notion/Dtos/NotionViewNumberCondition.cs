namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>A number condition. Only <see cref="GreaterThan"/> is needed so far (a rollup count &gt; 0 for the
/// parent types' Needs-Attention view).</summary>
public sealed class NotionViewNumberCondition
{
    [JsonPropertyName("greater_than")]
    public double? GreaterThan { get; set; }
}
