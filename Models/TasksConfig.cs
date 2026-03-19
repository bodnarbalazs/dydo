namespace DynaDocs.Models;

using System.Text.Json.Serialization;

public class TasksConfig
{
    [JsonPropertyName("autoCompactInterval")]
    public int AutoCompactInterval { get; set; } = 20;
}
