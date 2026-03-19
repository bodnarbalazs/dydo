namespace DynaDocs.Models;

using System.Text.Json.Serialization;

public class GuardLiftMarker
{
    [JsonPropertyName("agent")]
    public string Agent { get; set; } = "";

    [JsonPropertyName("liftedBy")]
    public string LiftedBy { get; set; } = "";

    [JsonPropertyName("liftedAt")]
    public DateTime LiftedAt { get; set; }

    [JsonPropertyName("expiresAt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? ExpiresAt { get; set; }
}
