namespace DynaDocs.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Tool input data from the hook.
/// Contains the file path and other tool-specific data.
/// </summary>
public class ToolInputData
{
    [JsonPropertyName("file_path")]
    public string? FilePath { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("old_string")]
    public string? OldString { get; set; }

    [JsonPropertyName("new_string")]
    public string? NewString { get; set; }

    [JsonPropertyName("command")]
    public string? Command { get; set; }
}
