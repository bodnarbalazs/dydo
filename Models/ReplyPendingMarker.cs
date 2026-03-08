namespace DynaDocs.Models;

using System.Text.Json.Serialization;

public class ReplyPendingMarker
{
    [JsonPropertyName("to")]
    public required string To { get; init; }

    [JsonPropertyName("task")]
    public required string Task { get; init; }

    [JsonPropertyName("since")]
    public required DateTime Since { get; init; }
}
