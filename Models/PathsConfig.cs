namespace DynaDocs.Models;

using System.Text.Json.Serialization;

public class PathsConfig
{
    [JsonPropertyName("source")]
    public List<string> Source { get; set; } = ["src/**"];

    [JsonPropertyName("tests")]
    public List<string> Tests { get; set; } = ["tests/**"];
}
