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

    /// <summary>
    /// What the pattern is matched against: absent or "command" = the shell command line,
    /// "new-file" = the project-relative path of a file a write tool is about to create.
    /// Any other value matches nothing; <c>dydo validate</c> reports it.
    /// </summary>
    [JsonPropertyName("match")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Match { get; set; }

    [JsonIgnore]
    public bool IsCommandMatch => Match == null || string.Equals(Match, "command", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsNewFileMatch => string.Equals(Match, "new-file", StringComparison.OrdinalIgnoreCase);

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
