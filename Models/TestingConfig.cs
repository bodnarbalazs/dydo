namespace DynaDocs.Models;

using System.Text.Json.Serialization;

public class TestingConfig
{
    [JsonPropertyName("runner")]
    public List<string>? Runner { get; set; }
}
