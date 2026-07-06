namespace DynaDocs.Sync.Model;

using System.Text.Json.Serialization;

/// <summary>One sort in a view's order: the <see cref="Property"/> by name and a
/// <see cref="Direction"/> (<c>ascending</c> | <c>descending</c>).</summary>
public sealed class SyncViewSort
{
    [JsonPropertyName("property")]
    public string Property { get; set; } = "";

    [JsonPropertyName("direction")]
    public string Direction { get; set; } = "ascending";
}
