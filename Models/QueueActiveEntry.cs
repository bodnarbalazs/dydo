namespace DynaDocs.Models;

using System.Text.Json.Serialization;

/// <summary>
/// The currently active item in a dispatch queue.
/// Tracks the agent that was launched and the PID of its terminal process.
/// </summary>
public class QueueActiveEntry
{
    [JsonPropertyName("agent")]
    public required string Agent { get; set; }

    [JsonPropertyName("task")]
    public required string Task { get; set; }

    [JsonPropertyName("pid")]
    public int Pid { get; set; }

    [JsonPropertyName("started")]
    public DateTime Started { get; set; }
}
