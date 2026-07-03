namespace DynaDocs.Models;

using System.Text.Json.Serialization;

public class NudgeConfig
{
    [JsonPropertyName("pattern")]
    public string Pattern { get; set; } = "";

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "block";

    /// <summary>
    /// Tool names this nudge applies to (e.g. ["Edit", "Write", "NotebookEdit"]).
    /// Null/empty = bash-command nudge: the pattern is a regex matched against the
    /// command text. When set, the pattern is a '|'-separated list of glob patterns
    /// matched against the tool call's file path; {source}/{tests} placeholders
    /// expand to the path sets configured in dydo.json (Decision 026 §4).
    /// </summary>
    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Tools { get; set; }
}
