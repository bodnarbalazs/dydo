namespace DynaDocs.Models;

using System.Text.Json.Serialization;

public class ReviewDispatchedMarker
{
    [JsonPropertyName("task")]
    public required string Task { get; init; }

    [JsonPropertyName("dispatchedTo")]
    public required string DispatchedTo { get; init; }

    [JsonPropertyName("since")]
    public required DateTime Since { get; init; }
}
