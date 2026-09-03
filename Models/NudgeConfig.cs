namespace DynaDocs.Models;

using System.Text.Json.Serialization;

public class NudgeConfig
{
    private string _audience = "all";

    [JsonPropertyName("pattern")]
    public string Pattern { get; set; } = "";

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "block";

    [JsonIgnore]
    public string Audience
    {
        get => _audience;
        set => _audience = value.ToLowerInvariant();
    }

    [JsonIgnore]
    public bool HasAudience { get; private set; }

    [JsonPropertyName("audience")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SerializedAudience
    {
        get => _audience == "all" ? null : _audience;
        set
        {
            HasAudience = true;
            _audience = value?.ToLowerInvariant() ?? "";
        }
    }
}
