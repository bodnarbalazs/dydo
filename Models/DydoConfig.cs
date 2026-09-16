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

    /// <summary>
    /// Model-tier bindings (Decision 028). Null when the section is absent —
    /// every generated agent then inherits the session model.
    /// </summary>
    [JsonPropertyName("models")]
    public ModelsConfig? Models { get; set; }

    [JsonPropertyName("scanExclude")]
    public List<string> ScanExclude { get; set; } = new();

    [JsonPropertyName("nudges")]
    public List<NudgeConfig> Nudges { get; set; } = new();

    [JsonPropertyName("frameworkHashes")]
    public Dictionary<string, string> FrameworkHashes { get; set; } = new();
}
