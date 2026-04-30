namespace DynaDocs.Models;

using System.Text.Json.Serialization;

public class DispatchWaitMarker
{
    [JsonPropertyName("task")]
    public required string Task { get; init; }

    [JsonPropertyName("dispatcherAgent")]
    public required string DispatcherAgent { get; init; }

    [JsonPropertyName("dispatcherRole")]
    public string? DispatcherRole { get; init; }

    [JsonPropertyName("createdAt")]
    public required DateTime CreatedAt { get; init; }

    [JsonPropertyName("repliedAt")]
    public DateTime? RepliedAt { get; set; }
}
