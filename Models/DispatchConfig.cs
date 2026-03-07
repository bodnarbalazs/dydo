namespace DynaDocs.Models;

using System.Text.Json.Serialization;

public class DispatchConfig
{
    [JsonPropertyName("launchInTab")]
    public bool LaunchInTab { get; set; } = true;

    [JsonPropertyName("autoClose")]
    public bool AutoClose { get; set; } = false;
}
