namespace DynaDocs.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Types of events that can be logged in the audit system.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<AuditEventType>))]
public enum AuditEventType
{
    /// <summary>Agent claimed an identity</summary>
    Claim,
    /// <summary>Agent released their identity</summary>
    Release,
    /// <summary>Agent set or changed their role</summary>
    Role,
    /// <summary>File was read</summary>
    Read,
    /// <summary>File was written/created</summary>
    Write,
    /// <summary>File was edited</summary>
    Edit,
    /// <summary>File was deleted</summary>
    Delete,
    /// <summary>Bash command was executed</summary>
    Bash,
    /// <summary>Git commit was made</summary>
    Commit,
    /// <summary>Action was blocked by guard</summary>
    Blocked
}

/// <summary>
/// Represents a single audit event in the system.
/// </summary>
public class AuditEvent
{
    /// <summary>
    /// ISO 8601 timestamp of when the event occurred.
    /// </summary>
    [JsonPropertyName("ts")]
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// The type of event.
    /// </summary>
    [JsonPropertyName("event")]
    public AuditEventType EventType { get; set; }

    /// <summary>
    /// The file path involved (for file operations).
    /// </summary>
    [JsonPropertyName("path")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Path { get; set; }

    /// <summary>
    /// The tool that triggered this event (Read, Write, Edit, Bash, etc.).
    /// </summary>
    [JsonPropertyName("tool")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Tool { get; set; }

    /// <summary>
    /// The bash command executed (for Bash events).
    /// </summary>
    [JsonPropertyName("cmd")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Command { get; set; }

    /// <summary>
    /// Exit code for bash commands.
    /// </summary>
    [JsonPropertyName("exit")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ExitCode { get; set; }

    /// <summary>
    /// The role set (for Role events).
    /// </summary>
    [JsonPropertyName("role")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Role { get; set; }

    /// <summary>
    /// The task associated with this action.
    /// </summary>
    [JsonPropertyName("task")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Task { get; set; }

    /// <summary>
    /// Git commit hash (for Commit events).
    /// </summary>
    [JsonPropertyName("hash")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CommitHash { get; set; }

    /// <summary>
    /// Commit message (for Commit events).
    /// </summary>
    [JsonPropertyName("msg")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CommitMessage { get; set; }

    /// <summary>
    /// The agent name (for Claim events).
    /// </summary>
    [JsonPropertyName("agent")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AgentName { get; set; }

    /// <summary>
    /// The reason for blocking (for Blocked events).
    /// </summary>
    [JsonPropertyName("reason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BlockReason { get; set; }
}
