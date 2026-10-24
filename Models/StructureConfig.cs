namespace DynaDocs.Models;

using System.Text.Json.Serialization;

public class StructureConfig
{
    [JsonPropertyName("root")]
    public string Root { get; set; } = "dydo";

}
