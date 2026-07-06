namespace DynaDocs.Sync.Model;

using System.Text.Json.Serialization;

/// <summary>
/// One Notion view a sync-model object type provisions beyond the auto-created default (DR: board views).
/// Declarative and view-agnostic in spirit, though the vocabulary maps to Notion's <c>POST /v1/views</c>:
/// a <see cref="Name"/>, a <see cref="Type"/> (<c>table</c>|<c>board</c>|<c>timeline</c>), an optional
/// single-property <see cref="Filter"/> (e.g. hide resolved issues), <see cref="Sort"/> order,
/// <see cref="GroupBy"/> for a board, and the date columns for a timeline. Property references are by NAME;
/// the provisioner resolves them to Notion property ids against the live schema. <see cref="Hide"/> names
/// properties to hide in this view (compute-only helpers); everything else shows in the model's declared
/// property order.
/// </summary>
public sealed class SyncViewDef
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "table";

    [JsonPropertyName("filter")]
    public SyncViewFilter? Filter { get; set; }

    [JsonPropertyName("sort")]
    public List<SyncViewSort>? Sort { get; set; }

    /// <summary>For a board, the select property name to group columns by.</summary>
    [JsonPropertyName("groupBy")]
    public string? GroupBy { get; set; }

    /// <summary>For a timeline, the date (or date-rollup) property the bars start on.</summary>
    [JsonPropertyName("dateStart")]
    public string? DateStart { get; set; }

    /// <summary>For a timeline, the date (or date-rollup) property the bars end on.</summary>
    [JsonPropertyName("dateEnd")]
    public string? DateEnd { get; set; }

    /// <summary>Property names to hide in this view, on top of the type-wide compute-only hides.</summary>
    [JsonPropertyName("hide")]
    public List<string>? Hide { get; set; }
}
