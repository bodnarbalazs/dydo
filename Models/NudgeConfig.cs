namespace DynaDocs.Models;

using System.Text.Json.Serialization;

public class NudgeConfig
{
    [JsonPropertyName("pattern")]
    public string Pattern { get; set; } = "";

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "block";
}
