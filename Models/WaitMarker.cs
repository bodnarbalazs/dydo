namespace DynaDocs.Models;

using System.Text.Json.Serialization;

public class WaitMarker
{
    [JsonPropertyName("target")]
    public required string Target { get; init; }

    [JsonPropertyName("task")]
    public required string Task { get; init; }

    [JsonPropertyName("since")]
    public required DateTime Since { get; init; }
}
