namespace DynaDocs.Models;

using System.Text.Json.Serialization;

/// <summary>
/// The JSON a PreToolUse hook sends on stdin, from Claude Code or Codex.
/// </summary>
public class HookInput
{
    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }

    /// <summary>
    /// Unique id of the sub-agent making this call. Present only when the hook fires
    /// inside a sub-agent (Agent tool or workflow-spawned); absent on the main thread.
    /// </summary>
    [JsonPropertyName("agent_id")]
    public string? AgentId { get; set; }

    /// <summary>
    /// Agent type name of the sub-agent (e.g. "reviewer", "Explore", "workflow-subagent").
    /// Carries the crew member's role. Present only alongside agent_id.
    /// </summary>
    [JsonPropertyName("agent_type")]
    public string? AgentType { get; set; }

    [JsonPropertyName("transcript_path")]
    public string? TranscriptPath { get; set; }

    [JsonPropertyName("cwd")]
    public string? Cwd { get; set; }

    [JsonPropertyName("permission_mode")]
    public string? PermissionMode { get; set; }

    [JsonPropertyName("hook_event_name")]
    public string? HookEventName { get; set; }

    [JsonPropertyName("tool_name")]
    public string? ToolName { get; set; }

    [JsonPropertyName("tool_input")]
    public ToolInputData? ToolInput { get; set; }

    [JsonPropertyName("tool_use_id")]
    public string? ToolUseId { get; set; }

    /// <summary>
    /// The Codex turn id, a Codex-only extension of the hook payload: present only when Codex
    /// sent the call.
    /// </summary>
    [JsonPropertyName("turn_id")]
    public string? TurnId { get; set; }
}
