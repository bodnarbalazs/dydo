namespace DynaDocs.Models;

using System.Text.Json.Serialization;

public class DispatchConfig
{
    [JsonPropertyName("launchInTab")]
    public bool LaunchInTab { get; set; } = true;

    [JsonPropertyName("autoClose")]
    public bool AutoClose { get; set; } = false;

    /// <summary>
    /// Codex launch posture (issue 0253). Non-nullable so an absent <c>dispatch.codex</c> section
    /// deserializes to the shipped defaults, never a bare launch.
    /// </summary>
    [JsonPropertyName("codex")]
    public CodexDispatchConfig Codex { get; set; } = new();
}
