namespace DynaDocs.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Represents a complete audit session containing all events for a single agent session.
/// Stored as JSON files in dydo/_system/audit/YYYY/yyyy-mm-dd-sessionid.json
/// </summary>
public class AuditSession
{
    /// <summary>
    /// The session ID from the AI assistant (Claude Code, etc.).
    /// </summary>
    [JsonPropertyName("session")]
    public required string SessionId { get; set; }

    /// <summary>
    /// The agent name (if claimed), e.g., "Alpha", "Beta".
    /// </summary>
    [JsonPropertyName("agent")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AgentName { get; set; }

    /// <summary>
    /// The human operating this session.
    /// </summary>
    [JsonPropertyName("human")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Human { get; set; }

    /// <summary>
    /// When this session started (first event timestamp).
    /// </summary>
    [JsonPropertyName("started")]
    public DateTime Started { get; set; }

    /// <summary>
    /// The git HEAD commit hash at session start.
    /// </summary>
    [JsonPropertyName("git_head")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GitHead { get; set; }

    /// <summary>
    /// All events in this session, in chronological order.
    /// </summary>
    [JsonPropertyName("events")]
    public List<AuditEvent> Events { get; set; } = [];
}
