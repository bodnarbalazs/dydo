namespace DynaDocs.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Root configuration object for dydo.json
/// </summary>
public class DydoConfig
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("structure")]
    public StructureConfig Structure { get; set; } = new();

    [JsonPropertyName("integrations")]
    public Dictionary<string, bool> Integrations { get; set; } = new();

    [JsonPropertyName("scanExclude")]
    public List<string> ScanExclude { get; set; } = new();

    [JsonPropertyName("nudges")]
    public List<NudgeConfig> Nudges { get; set; } = new();

    [JsonPropertyName("testing")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TestingConfig? Testing { get; set; }
}
