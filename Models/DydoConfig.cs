namespace DynaDocs.Models;

using System.Text.Json.Serialization;

public class DydoConfig
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    /// <summary>
    /// The release whose skills and documents the project adopted, kept apart from the CLI version
    /// because a teammate's newer CLI has not adopted anything; --join must leave it alone.
    /// </summary>
    [JsonPropertyName("dydoVersion")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DydoVersion { get; set; }

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

    [JsonPropertyName("clean")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CleanConfig? Clean { get; set; }
}
