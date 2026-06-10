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
    Blocked,
    /// <summary>Guard RBAC was temporarily lifted for an agent</summary>
    GuardLift,
    /// <summary>Guard RBAC was restored for an agent</summary>
    GuardRestore
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
    /// Sub-agent instance id (Tier-2 worker calls only). Events without it are Tier-1.
    /// </summary>
    [JsonPropertyName("agent_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AgentId { get; set; }

    /// <summary>
    /// Sub-agent type, which carries the worker's role (Tier-2 worker calls only).
    /// </summary>
    [JsonPropertyName("agent_type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AgentType { get; set; }

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

    /// <summary>
    /// Whether this action occurred while guard was lifted.
    /// </summary>
    [JsonPropertyName("lifted")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Lifted { get; set; }

    /// <summary>
    /// Categorises a Claim event for the 4-bucket recovery analysis (PR3 of agent-crash-fixes):
    /// "fresh" (no prior session), "auto" (same-session reclaim after a watchdog resume launch),
    /// "manual" (new session ID after an unreleased prior session — typically a user-initiated
    /// re-claim with no `resume` event in between). Null on non-Claim events and on idempotent
    /// reclaims that did not follow a resume launch.
    /// </summary>
    [JsonPropertyName("recovery_kind")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RecoveryKind { get; set; }

    /// <summary>
    /// Prior <c>.session.SessionId</c> at the moment of a Claim event when the agent had an
    /// unreleased prior session. Equal to the new session ID for "auto" recovery (same-session
    /// reclaim); differs for "manual". Null on "fresh" claims and on non-Claim events. Lets the
    /// follow-up inquisition trace recovery chains without correlating watchdog.log.
    /// </summary>
    [JsonPropertyName("resume_predecessor_session")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ResumePredecessorSession { get; set; }

    /// <summary>
    /// Snapshot of <c>state.ResumeAttempts</c> captured at Claim time, before the same-session
    /// reclaim path resets the resume budget. Lets the follow-up inquisition compute "how many
    /// auto-resumes preceded this claim" without joining against watchdog.log. Null on Claim
    /// events that lacked prior resume bookkeeping.
    /// </summary>
    [JsonPropertyName("resume_attempts_at_claim")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ResumeAttemptsAtClaim { get; set; }
}
