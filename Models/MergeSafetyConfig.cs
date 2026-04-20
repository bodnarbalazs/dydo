namespace DynaDocs.Models;

using System.Text.Json.Serialization;

public class MergeSafetyConfig
{
    [JsonPropertyName("ignore")]
    public List<string> Ignore { get; set; } = new();

    [JsonPropertyName("ignoreDefaults")]
    public bool IgnoreDefaults { get; set; } = true;
}
