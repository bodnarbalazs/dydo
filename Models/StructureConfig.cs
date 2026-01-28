namespace DynaDocs.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Configuration for folder structure
/// </summary>
public class StructureConfig
{
    [JsonPropertyName("root")]
    public string Root { get; set; } = "dydo";

    [JsonPropertyName("tasks")]
    public string Tasks { get; set; } = "project/tasks";
}
